using UnityEngine;

public class Leaf_DeadPose : TreeNode_Base
{
	float timer;
	public Leaf_DeadPose(Bat b)  : base(b)
	{
		// 子ノードなし
	}
	
	protected override void OnEnter()
	{
		base.OnEnter();
		bat.speed.x = 0.0f;
		bat.speed.y = 0.0f;
		bat.DeadAction();
		timer=5f;
	}

	protected override BT_Status OnUpdate()
	{
		bat.speed.y -= 0.2f;
		timer -= Time.deltaTime;
		if(timer < 0){
			return BT_Status.SUCCESS;
		}else{
			return BT_Status.RUNNING;
		}
	}

}
