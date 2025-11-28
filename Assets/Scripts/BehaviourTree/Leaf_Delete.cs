using UnityEngine;

public class Leaf_Delete : TreeNode_Base
{
	float timer;
	public Leaf_Delete(Bat b)  : base(b)
	{
		// 子ノードなし
	}
	
	protected override BT_Status OnUpdate()
	{
		bat.Delete();
		return BT_Status.SUCCESS;
	}

}
