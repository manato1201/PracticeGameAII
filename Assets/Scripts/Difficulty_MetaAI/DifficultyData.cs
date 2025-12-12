using UnityEngine;

// ↓ この1行が「右クリックメニュー」を作る魔法のコードです
[CreateAssetMenu(fileName = "NewDifficultyData", menuName = "MetaAI/DifficultyData")]
public class DifficultyData : ScriptableObject
{
    [System.Serializable]
    public struct Param
    {
        public string label;        // "Easy", "Normal", "Hard" 

        [Header("Global Multipliers")]
        public float hpMultiplier;      // HP倍率
        public float speedMultiplier;   // 移動速度倍率
        public float attackWaitMultiplier; // 攻撃待ち時間倍率

        [Header("Specific AI Settings")]
        [Range(0f, 1f)] public float feintChance;
        public float detectionRadius;
    }

    public Param[] difficultyLevels;
}