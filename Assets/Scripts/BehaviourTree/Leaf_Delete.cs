using UnityEngine;

public class Leaf_Delete : TreeNode_Base
{
    public Leaf_Delete(Bat b) : base(b)
    {
        // 子ノードなし
    }

    protected override BT_Status OnUpdate()
    {
        if (bat != null && bat.gameObject != null)
        {
            Object.Destroy(bat.gameObject);
        }
        return BT_Status.SUCCESS;
    }

}
