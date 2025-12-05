using System.Collections.Generic;
using UnityEngine;

//======================================================================
// 個々の群れの個体を表すクラス
//======================================================================
public class Boid : MonoBehaviour
{
    [HideInInspector] public Vector2 velocity; // 現在の速度

    // 突撃役かどうか（true になってから chargeDelay 経過で突撃開始）
    public bool  isCharger        = false;
    public float chargeDelay      = 3f;     // 突撃までの待ち時間
    public float maxChargeDuration = 12f;    // 突撃を続ける最大時間
    public float chargeTimer      = 0f;     // 指名されてからの経過時間

    // 群れに戻す
    public void ResetCharge()
    {
        isCharger  = false;
        chargeTimer = 0f;
    }

    // プレイヤーに接触したら突撃終了 → 群れに復帰
    private void OnTriggerEnter2D(Collider2D other)
    {
        // プレイヤー側のオブジェクトに "Player" タグを付けておくこと
        if (other.CompareTag("Player"))
        {
            ResetCharge();
        }
    }
}
