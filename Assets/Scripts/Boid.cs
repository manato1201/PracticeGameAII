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


    // -------------------------
    // 個体差用パラメータ
    // -------------------------
    public float cohesionFactor   = 1f; // 群れにまとまろうとする度合い
    public float separationFactor = 1f; // 仲間と距離を取ろうとする度合い
    public float alignmentFactor  = 1f; // 向きを合わせようとする度合い
    public float leaderFactor     = 1f; // リーダー／退却方向への追従
    public float protectFactor    = 1f; // シールド行動のやる気
    public float speedFactor      = 1f; // 基本スピード倍率
    public float randomnessFactor = 1f; // ランダムさの強さ
    public float reactionLerp     = 5f; // 反応速度（大きいほど素早く向き・速度が変わる）

    public float skill = 0.5f;          // 0〜1: 優秀さ

    public BoidRole role = BoidRole.Normal;
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
public enum BoidRole
{
    Normal,
    Elite,    // 優秀
    Clumsy    // ポンコツ
}
