using Unity.VisualScripting;
using UnityEngine;

/*	行動を選択する例
*/
public class Selector_IsAlive : TreeNode_Base
{
	public Selector_IsAlive(Bat b) : base(b)
	{
		childrenNodes.Add(new Leaf_Wait(b));
		childrenNodes.Add(new Leaf_Move(b));
		childrenNodes.Add(new Sequence_Dead(b));
	}
	
	// OnEnter() はオーバーライド不要
	
	protected override BT_Status OnUpdate()
	{
		if(bat.life < 0)
		{
			return ExecuteChild(2);
			// Sequence_Dead
		}else{
			float distance = bat.tool.DistanceXToPlayer();
			if (distance < 1f)
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
