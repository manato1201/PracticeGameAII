using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attack : MonoBehaviour
{
	public float dmgValue = 1;
	public GameObject throwableObject;
	public Transform attackCheck;
	private Rigidbody2D m_Rigidbody2D;
	public Animator animator;
	public bool canAttack = true;
	public bool isTimeToCheck = false;

    // 【追加】プレイヤーの移動スクリプトへの参照
    public CharacterController2D playerMovement;
    
    [Header("Skill 2 Settings")]
    public float throwSpeed = 15f;
    public float skill2DmgValue = 3f;

    [Header("Skill 3 Settings")]
    public float speedMultiplier = 1.3f;
    public float jumpMultiplier = 1.7f;
    public float buffDuration = 8f;
    private bool isBuffActive = false; // バフ状態を追跡するフラグ
    private float originalRunSpeed; // 元の速度保存用
    private float originalJumpForce; // 元のジャンプ力保存用

    public GameObject skill1EffectPrefab;
    public GameObject cam;

	private void Awake()
	{
		m_Rigidbody2D = GetComponent<Rigidbody2D>();
        // 同じGameObjectにあるCharacterController2Dを取得
        playerMovement = GetComponent<CharacterController2D>(); 
	}

	void Start()
	{

	}

	void Update()
	{
		if (Input.GetButtonDown("Attack") && canAttack)
		{
			canAttack = false;
			animator.SetBool("IsAttacking", true);
			StartCoroutine(AttackCooldown(0.25f, "IsAttacking")); // 修正
		}

		if (Input.GetButtonDown("Skill1") && canAttack)
		{
			canAttack = false;
			animator.SetBool("Skill1", true);
			StartCoroutine(AttackCooldown(0.5f, "Skill1")); // 修正
			Skill1Logic(); 
        }

		if (Input.GetButtonDown("Skill2") && canAttack)
		{
			canAttack = false;
			animator.SetBool("Skill2", true);
			StartCoroutine(AttackCooldown(0.7f, "Skill2")); // 修正
			Skill2Logic();
		}

		if (Input.GetButtonDown("Skill3") && canAttack)
		{
			canAttack = false;
			animator.SetBool("Skill3", true);
			StartCoroutine(AttackCooldown(1.0f, "Skill3")); // 修正
			Skill3Logic();
		}
	}

    // 【修正】引数を受け取り、アニメーションBoolをリセットするように変更
	IEnumerator AttackCooldown(float duration, string animBoolName)
	{
		yield return new WaitForSeconds(duration);
		animator.SetBool(animBoolName, false);
		canAttack = true;
	}

	public void DoDashDamage()
	{
		dmgValue = Mathf.Abs(dmgValue);
		Collider2D[] collidersEnemies = Physics2D.OverlapCircleAll(attackCheck.position, 0.9f);
		for (int i = 0; i < collidersEnemies.Length; i++)
		{
			if (collidersEnemies[i].gameObject.tag == "Enemy")
			{
				if (collidersEnemies[i].transform.position.x - transform.position.x < 0)
				{
					dmgValue = -dmgValue;
				}
				collidersEnemies[i].gameObject.SendMessage("ApplyDamage", dmgValue);
				cam.GetComponent<CameraFollow>().ShakeCamera();
			}
		}
	}

	public void Skill1Logic()
	{
		Debug.Log("sukiru1aaaaaaaaaaaa");
		float currentDmg = Mathf.Abs(dmgValue);

		Collider2D[] collidersEnemies = Physics2D.OverlapCircleAll(attackCheck.position, 3.9f);

		for (int i = 0; i < collidersEnemies.Length; i++)
		{
			if (collidersEnemies[i].gameObject.tag == "Enemy")
			{
				collidersEnemies[i].gameObject.SendMessage("ApplyDamage", currentDmg);

				cam.GetComponent<CameraFollow>().ShakeCamera();
			}
		}
        SpawnSkillEffect(skill1EffectPrefab, transform.position);
    }

    public void Skill2Logic()
    {
        if (throwableObject == null)
        {
            Debug.LogWarning("throwableObject が設定されていません。Skill 2 を実行できません。");
            return;
        }

        Vector3 spawnPosition = attackCheck.position;
        GameObject projectile = Instantiate(throwableObject, spawnPosition, Quaternion.identity);
        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
        
        if (rb != null)
        {
            float direction = transform.localScale.x > 0 ? 1f : -1f;
            rb.linearVelocity = new Vector2(direction * throwSpeed, 0f);

            // 弾のスクリプトにダメージ値を設定 (Projectile.csがアタッチされている前提)
            Projectile projectileScript = projectile.GetComponent<Projectile>();
            if (projectileScript != null)
            {
                projectileScript.damage = skill2DmgValue;
            }
        }
        else
        {
            Debug.LogError("throwableObject に Rigidbody2D がアタッチされていません。");
        }
    }

	public void Skill3Logic()
	{
        if (isBuffActive)
        {
            Debug.Log("加速バフはすでにアクティブです。");
            return;
        }
        
        if (playerMovement == null)
        {
            Debug.LogError("CharacterController2D が見つかりません。スキル3を実行できません。");
            return;
        }

        StartCoroutine(SpeedJumpBoostBuff(buffDuration));
	}

    IEnumerator SpeedJumpBoostBuff(float duration)
    {
        isBuffActive = true;
        
        // 1. 元の値を保存
        originalRunSpeed = playerMovement.m_RunSpeed;
        originalJumpForce = playerMovement.m_JumpForce;

        // 2. バフを適用
        playerMovement.ApplyBuff(speedMultiplier, jumpMultiplier);

        Debug.Log("加速バフ: 移動速度" + speedMultiplier + "倍、ジャンプ力" + jumpMultiplier + "倍を適用。持続時間: " + duration + "秒");
        
        // 3. 指定された時間待機
        yield return new WaitForSeconds(duration);

        // 4. 元の値に戻す
        playerMovement.RemoveBuff(originalRunSpeed, originalJumpForce);
        
        isBuffActive = false;
        Debug.Log("加速バフが終了し、元の能力値に戻りました。");
    }

    private void SpawnSkillEffect(GameObject effectPrefab, Vector3 position)
    {
        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);
            Destroy(effect, 1.0f); 
        }
    }
}