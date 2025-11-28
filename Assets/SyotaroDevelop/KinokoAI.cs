using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    // --- インスペクター設定項目 ---

    [Header("ターゲット設定")]
    [Tooltip("プレイヤーのタグ")]
    public string playerTag = "Player";
    private Transform playerTransform;

    [Header("アニメーションとコンポーネント")]
    public Animator animator;
    public Rigidbody2D rb;
    private RigidbodyType2D originalRbType = RigidbodyType2D.Dynamic;

    [Header("巡回/待ち伏せ時間")]
    public float idleDuration = 3f;
    public float ambushDuration = 20f;
    private bool isExecutingRoutine = false;
    private Coroutine routineCoroutine;
    [Tooltip("Ambush/Idle間のアニメーション遷移に必要な待ち時間")]
    public float routineTransitionTime = 0.5f;

    [Header("行動範囲の距離と速度")]
    [Tooltip("Runに遷移する距離")]
    public float runRange = 8f;
    [Tooltip("Attackに遷移する距離")]
    public float attackRange = 2f;
    public float moveSpeed = 5f;

    [Header("攻撃制御")]
    [Tooltip("連続攻撃を防ぐためのフラグ")]
    public bool canExecuteAttack = true;
    [Tooltip("攻撃クールダウンの長さ（アニメーション時間より長く推奨）")]
    public float attackCooldownTime = 1.0f;

    // ★★★ ダメージと被弾状態に必要な変数を追加 ★★★
    [Header("ライフと被弾")]
    public float life = 100f; // ライフポイント
    public bool isInvincible = false; // 無敵状態フラグ
    public bool isHitted = false; // 被弾中フラグ
    private Coroutine hitCoroutine; // HitTimeコルーチンの参照
    // ★★★ ------------------------------------ ★★★

    // --- アニメーションパラメータ名 ---
    private const string RUN_BOOL = "Run";
    private const string ATTACK_TRIGGER = "Attack";
    private const string IS_AMBUSH_BOOL = "IsAmbush";
    private const string ANIM_END_TRIGGER = "AnimEnd";

    // --- 内部状態 ---
    private enum EnemyState { Routine, Attacking, Running }
    private EnemyState currentState = EnemyState.Routine;

    // ------------------------------------

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            originalRbType = rb.bodyType;
        }
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogError("PlayerTag: " + playerTag + " のオブジェクトが見つかりません。");
        }

        if (animator != null)
        {
            animator.SetBool(IS_AMBUSH_BOOL, false);
        }

        routineCoroutine = StartCoroutine(RoutineLoop());
    }

    void Update()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // ★ 被弾中はAIロジックを停止
        if (isHitted)
        {
            // 被弾中は移動を強制停止
            rb.linearVelocity = Vector2.zero;
            animator.SetBool(RUN_BOOL, false);
            return;
        }

        if (currentState == EnemyState.Attacking || !canExecuteAttack)
        {
            animator.SetBool(RUN_BOOL, false);
            return;
        }

        // ------------------ 行動ロジック ------------------

        if (distanceToPlayer <= attackRange)
        {
            if (canExecuteAttack)
            {
                SetState(EnemyState.Attacking);
                AttackPlayer();
            }
        }
        else if (distanceToPlayer <= runRange)
        {
            // --- 追跡 (Run) ---
            if (currentState != EnemyState.Running)
            {
                SetState(EnemyState.Running);

                // Routineを停止
                if (routineCoroutine != null)
                {
                    StopCoroutine(routineCoroutine);
                    routineCoroutine = null;
                }
                isExecutingRoutine = false;

                // 追跡開始時、Ambushアニメーションを確実に終了させる
                animator.SetBool(IS_AMBUSH_BOOL, false);
            }

            // Runningステート中は、常にRunアニメーションを有効にする
            animator.SetBool(RUN_BOOL, true);

            MoveToPlayer();
        }
        else
        {
            // --- 巡回/待ち伏せ (Routine) ---
            if (currentState != EnemyState.Routine)
            {
                SetState(EnemyState.Routine);

                // 移動を明示的に停止する
                rb.linearVelocity = Vector2.zero;

                // Runアニメーションを確実にオフにする
                animator.SetBool(RUN_BOOL, false);
            }

            // ルーチンが停止していたら再開
            if (!isExecutingRoutine)
            {
                if (routineCoroutine == null)
                {
                    routineCoroutine = StartCoroutine(RoutineLoop());
                }
            }
        }
    }

    // --- ステート管理 ---
    private void SetState(EnemyState newState)
    {
        currentState = newState;
    }

    // ------------------------------------
    // --- 行動実装部分 ---
    // ------------------------------------

    // IdleとAmbushを繰り返すルーチン
    IEnumerator RoutineLoop()
    {
        isExecutingRoutine = true;
        while (currentState == EnemyState.Routine)
        {
            // 1. Idle (待機)
            if (currentState != EnemyState.Routine) break;
            animator.SetBool(IS_AMBUSH_BOOL, false);

            yield return new WaitForSeconds(routineTransitionTime);

            Debug.Log("State: Idle");

            if (currentState != EnemyState.Routine) break;
            yield return new WaitForSeconds(idleDuration);

            // 2. Ambush (待ち伏せ)

            if (currentState != EnemyState.Routine) break;
            animator.SetBool(IS_AMBUSH_BOOL, true);

            yield return new WaitForSeconds(routineTransitionTime);

            Debug.Log("State: Ambush");

            if (currentState != EnemyState.Routine) break;
            yield return new WaitForSeconds(ambushDuration);
        }
        isExecutingRoutine = false;
        routineCoroutine = null;
    }

    // Run (プレイヤーを追跡)
    void MoveToPlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);
        Flip(direction.x);
    }

    // Attack (プレイヤーを攻撃)
    void AttackPlayer()
    {
        canExecuteAttack = false;

        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            originalRbType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        rb.linearVelocity = Vector2.zero;

        animator.SetTrigger(ATTACK_TRIGGER);

        StartCoroutine(AttackCooldown(attackCooldownTime));
    }

    // 攻撃クールダウンコルーチン
    IEnumerator AttackCooldown(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (currentState == EnemyState.Attacking)
        {
            // クールダウンが終了したら、強制的にRunningステートに戻す
            SetState(EnemyState.Running);
        }

        rb.bodyType = originalRbType;

        canExecuteAttack = true;
    }

    // 向きの反転
    void Flip(float moveDirection)
    {
        if (moveDirection > 0 && transform.localScale.x < 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (moveDirection < 0 && transform.localScale.x > 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    // アニメーションイベントから呼び出すためのメソッド
    public void AnimEnd()
    {
        animator.SetTrigger(ANIM_END_TRIGGER);

        if (currentState == EnemyState.Attacking)
        {
            SetState(EnemyState.Running);
        }
    }

    // ★★★ 修正されたダメージ処理 ★★★
    public void ApplyDamage(float damage)
    {
        if (!isInvincible)
        {
            // ★ RoutineLoopを停止し、アニメーションの上書きを防ぐ
            if (routineCoroutine != null)
            {
                StopCoroutine(routineCoroutine);
                routineCoroutine = null;
            }
            isExecutingRoutine = false;

            // ダメージの向きを計算 (正なら右から、負なら左から)
            float direction = damage / Mathf.Abs(damage);
            damage = Mathf.Abs(damage);

            animator.SetBool("Hit", true);
            life -= damage;

            // ノックバック処理
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(direction * 500f, 100f));

            if (hitCoroutine != null)
            {
                StopCoroutine(hitCoroutine);
            }
            hitCoroutine = StartCoroutine(HitTime());
        }
    }

    // 攻撃を受けた後、一定時間無敵になる
    IEnumerator HitTime()
    {
        isHitted = true;
        isInvincible = true;

        yield return new WaitForSeconds(0.3f);

        isHitted = false;
        isInvincible = false;
        animator.SetBool("Hit", false);

        hitCoroutine = null;

        // ★ 被弾処理終了後、次のステートを決定する
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= runRange)
            {
                // プレイヤーが近くにいたら追跡を再開
                SetState(EnemyState.Running);
            }
            else
            {
                // プレイヤーが遠くにいたらRoutineを再開
                SetState(EnemyState.Routine);
            }
        }
        else
        {
            SetState(EnemyState.Routine);
        }
    }
}