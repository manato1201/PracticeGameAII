using UnityEngine;
using System.Collections;

public class AllyController : MonoBehaviour
{
    [Header("ステータス")]
    public float moveSpeed = 5f;
    public float attackRange = 1.2f; // 攻撃が届く距離
    public float damage = 3f;
    public float attackCooldown = 1.0f;
    public float hp = 10f;

    private Transform targetEnemy;
    private bool canAttack = true;
    private Rigidbody2D rb;
    private Animator anim;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // 攻撃中は動かないようにする
        if (!canAttack)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        FindNearestEnemy();

        if (targetEnemy != null)
        {
            float distance = Vector2.Distance(transform.position, targetEnemy.position);

            if (distance <= attackRange)
            {
                StopMoving();
                if (canAttack) StartCoroutine(AttackRoutine());
            }
            else
            {
                // 範囲外なら追いかける
                MoveToEnemy();
            }
        }
        else
        {
            StopMoving();
        }
    }

    void FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float closestDistance = Mathf.Infinity;
        GameObject closestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
        }

        if (closestEnemy != null)
        {
            targetEnemy = closestEnemy.transform;
        }
    }

    void MoveToEnemy()
    {
        Vector2 direction = (targetEnemy.position - transform.position).normalized;
        rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);

        float scaleSize = Mathf.Abs(transform.localScale.x);

        if (direction.x > 0.1f)
        {
            transform.localScale = new Vector3(scaleSize, scaleSize, 1); // 右向き
        }
        else if (direction.x < -0.1f)
        {
            transform.localScale = new Vector3(-scaleSize, scaleSize, 1); // 左向き
        }

        if (anim != null) anim.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
    }

    void StopMoving()
    {
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (anim != null) anim.SetFloat("Speed", 0f);
    }

    IEnumerator AttackRoutine()
    {
        canAttack = false;

        if (anim != null) anim.SetBool("IsAttacking", true);

        yield return new WaitForSeconds(0.3f);

        if (targetEnemy != null)
        {
            if (Vector2.Distance(transform.position, targetEnemy.position) <= attackRange + 0.5f)
            {
                targetEnemy.SendMessage("ApplyDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }

        // アニメーションを戻す
        if (anim != null) anim.SetBool("IsAttacking", false);

        // クールタイム待機
        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }
    public void DoDashDamage() { }

    // --- ダメージを受ける処理 ---
    // Soldierなどから呼ばれる可能性がある
    public void ApplyDamage(float damage, Vector3 position)
    {
        hp -= damage;
        if (hp <= 0)
        {
            Destroy(gameObject);
        }
    }
}