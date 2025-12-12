using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    // ============================================================
    // ★ 基本パラメータ（旧AI + 新AI 両対応仕様）
    // ============================================================
    public float life = 10f;
    public float maxLife;

    public bool isHitted = false;
    private bool isInvincible = false;

    public Vector2 speed = Vector2.zero;        // FSMが参照する
    public Vector2 position;                    // FSMが参照する
    public float timer;                         // VisionSeekで使用

    public bool facingLeft = true;

    Rigidbody2D rb;
    Animator animator;
    UnityEngine.Transform attackCheck;


    // ============================================================
    // ★ FSM が必要とする旧AIパラメータ（完全復活）
    // ============================================================
    [Header("Old AI Settings (for FSM compatibility)")]
    public float lowHPThreshold = 5f;
    public float batSearchRange = 12f;

    public float detectRange = 6f;
    public float attackRange = 1.2f;
    public float viewAngle = 120f;

    public float gridSize = 0.7f;
    public Vector2 mapMin = new Vector2(-20, -20);
    public Vector2 mapMax = new Vector2(20, 20);
    public string obstacleTag = "Wall";

    public EnemyComp tool;                      // FSM が使用
    public bool leader;                         // チームAI
    public int set;                             // チームAI
    public Soldier[] teamMember;                // チームAI

    public GameObject throwableObject;          // RangeAttackで使用


    // ============================================================
    // ★ moveType（旧AIの状態管理）
    // ============================================================
    public enum MoveType
    {
        Simple = 0,     // ★ 新AI（プロゲーマーAI）はここで動作
        BatAbsorb = 1,
        VisionSeek = 2,
        AStarPath = 3,
        JumpDodge = 4,
    }
    public MoveType moveType = MoveType.Simple;


    // ============================================================
    // ★ プロゲーマーAI（新AI）のパラメータ
    // ============================================================
    [Header("Pro Gamer AI Settings")]
    public GameObject player;

    public List<GameObject> playerBulletPrefabs;
    public List<Collider2D> playerAttackHitBoxes;

    public float dodgeDistance = 2.5f;
    public float dodgeCooldown = 0.8f;
    public float behindTeleportCooldown = 2.0f;
    public float feintChance = 0.25f;
    public float retreatHPThreshold = 0.3f;
    public float retreatDistance = 4.0f;
    public float attackDelayMin = 0.1f;
    public float attackDelayMax = 0.35f;

    float nextDodgeTime = 0f;
    float nextBehindTime = 0f;

    public bool ai_forceAttack = false;
    public bool ai_retreating = false;


    // ============================================================
    // ★ ステートマシン基盤
    // ============================================================
    public enum State
    {
        WAIT,
        RUN,
        MELEE_ATTACK,
        RANGE_ATTACK,
        DAMAGE,
        DEAD,
    }
    State currentState;

    List<Soldier_FSM_Base> availableStates = new List<Soldier_FSM_Base>();

    Coroutine hitCoroutine;
    // ============================================================
    // Start
    // ============================================================
    void Start()
    {
        maxLife = life;

        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        attackCheck = transform.Find("AttackCheck");

        // EnemyComp（旧AI互換）
        tool = new EnemyComp(this.gameObject);

        // チームメンバー読み取り（旧AI互換）
        GameObject[] allies = tool.GetSinblings();
        teamMember = new Soldier[allies.Length];
        for (int i = 0; i < allies.Length; i++)
        {
            teamMember[i] = allies[i].GetComponent<Soldier>();
        }

        // FSM状態追加
        availableStates.Add(new Soldier_FSM_Wait(this));
        availableStates.Add(new Soldier_FSM_Run(this));
        availableStates.Add(new Soldier_FSM_Attack(this));
        availableStates.Add(new Soldier_FSM_RangeAttack(this));
        availableStates.Add(new Soldier_FSM_Damage(this));
        availableStates.Add(new Soldier_FSM_Dead(this));

        currentState = State.WAIT;
        availableStates[(int)currentState].OnEnter();
    }


    // ============================================================
    // Update
    // ============================================================
    void Update()
    {
        UpdatePositionInfo();

        // ★ 新AIは moveType.Simple のときのみ有効化
        if (currentState == State.RUN && moveType == MoveType.Simple)
        {
            ProGamerAI_Update();
        }

        // FSMの遷移処理
        State nextState = availableStates[(int)currentState].CheckTransitions();

        if (life <= 0 && currentState != State.DEAD)
        {
            nextState = State.DEAD;
        }

        if (nextState != currentState)
        {
            availableStates[(int)currentState].OnExit();
            availableStates[(int)nextState].OnEnter();
            currentState = nextState;
        }

        availableStates[(int)currentState].OnUpdate();

        Movement();
    }


    // ============================================================
    // Position & Movement
    // ============================================================
    void UpdatePositionInfo()
    {
        position = transform.position;
        speed = rb.linearVelocity;
    }

    void Movement()
    {
        GetComponent<SpriteRenderer>().flipX = !facingLeft;
        rb.linearVelocity = speed;
    }


    // ============================================================
    // FSM Action Methods
    // ============================================================
    public void WaitAction()
    {
        animator.SetBool("Run", false);
    }

    public void RunAction()
    {
        animator.SetBool("Run", true);
    }

    public void AttackAction()
    {
        animator.SetBool("Run", false);
        animator.SetTrigger("Attack");
        MakeAttackHit();
    }

    public void RangeAttackAction()
    {
        animator.SetBool("Run", false);
        MakeShot(facingLeft);
    }

    public void DeadAction()
    {
        animator.SetBool("IsDead", true);
        ChangeBodyHitToDead();
    }

    public void Delete()
    {
        Destroy(gameObject);
    }
    // ============================================================
    // ★ プロゲーマーAIのメイン処理（RUNステートで実行）
    // ============================================================
    void ProGamerAI_Update()
    {
        // ★ player が割り当てられていない or 破棄されている場合は停止
        if (player == null || player.Equals(null))
            return;

        // transform アクセス前にさらにガード
        if (player.transform == null)
            return;
        float dist = Vector2.Distance(transform.position, player.transform.position);

        // -----------------------------
        // ① HP低下で撤退行動
        // -----------------------------
        if (life < maxLife * retreatHPThreshold)
        {
            RetreatFromPlayer();
            return;
        }

        // -----------------------------
        // ② プレイヤー弾を検知 → 即時回避
        // -----------------------------
        if (IsBulletNear(out Vector2 avoidDir))
        {
            PerformDodge(avoidDir);
            return;
        }

        // -----------------------------
        // ③ プレイヤー攻撃HitBox → 即時回避
        // -----------------------------
        if (IsPlayerAttacking())
        {
            PerformRandomDodge();
            return;
        }

        // -----------------------------
        // ④ 背後取り（ランダム＆クールダウンあり）
        // -----------------------------
        if (dist < 5f && Time.time > nextBehindTime)
        {
            if (Random.value < 0.1f)
            {
                TeleportBehindPlayer();
                return;
            }
        }

        // -----------------------------
        // ⑤ 間合い管理 + フェイント
        // -----------------------------
        if (dist < attackRange)
        {
            TryFeintAttack();
        }
        else if (dist < 3f)
        {
            TryFeintMovement();
        }
        else
        {
            ApproachPlayer();
        }
    }


    // ============================================================
    // プレイヤー攻撃HitBox検知
    // ============================================================
    bool IsPlayerAttacking()
    {
        foreach (var hitbox in playerAttackHitBoxes)
        {
            if (hitbox != null && hitbox.enabled)
            {
                float d = Vector2.Distance(transform.position, hitbox.transform.position);
                if (d < 2f) return true;
            }
        }
        return false;
    }


    // ============================================================
    // プレイヤー弾の検知（Prefab名一致で判断）
    // ============================================================
    bool IsBulletNear(out Vector2 avoidDir)
    {
        avoidDir = Vector2.zero;

        Rigidbody2D[] objs = GameObject.FindObjectsOfType<Rigidbody2D>();
        foreach (var obj in objs)
        {
            foreach (var prefab in playerBulletPrefabs)
            {
                if (prefab == null) continue;

                if (obj.gameObject.name.Contains(prefab.name))
                {
                    float d = Vector2.Distance(transform.position, obj.transform.position);
                    if (d < 3f)
                    {
                        avoidDir = (transform.position - obj.transform.position).normalized;
                        return true;
                    }
                }
            }
        }
        return false;
    }


    // ============================================================
    // 回避行動
    // ============================================================
    void PerformDodge(Vector2 dir)
    {
        if (Time.time < nextDodgeTime) return;
        rb.linearVelocity = dir * 8f;
        nextDodgeTime = Time.time + dodgeCooldown;
    }

    void PerformRandomDodge()
    {
        PerformDodge(Random.insideUnitCircle.normalized);
    }


    // ============================================================
    // 背後取り瞬間移動
    // ============================================================
    void TeleportBehindPlayer()
    {
        if (player == null) return;

        Vector3 pos = player.transform.position;

        float offsetX = 1.2f;
        if (player.transform.localScale.x > 0)
            offsetX = -1.2f;

        transform.position = pos + new Vector3(offsetX, 0, 0);

        nextBehindTime = Time.time + behindTeleportCooldown;
    }


    // ============================================================
    // プレイヤーへ接近
    // ============================================================
    void ApproachPlayer()
    {
        if (player == null) return;
        Vector2 dir = (player.transform.position - transform.position).normalized;
        speed = dir * 3f;
    }


    // ============================================================
    // フェイント移動
    // ============================================================
    void TryFeintMovement()
    {
        if (Random.value < feintChance)
        {
            Vector2 side = new Vector2(Random.value < 0.5f ? -1 : 1, 0);
            rb.linearVelocity = side * 3.5f;
        }
        else
        {
            ApproachPlayer();
        }
    }


    // ============================================================
    // フェイント攻撃
    // ============================================================
    void TryFeintAttack()
    {
        if (Random.value < feintChance)
        {
            StartCoroutine(DelayedAttack(Random.Range(attackDelayMin, attackDelayMax)));
        }
        else
        {
            ai_forceAttack = true;
        }
    }

    IEnumerator DelayedAttack(float delay)
    {
        yield return new WaitForSeconds(delay);
        ai_forceAttack = true;
    }


    // ============================================================
    // 撤退AI（画面外に出ないよう制限）
    // ============================================================
    void RetreatFromPlayer()
    {
        ai_retreating = true;

        Vector2 dir = (transform.position - player.transform.position).normalized;
        Vector2 target = (Vector2)transform.position + dir * retreatDistance;

        target.x = Mathf.Clamp(target.x, mapMin.x, mapMax.x);
        target.y = Mathf.Clamp(target.y, mapMin.y, mapMax.y);

        transform.position = target;
    }
    // ============================================================
    // 近接攻撃ヒット処理
    // ============================================================
    void MakeAttackHit()
    {
        if (attackCheck == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackCheck.position, 0.9f);

        foreach (var c in hits)
        {
            if (c.CompareTag("Player"))
            {
                var playerCtr = c.GetComponent<CharacterController2D>();
                if (playerCtr != null)
                {
                    playerCtr.ApplyDamage(2f, transform.position);
                }
            }
        }
    }


    // ============================================================
    // 射撃攻撃
    // ============================================================
    void MakeShot(bool toLeft)
    {
        if (throwableObject == null) return;

        float offset = toLeft ? -0.5f : 0.5f;
        float bulletSpeed = toLeft ? -0.5f : 0.5f;

        GameObject proj = Instantiate(
            throwableObject,
            transform.position + new Vector3(offset, -0.2f),
            Quaternion.identity
        );

        var tp = proj.GetComponent<ThrowableProjectile>();
        if (tp != null)
        {
            tp.owner = gameObject;
            tp.direction = new Vector2(bulletSpeed, 0);
        }
    }


    // ============================================================
    // ダメージ処理（被弾 → ノックバック → 無敵時間）
    // ============================================================
    public void ApplyDamage(float damage)
    {
        if (isInvincible) return;

        float dir = Mathf.Sign(damage);
        damage = Mathf.Abs(damage);

        animator.SetBool("Hit", true);
        life -= damage;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(dir * 500f, 100f));

        // ★ 新AI（Simple）が有効なら被弾回避を混ぜる
        if (moveType == MoveType.Simple)
        {
            if (Random.value < 0.7f)
            {
                PerformRandomDodge();
            }
        }

        if (hitCoroutine != null)
            StopCoroutine(hitCoroutine);

        hitCoroutine = StartCoroutine(HitTime());
    }

    IEnumerator HitTime()
    {
        isHitted = true;
        isInvincible = true;

        yield return new WaitForSeconds(0.3f);

        isHitted = false;
        isInvincible = false;
        hitCoroutine = null;
    }


    // ============================================================
    // 死亡時の当たり判定を変更
    // ============================================================
    void ChangeBodyHitToDead()
    {
        CapsuleCollider2D cc = GetComponent<CapsuleCollider2D>();
        if (cc != null)
        {
            cc.size = new Vector2(1f, 0.25f);
            cc.offset = new Vector2(0f, -0.8f);
            cc.direction = CapsuleDirection2D.Horizontal;
        }
    }


    // ============================================================
    // Debug Gizmo（視認用）
    // ============================================================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
