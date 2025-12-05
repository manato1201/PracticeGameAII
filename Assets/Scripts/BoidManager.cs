using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    [Header("Boid設定")]
    public GameObject boidPrefab;   // 群れの個体となるPrefab
    public int boidCount = 100;     // 個体数
    public float speed = 2f;        // 通常時の移動速度

    [Header("距離の設定")]
    public float neighborDistance = 4f;     // 仲間を認識する距離
    public float separationDistance = 1f;   // 近づきすぎた場合に離れる距離

    [Header("動作の強さの設定")]
    public float cohesionWeight   = 0.03f;  // 結合の強さ
    public float separationWeight = 0.08f;  // 反発の強さ
    public float alignmentWeight  = 0.05f;  // 整列の強さ
    public float leaderWeight     = 0.02f;  // リーダーに従う強さ

    [Header("リーダー設定")]
    public Transform leader;                // 群れが追いかけるリーダー（いなければ null）

    [Header("突撃設定")]
    public Transform player;                // 突撃先（プレイヤー）
    public float chargeDelay        = 3f;   // 指名されてから何秒後に突撃開始するか
    public float maxChargeDuration  = 2f;   // 突撃を続ける最大時間（これを超えたら戻る）
    public float chargeInterval     = 5f;   // 何秒ごとに新しい突撃役を選ぶか
    public int   maxConcurrentChargers = 3; // 同時に突撃状態になれる最大人数
    public float chargeAcceleration = 10f;  // 突撃時の加速度
    public float maxChargeSpeed     = 8f;   // 突撃時の最大速度
    [Range(3, 12)]
    public int sectorCount = 4;             // 角度セクター数（波状攻撃用）

    [Header("防衛 / シールド設定")]
    public Transform protectTarget;         // 守る対象（Soldier / Bat など）
    public float protectWeight   = 0.05f;   // シールド行動の基本重み
    public float surroundRadius  = 4f;      // Surround 時にターゲット周辺に集まる半径

    [Header("隊ステート設定")]
    public float attackDistance        = 10f; // プレイヤーが近いと攻撃モード
    public int   retreatBoidThreshold  = 3;   // これ未満になったら退却
    public float retreatDuration       = 3f;  // 退却が続く時間
    public float chargeWaveDuration    = 2.5f;// ChargeWave の継続時間
    public float surroundDistance      = 8f;  // プレイヤーがこの距離以内なら Surround

    public enum FlockState
    {
        Idle,       // うろつき
        Surround,   // 守る対象を囲む
        ChargeWave, // 波状攻撃中
        Retreat     // 退却中
    }

    [SerializeField]
    public FlockState flockState = FlockState.Idle;

    // 内部タイマー
    private float chargeIntervalTimer = 0f;
    private float chargeWaveTimer     = 0f;
    private float retreatTimer        = 0f;

    private readonly List<Boid> boids = new List<Boid>();

    //====================================================================
    // 初期化
    //====================================================================
    void Start()
    {
        // Boid生成
        for (int i = 0; i < boidCount; i++)
        {
            Vector2 pos = Random.insideUnitCircle * 5f;
            var boidObj = Instantiate(boidPrefab, pos, Quaternion.identity);
            var boid    = boidObj.AddComponent<Boid>();

            boid.velocity          = Random.insideUnitCircle.normalized * speed;
            boid.isCharger         = false;
            boid.chargeDelay       = chargeDelay;
            boid.maxChargeDuration = maxChargeDuration;

            boids.Add(boid);
        }
    }

    //====================================================================
    // 毎フレーム更新
    //====================================================================
    void Update()
    {
        // 破棄済みBoidを掃除（MissingReference対策）
        boids.RemoveAll(b => b == null);

        // 隊全体ステート更新
        UpdateFlockState();

        // 突撃役の選出（ChargeWave のトリガー）
        HandleChargeSelection();

        // 各 Boid 更新
        for (int i = 0; i < boids.Count; i++)
        {
            Boid boid = boids[i];
            if (boid == null) continue;

            UpdateBoid(boid);
        }
    }

    //====================================================================
    // 隊全体ステート更新
    //====================================================================
    void UpdateFlockState()
    {
        if (boids.Count == 0)
        {
            flockState = FlockState.Idle;
            return;
        }

        // ChargeWave 中はタイマーが切れるまで固定
        if (chargeWaveTimer > 0f)
        {
            chargeWaveTimer -= Time.deltaTime;
            if (chargeWaveTimer > 0f)
            {
                flockState = FlockState.ChargeWave;
                return;
            }
        }

        // Retreat 中もタイマーが切れるまで固定
        if (retreatTimer > 0f)
        {
            retreatTimer -= Time.deltaTime;
            flockState = FlockState.Retreat;
            return;
        }

        // Boid 残数が閾値より少なくなったら退却
        if (boids.Count <= retreatBoidThreshold)
        {
            retreatTimer = retreatDuration;
            flockState   = FlockState.Retreat;
            return;
        }

        // Surround 判定
        if (protectTarget != null && player != null)
        {
            float d = Vector2.Distance(protectTarget.position, player.position);
            if (d < surroundDistance)
            {
                flockState = FlockState.Surround;
                return;
            }
        }

        // それ以外は Idle
        flockState = FlockState.Idle;
    }

    //====================================================================
    // 波状攻撃用：一定間隔で Charger をセクターごとに割り当て
    //====================================================================
    void HandleChargeSelection()
    {
        if (player == null || boids.Count == 0) return;
        if (flockState == FlockState.Retreat) return; // 退却中は突撃しない

        chargeIntervalTimer += Time.deltaTime;
        if (chargeIntervalTimer < chargeInterval) return;
        chargeIntervalTimer = 0f;

        // 現在の突撃役数
        int currentChargerCount = 0;
        for (int i = 0; i < boids.Count; i++)
        {
            var b = boids[i];
            if (b != null && b.isCharger)
                currentChargerCount++;
        }

        int capacity = maxConcurrentChargers - currentChargerCount;
        if (capacity <= 0) return;

        // セクターごとに Charger を割り当て
        int assigned = AssignChargersBySector(capacity);

        // 1体でも新規に突撃役が生まれたら、ChargeWave 状態に移行
        if (assigned > 0)
        {
            flockState      = FlockState.ChargeWave;
            chargeWaveTimer = chargeWaveDuration;
        }
    }

    //====================================================================
    // セクターごとに Charger を割り当てる
    //====================================================================
    int AssignChargersBySector(int capacity)
    {
        if (player == null || boids.Count == 0) return 0;
        if (sectorCount <= 0) sectorCount = 4;

        // セクターごとに候補を分類
        List<Boid>[] sectorBoids = new List<Boid>[sectorCount];
        for (int s = 0; s < sectorCount; s++)
            sectorBoids[s] = new List<Boid>();

        for (int i = 0; i < boids.Count; i++)
        {
            Boid b = boids[i];
            if (b == null || b.isCharger) continue;

            int idx = GetSectorIndex(b.transform.position, player.position, sectorCount);
            sectorBoids[idx].Add(b);
        }

        // 候補があるセクターだけを列挙
        List<int> nonEmptySectors = new List<int>();
        for (int s = 0; s < sectorCount; s++)
        {
            if (sectorBoids[s].Count > 0)
                nonEmptySectors.Add(s);
        }

        if (nonEmptySectors.Count == 0) return 0;

        // ランダムなセクターを一つ選ぶ
        int chosenSector = nonEmptySectors[Random.Range(0, nonEmptySectors.Count)];
        List<Boid> candidates = sectorBoids[chosenSector];

        int toAssign = Mathf.Min(capacity, candidates.Count);
        for (int n = 0; n < toAssign; n++)
        {
            int idx = Random.Range(0, candidates.Count);
            Boid chosen = candidates[idx];
            candidates.RemoveAt(idx);

            chosen.isCharger  = true;
            chosen.chargeTimer = 0f;
        }

        return toAssign;
    }

    int GetSectorIndex(Vector2 from, Vector2 to, int sectorCount)
    {
        Vector2 dir = (to - from).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x);    // -PI〜PI
        if (angle < 0f) angle += Mathf.PI * 2f;     // 0〜2PI

        float sectorSize = (Mathf.PI * 2f) / sectorCount;
        int idx = Mathf.FloorToInt(angle / sectorSize);
        if (idx >= sectorCount) idx = sectorCount - 1;
        if (idx < 0) idx = 0;
        return idx;
    }

    //====================================================================
    // 各 Boid の更新処理
    //====================================================================
    void UpdateBoid(Boid boid)
    {
        if (boid == null) return;

        // まず基本の群れ行動
        ApplyFlockBehavior(boid);

        // 突撃役なら突撃処理を追加
        if (boid.isCharger)
        {
            boid.chargeTimer += Time.deltaTime;
            float chargeStartTime = boid.chargeDelay;
            float chargeEndTime   = boid.chargeDelay + boid.maxChargeDuration;

            if (player != null && boid.chargeTimer >= chargeStartTime)
            {
                Vector2 dir = ((Vector2)player.position - (Vector2)boid.transform.position).normalized;
                boid.velocity += dir * (chargeAcceleration * Time.deltaTime);
            }

            // 時間切れで群れに復帰
            if (boid.chargeTimer >= chargeEndTime)
            {
                boid.ResetCharge();
            }
        }

        // 速度制限
        float limitSpeed = boid.isCharger ? maxChargeSpeed : speed;
        if (boid.velocity.sqrMagnitude > 0.0001f)
        {
            boid.velocity = Vector2.ClampMagnitude(boid.velocity, limitSpeed);
        }
        else
        {
            // 完全停止していた場合の保険
            boid.velocity = Random.insideUnitCircle.normalized * speed;
        }

        // 実際の移動
        boid.transform.position += (Vector3)(boid.velocity * Time.deltaTime);
    }

    //====================================================================
    // 群れ行動（cohesion / separation / alignment / leader / protect）
    //====================================================================
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
                cohesion  += (Vector2)other.transform.position;
                alignment += other.velocity;
                neighborCount++;

                if (dist < separationDistance)
                {
                    // 近すぎる場合は反発
                    Vector2 diff     = (Vector2)other.transform.position - (Vector2)boid.transform.position;
                    float safeDist   = Mathf.Max(dist, 0.0001f);
                    separation      -= diff.normalized / safeDist;
                }
            }
        }

        if (neighborCount > 0)
        {
            // 結合：仲間の中心 - 自分 の方向
            cohesion = (cohesion / neighborCount - (Vector2)boid.transform.position);
            // 整列：平均速度 - 自分の速度
            alignment = (alignment / neighborCount - boid.velocity);
        }

        // 基本重み
        float cW = cohesionWeight;
        float sW = separationWeight;
        float aW = alignmentWeight;
        float lW = leaderWeight;
        float pW = protectWeight;

        // ステートによる重み調整
        switch (flockState)
        {
            case FlockState.Idle:
                // デフォルト
                break;

            case FlockState.Surround:
                // まとまりを強め、多少密集
                cW *= 2f;
                sW *= 0.7f;
                pW *= 1.5f;
                break;

            case FlockState.ChargeWave:
                // ばらけつつ攻撃する感じ
                sW *= 1.5f;
                cW *= 0.8f;
                pW *= 0.5f;
                break;

            case FlockState.Retreat:
                // リーダー追従を強めて逃げる
                lW *= 3f;
                cW *= 0.3f;
                pW  = 0f;
                break;
        }

        cohesion   *= cW;
        separation *= sW;
        alignment  *= aW;

        // リーダー追従 or プレイヤーから離れる
        Vector2 followLeader = Vector2.zero;
        if (leader != null)
        {
            followLeader = ((Vector2)leader.position - (Vector2)boid.transform.position) * lW;
        }
        else if (flockState == FlockState.Retreat && player != null)
        {
            Vector2 away = ((Vector2)boid.transform.position - (Vector2)player.position).normalized;
            followLeader = away * lW * 5f;
        }

        // シールド行動（守る対象とプレイヤーの中間へ寄る）
        Vector2 protectDir = Vector2.zero;
        if (protectTarget != null && player != null)
        {
            Vector2 line = (Vector2)(protectTarget.position - player.position);
            Vector2 mid  = (Vector2)player.position + line * 0.5f;

            // Surround のときは守る対象の周囲にリング状に配置したいので、少し中心寄りに
            if (flockState == FlockState.Surround)
            {
                Vector2 toBoid = (Vector2)boid.transform.position - (Vector2)protectTarget.position;
                if (toBoid.sqrMagnitude > 0.001f)
                {
                    Vector2 ringPos = (Vector2)protectTarget.position + toBoid.normalized * surroundRadius;
                    mid = Vector2.Lerp(mid, ringPos, 0.6f);
                }
            }

            protectDir = (mid - (Vector2)boid.transform.position) * pW;
        }

        // 最終的なベクトル合成
        boid.velocity += cohesion + separation + alignment + followLeader + protectDir;
    }
}


