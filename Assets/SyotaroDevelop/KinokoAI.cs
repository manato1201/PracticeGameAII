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
    public float life = 100f;
    public bool isInvincible = false;
    public bool isHitted = false;
    private Coroutine hitCoroutine;

    // --- アニメーションパラメータ名 ---
    private const string RUN_BOOL = "Run";
    private const string ATTACK_TRIGGER = "Attack";
    private const string IS_AMBUSH_BOOL = "IsAmbush";
    private const string ANIM_END_TRIGGER = "AnimEnd";
    private const string DEATH_TRIGGER = "Death";

    // --- 内部状態 ---
    private enum EnemyState { Routine, Attacking, Running, Dead }
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

        originalScale = transform.localScale;
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

        if (currentState == EnemyState.Dead) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (isHitted)
        {
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

                if (routineCoroutine != null)
                {
                    StopCoroutine(routineCoroutine);
                    routineCoroutine = null;
                }
                isExecutingRoutine = false;

                animator.SetBool(IS_AMBUSH_BOOL, false);
                if (transform.localScale != originalScale)
                {
                    transform.localScale = originalScale;
                }
            }

            animator.SetBool(RUN_BOOL, true);

            MoveToPlayer();
        }
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

    void MoveToPlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);
        Flip(direction.x);
    }

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

    IEnumerator AttackCooldown(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (currentState == EnemyState.Attacking)
        {
            SetState(EnemyState.Running);
        }

        rb.bodyType = originalRbType;

        canExecuteAttack = true;
    }

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

                // 攻撃したのが自分自身でないことを確認
                if (other == gameObject) continue;

                // 攻撃方向を計算 (ノックバックのため)
                float damageDirection = (other.transform.position.x > transform.position.x) ? 1f : -1f;

                if (other.tag == "Player")
                {
                    CharacterController2D playerController = other.GetComponent<CharacterController2D>();

                    if (playerController != null)
                    {
                        playerController.ApplyDamage(attackDamage, attackCenter);
                        Debug.Log("Playerにダメージを与えました: " + attackDamage);
                    }
                }
                else if (other.tag == "Enemy")
                {
                    // --- 敵へのダメージ処理 ---
                    float damageToApply = attackDamage * damageDirection;
                    bool damaged = false;

                    // 1. Enemy スクリプトを持つかチェック
                    Enemy enemyController = other.GetComponent<Enemy>();
                    if (enemyController != null)
                    {
                        enemyController.ApplyDamage(damageToApply);
                        damaged = true;
                    }

                    // 2. Soldier スクリプトを持つかチェック
                    Soldier soldier = other.GetComponent<Soldier>();
                    if (soldier != null)
                    {
                        soldier.ApplyDamage(damageToApply);
                        damaged = true;
                    }

                    // 3. Bat スクリプトを持つかチェック
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
                    // --- ------------------ ---
                }
            }
        }

        if (currentState == EnemyState.Attacking)
        {
            SetState(EnemyState.Running);
        }
    }

    // ------------------------------------
    // --- ダメージと死亡処理 ---
    // ------------------------------------

    public void ApplyDamage(float damage)
    {
        if (currentState == EnemyState.Dead || isInvincible) return;

        if (routineCoroutine != null)
        {
            StopCoroutine(routineCoroutine);
            routineCoroutine = null;
        }
        isExecutingRoutine = false;

        float direction = damage / Mathf.Abs(damage);
        damage = Mathf.Abs(damage);

        animator.SetBool("Hit", true);
        life -= damage;

        if (life <= 0)
        {
            Die();
            return;
        }

        rb.linearVelocity = Vector2.zero;
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

        if (playerTransform != null)
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