using UnityEngine;

public class Leaf_Wait : TreeNode_Base
{
	public Leaf_Wait(Bat b)  : base(b)
	{
		// 子ノードなし
	}
	
	protected override void OnEnter()
	{
		base.OnEnter();
		bat.speed.x = 0.0f;
		bat.speed.y = 0.0f;		
	}

	protected override BT_Status OnUpdate()
	{
		return BT_Status.RUNNING;
	}

}
