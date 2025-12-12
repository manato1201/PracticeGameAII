using UnityEngine;
using UnityEngine.AI;

public class MetaAI_AgentTuner : MonoBehaviour
{
    // --- コンポーネント参照 ---
    private Soldier _soldier;
    private Bat _bat;
    private Boid _boid;
    private EnemyAI _mushroom;

    // --- 初期値キャッシュ ---

    // Soldier用 (HPと索敵範囲のみ)
    private float _baseSoldierLife;
    private float _baseSoldierDetectRange;

    // Bat用
    private float _baseBatLife;
    private float _baseBatChaseSpeed;
    private float _baseBatRoamSpeed;
    private float _baseBatSearchSpeed;

    // Boid用
    private float _baseBoidSpeed;
    private float _baseBoidLerp;

    // Mushroom用
    private float _baseMushroomLife;
    private float _baseMushroomSpeed;
    private float _baseMushroomCooldown;

    // 初期化フラグ
    private bool _isInitialized = false;

    void Awake()
    {
        _soldier = GetComponent<Soldier>();
        _bat = GetComponent<Bat>();
        _boid = GetComponent<Boid>();
        _mushroom = GetComponent<EnemyAI>();
    }

    void Start()
    {
        SaveBaseStats();

        if (MetaAI_Director.Instance != null)
        {
            ApplyBuff(MetaAI_Director.Instance.GetCurrentParams());
            MetaAI_Director.Instance.OnDifficultyChanged += ApplyBuff;
        }
    }

    void OnDestroy()
    {
        if (MetaAI_Director.Instance != null)
        {
            MetaAI_Director.Instance.OnDifficultyChanged -= ApplyBuff;
        }
    }

    // 初期値を保存する
    void SaveBaseStats()
    {
        if (_isInitialized) return;

        // --- Soldier ---
        if (_soldier != null)
        {
            _baseSoldierLife = _soldier.life;
            // Soldier.cs にもともとある detectRange (索敵範囲) は public なので操作可能
            _baseSoldierDetectRange = _soldier.detectRange;

            Debug.Log($"[MetaAI] Soldier Base Stats: Life={_baseSoldierLife}, Range={_baseSoldierDetectRange}");
        }

        // --- Bat ---
        if (_bat != null)
        {
            _baseBatLife = _bat.life;
            _baseBatChaseSpeed = _bat.chaseSpeed;
            _baseBatRoamSpeed = _bat.roamSpeed;
            _baseBatSearchSpeed = _bat.searchMoveSpeed;
        }

        // --- Boid ---
        if (_boid != null)
        {
            _baseBoidSpeed = _boid.speedFactor;
            _baseBoidLerp = _boid.reactionLerp;
        }

        // --- Mushroom ---
        if (_mushroom != null)
        {
            _baseMushroomLife = _mushroom.life;
            _baseMushroomSpeed = _mushroom.moveSpeed;
            _baseMushroomCooldown = _mushroom.attackCooldownTime;
        }

        _isInitialized = true;
    }

    // 強化実行
    private void ApplyBuff(DifficultyData.Param param)
    {
        if (!_isInitialized) SaveBaseStats();

        // --- Soldier の更新 ---
        if (_soldier != null)
        {
            // 1. HPの強化 (これは可能)
            float newMaxLife = _baseSoldierLife * param.hpMultiplier;
            _soldier.maxLife = newMaxLife;
            _soldier.life = newMaxLife;

            // 2. 索敵範囲の強化 (これも可能)
            // detectionRadius というパラメータがあればそれを使う
            _soldier.detectRange = param.detectionRadius > 0 ? param.detectionRadius : _baseSoldierDetectRange;

            // ※注意: Soldier.csには速度や攻撃頻度の変数がないため、ここでは変更できません。
            // 速度を変えたい場合は、Soldier.csの修正が必須となります。
        }

        // --- Bat の更新 ---
        if (_bat != null)
        {
            _bat.life = _baseBatLife * param.hpMultiplier;
            _bat.chaseSpeed = _baseBatChaseSpeed * param.speedMultiplier;
            _bat.roamSpeed = _baseBatRoamSpeed * param.speedMultiplier;
            _bat.searchMoveSpeed = _baseBatSearchSpeed * param.speedMultiplier;
            _bat.detectionRadius = param.detectionRadius;
        }

        // --- Boid の更新 ---
        if (_boid != null)
        {
            _boid.speedFactor = _baseBoidSpeed * param.speedMultiplier;
            _boid.reactionLerp = _baseBoidLerp * param.speedMultiplier;
        }

        // --- Mushroom の更新 ---
        if (_mushroom != null)
        {
            _mushroom.life = _baseMushroomLife * param.hpMultiplier;
            _mushroom.moveSpeed = _baseMushroomSpeed * param.speedMultiplier;
            _mushroom.attackCooldownTime = _baseMushroomCooldown * param.attackWaitMultiplier;
        }
    }
}