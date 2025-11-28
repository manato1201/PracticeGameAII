using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    public float life = 15;

    bool isInvincible = false;
    public bool isHitted = false;

    Transform attackCheck;
    public Rigidbody2D rb;
    public Animator animator;

    public EnemyComp tool;
    private Coroutine hitCoroutine;

    // Movement
    public bool facingLeft = true;
    public Vector2 speed = Vector2.zero;
    public Vector2 position;

    // Jump dodge
    bool isJumpDodging = false;
    bool landedThisFrame = false; // ジャンプ→着地を検知する安全なフラグ

    // State Machine
    public enum State
    {
        WAIT,
        RUN,
        MELEE_ATTACK,
        DAMAGE,
        DEAD,
    };

    public State currentState = State.WAIT;
    public List<Soldier_FSM_Base> availableStates = new List<Soldier_FSM_Base>();

    // AI parameters
    public Transform player;
    public float detectRange = 6f;
    public float attackRange = 1.2f;
    public float viewAngle = 120f;

    // Low HP Mode
    public float lowHPThreshold = 5f;

    // Bat search range
    public float batSearchRange = 12f;

    // Sight memory
    private float lastSeenTime = -999f;
    private Vector2 lastSeenPosition;
    public float searchDuration = 5f;

    // Attack cooldown
    private float nextAttackTime = 0f;

    // A* Pathfinding
    public Vector2 AI_TargetPoint;
    public float gridSize = 0.7f;
    public Vector2 mapMin = new Vector2(-20, -20);
    public Vector2 mapMax = new Vector2(20, 20);
    public LayerMask wallMask;

    float rand => Random.Range(-0.25f, 0.25f);



    // ==========================================================================
    // Start
    // ==========================================================================
    void Start()
    {
        tool = new EnemyComp(this.gameObject);
        animator = GetComponent<Animator>();
        attackCheck = transform.Find("AttackCheck").transform;
        rb = GetComponent<Rigidbody2D>();
        player = GameObject.FindGameObjectWithTag("Player").transform;

        batSearchRange = detectRange * 2f;

        availableStates.Add(new Soldier_FSM_Wait(this));
        availableStates.Add(new Soldier_FSM_Run(this));
        availableStates.Add(new Soldier_FSM_Attack(this));
        availableStates.Add(new Soldier_FSM_Damage(this));
        availableStates.Add(new Soldier_FSM_Dead(this));

        availableStates[(int)currentState].OnEnter();
    }



    // ==========================================================================
    // Update
    // ==========================================================================
    void Update()
    {
        FirstInUpdate();

        // ★AIThink は絶対に止まらない
        State aiResult = AIThink();
        State nextState = availableStates[(int)currentState].CheckTransitions();

        if (nextState == currentState)
            nextState = aiResult;

        if (life <= 0) nextState = State.DEAD;

        if (nextState != currentState)
        {
            availableStates[(int)currentState].OnExit();
            availableStates[(int)nextState].OnEnter();
            currentState = nextState;
        }

        availableStates[(int)currentState].OnUpdate();

        // ★着地攻撃
        if (landedThisFrame && isJumpDodging)
        {
            isJumpDodging = false;
            ForceAttack();
        }

        Movement();
        landedThisFrame = false;
    }



    // ==========================================================================
    void FaceTo(Vector2 target)
    {
        facingLeft = target.x < transform.position.x;
    }



    // ==========================================================================
    // ジャンプ回避
    // ==========================================================================
    void DoJumpDodge()
    {
        rb.AddForce(new Vector2(0, 280f));

        float dir = (Random.value < 0.5f) ? -1f : 1f;
        rb.AddForce(new Vector2(dir * 180f, 0));

        speed = new Vector2(dir * 2f, rb.linearVelocity.y);

        isJumpDodging = true;
    }



    // ==========================================================================
    // AIThink（LowHPは回復最優先、AI停止しない版）
    // ==========================================================================
    State AIThink()
    {
        float distPlayer = Vector2.Distance(position, player.position);

        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        Vector2 lookDir = facingLeft ? Vector2.left : Vector2.right;
        float angle = Vector2.Angle(lookDir, dirToPlayer);
        bool inView = angle < viewAngle * 0.5f && distPlayer <= detectRange;

        if (inView)
        {
            lastSeenTime = Time.time;
            lastSeenPosition = player.position;
        }

        // ===========================================================
        // ★ HP 半分以下 → 回復のためコウモリへ “瞬間移動”
        // ===========================================================
        if (life < lowHPThreshold)
        {
            Transform bat = FindNearestBat();

            if (bat != null)
            {
                // コウモリへ瞬間移動
                transform.position = bat.position;

                // コウモリ即死
                Destroy(bat.gameObject);

                // 回復（最大HPの1/3）
                life = Mathf.Min(life + 3f, 10f);

                // 少し遅延して WAIT に戻る
                nextAttackTime = Time.time + 0.3f;
                return State.WAIT;
            }

            // コウモリがいない → 逃げるだけ
            Vector2 away = (position - (Vector2)player.position).normalized * 2f;
            AI_TargetPoint = position + away;
            return State.RUN;
        }

        // ===========================================================
        // ここから下は通常モード（プレイヤーと戦う）
        // ===========================================================

        bool canAttack = distPlayer < attackRange && inView;

        float attackInstinct = 0.2f + rand * 0.1f;
        float hpFactor = Mathf.Clamp01(life / 10f);
        float attackProbability = attackInstinct * hpFactor;

        if (canAttack && Time.time > nextAttackTime)
        {
            if (Random.value < attackProbability)
            {
                FaceTo(player.position);
                nextAttackTime = Time.time + 0.5f;
                return State.MELEE_ATTACK;
            }
            else
            {
                if (Random.value < 0.3f)
                {
                    AI_TargetPoint = position + (facingLeft ? Vector2.right : Vector2.left) * 1.2f;
                }
                else
                {
                    DoJumpDodge();
                }

                return State.RUN;
            }
        }

        if (inView)
        {
            AI_TargetPoint = PathTarget(player.position);
            return State.RUN;
        }

        if (Time.time - lastSeenTime < searchDuration)
        {
            AI_TargetPoint = PathTarget(lastSeenPosition);
            return State.RUN;
        }

        return State.WAIT;
    }

    // ==========================================================================
    Transform FindNearestBat()
    {
        GameObject[] bats = GameObject.FindGameObjectsWithTag("Bat");
        float bestDist = Mathf.Infinity;
        Transform best = null;

        foreach (var b in bats)
        {
            float d = Vector2.Distance(position, b.transform.position);

            if (d < bestDist && d < batSearchRange)
            {
                bestDist = d;
                best = b.transform;
            }
        }
        return best;
    }



    // ==========================================================================
    // ★着地検知（ジャンプ回避後に攻撃する安全方式）
    // ==========================================================================
    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.contacts.Length > 0)
        {
            Vector2 normal = col.contacts[0].normal;

            if (normal.y > 0.5f) // 下からの衝突→地面
            {
                landedThisFrame = true;
            }
        }
    }



    void ForceAttack()
    {
        availableStates[(int)currentState].OnExit();
        currentState = State.MELEE_ATTACK;
        availableStates[(int)currentState].OnEnter();

        nextAttackTime = Time.time + 0.4f;
    }



    // ==========================================================================
    public Vector2 PathTarget(Vector2 goal)
    {
        List<Vector2> pts = AStar(position, goal);
        if (pts.Count > 1) return pts[1];
        return goal;
    }


    class Node
    {
        public Vector2 pos;
        public Node parent;
        public float g, h, f;

        public Node(Vector2 pos, Node parent, float g, float h)
        {
            this.pos = pos;
            this.parent = parent;
            this.g = g;
            this.h = h;
            this.f = g + h;
        }
    }

    List<Vector2> AStar(Vector2 start, Vector2 goal)
    {
        var open = new List<Node>();
        var closed = new HashSet<Vector2>();

        open.Add(new Node(start, null, 0, Vector2.Distance(start, goal)));

        while (open.Count > 0)
        {
            open.Sort((a, b) => a.f.CompareTo(b.f));
            Node cur = open[0];
            open.RemoveAt(0);

            if (Vector2.Distance(cur.pos, goal) < gridSize)
                return BuildPath(cur);

            closed.Add(cur.pos);

            foreach (var next in Neighbors(cur.pos))
            {
                if (closed.Contains(next)) continue;

                float ng = cur.g + Vector2.Distance(cur.pos, next);
                float nh = Vector2.Distance(next, goal);
                Node nn = new Node(next, cur, ng, nh);

                bool skip = false;
                foreach (var o in open)
                    if (o.pos == next && o.f <= nn.f)
                        skip = true;

                if (!skip) open.Add(nn);
            }
        }
        return new List<Vector2>();
    }


    IEnumerable<Vector2> Neighbors(Vector2 pos)
    {
        Vector2[] dirs = {
            new Vector2(1,0), new Vector2(-1,0),
            new Vector2(0,1), new Vector2(0,-1),
            new Vector2(1,1), new Vector2(-1,1),
            new Vector2(1,-1), new Vector2(-1,-1)
        };

        foreach (Vector2 d in dirs)
        {
            Vector2 p = pos + d * gridSize;

            if (p.x < mapMin.x || p.x > mapMax.x || p.y < mapMin.y || p.y > mapMax.y)
                continue;

            if (Physics2D.OverlapBox(p, Vector2.one * gridSize * 0.8f, 0, wallMask))
                continue;

            yield return p;
        }
    }


    List<Vector2> BuildPath(Node n)
    {
        List<Vector2> list = new List<Vector2>();
        while (n != null)
        {
            list.Add(n.pos);
            n = n.parent;
        }
        list.Reverse();
        return list;
    }



    // ==========================================================================
    void FirstInUpdate()
    {
        position = transform.position;
        speed = rb.linearVelocity;
    }



    void Movement()
    {
        GetComponent<SpriteRenderer>().flipX = !facingLeft;
        rb.linearVelocity = speed;
    }



    public void WaitAction() => animator.SetBool("Run", false);
    public void RunAction() => animator.SetBool("Run", true);



    public void AttackAction()
    {
        animator.SetBool("Run", false);
        animator.SetTrigger("Attack");
        MakeAttackHit();
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



    // ==========================================================================
    void MakeAttackHit()
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(attackCheck.position, 0.9f);

        foreach (var c in cols)
        {
            if (c.CompareTag("Player"))
            {
                c.GetComponent<CharacterController2D>().ApplyDamage(2f, transform.position);
            }

            if (c.CompareTag("Bat"))
            {
                life = Mathf.Min(life + 3f, 10f);

                Rigidbody2D batRb = c.GetComponent<Rigidbody2D>();
                if (!batRb) batRb = c.gameObject.AddComponent<Rigidbody2D>();

                Vector2 push = (c.transform.position - transform.position).normalized;
                batRb.gravityScale = 2f;
                batRb.AddForce(push * 600f + Vector2.up * 300f);

                Destroy(c.gameObject, 0.4f);
            }
        }
    }



    void ChangeBodyHitToDead()
    {
        CapsuleCollider2D cap = GetComponent<CapsuleCollider2D>();
        cap.size = new Vector2(1f, 0.25f);
        cap.offset = new Vector2(0f, -0.8f);
        cap.direction = CapsuleDirection2D.Horizontal;
    }



    public void ApplyDamage(float damage)
    {
        if (!isInvincible)
        {
            float dir = Mathf.Sign(damage);
            damage = Mathf.Abs(damage);

            animator.SetBool("Hit", true);
            life -= damage;

            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(dir * 500f, 100f));

            if (hitCoroutine != null) StopCoroutine(hitCoroutine);
            hitCoroutine = StartCoroutine(HitTime());
        }
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



    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, batSearchRange);
    }
}
