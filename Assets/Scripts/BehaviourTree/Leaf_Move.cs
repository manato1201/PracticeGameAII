using UnityEngine;

public class Leaf_Move : TreeNode_Base
{
	public Leaf_Move(Bat b)  : base(b)
	{
		// 子ノードなし
	}
	
	protected override void OnEnter()
	{
		base.OnEnter();
		bat.speed.y = 0.0f;
		bat.speed.y = 0.0f;
		bat.facingLeft = bat.tool.IsPlayerLeftside();
	}

	protected override BT_Status OnUpdate()
	{
		bat.speed.y = 2f*Mathf.Sin(Time.time*2.5f);
		return BT_Status.RUNNING;
	}

}
