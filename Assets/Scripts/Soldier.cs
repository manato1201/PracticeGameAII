using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    // ============================================================
    // ★ FSM互換フィールド（ブラックボックスFSMが要求する旧変数）
    // ============================================================

    public Vector2 position;
    public bool facingLeft = true;

    public float lowHPThreshold = 5f;

    public float viewAngle = 120f;

    public float timer = 0f;

    public float gridSize = 0.7f;
    public Vector2 mapMin = new Vector2(-20, -20);
    public Vector2 mapMax = new Vector2(20, 20);
    public string obstacleTag = "Wall";

    public float dodgeDistance = 2f;
    public float dodgeCooldown = 0.6f;


    // ============================================================
    // ★ HP関連
    // ============================================================

    [Header("HP")]
    public float life = 10f;
    public float maxLife = 10f;


    // ============================================================
    // ★ AI Settings
    // ============================================================

    [Header("AI Settings")]
    public float detectRange = 24f;
    public float combatRange = 6f;
    public float loseSightRange = 40f;


    // ============================================================
    // ★ 吸血AI
    // ============================================================

    [Header("Absorb HP")]
    public string batTag = "Bat";
    public float batSearchRange = 15f;
    public float absorbHealAmount = 4f;


    // ============================================================
    // ★ 戦闘AI
    // ============================================================

    [Header("Combat AI")]
    public float projectileSpeed = 8f;
    public float shootCooldown = 1.2f;
    public GameObject projectilePrefab;


    // ============================================================
    // ★ プレイヤー攻撃観察（学習AI）
    // ============================================================

    [Header("Learning AI (Meta)")]
    public Collider2D[] playerAttackHitBox;
    public GameObject[] playerBullets;


    // ============================================================
    // ★ Movement
    // ============================================================

    public Vector2 speed;
    public float walkSpeed = 1.2f;


    // ============================================================
    // ★ その他内部状態
    // ============================================================

    public float stateTimer = 0f;
    public bool isHitted = false;
    public bool isInvincible = false;
    public bool isDead = false;

    public SpriteRenderer sprite;
    public Rigidbody2D rb;
    public Animator animator;

    public EnemyComp tool;

    // ============================================================
    // ★ 旧FSM互換 MoveType（使わないが必須）
    // ============================================================

    public enum MoveType
    {
        Simple = 0,
        BatAbsorb = 1,
        VisionSeek = 2,
        AStarPath = 3,
        JumpDodge = 4
    }

    // FSM が参照する現在の移動タイプ（新AIは使用しない）
    public MoveType moveType = MoveType.Simple;


    // ============================================================
    // ★ FSM互換 State（ブラックボックス用ダミー）
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


    // ============================================================
    // ★ 新AIのメインステート
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


    // ============================================================
    // ★ 戦闘サブステート
    // ============================================================

    public enum CombatState
    {
        APPROACH,
        EVADE,
        SHOOT,
        FEINT,
        BEHIND,
        ADAPT
    }
    CombatState combatState = CombatState.APPROACH;


    // ============================================================
    // ★ プレイヤー情報
    // ============================================================

    Transform player;
    Vector2 lastPlayerPos;


    // ============================================================
    // ★ Meta学習パラメータ
    // ============================================================

    float sampleCount = 1;
    float atkCount = 0;
    float runCount = 0;
    float jumpCount = 0;

    float aggression = 1f;
    float dodgeRate = 1f;
    float feintRate = 1f;

    float fear = 0f;
    float fearThreshold = 1.4f;
    float fearIncreaseRate = 1.5f;
    float fearDecreaseRate = 0.8f;

    float idleWalkTimer = 0f;
    bool idleWalking = false;

    float nextShootTime = 0f;


    // ============================================================
    // ★ Start
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

        maxLife = life;
    }


    // ============================================================
    // ★ Update
    // ============================================================

    void Update()
    {
        if (isDead || player == null) return;

        // 旧FSM互換の毎フレーム更新
        position = transform.position;
        facingLeft = sprite.flipX;
        timer += Time.deltaTime;

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
    // ★ IDLE（NPC歩行）
    // ============================================================

    void State_IDLE(float dist)
    {
        stateTimer += Time.deltaTime;
        NPCWalkBehaviour();

        if (dist < detectRange)
        {
            highState = HighState.ALERT;
            stateTimer = 0;
        }
    }


    // ============================================================
    // ★ ALERT（警戒）
    // ============================================================

    void State_ALERT(float dist)
    {
        speed = Vector2.zero;

        if (Fear_Update())
        {
            Vector2 away = (transform.position - player.position).normalized;
            speed = away * 2f;
        }

        if (stateTimer > 0.5f)
        {
            highState = HighState.CHASE;
            stateTimer = 0;
        }

        stateTimer += Time.deltaTime;
    }


    // ============================================================
    // ★ CHASE（追跡）
    // ============================================================

    void State_CHASE(float dist)
    {
        Vector2 dir = (player.position - transform.position).normalized;
        speed = dir * (2f + aggression);

        if (dist < combatRange)
        {
            highState = HighState.COMBAT;
            stateTimer = 0;
        }

        if (dist > loseSightRange)
        {
            highState = HighState.SEARCH;
            stateTimer = 0;
        }
    }


    // ============================================================
    // ★ SEARCH（捜索）
    // ============================================================

    void State_SEARCH(float dist)
    {
        stateTimer += Time.deltaTime;

        Vector2 dir = (lastPlayerPos - (Vector2)transform.position).normalized;
        speed = dir * 1.5f;

        if (Random.value < 0.02f)
            sprite.flipX = !sprite.flipX;

        if (dist < detectRange)
        {
            highState = HighState.ALERT;
            stateTimer = 0;
        }

        if (stateTimer > 10f)
        {
            highState = HighState.IDLE;
            stateTimer = 0;
        }
    }


    // ============================================================
    // ★ ABSORB（吸血）
    // ============================================================

    void State_ABSORB(float dist)
    {
        speed = Vector2.zero;

        GameObject bat = FindNearestBat();
        if (bat != null)
        {
            transform.position = bat.transform.position;
            Destroy(bat);

            life = Mathf.Min(maxLife, life + absorbHealAmount);

            highState = HighState.ALERT;
            return;
        }

        if (dist < 3f)
        {
            Vector3 p = player.position;
            float dx = (player.localScale.x > 0) ? -1.3f : 1.3f;

            transform.position = new Vector3(p.x + dx, p.y, p.z);

            life = Mathf.Min(maxLife, life + absorbHealAmount);

            highState = HighState.COMBAT;
            return;
        }

        Vector2 d2 = (player.position - transform.position).normalized;
        speed = d2 * 2f;
    }


    // ============================================================
    // ★ COMBAT（戦闘AI）
    // ============================================================

    void State_COMBAT(float dist)
    {
        lastPlayerPos = player.position;

        if (dist > combatRange)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            speed = dir * (2.8f * aggression);
        }
        else
        {
            speed = Vector2.zero;
        }

        switch (combatState)
        {
            case CombatState.APPROACH: Combat_Approach(dist); break;
            case CombatState.EVADE: Combat_Evade(dist); break;
            case CombatState.SHOOT: Combat_Shoot(dist); break;
            case CombatState.FEINT: Combat_Feint(dist); break;
            case CombatState.BEHIND: Combat_Behind(dist); break;
            case CombatState.ADAPT: Combat_Adapt(dist); break;
        }

        if (dist > loseSightRange)
        {
            highState = HighState.SEARCH;
            stateTimer = 0;
            return;
        }

        if (life < maxLife * 0.5f)
        {
            highState = HighState.ABSORB;
            return;
        }

        Combat_SelectNextState(dist);
    }


    // ===== Approach
    void Combat_Approach(float dist)
    {
        if (dist > 4f)
            speed = (player.position - transform.position).normalized * (2.5f * aggression);
    }

    // ===== Evade
    void Combat_Evade(float dist)
    {
        Vector2 avoid;

        if (IsBulletNear(out avoid))
            PerformDodge(avoid);
        else
            PerformDodge((transform.position - player.position).normalized);

        combatState = CombatState.APPROACH;
    }

    // ===== Shoot
    void Combat_Shoot(float dist)
    {
        if (Time.time > nextShootTime)
        {
            ShootProjectile();
            nextShootTime = Time.time + (shootCooldown / aggression);
        }
        combatState = CombatState.APPROACH;
    }

    // ===== Feint
    void Combat_Feint(float dist)
    {
        if (Random.value < 0.5f)
        {
            Vector2 perp = Vector2.Perpendicular(player.position - transform.position).normalized;
            if (Random.value < 0.5f) perp = -perp;
            speed = perp * (2.5f * feintRate);
        }
        else
        {
            if (animator != null)
                animator.SetTrigger("Attack");
        }
        combatState = CombatState.APPROACH;
    }

    // ===== Behind
    void Combat_Behind(float dist)
    {
        Vector3 p = player.position;
        float offset = (player.localScale.x > 0) ? -1.2f : 1.2f;
        transform.position = new Vector3(p.x + offset, p.y, p.z);

        combatState = CombatState.APPROACH;
    }

    // ===== Adapt
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
    // ★ 戦闘ステート選択
    // ============================================================

    void Combat_SelectNextState(float dist)
    {
        float r = Random.value;

        Vector2 dummy;
        if (IsBulletNear(out dummy))
        {
            combatState = CombatState.EVADE;
            return;
        }

        if (dist > combatRange)
        {
            combatState = (Random.value < 0.6f) ? CombatState.APPROACH : CombatState.SHOOT;
            return;
        }

        if (dist < 3f)
        {
            if (r < 0.3f * feintRate)
                combatState = CombatState.FEINT;
            else if (r < 0.5f * feintRate)
                combatState = CombatState.BEHIND;
            else
                combatState = CombatState.SHOOT;

            return;
        }

        if (r < 0.5f * aggression) combatState = CombatState.SHOOT;
        else if (r < 0.75f * dodgeRate) combatState = CombatState.EVADE;
        else if (r < 0.9f * feintRate) combatState = CombatState.FEINT;
        else combatState = CombatState.ADAPT;
    }


    // ============================================================
    // ★ Meta学習
    // ============================================================

    void Meta_ObservePlayer()
    {
        sampleCount++;

        Vector2 now = player.position;
        Vector2 delta = now - lastPlayerPos;

        if (delta.magnitude > 0.1f) runCount++;
        if (delta.y > 0.15f) jumpCount++;

        foreach (var hb in playerAttackHitBox)
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
    // ★ 恐怖AI
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
    // ★ NPC歩行
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
            float dir = (Random.value < 0.5f) ? -1f : 1f;
            speed = new Vector2(dir * walkSpeed, 0);
        }
        else
        {
            speed = Vector2.zero;
        }
    }


    // ============================================================
    // ★ 弾回避AI
    // ============================================================

    bool IsBulletNear(out Vector2 avoidDir)
    {
        avoidDir = Vector2.zero;

        Rigidbody2D[] all = GameObject.FindObjectsOfType<Rigidbody2D>();

        foreach (Rigidbody2D body in all)
        {
            if (body == null) continue;

            foreach (var bullet in playerBullets)
            {
                if (bullet == null) continue;

                if (body.gameObject.name.Contains(bullet.name))
                {
                    float d = Vector2.Distance(transform.position, body.transform.position);

                    if (d < 2.5f)
                    {
                        avoidDir = (transform.position - body.transform.position).normalized;
                        return true;
                    }
                }
            }
        }
        return false;
    }


    // ============================================================
    // ★ Dodge（回避）
    // ============================================================

    void PerformDodge(Vector2 dir)
    {
        float power = 6f * dodgeRate;
        rb.AddForce(dir.normalized * power, ForceMode2D.Impulse);
    }


    // ============================================================
    // ★ 射撃
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

        Rigidbody2D prb = proj.GetComponent<Rigidbody2D>();
        if (prb != null)
            prb.linearVelocity = dir * projectileSpeed;
    }


    // ============================================================
    // ★ Bat探索
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

    public void ApplyDamage(float dmg)
    {
        if (isInvincible || isDead) return;

        life -= dmg;

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
        yield return new WaitForSeconds(1.0f);
        Delete();
    }


    // ============================================================
    // ★ Movement（FSMより優先）
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
    // ★ FSM互換ダミー関数
    // ============================================================

    public void WaitAction() { }
    public void RunAction() { }
    public void AttackAction() { }
    public void RangeAttackAction() { }
    public void DeadAction()
    {
        if (animator != null)
            animator.SetBool("IsDead", true);
    }

    public void Delete()
    {
        Destroy(gameObject);
    }


    // ============================================================
    // ★ Gizmos
    // ============================================================

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, combatRange);
    }
}
