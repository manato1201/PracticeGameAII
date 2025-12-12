using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier : MonoBehaviour
{
    // ===================================================
    //  基礎パラメータ（Inspector 表示は必要最小限）
    // ===================================================

    [SerializeField] private float _life = 10f;
    public float life
    {
        get => _life;
        set => _life = value;
    }

    [SerializeField] private GameObject throwableObject;
    [SerializeField] private GameObject player;

    [HideInInspector] public bool isHitted = false;

    [HideInInspector] public float maxLife = 10f;
    private bool isAlert = false;   // 索敵状態かどうか

    public Vector2 position => transform.position;

    // ===================================================
    //  FSM が必要とするパラメータ（Inspector 非表示）
    // ===================================================
    [HideInInspector] public float lowHPThreshold = 5f;
    [HideInInspector] public float detectRange = 12f;
    [HideInInspector] public float viewAngle = 120f;

    [HideInInspector] public float batSearchRange = 24f;

    [HideInInspector] public float gridSize = 0.7f;
    [HideInInspector] public Vector2 mapMin = new Vector2(-20, -20);
    [HideInInspector] public Vector2 mapMax = new Vector2(20, 20);
    [HideInInspector] public string obstacleTag = "Wall";

    [HideInInspector] public float dodgeDistance = 2f;
    [HideInInspector] public float dodgeCooldown = 0.6f;

    // ===================================================
    //  移動系
    // ===================================================

    [HideInInspector] public Vector2 speed = Vector2.zero;
    private Rigidbody2D rb;

    [HideInInspector] public bool facingLeft = true;

    [HideInInspector] public float timer = 0f;

    // ===================================================
    //  FSM ステート
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

    private State currentState = State.WAIT;

    private List<Soldier_FSM_Base> availableStates = new List<Soldier_FSM_Base>();

    // FSM 用 EnemyComp
    [HideInInspector] public EnemyComp tool;

    // MoveType（FSM 互換のため復元）
    public enum MoveType
    {
        Simple = 0,
        BatAbsorb = 1,
        VisionSeek = 2,
        AStarPath = 3,
        JumpDodge = 4
    }
    [HideInInspector] public MoveType moveType = MoveType.Simple;

    // Animator
    private Animator animator;

    // ===================================================
    //  Start
    // ===================================================
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        tool = new EnemyComp(gameObject);

        maxLife = life;

        // FSM 登録
        availableStates.Add(new Soldier_FSM_Wait(this));
        availableStates.Add(new Soldier_FSM_Run(this));
        availableStates.Add(new Soldier_FSM_Attack(this));
        availableStates.Add(new Soldier_FSM_RangeAttack(this));
        availableStates.Add(new Soldier_FSM_Damage(this));
        availableStates.Add(new Soldier_FSM_Dead(this));

        currentState = State.WAIT;
        availableStates[(int)currentState].OnEnter();
    }

    // ===================================================
    //  Update（FSM → AI → Movement）
    // ===================================================
    void Update()
    {
        timer += Time.deltaTime;

        // FSM 判定
        State next = availableStates[(int)currentState].CheckTransitions();

        if (life <= 0 && currentState != State.DEAD)
            next = State.DEAD;

        if (next != currentState)
        {
            availableStates[(int)currentState].OnExit();
            currentState = next;
            availableStates[(int)currentState].OnEnter();
        }

        availableStates[(int)currentState].OnUpdate();

        Movement();
    }

    // ===================================================
    // Movement
    // ===================================================
    void Movement()
    {
        GetComponent<SpriteRenderer>().flipX = !facingLeft;
        rb.linearVelocity = speed;
    }
    // ===================================================
    //  ダメージ処理（FSM 互換）
    // ===================================================
    public void ApplyDamage(float damage)
    {
        life -= damage;

        if (life <= 0)
        {
            life = 0;
            return;
        }

        isHitted = true;
        StartCoroutine(HitRecover());
    }

    IEnumerator HitRecover()
    {
        yield return new WaitForSeconds(0.3f);
        isHitted = false;
    }

    // ===================================================
    //  攻撃アクション（近距離攻撃は削除 → 空処理）
    // ===================================================
    public void AttackAction()
    {
        // 旧FSMが呼ぶが、近距離攻撃は廃止しているため何もしない
    }

    // ===================================================
    //  射撃攻撃（遠距離攻撃のみ有効）
    // ===================================================
    public void RangeAttackAction()
    {
        if (throwableObject == null) return;

        float offset = facingLeft ? -0.5f : 0.5f;
        float bulletSpeed = facingLeft ? -0.5f : 0.5f;

        GameObject proj = Instantiate(
            throwableObject,
            transform.position + new Vector3(offset, -0.2f, 0),
            Quaternion.identity
        );

        ThrowableProjectile tp = proj.GetComponent<ThrowableProjectile>();
        tp.owner = gameObject;
        tp.direction = new Vector2(bulletSpeed, 0);
    }

    // ===================================================
    //  バット検索（FSM 互換）
    // ===================================================
    public GameObject FindNearestBat()
    {
        GameObject[] bats = GameObject.FindGameObjectsWithTag("Bat");
        if (bats.Length == 0) return null;

        float best = float.MaxValue;
        GameObject nearest = null;

        foreach (GameObject b in bats)
        {
            if (b == null) continue;

            float d = Vector2.Distance(transform.position, b.transform.position);
            if (d < best)
            {
                best = d;
                nearest = b;
            }
        }
        return nearest;
    }

    // ===================================================
    //  吸血行動（瞬間移動 → HP回復）
    // ===================================================
    public void AbsorbBatAndHeal(GameObject bat)
    {
        if (bat == null) return;

        transform.position = bat.transform.position;

        life = Mathf.Min(maxLife, life + 5f);

        Destroy(bat);
    }

    // ===================================================
    //  ここから FSM 互換レイヤー（旧API）
    // ===================================================

    // FSM Wait → Soldier.WaitAction()
    public void WaitAction()
    {
        if (animator != null)
            animator.SetBool("Run", false);
    }

    // FSM Run → Soldier.RunAction()
    public void RunAction()
    {
        if (animator != null)
            animator.SetBool("Run", true);
    }

    // FSM Dead → Soldier.DeadAction()
    public void DeadAction()
    {
        if (animator != null)
            animator.SetBool("IsDead", true);
    }

    // Dead 後に呼ばれる
    public void Delete()
    {
        Destroy(gameObject);
    }
    // ===================================================
    //  Meta AI（索敵 → 警戒 → 戦闘）
    // ===================================================

    private Vector3 lastKnownPlayerPos;
    private bool searchingPlayer = false;

    void ProGamerAI_Update()
    {
        // Player をまだ捕捉していない場合でも索敵する
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            NPC_Walk();
            return;
        }

        Vector2 soldierPos = transform.position;
        Vector2 pPos = p.transform.position;
        float dist = Vector2.Distance(soldierPos, pPos);

        // ---------------------------------------------------
        // 1. 索敵範囲外
        // ---------------------------------------------------
        if (dist > detectRange)
        {
            isAlert = false;
            searchingPlayer = false;
            NPC_Walk();
            return;
        }

        // ---------------------------------------------------
        // 2. 索敵範囲内に入った → 捜索開始
        // ---------------------------------------------------
        isAlert = true;

        // まだプレイヤーを捕捉していない場合
        if (player == null)
        {
            SearchAround();   // 見回し・警戒行動
            return;
        }

        // ---------------------------------------------------
        // 3. プレイヤー発見
        // ---------------------------------------------------
        player = p;
        lastKnownPlayerPos = pPos;
        searchingPlayer = true;

        float combatDist = detectRange * 0.5f;

        if (dist <= combatDist)
        {
            CombatAI(pPos, dist);
        }
        else
        {
            ApproachPlayer(pPos);
        }
    }

    void SearchAround()
    {
        // その場で周囲を警戒する挙動
        speed = Vector2.zero;

        // ランダムに向きを変える（見回し）
        if (Random.value < 0.02f)
        {
            facingLeft = !facingLeft;
        }

        // 視界内に入ったら捕捉
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;

        float dist = Vector2.Distance(transform.position, p.transform.position);
        if (dist <= detectRange)
        {
            player = p;
            lastKnownPlayerPos = p.transform.position;
        }
    }

    // ===================================================
    //  NPC歩行AI（自然な動き）
    // ===================================================
    void NPC_Walk()
    {
        if (Random.value < 0.005f)
        {
            float dir = Random.value < 0.5f ? -1f : 1f;
            facingLeft = (dir < 0);
            speed = new Vector2(dir * 1.2f, rb.linearVelocity.y);
        }
    }

    // ===================================================
    //  プレイヤーへ近づく（自然）
    // ===================================================
    void ApproachPlayer(Vector2 pPos)
    {
        float dir = (pPos.x < transform.position.x) ? -1f : 1f;
        facingLeft = (dir < 0);

        speed = new Vector2(dir * 2.0f, rb.linearVelocity.y);
    }

    // ===================================================
    //  プロAI：間合い管理・攻撃・回避・フェイント
    // ===================================================
    void CombatAI(Vector2 pPos, float dist)
    {
        float dir = (pPos.x < transform.position.x) ? -1f : 1f;
        facingLeft = (dir < 0);

        float ideal = detectRange * 0.4f;

        // ---------------------------------------------------
        // A：間合い管理（プロ AI）
        // ---------------------------------------------------
        if (dist < ideal * 0.7f)
        {
            // 恐怖で後退
            speed = new Vector2(-dir * 2f, rb.linearVelocity.y);
        }
        else if (dist > ideal * 1.2f)
        {
            // 詰める
            speed = new Vector2(dir * 2.5f, rb.linearVelocity.y);
        }
        else
        {
            // 射撃姿勢
            speed = Vector2.zero;

            // フェイント射撃（時々）
            if (Random.value < 0.02f)
                RangeAttackAction();
        }

        // ---------------------------------------------------
        // B：弾避け（プレイヤー弾が近い）
        // ---------------------------------------------------
        if (CheckIncomingBullet())
            Dodge(dir);
    }

    // ===================================================
    //  プレイヤー弾の接近チェック
    // ===================================================
    bool CheckIncomingBullet()
    {
        GameObject[] bullets = GameObject.FindGameObjectsWithTag("PlayerBullet");

        foreach (GameObject b in bullets)
        {
            if (Vector2.Distance(transform.position, b.transform.position) < 3f)
                return true;
        }
        return false;
    }

    // ===================================================
    //  回避アクション
    // ===================================================
    void Dodge(float dir)
    {
        speed = new Vector2(-dir * 4f, rb.linearVelocity.y);
    }

    // ===================================================
    //  プレイヤーを見失ったときの捜索行動
    // ===================================================
    void SearchPlayer()
    {
        if (!searchingPlayer) return;

        float dist = Vector2.Distance(transform.position, lastKnownPlayerPos);

        if (dist > 0.5f)
        {
            float dir = (lastKnownPlayerPos.x < transform.position.x) ? -1 : 1;
            facingLeft = (dir < 0);
            speed = new Vector2(dir * 1.5f, rb.linearVelocity.y);
        }
        else
        {
            // 捜索フェイズ：その場で索敵
            if (Random.value < 0.01f)
            {
                float dir = Random.value < 0.5f ? -1f : 1f;
                facingLeft = (dir < 0);
            }
            speed = Vector2.zero;
        }
    }
    // ===================================================
    //  Gizmos（索敵範囲・戦闘範囲・視界を可視化）
    // ===================================================
    void OnDrawGizmos()
    {
        // 索敵範囲（緑）
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectRange);

        // 戦闘範囲（赤）
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectRange * 0.5f);

        // 吸血探索範囲（紫）
        Gizmos.color = new Color(1f, 0f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, batSearchRange);

        // 視野角（青）
        float half = viewAngle * 0.5f;
        Vector3 baseDir = facingLeft ? Vector3.left : Vector3.right;

        Vector3 left = Quaternion.Euler(0, 0, half) * baseDir;
        Vector3 right = Quaternion.Euler(0, 0, -half) * baseDir;

        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        Gizmos.DrawRay(transform.position, left * detectRange);
        Gizmos.DrawRay(transform.position, right * detectRange);
    }

    // ===================================================
    //  LateUpdate（FSMの後にAIを動かす）
    // ===================================================
    void LateUpdate()
    {
        ProGamerAI_Update(); // 状況判断AI
        SearchPlayer();      // 捜索AI
    }
}
