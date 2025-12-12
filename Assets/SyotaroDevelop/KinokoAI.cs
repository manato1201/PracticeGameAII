using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    // --- インスペクター設定項目 ---

    [Header("ターゲット設定")]
    [Tooltip("プレイヤーのタグ")]
    public string playerTag = "Player";
    public string enemyTag = "Enemy";
    private Transform playerTransform; // 通常ターゲット
    private Transform currentTarget;   // 激怒時のターゲット

    [Header("アニメーションとコンポーネント")]
    public Animator animator;
    public Rigidbody2D rb;
    private SpriteRenderer sp;
    private RigidbodyType2D originalRbType = RigidbodyType2D.Dynamic;

    [Header("巡回/待ち伏せ時間")]
    public float idleDuration = 3f;
    public float ambushDuration = 20f;
    private bool isExecutingRoutine = false;
    private Coroutine routineCoroutine;
    [Tooltip("Ambush/Idle間のアニメーション遷移に必要な待ち時間")]
    public float routineTransitionTime = 0.5f;

    [Header("スケール遷移設定 (Ambush用)")]
    [Tooltip("アンブッシュ時の目標スケール (例: 0.2f で 1/5)")]
    public float ambushTargetScale = 0.2f;
    [Tooltip("スケール変更にかける時間")]
    public float scaleTransitionTime = 0.5f;
    private Vector3 originalScale;

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
    [Tooltip("実際にダメージを与えるための攻撃判定範囲")]
    public float attackRangeForDamage = 4.0f;
    [Tooltip("攻撃ダメージ量")]
    public float attackDamage = 1f;

    public Transform AttackPoint;

    [Header("ライフと被弾")]
    public float maxLife = 100f;
    public float life = 100f;
    public bool isInvincible = false;
    public bool isHitted = false;
    private Coroutine hitCoroutine;

    [Header("激怒状態設定 (HPが半分以下で移行)")]
    [Tooltip("激怒状態に移行するHPの割合 (例: 0.5f で HP 50%以下)")]
    public float enragedHpRatio = 0.5f;
    [Tooltip("激怒状態での移動速度")]
    public float enragedMoveSpeed = 8f;
    private bool isEnraged = false;

    // --- アニメーションパラメータ名 ---
    private const string RUN_BOOL = "Run";
    private const string ATTACK_TRIGGER = "Attack";
    private const string IS_AMBUSH_BOOL = "IsAmbush";
    private const string ANIM_END_TRIGGER = "AnimEnd";
    private const string DEATH_TRIGGER = "Death";

    // --- 内部状態 ---
    private enum EnemyState { Routine, Attacking, Running, Enraged, Dead }
    private EnemyState currentState = EnemyState.Routine;

    // ------------------------------------

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (sp == null) sp = GetComponent<SpriteRenderer>();

        if (rb != null)
        {
            originalRbType = rb.bodyType;
        }

        originalScale = transform.localScale;
        maxLife = life;
        currentTarget = playerTransform;
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            currentTarget = playerTransform;
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
        if (currentState == EnemyState.Dead) return;

        // 激怒状態であれば、ターゲットを更新
        if (isEnraged)
        {
            currentTarget = FindClosestTarget();
            if (currentTarget == null)
            {
                // ターゲットがいなくなったら激怒解除しRoutineへ
                isEnraged = false;
                SetState(EnemyState.Routine);
                return;
            }
        }
        else
        {
            currentTarget = playerTransform; // 通常状態ではプレイヤーをターゲット
        }

        if (currentTarget == null) return;

        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
        float currentMoveSpeed = isEnraged ? enragedMoveSpeed : moveSpeed;


        // 被弾中は移動・アニメーションを一時停止
        if (isHitted)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetBool(RUN_BOOL, false);
            return;
        }

        // ------------------ 行動ロジック ------------------

        if (currentState == EnemyState.Attacking || !canExecuteAttack)
        {
            // 攻撃中/クールダウン中は移動を停止し、向きだけターゲットに合わせる (★修正ポイント)
            rb.linearVelocity = Vector2.zero;
            animator.SetBool(RUN_BOOL, false);

            // 攻撃中でも向きを変える
            Vector2 direction = (currentTarget.position - transform.position).normalized;
            Flip(direction.x);

            return;
        }


        // 攻撃範囲内
        if (distanceToTarget <= attackRange)
        {
            if (canExecuteAttack)
            {
                SetState(EnemyState.Attacking);
                AttackPlayer();
            }
        }
        // 追跡範囲内、または激怒状態（常に追跡）
        else if (isEnraged || distanceToTarget <= runRange)
        {
            // --- 追跡 (Run / Enraged) ---
            if (currentState != EnemyState.Running && currentState != EnemyState.Enraged)
            {
                // Routineからの移行処理
                SetState(isEnraged ? EnemyState.Enraged : EnemyState.Running);

                if (routineCoroutine != null)
                {
                    StopCoroutine(routineCoroutine);
                    routineCoroutine = null;
                }
                isExecutingRoutine = false;

                // 激怒または追跡に移行する場合、擬態（Ambush）を解除
                animator.SetBool(IS_AMBUSH_BOOL, false);
                if (transform.localScale != originalScale)
                {
                    transform.localScale = originalScale;
                }
            }

            // ステートがRunまたはEnragedであることを確認
            if (currentState == EnemyState.Running || currentState == EnemyState.Enraged)
            {
                animator.SetBool(RUN_BOOL, true);
                MoveToTarget(currentTarget, currentMoveSpeed);
            }
        }
        // 巡回/待ち伏せ (通常状態かつ範囲外)
        else
        {
            // --- 巡回/待ち伏せ (Routine) ---
            if (currentState != EnemyState.Routine)
            {
                SetState(EnemyState.Routine);

                rb.linearVelocity = Vector2.zero;
                animator.SetBool(RUN_BOOL, false);
            }

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

    IEnumerator RoutineLoop()
    {
        isExecutingRoutine = true;
        while (currentState == EnemyState.Routine)
        {
            // 1. Idle (待機)
            if (currentState != EnemyState.Routine) break;

            yield return StartCoroutine(ScaleTransition(originalScale, scaleTransitionTime));

            animator.SetBool(IS_AMBUSH_BOOL, false);

            yield return new WaitForSeconds(routineTransitionTime);

            Debug.Log("State: Idle");

            if (currentState != EnemyState.Routine) break;
            yield return new WaitForSeconds(idleDuration);

            // 2. Ambush (待ち伏せ)

            if (currentState != EnemyState.Routine) break;
            animator.SetBool(IS_AMBUSH_BOOL, true);

            Vector3 targetScale = new Vector3(originalScale.x * ambushTargetScale, originalScale.y * ambushTargetScale, originalScale.z);
            yield return StartCoroutine(ScaleTransition(targetScale, scaleTransitionTime));

            yield return new WaitForSeconds(routineTransitionTime);

            Debug.Log("State: Ambush");

            if (currentState != EnemyState.Routine) break;
            yield return new WaitForSeconds(ambushDuration);
        }

        // ループ終了時（Routineから他の状態へ遷移したとき）
        if (transform.localScale != originalScale)
        {
            transform.localScale = originalScale;
        }

        isExecutingRoutine = false;
        routineCoroutine = null;
    }

    IEnumerator ScaleTransition(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (transform.localScale == targetScale)
                break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    void MoveToTarget(Transform target, float speed)
    {
        Vector2 direction = (target.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * speed, rb.linearVelocity.y);
        Flip(direction.x);
    }

    Transform FindClosestTarget()
    {
        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity;

        // プレイヤーをチェック
        if (playerTransform != null)
        {
            float distanceSqr = (playerTransform.position - transform.position).sqrMagnitude;
            minDistanceSqr = distanceSqr;
            closestTarget = playerTransform;
        }

        // 他の敵をチェック
        // EnemyAIがアタッチされているオブジェクトがEnemyタグを持っていることを前提
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        foreach (GameObject enemy in enemies)
        {
            // 自分自身は無視
            if (enemy == gameObject) continue;

            if (enemy.GetComponent<EnemyAI>() != null)
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestTarget = enemy.transform;
            }
        }

        return closestTarget;
    }


    void AttackPlayer()
    {
        canExecuteAttack = false;

        // 攻撃アニメーション中は動かないようにするが、
        // 向きはUpdateで変更するため、rb.bodyTypeの変更は維持する
        if (rb.bodyType != RigidbodyType2D.Kinematic)
        {
            originalRbType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        rb.linearVelocity = Vector2.zero; // 念のため速度をリセット

        animator.SetTrigger(ATTACK_TRIGGER);

        StartCoroutine(AttackCooldown(attackCooldownTime));
    }

    IEnumerator AttackCooldown(float duration)
    {
        yield return new WaitForSeconds(duration);

        // クールダウン終了後、元のボディタイプに戻す
        rb.bodyType = originalRbType;

        if (currentState == EnemyState.Attacking)
        {
            // 激怒状態であれば、Enraged状態に戻る
            if (isEnraged)
            {
                SetState(EnemyState.Enraged);
            }
            else
            {
                SetState(EnemyState.Running);
            }
        }

        canExecuteAttack = true;
    }

    void Flip(float moveDirection)
    {
        // ターゲットの方向が右向き(正)で、現在のスケールが左向き(負)なら反転
        if (moveDirection > 0 && transform.localScale.x < 0)
        {
            // Absを使って、localScale.xが元々負の値だったとしても絶対値を使うことで左右の反転だけを制御
            transform.localScale = new Vector3(Mathf.Abs(originalScale.x), transform.localScale.y, transform.localScale.z);
        }
        // ターゲットの方向が左向き(負)で、現在のスケールが右向き(正)なら反転
        else if (moveDirection < 0 && transform.localScale.x > 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(originalScale.x), transform.localScale.y, transform.localScale.z);
        }
        // Ambushによってスケールが変更されている場合があるため、元のスケールの絶対値を参照するように修正（より堅牢に）
    }

    public void AnimEnd()
    {
        animator.SetTrigger(ANIM_END_TRIGGER);

        if (currentState == EnemyState.Attacking && AttackPoint != null)
        {
            Vector2 attackCenter = AttackPoint.position;
            float attackRadius = attackRangeForDamage;

            Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackCenter, attackRadius);

            foreach (Collider2D hit in hitObjects)
            {
                GameObject other = hit.gameObject;

                if (other == gameObject) continue;

                float damageDirection = (other.transform.position.x > transform.position.x) ? 1f : -1f;

                if (other.tag == "Player")
                {
                    CharacterController2D playerController = other.GetComponent<CharacterController2D>();

                    if (playerController != null)
                    {
                        // 攻撃の原点を渡してダメージ適用
                        playerController.ApplyDamage(attackDamage, attackCenter);
                        Debug.Log("Playerにダメージを与えました: " + attackDamage);
                    }
                }
                else if (other.tag == "Enemy")
                {
                    float damageToApply = attackDamage * damageDirection;
                    bool damaged = false;

                    // 複数コンポーネントのチェック（他の敵のHPも減らす）
                    Enemy enemyController = other.GetComponent<Enemy>();
                    if (enemyController != null)
                    {
                        enemyController.ApplyDamage(damageToApply);
                        damaged = true;
                    }

                    Soldier soldier = other.GetComponent<Soldier>();
                    if (soldier != null)
                    {
                        soldier.ApplyDamage(damageToApply);
                        damaged = true;
                    }

                    Bat bat = other.GetComponent<Bat>();
                    if (bat != null)
                    {
                        bat.ApplyDamage(damageToApply);
                        damaged = true;
                    }

                    if (damaged)
                    {
                        Debug.Log("他のEnemyにダメージを与えました: " + attackDamage);
                    }
                }
            }
        }

        if (currentState == EnemyState.Attacking)
        {
            // 激怒状態であれば、Enraged状態に戻る
            if (isEnraged)
            {
                SetState(EnemyState.Enraged);
            }
            else
            {
                SetState(EnemyState.Running);
            }
        }
    }

    // ------------------------------------
    // --- ダメージと死亡処理 ---
    // ------------------------------------

    public void ApplyDamage(float damage)
    {
        if (currentState == EnemyState.Dead || isInvincible) return;

        // 激怒状態になるため、Routineを停止し、スケールを元に戻す処理
        if (routineCoroutine != null)
        {
            StopCoroutine(routineCoroutine);
            routineCoroutine = null;
            animator.SetBool(IS_AMBUSH_BOOL, false); // 擬態アニメーションをオフ
            if (transform.localScale != originalScale)
            {
                transform.localScale = originalScale; // スケールを元に戻す
            }
        }
        isExecutingRoutine = false;

        float direction = damage / Mathf.Abs(damage);
        damage = Mathf.Abs(damage);

        animator.SetBool("Hit", true);
        life -= damage;

        // 激怒状態への遷移チェック
        if (!isEnraged && life <= maxLife * enragedHpRatio)
        {
            isEnraged = true;
            sp.color = new Color32(200, 0, 0, 255);
            Debug.Log(gameObject.name + ": 激怒状態に移行します！");
            SetState(EnemyState.Enraged);
        }

        if (life <= 0)
        {
            Die();
            return;
        }

        // 被弾によるノックバック
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = originalRbType; // 被弾時にKinematicになっていたらDynamicに戻す
        rb.AddForce(new Vector2(direction * 500f, 100f));

        if (hitCoroutine != null)
        {
            StopCoroutine(hitCoroutine);
        }
        hitCoroutine = StartCoroutine(HitTime());
    }

    private void Die()
    {
        SetState(EnemyState.Dead);

        if (routineCoroutine != null) StopCoroutine(routineCoroutine);
        if (hitCoroutine != null) StopCoroutine(hitCoroutine);

        animator.SetBool(RUN_BOOL, false);
        animator.SetBool("Hit", false);
        animator.SetTrigger(DEATH_TRIGGER);

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;

        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            col.enabled = false;
        }

        StartCoroutine(DisableObjectAfterDeathAnimation(2.0f));
    }

    IEnumerator DisableObjectAfterDeathAnimation(float duration)
    {
        yield return new WaitForSeconds(duration);

        gameObject.SetActive(false);
    }

    IEnumerator HitTime()
    {
        isHitted = true;
        isInvincible = true;

        yield return new WaitForSeconds(0.3f);

        isHitted = false;
        isInvincible = false;
        animator.SetBool("Hit", false);

        hitCoroutine = null;

        // 被弾後の状態復帰ロジック
        if (isEnraged)
        {
            SetState(EnemyState.Enraged);
        }
        else if (playerTransform != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= runRange)
            {
                SetState(EnemyState.Running);
            }
            else
            {
                SetState(EnemyState.Routine);
            }
        }
        else
        {
            SetState(EnemyState.Routine);
        }
    }
}