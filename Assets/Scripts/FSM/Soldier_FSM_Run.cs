using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier_FSM_Run : Soldier_FSM_Base
{
    // 共通で使う参照
    Transform player;

    // 視界索敵用
    float lastSeenTime = -999f;
    Vector2 lastSeenPosition;

    // A* 用
    List<Vector2> currentPath = new List<Vector2>();
    int pathIndex;
    float repathInterval = 1.0f;
    float repathTimer;

    // ジャンプ回避用
    bool didDodge;
    float dodgeTimer;

    public Soldier_FSM_Run(Soldier s) : base(s)
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    public override void OnEnter()
    {
        soldier.RunAction();
        // 毎回入り直したときに軽く初期化しておく
        repathTimer = 0f;
        pathIndex = 0;
        didDodge = false;
        dodgeTimer = 0f;
    }

    public override void OnUpdate()
    {
        if (player == null)
        {
            soldier.speed = Vector2.zero;
            return;
        }

        switch (soldier.moveType)
        {
            case Soldier.MoveType.Simple:
                UpdateSimple();
                break;

            case Soldier.MoveType.BatAbsorb:
                UpdateBatAbsorb();
                break;

            case Soldier.MoveType.VisionSeek:
                UpdateVisionSeek();
                break;

            case Soldier.MoveType.AStarPath:
                UpdateAStarPath();
                break;

            case Soldier.MoveType.JumpDodge:
                UpdateJumpDodge();
                break;
        }
    }

    public override void OnExit()
    {
        // 特になし
    }

    public override Soldier.State CheckTransitions()
    {
        if (soldier.isHitted)
        {
            return Soldier.State.DAMAGE;
        }

        if (player != null)
        {
            float d = Vector2.Distance(soldier.position, player.position);
            if (d < 2f)
                return Soldier.State.MELEE_ATTACK;
            if (d < 8f)
                return Soldier.State.RANGE_ATTACK;
        }

        return Soldier.State.RUN;
    }

    // =========================================================
    // 1) Simple：旧 Run と同じ「とりあえずプレイヤー追うだけ」
    // =========================================================
    void UpdateSimple()
    {
        soldier.facingLeft = soldier.tool.IsPlayerLeftside();
        soldier.speed.x = soldier.facingLeft ? -2.5f : 2.5f;
        // Y は重力任せ
    }

    // =========================================================
    // 2) BatAbsorb：HP 低下時にコウモリ瞬間移動＋回復
    // =========================================================
    void UpdateBatAbsorb()
    {
        float hp = soldier.life;
        float low = soldier.lowHPThreshold <= 0f ? 5f : soldier.lowHPThreshold;

        if (hp < low)
        {
            // バット探索と吸収
            if (TryAbsorbBat())
            {
                soldier.speed = Vector2.zero;
                return;
            }
            else
            {
                // コウモリいない → プレイヤーから離れる
                MoveAwayFromPlayer(2.0f);
                return;
            }
        }

        // 通常時はプレイヤー追尾
        MoveToPlayer(2.5f);
    }

    bool TryAbsorbBat()
    {
        GameObject[] bats = GameObject.FindGameObjectsWithTag("Bat");
        if (bats == null || bats.Length == 0) return false;

        Vector2 myPos = soldier.position;
        float range = (soldier.batSearchRange > 0f) ? soldier.batSearchRange : 12f;

        GameObject best = null;
        float bestDist = float.MaxValue;

        foreach (var b in bats)
        {
            float d = Vector2.Distance(myPos, b.transform.position);
            if (d < bestDist && d <= range)
            {
                bestDist = d;
                best = b;
            }
        }

        if (best == null) return false;

        soldier.transform.position = best.transform.position;
        Object.Destroy(best);

        // HP回復（最大 10 とする）
        soldier.life = Mathf.Min(soldier.life + 3f, 10f);

        return true;
    }

    // =========================================================
    // 3) VisionSeek：視界＋ラストシーン記憶で追跡
    // =========================================================
    void UpdateVisionSeek()
    {
        Vector2 myPos = soldier.position;
        Vector2 toPlayer = (Vector2)player.position - myPos;
        float dist = toPlayer.magnitude;

        float detect = (soldier.detectRange > 0f) ? soldier.detectRange : 6f;
        float view = (soldier.viewAngle > 0f) ? soldier.viewAngle : 120f;

        Vector2 lookDir = soldier.facingLeft ? Vector2.left : Vector2.right;
        float angle = Vector2.Angle(lookDir, toPlayer.normalized);
        bool inView = angle < view * 0.5f && dist <= detect;

        if (inView)
        {
            lastSeenTime = Time.time;
            lastSeenPosition = player.position;
        }

        float searchDuration = (soldier.timer > 0f) ? soldier.timer : 5f; // timer 未使用なら 5秒扱いでもいい

        Vector2 targetPos;
        if (inView)
        {
            targetPos = player.position;
        }
        else if (Time.time - lastSeenTime < searchDuration)
        {
            targetPos = lastSeenPosition;
        }
        else
        {
            // 追跡対象なし
            soldier.speed = Vector2.zero;
            return;
        }

        Vector2 dir = (targetPos - myPos).normalized;
        float moveSpeed = 2.5f;
        soldier.speed = dir * moveSpeed;
        soldier.facingLeft = dir.x < 0f;
    }

    // =========================================================
    // 4) AStarPath：A* で計算した経路を辿る
    // =========================================================
    void UpdateAStarPath()
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            RebuildPath();
        }

        if (currentPath == null || currentPath.Count == 0 || pathIndex >= currentPath.Count)
        {
            soldier.speed = Vector2.zero;
            return;
        }

        Vector2 myPos = soldier.position;
        Vector2 target = currentPath[pathIndex];
        Vector2 toTarget = target - myPos;

        float gridSize = soldier.gridSize > 0f ? soldier.gridSize : 0.7f;

        if (toTarget.magnitude < gridSize * 0.3f)
        {
            pathIndex++;
            if (pathIndex >= currentPath.Count)
            {
                soldier.speed = Vector2.zero;
                return;
            }
            target = currentPath[pathIndex];
            toTarget = target - myPos;
        }

        Vector2 dir = toTarget.normalized;
        float moveSpeed = 2.5f;
        soldier.speed = dir * moveSpeed;
        soldier.facingLeft = dir.x < 0f;
    }

    void RebuildPath()
    {
        repathTimer = repathInterval;

        if (player == null)
        {
            currentPath.Clear();
            return;
        }

        Vector2 start = soldier.position;
        Vector2 goal  = player.position;
        currentPath = AStar(start, goal);
        pathIndex   = 0;
    }

    class Node
    {
        public Vector2 pos;
        public Node parent;
        public float g, h, f;

        public Node(Vector2 pos, Node parent, float g, float h)
        {
            this.pos    = pos;
            this.parent = parent;
            this.g      = g;
            this.h      = h;
            this.f      = g + h;
        }
    }

    List<Vector2> AStar(Vector2 start, Vector2 goal)
    {
        float gridSize = soldier.gridSize > 0f ? soldier.gridSize : 0.7f;
        Vector2 mapMin = soldier.mapMin;
        Vector2 mapMax = soldier.mapMax;

        // タグが未設定なら "Wall" を使う
        string obstacleTag = string.IsNullOrEmpty(soldier.obstacleTag)
            ? "Wall"
            : soldier.obstacleTag;

        var open   = new List<Node>();
        var closed = new HashSet<Vector2>();

        open.Add(new Node(start, null, 0f, Vector2.Distance(start, goal)));

        while (open.Count > 0)
        {
            open.Sort((a, b) => a.f.CompareTo(b.f));
            Node cur = open[0];
            open.RemoveAt(0);

            if (Vector2.Distance(cur.pos, goal) < gridSize)
                return BuildPath(cur);

            closed.Add(cur.pos);

            foreach (var next in Neighbors(cur.pos, gridSize, mapMin, mapMax, obstacleTag))
            {
                if (closed.Contains(next)) continue;

                float ng = cur.g + Vector2.Distance(cur.pos, next);
                float nh = Vector2.Distance(next, goal);
                Node nn  = new Node(next, cur, ng, nh);

                bool skip = false;
                foreach (var o in open)
                {
                    if (o.pos == next && o.f <= nn.f)
                    {
                        skip = true;
                        break;
                    }
                }
                if (!skip) open.Add(nn);
            }
        }

        return new List<Vector2>();
    }

    IEnumerable<Vector2> Neighbors(
        Vector2 pos,
        float gridSize,
        Vector2 mapMin,
        Vector2 mapMax,
        string obstacleTag)
    {
        Vector2[] dirs = {
            new Vector2( 1,  0), new Vector2(-1,  0),
            new Vector2( 0,  1), new Vector2( 0, -1),
            new Vector2( 1,  1), new Vector2(-1,  1),
            new Vector2( 1, -1), new Vector2(-1, -1)
        };

        foreach (Vector2 d in dirs)
        {
            Vector2 p = pos + d * gridSize;

            // マップ外は除外
            if (p.x < mapMin.x || p.x > mapMax.x || p.y < mapMin.y || p.y > mapMax.y)
                continue;

            // OverlapBoxAll でヒットしたコライダの中に obstacleTag がいるかどうか
            var hits = Physics2D.OverlapBoxAll(
                p,
                Vector2.one * gridSize * 0.8f,
                0f
            );

            bool blocked = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].CompareTag(obstacleTag))
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
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

    // =========================================================
    // 5) JumpDodge：近距離でジャンプ回避、その後追尾
    // =========================================================
    void UpdateJumpDodge()
    {
        float dist = Vector2.Distance(soldier.position, player.position);
        float dodgeDist = soldier.dodgeDistance > 0f ? soldier.dodgeDistance : 2f;

        // まだジャンプしていなくて、プレイヤーが近い → 回避
        if (!didDodge && dist < dodgeDist)
        {
            DoJumpDodge();
            didDodge = true;
            dodgeTimer = (soldier.dodgeCooldown > 0f) ? soldier.dodgeCooldown : 0.6f;
            return;
        }

        // ジャンプ直後のクールタイム中は速度は物理任せ
        if (didDodge && dodgeTimer > 0f)
        {
            dodgeTimer -= Time.deltaTime;
            return;
        }

        // それ以外は普通に追尾
        MoveToPlayer(2.5f);
    }

    void DoJumpDodge()
    {
        Rigidbody2D rb = soldier.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        rb.AddForce(new Vector2(0f, 280f)); // 上方向

        float dir = (Random.value < 0.5f) ? -1f : 1f;
        rb.AddForce(new Vector2(dir * 180f, 0f));

        soldier.speed = new Vector2(dir * 2f, rb.linearVelocity.y);
        soldier.facingLeft = dir < 0f;
    }

    // =========================================================
    // 共通ユーティリティ
    // =========================================================
    void MoveToPlayer(float moveSpeed)
    {
        Vector2 dir = ((Vector2)player.position - soldier.position).normalized;
        soldier.speed = dir * moveSpeed;
        soldier.facingLeft = dir.x < 0f;
    }

    void MoveAwayFromPlayer(float moveSpeed)
    {
        Vector2 dir = (soldier.position - (Vector2)player.position).normalized;
        soldier.speed = dir * moveSpeed;
        soldier.facingLeft = dir.x < 0f;
    }
}
