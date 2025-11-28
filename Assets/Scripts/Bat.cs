using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bat : MonoBehaviour
{
	// ----------------------------------------
    // パラメータ
    // ----------------------------------------
    [Header("Status")]
    public float life = 10f;
    public bool useBehaviorTree = false;

    bool isInvincible = false;
    bool isHitted = false;
    Coroutine hitCoroutine;
    Coroutine deadCoroutine;

    Animator animator;
    SpriteRenderer spriteRenderer;

    // ----------------------------------------
    // 移動（身体）
    // ----------------------------------------
    [Header("Movement")]
    public bool facingLeft = true;
    public Vector2 speed = Vector2.zero;
    Vector2 position;

    // ----------------------------------------
    // AIターゲット
    // ----------------------------------------
    [Header("Target")]
    [SerializeField] Transform player;   // できればインスペクタでアサイン
                                        // 未設定なら Start で tag 検索する
    // ----------------------------------------
    // ステート
    // ----------------------------------------
    enum State
    {
        Idle,   // 待機
        Chase,  // 追跡
        Search, // 捜索
        Dead,   // 死亡
    }

    State state = State.Idle;

    // ----------------------------------------
    // AI 調整用
    // ----------------------------------------
    [Header("AI Settings")]
    public float detectionRadius = 3.0f; // 索敵開始距離
    public float lostRadius      = 5.0f; // 見失う距離
    public float chaseSpeed      = 3.0f; // 追跡速度

    public float searchDuration  = 6.0f; // 探索を続ける時間
    public float circleRadius    = 3.0f; // 探索時の円の半径
    public float circleSpeed     = 8.0f; // 円の角速度
    public float searchMoveSpeed = 5.0f; // 探索時にターゲットへ近づく速度

    float searchTimer = 0f;
    Vector3 searchCenter;
    Vector2 searchTarget;

    // ----------------------------------------
    // 歩き回る（動き回る）
    // ----------------------------------------
    [Header("Roam Settings")]
    public float roamRadius = 6.0f;
    public float roamSpeed  = 2.0f;
    public float roamTurnSpeed = 2.0f;

    Vector2 roamTarget;
    Vector2 startPosition;

    // ----------------------------------------
    // Behavior Tree (必要なら)
    // ----------------------------------------
    private TreeNode_Base rootNode = null;

    // ========================================
    // Unityイベント
    // ========================================
    void Start()
    {
        animator       = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Player 未設定なら tag で探す（名前ハードコードよりマシ）
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        startPosition = transform.position;
        roamTarget = GetRandomPosition();
        // ステート初期化
        ChangeState(State.Idle);

        // BehaviorTreeを使う場合はここでルートノード生成（ユーザー側の実装前提）
        if (useBehaviorTree)
        {
            rootNode = new Selector_IsAlive(this); // 元コードを踏襲
        }
    }

    void OnDestroy()
    {
        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
            hitCoroutine = null;
        }

        if (deadCoroutine != null)
        {
            StopCoroutine(deadCoroutine);
            deadCoroutine = null;
        }
    }

    void Update()
    {
        FirstInUpdate();

        // すでに死んでいるならステートだけ管理
        if (life <= 0f && state != State.Dead)
        {
            ChangeState(State.Dead);
        }

        if (!useBehaviorTree)
        {
            // -------------------------------
            // 手書きステートマシン
            // -------------------------------
            switch (state)
            {
                case State.Idle:
                    UpdateIdle();
                    break;

                case State.Chase:
                    UpdateChase();
                    break;

                case State.Search:
                    UpdateSearch();
                    break;

                case State.Dead:
                    UpdateDead();
                    break;
            }
        }
        else
        {
            // -------------------------------
            // Behavior Tree
            // -------------------------------
            if (rootNode != null)
            {
                rootNode.ExecuteAsRoot();
            }
        }

        // ヒットストップ中は移動だけ止める（AIは動き続ける）
        if (!isHitted)
        {
            Movement();
        }
    }

    // ========================================
    // 基本処理
    // ========================================

    void FirstInUpdate()
    {
        position = transform.position;
    }

    void Movement()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = facingLeft;
        }

        Vector3 pos = transform.position;
        pos.x += speed.x * Time.deltaTime;
        pos.y += speed.y * Time.deltaTime;

        // 簡易地形あたり
        if (pos.y < 0.8f)
        {
            pos.y = 0.8f;
        }

        transform.position = pos;
    }

    void ChangeState(State next)
    {
        state = next;

        // ステート切り替え時に初期化したいものがあればここで
        if (state == State.Search)
        {
            searchTimer  = searchDuration;
            searchCenter = transform.position;

            searchTarget = searchCenter + (Vector3)(Random.insideUnitCircle * circleRadius);
            if (searchTarget.y < 1.0f) searchTarget.y = 1.0f;
        }
        else if (state == State.Idle)
        {
            // Idleに戻った時、最初の目的地を再設定
            roamTarget = GetFarRandomPosition();
        }
    }

    Vector2 GetFarRandomPosition()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 candidate = startPosition + (Random.insideUnitCircle * roamRadius);
            if (candidate.y < 1.0f) candidate.y = 1.0f;

            if (Vector2.Distance(candidate, transform.position) > 2.0f)
            {
                return candidate;
            }
        }
        return startPosition;
    }

    // ========================================
    // 各ステート処理
    // ========================================

    // Idle：その場で待機（プレイヤーが一定距離に来たら追跡）
    // →プレイヤーを見つけるまで、ゆらゆら動く

    float hoverSpeed = 2.0f;
    float hoverAmplitude = 1.0f;
    void UpdateIdle()
    {
        if (spriteRenderer != null) spriteRenderer.color = Color.white;

        // --- 1. 目的地との距離チェック ---
        float distToTarget = Vector2.Distance(transform.position, roamTarget);

        if (distToTarget < 0.5f)
        {
            roamTarget = GetRandomPosition();
        }

        // --- 2. 滑らかな移動（ここがポイント） ---
        Vector2 direction = (roamTarget - (Vector2)transform.position).normalized;
        Vector2 desiredVelocity = direction * roamSpeed;

        speed = Vector2.Lerp(speed, desiredVelocity, Time.deltaTime * roamTurnSpeed);

        // --- 3. 向きの見た目更新 ---
        if (speed.x > 0.1f) facingLeft = false;
        if (speed.x < -0.1f) facingLeft = true;

        // --- 4. プレイヤー発見処理（変更なし） ---
        if (player == null) return;
        if (Vector3.Distance(transform.position, player.position) < detectionRadius)
        {
            ChangeState(State.Chase);
        }
    }

    Vector2 GetRandomPosition()
    {
        // startPositionを中心に、roamRadiusの範囲内でランダムな場所を取得
        Vector2 randomPoint = startPosition + (Random.insideUnitCircle * roamRadius);
        if (randomPoint.y < 1.0f) randomPoint.y = 1.0f;

        return randomPoint;
    }

    // Chase：プレイヤーを追いかける
    void UpdateChase()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }

        if (player == null)
        {
            // プレイヤーが消えたら Idle 戻り
            ChangeState(State.Idle);
            return;
        }

        float distance   = Vector3.Distance(transform.position, player.position);
        Vector3 dir      = (player.position - transform.position).normalized;

        speed.x = dir.x * chaseSpeed;
        speed.y = dir.y * chaseSpeed;

        // 向き
        facingLeft = (player.position.x < transform.position.x);

        // 一定以上離れたら Search へ
        if (distance > lostRadius)
        {
            ChangeState(State.Search);
        }
    }
    void UpdateSearch()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.yellow;
        }

        searchTimer -= Time.deltaTime;

        // 目的地との距離を測る
        float dist = Vector2.Distance(transform.position, searchTarget);

        // 目的地に近づいたら、すぐ次の場所へ！（待機しない＝焦っている表現）
        if (dist < 0.5f)
        {
            Vector2 randomPoint = (Vector2)searchCenter + (Random.insideUnitCircle * circleRadius);
            if (randomPoint.y < 1.0f) randomPoint.y = 1.0f;
            searchTarget = randomPoint;
        }

        Vector2 direction = ((Vector2)searchTarget - (Vector2)transform.position).normalized;
        Vector2 desiredVelocity = direction * searchMoveSpeed;

        speed = Vector2.Lerp(speed, desiredVelocity, Time.deltaTime * roamTurnSpeed);

        // 向き更新
        if (speed.x > 0.1f)  facingLeft = false;
        if (speed.x < -0.1f) facingLeft = true;

        // プレイヤーを再発見したら追跡に戻る
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance < detectionRadius)
            {
                ChangeState(State.Chase);
                return;
            }
        }

        // 探索時間が終わったら Idle に戻る
        if (searchTimer <= 0f)
        {
            ChangeState(State.Idle);
        }
    }

    // Dead：死亡アニメ再生＋一定時間後に消す
    void UpdateDead()
    {
        speed.x = 0f;
        // 死亡後に少し落下させたければここで y に重力を足す
        // speed.y -= 9.8f * Time.deltaTime;

        if (animator != null)
        {
            animator.SetBool("IsDead", true);
        }

        // コルーチンを1回だけ起動
        if (deadCoroutine == null)
        {
            deadCoroutine = StartCoroutine(DestroyEnemy());
        }
    }

    // ========================================
    // ダメージ・接触
    // ========================================

    public void ApplyDamage(float damage)
    {
        if (life <= 0f) return;
        if (isInvincible) return;

        // direction が欲しければ使えるようにしておく（今は未使用）
        float direction = damage / Mathf.Abs(damage);
        damage          = Mathf.Abs(damage);

        life -= damage;

        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
        }
        hitCoroutine = StartCoroutine(HitTime());
    }

    IEnumerator HitTime()
    {
        isHitted    = true;
        isInvincible = true;
        yield return new WaitForSeconds(0.5f);
        isHitted    = false;
        isInvincible = false;
        hitCoroutine = null;
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.CompareTag("Player") && life > 0f)
        {
            var controller = collider.GetComponent<CharacterController2D>();
            if (controller != null)
            {
                controller.ApplyDamage(2f, transform.position);
            }
        }
    }

    IEnumerator DestroyEnemy()
    {
        // 死亡演出の時間
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    // ========================================
    // デバッグ表示(Gizmos)
    // ========================================
    void OnDrawGizmos()
    {
        // 発見する距離（赤）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 見失う距離（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lostRadius);

        // 捜索範囲（青）
        if (Application.isPlaying && state == State.Search)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(searchCenter, circleRadius);
        }
    }

}
