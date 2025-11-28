using Unity.VisualScripting;
using UnityEngine;

/*	行動を選択する例
*/
public class Selector_IsAlive : TreeNode_Base
{
    Transform player;

    public Selector_IsAlive(Bat b) : base(b)
    {
        // 子ノード登録（Wait / Move / Dead）
        childrenNodes.Add(new Leaf_Wait(b));
        childrenNodes.Add(new Leaf_Move(b));
        childrenNodes.Add(new Sequence_Dead(b));

        // Player 参照を確保
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
        }
    }

    // OnEnter() はオーバーライド不要

    protected override BT_Status OnUpdate()
    {
        // 死亡チェック
        if (bat.life <= 0f)
        {
            // Sequence_Dead
            return ExecuteChild(2);
        }
        else
        {
            float distanceX = float.MaxValue;

            if (player != null)
            {
                distanceX = Mathf.Abs(player.position.x - bat.transform.position.x);
            }

            // プレイヤーが一定距離内にいるとき Move、それ以外は Wait
            if (distanceX < 1f)
            {
                return ExecuteChild(1); // Move
            }
            else
            {
                return ExecuteChild(0); // Wait
            }
        }
    }

}
