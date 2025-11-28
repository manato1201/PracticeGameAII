using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bat : MonoBehaviour
{
	public float life = 10;
	public int set = 0;
	public bool useBehaviorTree = false;
	
	bool isInvincible = false;
	bool isHitted = false;
	
	// 自身のanimatorの保持
	Animator animator;
	// 便利関数つめあわせ
	public EnemyComp tool;
	private Coroutine hitCoroutine;
	
	// ------------------------------------------------------
	// #1 キャラクターの動き（身体）
	// 向き： true の時に左向き、false の時に右向き
	public bool facingLeft = true;
	// 移動速度
	public Vector2 speed = Vector2.zero;	// x:右が+。左が-  y:上が+、下が-
	// 位置；参照用
	Vector2 position;
	float timer;
	
	// ------------------------------------------------------
	// #2 意思決定
	// ステート
	enum State {
		WAIT,
		DEAD,
	};
	State state = 0;
	int step;
	// ステートを nextStateにする
	void StateChange(State nextState){
		state = nextState;
		step = 0;
	}
	
	// ビヘイビアツリーを使う場合
	private TreeNode_Base rootNode = null;

	// Start is called before the first frame update
	void Start()
	{
		animator = GetComponent<Animator>();
		tool = new EnemyComp(this.gameObject);
		StateChange(State.WAIT);

		if (useBehaviorTree)
		{
			rootNode = new Selector_IsAlive(this);
		}
	}
	
	void OnDestroy()
	{
		if(hitCoroutine != null)
		{
			StopCoroutine(hitCoroutine);
			hitCoroutine = null;
		}
	}
	
	// Updateの最初に行う
	void FirstInUpdate()
	{
		position = transform.position;
	}
	
	// 待機
	void Wait()
	{
		facingLeft = tool.IsPlayerLeftside();
		
		speed.x = 0.0f;    // 右が+、左が-になります
		speed.y = 0.0f;    // 上が+、下が-になります
	}
	
	// やられた
	void Dead()
	{
		switch(step){
			case 0:
				animator.SetBool("IsDead", true);
				speed.x = 0.0f;
				speed.y = 0.0f;
				timer = 5f;
				step++;
				break;
			case 1:
				timer -= Time.deltaTime;
				if(timer < 0){
					step++;
				}
				break;
			case 2:
				Destroy(gameObject);
				break;	
		}
		speed.y -= 0.2f;
	}
	
	public void DeadAction()
	{
		animator.SetBool("IsDead", true);		
	}

	// このキャラクターを削除する
	public void Delete()
	{
		Destroy(gameObject);
	}
	
	// Update is called once per frame
	void Update()
	{
		FirstInUpdate();
		
		
		// ===================================================
		// AIを作りましょう
		// 死亡チェック
		if(!useBehaviorTree){
			GameManager.instance.DispStr("Bat" + set + ":" + state + ":" + new Vector2(position.x, position.y));
			if (life <= 0 && state != State.DEAD) {
				StateChange(State.DEAD);
			}
		
			switch(state){
				case State.WAIT:
					Wait();
					break;
				case State.DEAD:
					Dead();
					break;
			};
		}else{
			GameManager.instance.DispStr("Behavior Tree");
			// ビヘイビアツリーを使う
			rootNode.ExecuteAsRoot();
		}
		// ===================================================
		
		if(!isHitted){
			// AIではなくゲームシステム側でヒットストップを実装
			Movement();
		}
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
	
}
