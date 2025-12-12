using UnityEngine;
using System; // ← これが必要です！

public class MetaAI_Director : MonoBehaviour
{
    public static MetaAI_Director Instance;

    // 重要: 難易度が変わったことを敵に知らせる「号令」機能
    public event Action<DifficultyData.Param> OnDifficultyChanged;

    [Header("Settings")]
    public DifficultyData data;
    public GameObject player;

    [Header("Status (Read Only)")]
    public int currentRank = 0;      // 初期値はEasy
    public float stressValue = 0f;   // 時間経過カウンター

    void Awake()
    {
        Instance = this;
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
    }

    void Start()
    {
        ApplyRank();
    }

    void Update()
    {
        if (player == null) return;
        stressValue += Time.deltaTime;

        // --- ランク判定ロジック ---
        int nextRank = 0;

        // 20秒以上でHard, 7.5秒以上でNormal
        if (stressValue > 20f) nextRank = 2;
        else if (stressValue > 7.5f) nextRank = 1;
        if (data != null && data.difficultyLevels != null)
        {
            nextRank = Mathf.Clamp(nextRank, 0, data.difficultyLevels.Length - 1);
        }

        // ランクが変わった瞬間だけ「号令」を出す
        if (nextRank != currentRank)
        {
            currentRank = nextRank;
            ApplyRank(); // 変更通知を実行
        }
    }

    // 変更を適用して通知する関数
    void ApplyRank()
    {
        var currentParam = GetCurrentParams();
        if (OnDifficultyChanged != null)
        {
            OnDifficultyChanged.Invoke(currentParam);
        }

        Debug.Log($"[MetaAI] 難易度が変更されました: Rank {currentRank} ({currentParam.label})");
    }

    public DifficultyData.Param GetCurrentParams()
    {
        if (data == null || data.difficultyLevels.Length == 0)
            return new DifficultyData.Param { hpMultiplier = 1, speedMultiplier = 1 };

        return data.difficultyLevels[currentRank];
    }
}