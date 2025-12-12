using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    // ============================================================
    // ★ FSM（ブラックボックス）互換ステート
    // ============================================================

    public enum State
    {
        WAIT,
        RUN,
        MELEE_ATTACK,
        RANGE_ATTACK,
        DAMAGE,
        DEAD
    }

    [HideInInspector] public State currentState = State.WAIT;
    [HideInInspector] public State nextState = State.WAIT;


    // ============================================================
    // ★ FSM が参照する変数（public 必須だが Inspector 非表示）
    // ============================================================

    [HideInInspector] public Vector2 speed;
    [HideInInspector] public bool isHitted;
    [HideInInspector] public EnemyComp tool;
    [HideInInspector] public float life;
    [HideInInspector] public float batSearchRange = 12f;
    [HideInInspector] public float detectRange = 6f;

    // 旧AI 互換メンバー（FSM が参照する）
    [HideInInspector] public Vector2 position;
    [HideInInspector] public bool facingLeft = true;
    [HideInInspector] public float timer = 0f;

    public float lowHPThreshold = 5f;
    public float viewAngle = 120f;

    public float gridSize = 0.7f;
    public Vector2 mapMin = new Vector2(-20, -20);
    public Vector2 mapMax = new Vector2(20, 20);
    public string obstacleTag = "Wall";

    public float dodgeDistance = 2f;
    public float dodgeCooldown = 0.6f;


    // ============================================================
    // ★ MoveType（FSM が参照する）
    // ============================================================

    public enum MoveType
    {
        Simple = 0,
        BatAbsorb = 1,
        VisionSeek = 2,
        AStarPath = 3,
        JumpDodge = 4
    }
    public MoveType moveType = MoveType.Simple;


    // ============================================================
    // ★ Inspector に表示する必要がある設定（private + SerializeField）
    // ============================================================

    [Header("HP Settings")]
    [SerializeField] private float maxLife = 10f;

    [Header("Combat Settings")]
    [SerializeField] private float combatRange = 6f;
    [SerializeField] private float loseSightRange = 40f;

    [Header("Absorb Settings")]
    [SerializeField] private string batTag = "Bat";
    [SerializeField] private float absorbHeal = 4f;

    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private float projectileCD = 1.2f;

    [Header("Meta AI - Player Attack Detection")]
    [SerializeField] private Collider2D[] playerAttackHitBoxes;
    [SerializeField] private string bulletDetectName = "Bullet";

    [Header("NPC Walk Settings")]
    [SerializeField] private float walkSpeed = 1.2f;


    // ============================================================
    // ★ 内部専用（Inspector 非表示）
    // ============================================================

    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private Animator animator;
    private bool isDead = false;
    private bool isInvincible = false;

    private Transform player;
    private Vector2 lastPlayerPos;

    // Meta Learning AI
    private float sampleCount = 1;
    private float atkCount = 0;
    private float runCount = 0;
    private float jumpCount = 0;

    // AI パラメータに反映
    private float aggression = 1f;
    private float dodgeRate = 1f;
    private float feintRate = 1f;

    // Fear AI
    private float fear = 0f;
    private float fearThreshold = 1.4f;
    private float fearIncreaseRate = 1.5f;
    private float fearDecreaseRate = 0.8f;

    // NPC 歩行AI
    private bool idleWalking = false;
    private float idleWalkTimer = 0f;

    // 射撃管理
    private float nextShootTime = 0f;


    // ============================================================
    // ★ 高レベル AI ステート（あなたの最新AI）
    // ============================================================

    public enum HighState
    {
        IDLE,
        ALERT,
        CHASE,
        COMBAT,
        SEARCH,
        ABSORB
    }
    public HighState highState = HighState.IDLE;

    public enum CombatState
    {
        APPROACH,
        EVADE,
        SHOOT,
        FEINT,
        BEHIND,
        ADAPT
    }
    private CombatState combatState = CombatState.APPROACH;


    // ============================================================
    // Start
    // ============================================================
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        tool = new EnemyComp(this.gameObject);

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
            lastPlayerPos = player.position;

        life = maxLife;

        // 🔥 ここに追加！
        detectRange *= 2f;
        combatRange *= 2f;
    }

    // ============================================================
    // Update（高レベルAI・MetaAI・FSM互換更新）
    // ============================================================

    void Update()
    {
        if (isDead) return;
        if (player == null) return;

        // FSM互換更新
        position = transform.position;
        facingLeft = sprite.flipX;
        timer += Time.deltaTime;

        // MetaAI（プレイヤー行動解析）
        Meta_ObservePlayer();

        float dist = Vector2.Distance(transform.position, player.position);

        switch (highState)
        {
            case HighState.IDLE: State_IDLE(dist); break;
            case HighState.ALERT: State_ALERT(dist); break;
            case HighState.CHASE: State_CHASE(dist); break;
            case HighState.COMBAT: State_COMBAT(dist); break;
            case HighState.SEARCH: State_SEARCH(dist); break;
            case HighState.ABSORB: State_ABSORB(dist); break;
        }
    }


    // ============================================================
    // ★ IDLE（通常NPC歩行 + プレイヤー認知）
    // ============================================================

    void State_IDLE(float dist)
    {
        NPCWalkBehaviour();

        if (dist < detectRange)
        {
            highState = HighState.ALERT;
            timer = 0;
        }
    }


    // ============================================================
    // ★ ALERT（警戒状態：恐怖AIで後退もあり）
    // ============================================================

    void State_ALERT(float dist)
    {
        speed = Vector2.zero;

        if (Fear_Update())
        {
            // 恐怖による一時後退
            Vector2 away = (transform.position - player.position).normalized;
            speed = away * 2f;
        }

        timer += Time.deltaTime;

        if (timer > 0.5f)
        {
            highState = HighState.CHASE;
            timer = 0;
        }
    }


    // ============================================================
    // ★ CHASE（プレイヤー追跡）
    // ============================================================

    void State_CHASE(float dist)
    {
        Vector2 dir = (player.position - transform.position).normalized;
        speed = dir * (2f + aggression);

        if (dist < combatRange)
        {
            highState = HighState.COMBAT;
            timer = 0;
        }

        if (dist > loseSightRange)
        {
            highState = HighState.SEARCH;
            timer = 0;
        }
    }


    // ============================================================
    // ★ SEARCH（プレイヤー見失い → 探索移動）
    // ============================================================

    void State_SEARCH(float dist)
    {
        timer += Time.deltaTime;

        Vector2 dir = (lastPlayerPos - (Vector2)transform.position).normalized;
        speed = dir * 1.5f;

        // ランダムな方向転換で探索らしさを演出
        if (Random.value < 0.02f)
            sprite.flipX = !sprite.flipX;

        if (dist < detectRange)
        {
            highState = HighState.ALERT;
            timer = 0;
        }

        if (timer > 10f)
        {
            highState = HighState.IDLE;
            timer = 0;
        }
    }


    // ============================================================
    // ★ ABSORB（低HP → コウモリ瞬間移動吸血）
    // ============================================================

    void State_ABSORB(float dist)
    {
        speed = Vector2.zero;

        GameObject bat = FindNearestBat();
        if (bat != null)
        {
            // コウモリへ瞬間移動
            transform.position = bat.transform.position;
            Destroy(bat);

            life = Mathf.Min(maxLife, life + absorbHeal);

            highState = HighState.ALERT;
            return;
        }

        // コウモリがいない → プレイヤー背後に瞬間移動昏倒吸血
        if (dist < 3f)
        {
            Vector3 p = player.position;
            float dx = (player.localScale.x > 0) ? -1.3f : 1.3f;

            transform.position = new Vector3(p.x + dx, p.y, p.z);

            life = Mathf.Min(maxLife, life + absorbHeal);

            highState = HighState.COMBAT;
            return;
        }

        // 吸血距離まで接近
        Vector2 d2 = (player.position - transform.position).normalized;
        speed = d2 * 2f;
    }


    // ============================================================
    // ★ COMBAT（戦闘AI 中核）
    // ============================================================

    void State_COMBAT(float dist)
    {
        lastPlayerPos = player.position;

        // 戦闘距離外 → 接近
        if (dist > combatRange)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            speed = dir * (2.8f * aggression);
        }
        else
        {
            speed = Vector2.zero;
        }

        // 戦闘サブステートの振り分け
        switch (combatState)
        {
            case CombatState.APPROACH: Combat_Approach(dist); break;
            case CombatState.EVADE: Combat_Evade(dist); break;
            case CombatState.SHOOT: Combat_Shoot(dist); break;
            case CombatState.FEINT: Combat_Feint(dist); break;
            case CombatState.BEHIND: Combat_Behind(dist); break;
            case CombatState.ADAPT: Combat_Adapt(dist); break;
        }

        // 見失ったら SEARCH
        if (dist > loseSightRange)
        {
            highState = HighState.SEARCH;
            return;
        }

        // HP 半分以下 → 吸血へ
        if (life < maxLife * 0.5f)
        {
            highState = HighState.ABSORB;
            return;
        }

        // 次の戦闘ステート選択
        Combat_SelectNextState(dist);
    }
    // ============================================================
    // ★ COMBAT サブステート実装
    // ============================================================

    // --- 接近 ---
    void Combat_Approach(float dist)
    {
        if (dist > 4f)
            speed = (player.position - transform.position).normalized * (2.5f * aggression);
    }

    // --- 回避（弾・プレイヤー攻撃） ---
    void Combat_Evade(float dist)
    {
        Vector2 avoid;

        // 弾が近い → 即回避
        if (IsBulletNear(out avoid))
        {
            PerformDodge(avoid);
        }
        else
        {
            PerformDodge((transform.position - player.position).normalized);
        }

        combatState = CombatState.APPROACH;
    }

    // --- 射撃 ---
    void Combat_Shoot(float dist)
    {
        if (Time.time >= nextShootTime)
        {
            ShootProjectile();
            nextShootTime = Time.time + (projectileCD / aggression);
        }

        combatState = CombatState.APPROACH;
    }

    // --- フェイント攻撃 ---
    void Combat_Feint(float dist)
    {
        // フェイント動き（横ステップ）
        if (Random.value < 0.5f)
        {
            Vector2 perp = Vector2.Perpendicular(player.position - transform.position).normalized;
            if (Random.value < 0.5f) perp = -perp;
            speed = perp * (2.5f * feintRate);
        }
        else
        {
            // 射撃フェイント（タイミングずらし）
            if (Time.time > nextShootTime)
            {
                ShootProjectile();
                nextShootTime = Time.time + (projectileCD * 0.8f);
            }
        }

        combatState = CombatState.APPROACH;
    }
    // --- 背後取り（瞬間移動） ---
    void Combat_Behind(float dist)
    {
        Vector3 p = player.position;
        float offset = (player.localScale.x > 0) ? -1.2f : 1.2f;

        transform.position = new Vector3(p.x + offset, p.y, p.z);
        combatState = CombatState.APPROACH;
    }

    // --- 学習AIによる適応行動 ---
    void Combat_Adapt(float dist)
    {
        float atkRate = atkCount / sampleCount;
        float runRate = runCount / sampleCount;
        float jumpRate = jumpCount / sampleCount;

        aggression = Mathf.Clamp(1f + runRate * 1.3f - atkRate * 0.1f, 0.6f, 2.5f);
        dodgeRate = Mathf.Clamp(1f + atkRate * 1.8f + jumpRate * 0.4f, 0.7f, 3.0f);
        feintRate = Mathf.Clamp(1f + atkRate * 0.4f + runRate * 0.2f, 0.6f, 2.2f);

        combatState = CombatState.APPROACH;
    }


    // ============================================================
    // ★ 次の戦闘サブステートを選ぶ
    // ============================================================

    void Combat_SelectNextState(float dist)
    {
        float r = Random.value;

        // 弾回避優先
        Vector2 dummy;
        if (IsBulletNear(out dummy))
        {
            combatState = CombatState.EVADE;
            return;
        }

        // 射程外 → 接近 or 射撃
        if (dist > combatRange)
        {
            combatState = (Random.value < 0.6f) ? CombatState.APPROACH : CombatState.SHOOT;
            return;
        }

        // 近距離 → フェイント・背後取り・射撃
        if (dist < 3f)
        {
            if (r < 0.3f * feintRate) combatState = CombatState.FEINT;
            else if (r < 0.5f * feintRate) combatState = CombatState.BEHIND;
            else combatState = CombatState.SHOOT;

            return;
        }

        // 中距離 → 多彩な行動
        if (r < 0.5f * aggression) combatState = CombatState.SHOOT;
        else if (r < 0.75f * dodgeRate) combatState = CombatState.EVADE;
        else if (r < 0.9f * feintRate) combatState = CombatState.FEINT;
        else combatState = CombatState.ADAPT;
    }


    // ============================================================
    // ★ Meta AI：プレイヤー行動の観察（学習AI）
    // ============================================================

    void Meta_ObservePlayer()
    {
        sampleCount++;

        Vector2 now = player.position;
        Vector2 delta = now - lastPlayerPos;

        if (delta.magnitude > 0.15f) runCount++;
        if (delta.y > 0.15f) jumpCount++;

        // 攻撃検知
        foreach (var hb in playerAttackHitBoxes)
        {
            if (hb != null && hb.enabled)
            {
                atkCount++;
                break;
            }
        }

        lastPlayerPos = now;
    }


    // ============================================================
    // ★ Fear AI（恐怖による後退）
    // ============================================================

    bool Fear_Update()
    {
        float atkRate = atkCount / Mathf.Max(1, sampleCount);

        fear += atkRate * fearIncreaseRate * Time.deltaTime;
        fear -= fearDecreaseRate * Time.deltaTime;

        fear = Mathf.Clamp(fear, 0f, 3f);

        return fear > fearThreshold;
    }


    // ============================================================
    // ★ NPC（非戦闘）歩行AI
    // ============================================================

    void NPCWalkBehaviour()
    {
        idleWalkTimer -= Time.deltaTime;

        if (idleWalkTimer <= 0)
        {
            idleWalking = !idleWalking;
            idleWalkTimer = Random.Range(0.8f, 2f);
        }

        if (idleWalking)
        {
            float dir = Random.value < 0.5f ? -1f : 1f;
            speed = new Vector2(dir * walkSpeed, 0);
        }
        else
        {
            speed = Vector2.zero;
        }
    }


    // ============================================================
    // ★ 弾回避判定
    // ============================================================

    bool IsBulletNear(out Vector2 avoidDir)
    {
        avoidDir = Vector2.zero;

        Rigidbody2D[] bodies = GameObject.FindObjectsOfType<Rigidbody2D>();

        foreach (var b in bodies)
        {
            if (b == null) continue;
            if (!b.gameObject.name.Contains(bulletDetectName)) continue;

            float d = Vector2.Distance(transform.position, b.transform.position);

            if (d < 2.5f)
            {
                avoidDir = (transform.position - b.transform.position).normalized;
                return true;
            }
        }

        return false;
    }

    void PerformDodge(Vector2 dir)
    {
        float power = 6f * dodgeRate;
        rb.AddForce(dir.normalized * power, ForceMode2D.Impulse);
    }
    // ============================================================
    // ★ 射撃攻撃
    // ============================================================

    void ShootProjectile()
    {
        if (projectilePrefab == null) return;

        Vector2 dir = (player.position - transform.position).normalized;
        GameObject proj = Instantiate(
            projectilePrefab,
            transform.position + new Vector3(dir.x * 0.4f, 0, 0),
            Quaternion.identity
        );

        Rigidbody2D rbProj = proj.GetComponent<Rigidbody2D>();
        if (rbProj != null)
            rbProj.linearVelocity = dir * projectileSpeed;
    }


    // ============================================================
    // ★ Bat（コウモリ）探索
    // ============================================================

    GameObject FindNearestBat()
    {
        GameObject[] bats = GameObject.FindGameObjectsWithTag(batTag);
        GameObject best = null;
        float bestDist = batSearchRange;

        foreach (var b in bats)
        {
            float d = Vector2.Distance(transform.position, b.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = b;
            }
        }
        return best;
    }


    // ============================================================
    // ★ ダメージ処理
    // ============================================================

    public void ApplyDamage(float damage)
    {
        if (isInvincible || isDead) return;

        life -= damage;

        // ノックバック
        Vector2 knock = (transform.position - player.position).normalized * 2.2f;
        rb.AddForce(knock, ForceMode2D.Impulse);

        StartCoroutine(DamageInvincible());

        if (life <= 0f && !isDead)
        {
            isDead = true;
            DeadAction();
            StartCoroutine(DeathRoutine());
        }
    }

    IEnumerator DamageInvincible()
    {
        isInvincible = true;
        isHitted = true;

        yield return new WaitForSeconds(0.35f);

        isInvincible = false;
        isHitted = false;
    }

    IEnumerator DeathRoutine()
    {
        speed = Vector2.zero;
        yield return new WaitForSeconds(1f);
        Delete();
    }


    // ============================================================
    // ★ Movement（FSM互換 / Rigidbody2D）
    // ============================================================

    void FixedUpdate()
    {
        if (isDead) return;
        rb.linearVelocity = speed;
    }

    void LateUpdate()
    {
        if (isDead) return;

        rb.linearVelocity = speed;

        if (speed.x != 0)
            sprite.flipX = speed.x < 0;
    }


    // ============================================================
    // ★ FSM 用ダミーアクション（FSMから呼ばれるので必須）
    // ============================================================

    public void WaitAction() { }
    public void RunAction() { }
    // ============================================================
    // 近距離攻撃（廃止）
    // ============================================================

    // 近距離攻撃廃止
    public void AttackAction()
    {
        // 何もしない（FSM安全対策）
    }

    // 近距離攻撃ヒット無効化
    void MakeAttackHit()
    {
        // 完全に機能停止
        return;
    }


    // ============================================================
    // 遠距離攻撃（射撃アクション）
    // ============================================================

    public void RangeAttackAction()
    {
        ShootProjectile();
    }


    public void DeadAction()
    {
        animator?.SetBool("IsDead", true);
    }

    public void Delete()
    {
        Destroy(gameObject);
    }


    // ============================================================
    // ★ Debug Gizmos
    // ============================================================
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            // ゲーム停止中でも範囲を見たい場合
            DrawGizmosStatic();
            return;
        }

        DrawGizmosStatic();
    }

    void DrawGizmosStatic()
    {
        // ===========================================
        // ★ 1. 索敵範囲（黄色）
        // ===========================================
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // ===========================================
        // ★ 2. 戦闘範囲（赤）
        // ===========================================
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, combatRange);

        // ===========================================
        // ★ 3. 視野角（青の扇形）
        // ===========================================
        Gizmos.color = Color.cyan;

        Vector3 forwardDir = facingLeft ? Vector3.left : Vector3.right;

        int segments = 32;
        float halfAngle = viewAngle * 0.5f;

        Vector3 prevPoint = Vector3.zero;
        bool first = true;

        for (int i = 0; i <= segments; i++)
        {
            float angle = -halfAngle + (viewAngle * i / segments);
            float rad = angle * Mathf.Deg2Rad;

            Vector3 dir = Quaternion.Euler(0, 0, angle) * forwardDir;
            Vector3 point = transform.position + dir * detectRange;

            if (!first)
                Gizmos.DrawLine(prevPoint, point);

            prevPoint = point;
            first = false;
        }

        // 視界の中心線
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + forwardDir * detectRange);

        // ===========================================
        // ★ 4. 現在向いている方向を示す白線
        // ===========================================
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, transform.position + forwardDir * 1.5f);
    }
}
