using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bat : MonoBehaviour
{
	public float life = 10;
	
	bool isInvincible = false;
	bool isHitted = false;
	private Coroutine hitCoroutine;
	// 自身のanimatorの保持
	Animator animator;
	
	// ------------------------------------------------------
	// #1 キャラクターの動き（身体）
	// 向き： true の時に左向き、false の時に右向き
	public bool facingLeft = true;
	// 移動速度
	public Vector2 speed = Vector2.zero;	// x:右が+。左が-  y:上が+、下が	// 向き： true の時に左向き、false の時に右向き
	
	// プレイヤー GameObject の保持
	GameObject player;
	// 出現位置
	Vector3 startPosition;
	
	// Start is called before the first frame update
	void Start()
	{
		animator = GetComponent<Animator>();
		player = GameObject.Find("Player");
		startPosition = transform.position;
	}
	
	void OnDestroy()
	{
		if(hitCoroutine != null)
		{
			StopCoroutine(hitCoroutine);
			hitCoroutine = null;
		}
	}
	
	// Update is called once per frame
	void Update()
	{
		// 死亡チェック
		if (life <= 0) {
			animator.SetBool("IsDead", true);
			StartCoroutine(DestroyEnemy());
			// 死体GameObject を出してすぐに消えるという手もある
			return;
		}
		
		// ヒットストップ
		if(isHitted){
			return;
		}
		
		// ===================================================
		// プレイヤーに体当たりするAIを作りましょう
		
		// 例：プレイヤーの方を向く
		Vector3 player_position = player.transform.position;
		if(player_position.x > transform.position.x){
			facingLeft = false;
		}else{
			facingLeft = true;
		}
		
		speed.x = 0.0f;    // 右が+、左が-になります
		speed.y = 0.0f;    // 上が+、下が-になります
		
		// ===================================================
		
		Movement();
	}
	
	// 自キャラの移動処理をまとめています
	void Movement(){
		// 向き処理
		GetComponent<SpriteRenderer>().flipX = facingLeft;
		// 移動処理
		Vector3 pos = transform.position;
		pos.x += speed.x * Time.deltaTime;
		pos.y += speed.y * Time.deltaTime;
		// 簡易地形アタリ判定
		if(pos.y < 0.8f){
			pos.y = 0.8f;
		}
		transform.position = pos;
	}
	
	// ===============================================================
	// 以下、このシステムの処理
	
	// ダメージを受ける：プレイヤー側がコールする仕組みになっています
	public void ApplyDamage(float damage) {
		if (!isInvincible) 
		{
			// 攻撃を受けた方向が取れる仕組みになっています
			float direction = damage / Mathf.Abs(damage);
			damage = Mathf.Abs(damage);
			life -= damage;
			if(hitCoroutine != null)
			{
				StopCoroutine(hitCoroutine);
			}
			hitCoroutine = StartCoroutine(HitTime());
		}
	}
	
	// 無敵時間の設定 : WaitForSecondsで設定している間、isHittedとisInvinsibleをtrueにする
	IEnumerator HitTime()
	{
		isHitted = true;
		isInvincible = true;
		yield return new WaitForSeconds(0.5f);
		isHitted = false;
		isInvincible = false;
		hitCoroutine = null;
	}

	// プレイヤーとの接触時の処理
	void OnTriggerEnter2D(Collider2D collider)
	{
		// プレイヤーに体当たり攻撃
		if (collider.gameObject.tag == "Player" && life > 0)
		{
			collider.gameObject.GetComponent<CharacterController2D>().ApplyDamage(2f, transform.position);
		}
	}
	
	// 死亡処理
	IEnumerator DestroyEnemy()
	{
		yield return new WaitForSeconds(5f);
		Destroy(gameObject);
	}
	
	
}
