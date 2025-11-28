using UnityEngine;

/* 子ノードを順に実行する Sequence ノードの例
*/
public class Sequence_Dead : TreeNode_Base
{
    private int runningNode = 0;

    public Sequence_Dead(Bat b): base(b)
    {
        childrenNodes.Add(new Leaf_DeadPose(b));
        childrenNodes.Add(new Leaf_Delete(b));
		runningNode = 0;
    }
	
	protected override void OnEnter()
	{
		base.OnEnter();
		runningNode = 0;
	}

    protected override BT_Status OnUpdate()
    {
        BT_Status status = ExecuteChild(runningNode);
        if (status == BT_Status.SUCCESS && runningNode < childrenNodes.Count-1){
			runningNode += 1;
			status = BT_Status.RUNNING;	// まだ続いている
        }

        return status;
    }

}