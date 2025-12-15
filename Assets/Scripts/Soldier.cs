using System.Collections;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    // ===================================================
    // FSM 互換 enum
    // ===================================================
    public enum State
    {
        WAIT,
        RUN,
        MELEE_ATTACK,
        RANGE_ATTACK,
        DAMAGE,
        DEAD
    }

    public enum MoveType
    {
        Simple,
        BatAbsorb,
        VisionSeek,
        AStarPath,
        JumpDodge
    }

    // ===================================================
    // 基本ステータス（Inspector表示）
    // ===================================================
    [Header("Status")]
    public float life = 10f;
    public float maxLife = 10f;

    [Header("Detect")]
    public float detectRange = 24f;
    public float viewAngle = 120f;

    [Header("Attack")]
    public GameObject throwableObject;
    public string playerTag = "Player";

    // ===================================================
    // FSM 互換フィールド（参照されるので残す）
    // ===================================================
    [HideInInspector] public State currentState = State.WAIT;
    [HideInInspector] public MoveType moveType = MoveType.Simple;

    [HideInInspector] public bool isHitted = false;

    [HideInInspector] public float lowHPThreshold = 3f;
    [HideInInspector] public float batSearchRange = 24f;

    // ★ FSM側が参照している（今回のエラー原因）
    [HideInInspector] public float gridSize = 0.7f;
    [HideInInspector] public Vector2 mapMin = new Vector2(-20, -20);
    [HideInInspector] public Vector2 mapMax = new Vector2(20, 20);
    [HideInInspector] public string obstacleTag = "Wall";

    [HideInInspector] public float dodgeDistance = 2f;
    [HideInInspector] public float dodgeCooldown = 0.6f;

    [HideInInspector] public float timer = 0f;

    // FSMから直接触られる
    [HideInInspector] public Vector2 speed = Vector2.zero;
    [HideInInspector] public bool facingLeft = true;

    public Vector2 position => transform.position;

    // ★ FSMが参照する tool（今回のエラー原因）
    [HideInInspector] public EnemyComp tool;

    // ===================================================
    // 内部参照
    // ===================================================
    Rigidbody2D rb;
    SpriteRenderer sr;
    Animator animator;
    GameObject player;

    // ===================================================
    // ===== AAA AI =====
    // ===================================================
    enum AAAState
    {
        Idle,
        Search,
        Reposition,
        Dodge,
        Counter,
        Pressure,
        Retreat,
        FeintRush,
        Dead
    }

    AAAState aaaState = AAAState.Idle;
    float stateLockUntil = 0f;

    float reactionTime = 0.12f;
    float nextThinkTime = 0f;

    float shootCooldown = 0.45f;
    float nextShootTime = 0f;

    Vector2 dodgeDir;

    bool hasSeenPlayer = false;
    Vector2 lastSeenPos;

    // ===================================================
    // Unity
    // ===================================================
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        maxLife = life;

        // ★ FSM互換：tool を必ず生成
        tool = new EnemyComp(gameObject);
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (life <= 0)
        {
            aaaState = AAAState.Dead;
            speed = Vector2.zero;
            DeadAction();
            ApplyMovement();
            return;
        }

        if (player == null)
            player = GameObject.FindGameObjectWithTag(playerTag);

        if (player == null)
        {
            IdleBehavior();
            ApplyMovement();
            return;
        }

        Vector2 pPos = player.transform.position;
        float dist = Vector2.Distance(transform.position, pPos);

        if (dist > detectRange)
        {
            hasSeenPlayer = false;
            aaaState = AAAState.Idle;
            IdleBehavior();
            ApplyMovement();
            return;
        }

        ThinkAAA(pPos, dist);
        ExecuteAAA(pPos, dist);
        ApplyMovement();
    }

    // ===================================================
    // AAA 思考
    // ===================================================
    void ThinkAAA(Vector2 pPos, float dist)
    {
        if (Time.time < nextThinkTime) return;
        nextThinkTime = Time.time + reactionTime;

        facingLeft = pPos.x < transform.position.x;

        if (CheckIncomingBullet(out dodgeDir))
        {
            SetAAAState(AAAState.Dodge, 0.15f);
            return;
        }

        if (life <= maxLife * 0.3f)
        {
            SetAAAState(AAAState.Retreat, 0.25f);
            return;
        }

        if (!hasSeenPlayer)
        {
            SetAAAState(AAAState.Search, 0.2f);
            return;
        }

        if (dist < 3f)
        {
            SetAAAState(AAAState.Retreat, 0.2f);
            return;
        }

        if (dist > 7f)
        {
            SetAAAState(Random.value < 0.4f ? AAAState.FeintRush : AAAState.Pressure, 0.3f);
            return;
        }

        SetAAAState(AAAState.Reposition, 0.2f);
    }

    void SetAAAState(AAAState next, float lockTime)
    {
        if (aaaState == next) return;
        aaaState = next;
        stateLockUntil = Time.time + lockTime;
    }

    // ===================================================
    // AAA 実行
    // ===================================================
    void ExecuteAAA(Vector2 pPos, float dist)
    {
        if (Time.time < stateLockUntil) return;

        float dir = facingLeft ? -1f : 1f;

        switch (aaaState)
        {
            case AAAState.Search:
                speed = new Vector2(dir * 1.2f, 0);
                hasSeenPlayer = true;
                lastSeenPos = pPos;
                break;

            case AAAState.Dodge:
                speed = dodgeDir * 5.5f;
                SetAAAState(AAAState.Counter, 0.1f);
                break;

            case AAAState.Counter:
                speed = Vector2.zero;
                TryShoot();
                break;

            case AAAState.Reposition:
                speed = Vector2.right * Mathf.Sin(Time.time * 6f) * 2.5f;
                TryShoot();
                break;

            case AAAState.Pressure:
                speed = new Vector2(dir * 3f, 0);
                TryShoot();
                break;

            case AAAState.Retreat:
                speed = new Vector2(-dir * 3.5f, 0);
                break;

            case AAAState.FeintRush:
                speed = new Vector2(dir * 5.5f, 0);
                TryShoot();
                break;
        }
    }

    // ===================================================
    // 移動
    // ===================================================
    void ApplyMovement()
    {
        if (sr) sr.flipX = !facingLeft;

        // ※元コードに合わせて linearVelocity を使用（Unityバージョン次第では velocity へ変更してください）
        rb.linearVelocity = speed;
    }

    // ===================================================
    // 攻撃
    // ===================================================
    void TryShoot()
    {
        if (Time.time < nextShootTime) return;
        nextShootTime = Time.time + shootCooldown;
        RangeAttackAction();
    }

    // ★ FSM互換：AttackAction が呼ばれる（今回のエラー原因）
    // 旧FSMでは近接/遠距離の切替などをここでしていた可能性があるので、
    // とりあえず RangeAttack に寄せて動くようにしておく。
    public void AttackAction()
    {
        RangeAttackAction();
    }

    public void RangeAttackAction()
    {
        if (throwableObject == null) return;

        float sx = facingLeft ? -1f : 1f;
        GameObject proj = Instantiate(
            throwableObject,
            transform.position + new Vector3(sx * 0.5f, -0.2f, 0),
            Quaternion.identity
        );

        var tp = proj.GetComponent<ThrowableProjectile>();
        if (tp != null)
        {
            tp.owner = gameObject;
            tp.direction = new Vector2(sx, 0);
        }
    }

    // ===================================================
    // ダメージ
    // ===================================================
    public void ApplyDamage(float damage)
    {
        life -= damage;
        if (life <= 0) return;

        isHitted = true;
        StartCoroutine(HitRecover());
    }

    IEnumerator HitRecover()
    {
        yield return new WaitForSeconds(0.3f);
        isHitted = false;
    }

    // ===================================================
    // 非戦闘
    // ===================================================
    void IdleBehavior()
    {
        if (Random.value < 0.01f)
        {
            float d = Random.value < 0.5f ? -1f : 1f;
            facingLeft = d < 0;
            speed = new Vector2(d * 1.2f, 0);
        }
    }

    bool CheckIncomingBullet(out Vector2 outDir)
    {
        outDir = Vector2.zero;
        var bullets = FindObjectsOfType<ThrowableProjectile>();
        if (bullets == null || bullets.Length == 0) return false;

        foreach (var b in bullets)
        {
            if (b == null || b.owner == gameObject) continue;

            Vector2 toMe = (Vector2)(transform.position - b.transform.position);
            if (toMe.magnitude < 4.5f)
            {
                outDir = Vector2.Perpendicular(toMe).normalized;
                return true;
            }
        }
        return false;
    }

    // ===================================================
    // FSM互換アクション
    // ===================================================
    public void WaitAction() { if (animator) animator.SetBool("Run", false); }
    public void RunAction() { if (animator) animator.SetBool("Run", true); }
    public void DeadAction() { if (animator) animator.SetBool("IsDead", true); }
    public void Delete() { Destroy(gameObject); }
    // ===================================================
    // Gizmos：索敵範囲・戦闘範囲・視野角の可視化
    // ===================================================
    void OnDrawGizmos()
    {
        Vector3 pos = transform.position;

        // -------------------------------
        // 索敵範囲（緑）
        // -------------------------------
        Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(pos, detectRange);

        // -------------------------------
        // 戦闘範囲（赤）
        //  ※ 好きな比率に変えてOK
        // -------------------------------
        float combatRange = detectRange * 0.5f;
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(pos, combatRange);

        // -------------------------------
        // 視野角（青）
        // -------------------------------
        float halfAngle = viewAngle * 0.5f;

        Vector3 baseDir = facingLeft ? Vector3.left : Vector3.right;

        Vector3 leftDir = Quaternion.Euler(0, 0, halfAngle) * baseDir;
        Vector3 rightDir = Quaternion.Euler(0, 0, -halfAngle) * baseDir;

        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
        Gizmos.DrawRay(pos, leftDir * detectRange);
        Gizmos.DrawRay(pos, rightDir * detectRange);
    }
}
