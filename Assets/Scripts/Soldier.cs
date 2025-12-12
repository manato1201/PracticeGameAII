using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    // ============================================================
    // ★ フィールド定義（FSM互換＋新AIの両対応）
    // ============================================================

    [Header("HP")]
    public float life = 10f;
    public float maxLife = 10f;

    [Header("AI Settings")]
    public float detectRange = 24f;     // ★ 索敵4倍
    public float combatRange = 6f;
    public float loseSightRange = 40f;

    [Header("Absorb HP")]
    public string batTag = "Bat";
    public float batSearchRange = 15f;
    public float absorbHealAmount = 4f;

    [Header("Combat AI")]
    public float projectileSpeed = 8f;
    public float shootCooldown = 1.2f;
    public GameObject projectilePrefab;

    [Header("Learning AI (Meta)")]
    public Collider2D[] playerAttackHitBox;
    public GameObject[] playerBullets;

    [Header("Movement")]
    public Vector2 speed;
    public float walkSpeed = 1.2f;

    [Header("State Timer")]
    public float stateTimer = 0f;

    [Header("Internals")]
    public bool isHitted = false;
    public bool isInvincible = false;
    public bool isDead = false;

    public SpriteRenderer sprite;
    public Rigidbody2D rb;
    public Animator animator;

    // ============================================================
    // ★ FSM互換（ブラックボックス対策）
    // ============================================================

    public EnemyComp tool;   // 旧コード互換

    public enum MoveType
    {
        Simple = 0,
        BatAbsorb = 1,
        VisionSeek = 2,
        AStarPath = 3,
        JumpDodge = 4,
    }
    public MoveType moveType = MoveType.Simple;

    // FSM互換用のアニメーション呼び出しラッパー
    public void WaitAction() { if (animator) animator.SetBool("Run", false); }
    public void RunAction() { if (animator) animator.SetBool("Run", true); }
    public void AttackAction() { /* 旧近接削除 → 新AIで射撃のみに統合 */ }
    public void RangeAttackAction() { /* 旧FSM攻撃 → 無効化 */ }
    public void DeadAction() { if (animator) animator.SetBool("IsDead", true); }

    // 旧FSMが呼んでも壊れない空関数
    public void MakeAttackHit() { }
    public void MakeShot(bool left) { }

    // ============================================================
    // ★ 新AIの内部変数
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

    Transform player;
    Vector2 lastPlayerPos;

    // Meta学習
    float sampleCount = 1;
    float atkCount = 0;
    float runCount = 0;
    float jumpCount = 0;

    // 恐怖AI
    float fear = 0f;
    float fearThreshold = 1.4f;
    float fearIncreaseRate = 1.5f;
    float fearDecreaseRate = 0.8f;

    // 戦闘パラメータ（メタAIで変動）
    float aggression = 1f;
    float dodgeRate = 1f;
    float feintRate = 1f;

    // NPC歩行
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

        tool = new EnemyComp(this.gameObject);  // 旧FSM互換

        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player != null)
            lastPlayerPos = player.position;

        maxLife = life;
    }

    // ============================================================
    // ★ Update（新AIのメイン）
    // ============================================================
    void Update()
    {
        if (isDead) return;
        if (player == null) return;

        Meta_ObservePlayer();  // プレイヤー行動の学習

        float dist = Vector2.Distance(transform.position, player.position);

        // 高位ステート制御
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
    // ★ IDLE（待機）
    // ============================================================
    void State_IDLE(float dist)
    {
        stateTimer += Time.deltaTime;

        // NPC歩行 → 自然な待機
        NPCWalkBehaviour();

        // プレイヤーが索敵範囲4倍内に入ったら警戒へ
        if (dist < detectRange)
        {
            highState = HighState.ALERT;
            stateTimer = 0;
        }
    }

    // ============================================================
    // ★ ALERT（警戒フェーズ）
    // ============================================================
    void State_ALERT(float dist)
    {
        speed = Vector2.zero;  // 身構える

        // プレイヤーが攻撃的なら恐怖→後退
        if (Fear_Update())
        {
            Vector2 away = (transform.position - player.position).normalized;
            speed = away * 2.0f;
        }

        // プレイヤーを見て向きを合わせる
        sprite.flipX = (player.position.x > transform.position.x);

        // 一定時間で CHASE に移行
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

        // プレイヤーに向かって移動（もっと人間っぽく）
        speed = dir * (2.0f + aggression);

        // 戦闘距離に入ったら COMBAT
        if (dist < combatRange)
        {
            highState = HighState.COMBAT;
            stateTimer = 0;
            return;
        }

        // 視界外に出たら SEARCH
        if (dist > loseSightRange)
        {
            highState = HighState.SEARCH;
            stateTimer = 0;
            return;
        }
    }

    // ============================================================
    // ★ SEARCH（捜索）
    // ============================================================
    void State_SEARCH(float dist)
    {
        stateTimer += Time.deltaTime;

        // 最後に見た位置へ移動
        Vector2 dir = (lastPlayerPos - (Vector2)transform.position).normalized;
        speed = dir * 1.5f;

        // 探索中の NPC 的な挙動（左右確認）
        if (Random.value < 0.02f)
            sprite.flipX = !sprite.flipX;

        // プレイヤーを再発見したら ALERT → CHASE
        if (dist < detectRange)
        {
            highState = HighState.ALERT;
            stateTimer = 0;
            return;
        }

        // 10秒探して見つからなければ IDLE へ
        if (stateTimer > 10f)
        {
            highState = HighState.IDLE;
            stateTimer = 0;
            return;
        }
    }

    // ============================================================
    // ★ ABSORB（吸血行動 / HP回復）
    // ============================================================
    void State_ABSORB(float dist)
    {
        speed = Vector2.zero;

        // Bat を探す
        GameObject bat = FindNearestBat();

        if (bat != null)
        {
            // バットへ瞬間移動して吸収
            transform.position = bat.transform.position;
            Destroy(bat);

            life = Mathf.Min(maxLife, life + absorbHealAmount);

            highState = HighState.ALERT;
            return;
        }

        // Batがいない → プレイヤーから吸収
        if (dist < 3f)
        {
            // プレイヤー背後に瞬間移動
            Vector3 p = player.position;
            float dx = (player.localScale.x > 0) ? -1.3f : 1.3f;
            transform.position = new Vector3(p.x + dx, p.y, p.z);

            // HP回復
            life = Mathf.Min(maxLife, life + absorbHealAmount);

            highState = HighState.COMBAT;
            return;
        }

        // プレイヤー接近
        Vector2 d2 = (player.position - transform.position).normalized;
        speed = d2 * 2f;
    }
    // ============================================================
    // ★ 戦闘サブステート（プロゲーマー AI）
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
    // ★ COMBAT（戦闘AI：メインループ）
    // ============================================================
    void State_COMBAT(float dist)
    {
        lastPlayerPos = player.position;

        // 射程外 → 近づく
        if (dist > combatRange)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            speed = dir * (2.8f * aggression);
        }
        else
        {
            speed = Vector2.zero;
        }

        // 戦闘サブステート実行
        switch (combatState)
        {
            case CombatState.APPROACH: Combat_Approach(dist); break;
            case CombatState.EVADE: Combat_Evade(dist); break;
            case CombatState.SHOOT: Combat_Shoot(dist); break;
            case CombatState.FEINT: Combat_Feint(dist); break;
            case CombatState.BEHIND: Combat_Behind(dist); break;
            case CombatState.ADAPT: Combat_Adapt(dist); break;
        }

        // 見失った場合
        if (dist > loseSightRange)
        {
            highState = HighState.SEARCH;
            stateTimer = 0f;
            return;
        }

        // HP低下 → 吸血へ
        if (life < maxLife * 0.5f)
        {
            highState = HighState.ABSORB;
            return;
        }

        // 次の行動を決める
        Combat_SelectNextState(dist);
    }

    // ============================================================
    // APPROACH：間合いを計りつつ接近
    // ============================================================
    void Combat_Approach(float dist)
    {
        if (dist > 4f)
        {
            Vector2 dir = (player.position - transform.position).normalized;
            speed = dir * (2.5f * aggression);
        }
    }

    // ============================================================
    // EVADE：即時回避（弾 or 近接攻撃）
    // ============================================================
    void Combat_Evade(float dist)
    {
        Vector2 avoidDir;

        // 弾回避優先
        if (IsBulletNear(out avoidDir))
        {
            PerformDodge(avoidDir);
        }
        else
        {
            // プレイヤー攻撃方向へ反応
            Vector2 dir = (transform.position - player.position).normalized;
            PerformDodge(dir);
        }

        combatState = CombatState.APPROACH;
    }

    // ============================================================
    // SHOOT：射撃（プロAI）
    // ============================================================
    void Combat_Shoot(float dist)
    {
        if (Time.time > nextShootTime)
        {
            ShootProjectile();
            nextShootTime = Time.time + (shootCooldown / aggression);
        }

        combatState = CombatState.APPROACH;
    }

    // ============================================================
    // FEINT：フェイント（読み合い AI）
    // ============================================================
    void Combat_Feint(float dist)
    {
        if (Random.value < 0.5f)
        {
            // 横ステップフェイント
            Vector2 perp = Vector2.Perpendicular(player.position - transform.position).normalized;
            if (Random.value < 0.5f) perp = -perp;
            speed = perp * (2.5f * feintRate);
        }
        else
        {
            // 攻撃モーションだけ見せるフェイク
            if (animator != null)
                animator.SetTrigger("Attack");
        }

        combatState = CombatState.APPROACH;
    }

    // ============================================================
    // BEHIND：背後取り瞬間移動（プロ動作）
    // ============================================================
    void Combat_Behind(float dist)
    {
        Vector3 p = player.position;

        float offset = (player.localScale.x > 0) ? -1.2f : 1.2f;

        transform.position = new Vector3(p.x + offset, p.y, p.z);

        combatState = CombatState.APPROACH;
    }

    // ============================================================
    // ADAPT：メタAI ー プレイヤーの癖を学習
    // ============================================================
    void Combat_Adapt(float dist)
    {
        float atkRate = atkCount / sampleCount;
        float runRate = runCount / sampleCount;
        float jumpRate = jumpCount / sampleCount;

        // プレイヤーの行動傾向から内部パラメータを変化
        aggression = Mathf.Clamp(1f + runRate * 1.3f - atkRate * 0.1f, 0.6f, 2.5f);
        dodgeRate = Mathf.Clamp(1f + atkRate * 1.8f + jumpRate * 0.4f, 0.7f, 3.0f);
        feintRate = Mathf.Clamp(1f + atkRate * 0.4f + runRate * 0.2f, 0.6f, 2.2f);

        combatState = CombatState.APPROACH;
    }

    // ============================================================
    // 戦闘サブステートの選択ロジック（プロAIの頭脳）
    // ============================================================
    void Combat_SelectNextState(float dist)
    {
        float r = Random.value;

        // 弾が近い → 強制回避
        Vector2 dummy;
        if (IsBulletNear(out dummy))
        {
            combatState = CombatState.EVADE;
            return;
        }

        // 射程外 → 接近 or 射撃
        if (dist > combatRange)
        {
            combatState =
                (Random.value < 0.6f) ? CombatState.APPROACH : CombatState.SHOOT;
            return;
        }

        // 近距離 → フェイント or 背後取り
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

        // 通常状況
        if (r < 0.5f * aggression)
            combatState = CombatState.SHOOT;
        else if (r < 0.75f * dodgeRate)
            combatState = CombatState.EVADE;
        else if (r < 0.9f * feintRate)
            combatState = CombatState.FEINT;
        else
            combatState = CombatState.ADAPT;
    }
    // ============================================================
    // ★ MetaAI：プレイヤー行動の観察 → 学習
    // ============================================================
    void Meta_ObservePlayer()
    {
        sampleCount++;

        if (player == null) return;

        Vector2 now = player.position;
        Vector2 delta = now - lastPlayerPos;

        // 移動量 → 走り癖
        if (delta.magnitude > 0.1f)
            runCount++;

        // ジャンプ癖
        if (delta.y > 0.15f)
            jumpCount++;

        // 攻撃判定（Inspector設定Hitbox）
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
    // ★ 恐怖AI：プレイヤーの攻撃が激しい時に下がる
    // ============================================================
    bool Fear_Update()
    {
        float atkRate = atkCount / Mathf.Max(1, sampleCount);

        // 怖がるほど後退しやすくなる
        fear += atkRate * fearIncreaseRate * Time.deltaTime;

        // 時間経過で回復
        fear -= fearDecreaseRate * Time.deltaTime;

        fear = Mathf.Clamp(fear, 0f, 3f);

        return fear > fearThreshold;
    }

    // ============================================================
    // ★ NPC歩行：IDLEで自然な動き
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
    // ★ 弾回避AI：近距離の弾丸を感知
    // ============================================================
    bool IsBulletNear(out Vector2 avoidDir)
    {
        avoidDir = Vector2.zero;

        if (playerBullets == null || playerBullets.Length == 0)
            return false;

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
    // ★ Dodge（回避動作）
    // ============================================================
    void PerformDodge(Vector2 dir)
    {
        float power = 6f * dodgeRate;
        if (rb)
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
            prb.velocity = dir * projectileSpeed;
    }

    // ============================================================
    // ★ Bat探索（吸血AI）
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
    // ★ ダメージ処理（旧FSM互換 + 新AI制御）
    // ============================================================
    public void ApplyDamage(float dmg)
    {
        if (isInvincible || isDead) return;

        life -= dmg;

        // ノックバック
        Vector2 knock = (transform.position - player.position).normalized * 2.2f;
        rb.AddForce(knock, ForceMode2D.Impulse);

        // 無敵時間へ
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
    // ★ Movement（最終適用）— FSM が干渉しても新AIが勝つ
    // ============================================================
    void FixedUpdate()
    {
        if (isDead) return;

        // Rigidbody に新AIが決めた speed を適用
        if (rb != null)
            rb.linearVelocity = speed;
    }

    // ============================================================
    // ★ LateUpdate：旧FSMの干渉を完全無効化（最終上書き）
    // ============================================================
    void LateUpdate()
    {
        if (isDead) return;

        // speed 最終上書き（FSMの上書きを破棄）
        if (rb != null)
            rb.linearVelocity = speed;

        // 向きの最終決定（FSMが変更しても新AIが上書き）
        if (speed.x != 0)
        {
            bool faceLeft = speed.x < 0;

            if (sprite != null)
                sprite.flipX = faceLeft;
        }
    }

    // ============================================================
    // ★ FSM 互換メソッド（安全なダミー実装）
    // ============================================================

    // 旧FSMの RunAction が呼ばれても「アニメだけ再生」
    public void RunActionFSM()
    {
        if (animator != null)
            animator.SetBool("Run", true);
    }

    // WaitAction もアニメのみ
    public void WaitActionFSM()
    {
        if (animator != null)
            animator.SetBool("Run", false);
    }

    // DamageActionFSM → 新AIがダメージ管理するためアニメのみ
    public void DamageActionFSM()
    {
        if (animator != null)
            animator.SetBool("Hit", true);
    }

    // DeadAction（旧FSM用）— 新AIの死亡処理と同じ動作に統合
    public void DeadActionFSM()
    {
        if (animator != null)
            animator.SetBool("IsDead", true);
    }

    // 完全削除（旧FSMが使用）
    public void Delete()
    {
        Destroy(gameObject);
    }

    // ============================================================
    // ★ Gizmos（索敵 & 戦闘距離可視化）
    // ============================================================
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // 索敵範囲
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // 戦闘距離
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, combatRange);
    }
}
