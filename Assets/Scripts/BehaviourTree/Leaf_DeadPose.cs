using UnityEngine;

public class Leaf_DeadPose : TreeNode_Base
{
    float timer;
    Animator animator;

    public Leaf_DeadPose(Bat b) : base(b)
    {
        // 子ノードなし
    }

    protected override void OnEnter()
    {
        base.OnEnter();

        // 移動停止
        bat.speed = Vector2.zero;

        // Animator を直接操作（DeadAction 不要）
        animator = bat.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsDead", true);
        }

        // 一定時間経過後に次のノードへ
        timer = 5f;
    }

    protected override BT_Status OnUpdate()
    {
        // ちょっとずつ落下させたい場合は y に重力を足す
        bat.speed.y -= 0.2f;

        timer -= Time.deltaTime;
        if (timer < 0f)
        {
            return BT_Status.SUCCESS;
        }
        else
        {
            return BT_Status.RUNNING;
        }
    }

}
