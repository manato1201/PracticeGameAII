using UnityEngine;
using System.Collections;
using UnityEditor;

public class SummonController : MonoBehaviour
{
    private Animator _animator;

    private readonly int AnimParam_Run = Animator.StringToHash("Run");
    private readonly int AnimParam_Attack = Animator.StringToHash("Attack");
    private readonly int AnimParam_Summon = Animator.StringToHash("Summon");

    [Header("Targeting")]
    [SerializeField] private Transform target;
    [SerializeField] private float stopDistance = 1.0f;
    [SerializeField] private float maxTargetDistance = 20.0f;
    private bool m_FacingRight = true;
    private Vector3 originalScale;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.0f;
    private bool isRunning = false;
    private bool isSummoning = true; // 召喚中は移動を停止

    [Header("Attack")]
    [SerializeField] private float attackDuration = 0.5f;
    private bool isAttacking = false;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRangeForDamage = 1.0f;
    [SerializeField] public Transform AttackPoint;

    [Header("Initialization")]
    [SerializeField] private float summonTime = 1.0f;

    private float maxhp = 3f;
    private float hp;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            Debug.LogError("Animatorコンポーネントが見つかりません。AllyControllerを無効にします。");
            enabled = false;
        }
        originalScale = transform.localScale;
        hp = maxhp;
    }

    private void Start()
    {
        _animator.SetTrigger(AnimParam_Summon);
        StartCoroutine(WaitForSummonAndStartIdle());
    }

    private void Update()
    {
        if (isSummoning) return;

        if (target == null || !target.gameObject.activeInHierarchy || IsTargetTooFar())
        {
            FindClosestEnemyAndSetTarget();
        }

        if (isAttacking) return;
        HandleMovement();
    }

    private bool IsTargetTooFar()
    {
        if (target != null)
        {
            return Vector3.Distance(transform.position, target.position) > maxTargetDistance;
        }
        return false;
    }

    private IEnumerator WaitForSummonAndStartIdle()
    {
        yield return new WaitForSeconds(summonTime);
        isSummoning = false;
    }

    private void HandleMovement()
    {
        if (target == null || isAttacking)
        {
            isRunning = false;
            _animator.SetBool(AnimParam_Run, isRunning);
            return;
        }

        float distance = Vector3.Distance(AttackPoint.position, target.position);

        if (distance > stopDistance)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            transform.Translate(new Vector3(direction.x, 0, 0) * moveSpeed * Time.deltaTime);
            isRunning = true;
            Flip(direction.x);
        }
        else
        {
            isRunning = false;
            Flip((target.position - transform.position).x);
            StartAttack();
        }

        _animator.SetBool(AnimParam_Run, isRunning);
    }

    private void Flip(float moveDirection)
    {
        if (moveDirection > 0 && !m_FacingRight)
        {
            m_FacingRight = true;
            Vector3 theScale = transform.localScale;
            theScale.x = Mathf.Abs(originalScale.x);
            transform.localScale = theScale;
        }
        else if (moveDirection < 0 && m_FacingRight)
        {
            m_FacingRight = false;
            Vector3 theScale = transform.localScale;
            theScale.x = -Mathf.Abs(originalScale.x);
            transform.localScale = theScale;
        }
    }

    private void FindClosestEnemyAndSetTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        if (enemies.Length == 0)
        {
            target = null;
            return;
        }

        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity;
        Vector3 currentPosition = transform.position;

        foreach (GameObject enemy in enemies)
        {
            if (enemy.gameObject == gameObject) continue;
            if (!enemy.gameObject.activeInHierarchy) continue;

            float distanceSqr = (enemy.transform.position - currentPosition).sqrMagnitude;

            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestTarget = enemy.transform;
            }
        }

        target = closestTarget;
    }

    public void StartAttack()
    {
        if (isAttacking) return;

        isAttacking = true;
        _animator.SetBool(AnimParam_Attack, true);
        StartCoroutine(FinishAttackAfterDuration());
    }

    public void Attack()
    {
        if (AttackPoint == null)
        {
            Debug.LogError("AttackPointが設定されていません。");
            return;
        }

        Vector2 attackCenter = AttackPoint.position;
        float attackRadius = attackRangeForDamage;

        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(attackCenter, attackRadius);

        foreach (Collider2D hit in hitObjects)
        {
            GameObject other = hit.gameObject;
            if (other == gameObject) continue;

            if (other.CompareTag("Enemy"))
            {
                Bat batController = other.GetComponent<Bat>();
                if (batController != null)
                {
                    batController.ApplyDamage(attackDamage);
                    continue;
                }

                Soldier soldierController = other.GetComponent<Soldier>();
                if (soldierController != null)
                {
                    soldierController.ApplyDamage(attackDamage);
                    continue;
                }

                EnemyAI ea = other.GetComponent<EnemyAI>();
                if (ea != null)
                {
                    ea.ApplyDamage(attackDamage);
                    continue;
                }
            }
        }
    }

    private IEnumerator FinishAttackAfterDuration()
    {
        yield return new WaitForSeconds(attackDuration);

        _animator.SetBool(AnimParam_Attack, false);
        isAttacking = false;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }

    public void TakeDamage(float amount)
    {
        hp -= amount;
        if (hp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        _animator.SetTrigger("Die");
        Destroy(gameObject); // thisではなくgameObjectを破棄
    }
}