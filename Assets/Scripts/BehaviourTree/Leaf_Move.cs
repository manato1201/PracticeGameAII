using UnityEngine;

public class Leaf_Move : TreeNode_Base
{
    Transform player;

    public Leaf_Move(Bat b) : base(b)
    {
        // 子ノードなし
        // Player はタグで取得（名前ハードコードよりマシ）
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
        }
    }

    protected override void OnEnter()
    {
        base.OnEnter();

        // 速度リセット
        bat.speed = Vector2.zero;

        // プレイヤーの左右で向きを決定
        if (player != null)
        {
            bat.facingLeft = (player.position.x < bat.transform.position.x);
        }
    }

    protected override BT_Status OnUpdate()
    {
        // 上下にふわふわ動く
        bat.speed.y = 2f * Mathf.Sin(Time.time * 2.5f);
        // 横方向はここでは操作しない（必要なら bat.speed.x を追加）

        return BT_Status.RUNNING;
    }

}
