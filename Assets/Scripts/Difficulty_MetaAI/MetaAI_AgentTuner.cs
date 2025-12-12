using UnityEngine;
using UnityEngine.AI; // SoldierのNavMeshAgent用

public class MetaAI_AgentTuner : MonoBehaviour
{
    // --- コンポーネント参照 ---
    private Soldier _soldier;
    private Bat _bat;
    private Boid _boid;
    private EnemyAI _mushroom;

    // --- 初期値キャッシュ ---
    // Soldier用
    private float _baseSoldierLife;
    private float _baseSoldierDelayMin;
    private float _baseSoldierDelayMax;
    private float _baseSoldierSpeed;

    // Bat用 (ここが増えました)
    private float _baseBatLife;
    private float _baseBatChaseSpeed;  // 追跡
    private float _baseBatRoamSpeed;   // 徘徊
    private float _baseBatSearchSpeed; // 捜索

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
            _baseSoldierDelayMin = _soldier.attackDelayMin;
            _baseSoldierDelayMax = _soldier.attackDelayMax;

            var agent = _soldier.GetComponent<NavMeshAgent>();
            if (agent != null) _baseSoldierSpeed = agent.speed;
        }

        // --- Bat (3つのスピードを保存) ---
        if (_bat != null)
        {
            _baseBatLife = _bat.life;
            _baseBatChaseSpeed = _bat.chaseSpeed;      // 追跡用
            _baseBatRoamSpeed = _bat.roamSpeed;       // 徘徊用
            _baseBatSearchSpeed = _bat.searchMoveSpeed; // 捜索用
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

        // --- Soldier ---
        if (_soldier != null)
        {
            float newMaxLife = _baseSoldierLife * param.hpMultiplier;
            _soldier.maxLife = newMaxLife;
            _soldier.life = newMaxLife;
            _soldier.attackDelayMin = _baseSoldierDelayMin * param.attackWaitMultiplier;
            _soldier.attackDelayMax = _baseSoldierDelayMax * param.attackWaitMultiplier;
            _soldier.feintChance = param.feintChance;

            var agent = _soldier.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = _baseSoldierSpeed * param.speedMultiplier;
            }
        }

        // --- Bat ---
        if (_bat != null)
        {
            _bat.life = _baseBatLife * param.hpMultiplier;

            // 1. 追いかける速度
            _bat.chaseSpeed = _baseBatChaseSpeed * param.speedMultiplier;
            // 2. うろうろする速度
            _bat.roamSpeed = _baseBatRoamSpeed * param.speedMultiplier;
            // 3. 探す速度
            _bat.searchMoveSpeed = _baseBatSearchSpeed * param.speedMultiplier;

            _bat.detectionRadius = param.detectionRadius;

            // Debug.Log($"[MetaAI] Bat Speed Update: Chase={_bat.chaseSpeed}, Roam={_bat.roamSpeed}");
        }

        // --- Boid ---
        if (_boid != null)
        {
            _boid.speedFactor = _baseBoidSpeed * param.speedMultiplier;
            _boid.reactionLerp = _baseBoidLerp * param.speedMultiplier;
        }

        // --- Mushroom ---
        if (_mushroom != null)
        {
            _mushroom.life = _baseMushroomLife * param.hpMultiplier;
            _mushroom.moveSpeed = _baseMushroomSpeed * param.speedMultiplier;
            _mushroom.attackCooldownTime = _baseMushroomCooldown * param.attackWaitMultiplier;
        }
    }
}