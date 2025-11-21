using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    [Header("Boid設定")]
    public GameObject boidPrefab;   // 群れの個体となるPrefab
    public int boidCount = 100;     // 個体数
    public float speed = 2f;        // 移動速度

    [Header("距離の設定")]
    public float neighborDistance = 4f;     // 仲間を認識する距離
    public float separationDistance = 1f;   // 近づきすぎた場合に離れる距離

    [Header("動作の強さの設定")]
    public float cohesionWeight = 0.03f;    // 結合の強さ
    public float separationWeight = 1.0f;   // 分離の強さ
    public float alignmentWeight = 0.05f;   // 整列の強さ
    public float leaderWeight = 0.02f;      // リーダーに従う強さ

    [Header("リーダー設定")]
    public Transform leader;                // 群れが追いかけるリーダー

    [Header("突撃設定")]
    public Transform player;                // 突撃先（プレイヤー）
    public float chargeSpeed = 5f;          // 突撃中のスピード
    public float chargeDelay = 3f;          // 指名されてから何秒後に突撃開始するか
    public float maxChargeDuration = 2f;    // 突撃を続ける最大時間（これを超えたら戻る）
    public float chargeInterval = 5f;       // 何秒ごとに新しい突撃役を選ぶか
    public int maxConcurrentChargers = 3;   // 同時に突撃状態になれる最大人数

    private List<Boid> boids = new List<Boid>();
    private float chargeIntervalTimer = 0f; // 突撃役選出用タイマー

    void Start()
    {
        // Boid生成
        for (int i = 0; i < boidCount; i++)
        {
            Vector2 pos = Random.insideUnitCircle * 5f;
            var boidObj = Instantiate(boidPrefab, pos, Quaternion.identity);
            var boid = boidObj.AddComponent<Boid>();

            boid.velocity          = Random.insideUnitCircle.normalized * speed;
            boid.isCharger         = false;          // 初期は全員ふつう
            boid.chargeDelay       = chargeDelay;    // マネージャー設定をコピー
            boid.maxChargeDuration = maxChargeDuration;

            boids.Add(boid);
        }
    }

    void Update()
    {
        // 破棄済みBoidを掃除（MissingReference対策）
        boids.RemoveAll(b => b == null);

        // ===== 一定時間ごとに突撃役を追加で選ぶ =====
        chargeIntervalTimer += Time.deltaTime;

        if (chargeIntervalTimer >= chargeInterval)
        {
            chargeIntervalTimer = 0f;

            // 今何人 isCharger 中かカウント
            int currentChargerCount = 0;
            for (int i = 0; i < boids.Count; i++)
            {
                var b = boids[i];
                if (b != null && b.isCharger)
                {
                    currentChargerCount++;
                }
            }

            int capacity = maxConcurrentChargers - currentChargerCount;

            if (capacity > 0)
            {
                // まだ突撃役じゃない Boid を候補に集める
                List<Boid> candidates = new List<Boid>();
                for (int i = 0; i < boids.Count; i++)
                {
                    var b = boids[i];
                    if (b != null && !b.isCharger)
                    {
                        candidates.Add(b);
                    }
                }

                // capacity 分だけランダムに指名（候補が少なければその分だけ）
                int toAssign = Mathf.Min(capacity, candidates.Count);
                for (int n = 0; n < toAssign; n++)
                {
                    int idx = Random.Range(0, candidates.Count);
                    Boid chosen = candidates[idx];
                    candidates.RemoveAt(idx);

                    chosen.isCharger = true;
                    chosen.chargeTimer = 0f;  // タイマーリセット
                }
            }
        }

        // ===== 各Boidの更新 =====
        for (int i = 0; i < boids.Count; i++)
        {
            Boid boid = boids[i];
            if (boid == null) continue;

            // 突撃関連のタイマー更新と自動復帰チェック
            if (boid.isCharger)
            {
                boid.chargeTimer += Time.deltaTime;

                // 最大突撃時間を過ぎたら群れに戻す
                float chargeEndTime = boid.chargeDelay + boid.maxChargeDuration;
                if (boid.chargeTimer >= chargeEndTime)
                {
                    boid.ResetCharge();
                }
            }

            // ① 突撃中ならプレイヤー追尾
            if (boid.IsCharging)
            {
                if (player != null)
                {
                    Vector2 dir = ((Vector2)player.position - (Vector2)boid.transform.position).normalized;
                    boid.velocity = dir * chargeSpeed;
                    boid.transform.position += (Vector3)(boid.velocity * Time.deltaTime);
                }
                // 群衆ルールは適用しない
                continue;
            }

            // ② まだ突撃前 or 突撃役じゃない → 普通の群衆ルール
            ApplyFlockBehavior(boid);
        }
    }

    // 通常の群衆アルゴリズム
    void ApplyFlockBehavior(Boid boid)
    {
        Vector2 cohesion   = Vector2.zero;   // 近くの仲間の中心へ向かう
        Vector2 separation = Vector2.zero;   // 仲間から離れる
        Vector2 alignment  = Vector2.zero;   // 仲間の平均速度に合わせる
        int neighborCount  = 0;

        for (int i = 0; i < boids.Count; i++)
        {
            Boid other = boids[i];
            if (other == null || other == boid) continue;

            float dist = Vector2.Distance(boid.transform.position, other.transform.position);

            if (dist < neighborDistance)
            {
                cohesion += (Vector2)other.transform.position;
                alignment += other.velocity;
                neighborCount++;

                if (dist < separationDistance)
                {
                    // 近すぎる場合は反発
                    Vector2 diff = (Vector2)other.transform.position - (Vector2)boid.transform.position;
                    float safeDist = Mathf.Max(dist, 0.0001f);
                    separation -= diff.normalized / safeDist;
                }
            }
        }

        if (neighborCount > 0)
        {
            // 結合：仲間の中心 - 自分 の方向
            cohesion = (cohesion / neighborCount - (Vector2)boid.transform.position) * cohesionWeight;

            // 整列：仲間の平均速度 - 自分の速度
            alignment = (alignment / neighborCount - boid.velocity) * alignmentWeight;
        }

        separation *= separationWeight;

        // リーダー追従
        Vector2 followLeader = Vector2.zero;
        if (leader != null)
        {
            followLeader = ((Vector2)leader.position - (Vector2)boid.transform.position) * leaderWeight;
        }

        // 全ベクトルを合成
        boid.velocity += cohesion + separation + alignment + followLeader;

        // 速度を一定に保つ
        if (boid.velocity.sqrMagnitude > 0.0001f)
        {
            boid.velocity = boid.velocity.normalized * speed;
        }
        else
        {
            // 万が一ゼロになった時の保険（ランダムに少し動かす）
            boid.velocity = Random.insideUnitCircle.normalized * speed;
        }

        // 移動
        boid.transform.position += (Vector3)(boid.velocity * Time.deltaTime);
    }
}

// 個々の群れの個体を表すクラス
public class Boid : MonoBehaviour
{
    [HideInInspector] public Vector2 velocity; // 現在の速度

    // 突撃役かどうか（true になってから chargeDelay 経過で突撃開始）
    public bool isCharger = false;

    // 突撃までの待ち時間
    public float chargeDelay = 3f;

    // 突撃を続ける最大時間（BoidManager からコピーされる）
    public float maxChargeDuration = 2f;

    // 指名されてからの経過時間
    public float chargeTimer = 0f;

    // 「突撃役 かつ delay 経過 〜 最大時間まで」の間だけ true
    public bool IsCharging
    {
        get
        {
            if (!isCharger) return false;
            return chargeTimer >= chargeDelay &&
                   chargeTimer < (chargeDelay + maxChargeDuration);
        }
    }

    // 群れ状態に戻す
    public void ResetCharge()
    {
        isCharger = false;
        chargeTimer = 0f;
    }

    // プレイヤーに接触したら突撃終了 → 群れに復帰
    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤー側のオブジェクトに "Player" タグを付けておくこと
        if (other.CompareTag("Player"))
        {
            ResetCharge();
        }
    }
}
