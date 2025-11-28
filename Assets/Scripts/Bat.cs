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
    SpriteRenderer spriteRenderer;

    // ------------------------------------------------------
    // キャラクターの動き（身体）
    public bool facingLeft = true;
    public Vector2 speed = Vector2.zero;
    
    GameObject player;
    Vector3 startPosition;

    enum BatState
    {
        Idle,   // 待機
        Chase,  // 追跡
        Search  // 捜索
    }
    BatState currentState = BatState.Idle;

    public float detectionRadius = 3.0f; // 索敵範囲
    public float chaseSpeed = 3.0f;      // 追跡時no速度
    
    //  AI調整用の変数
    public float lostRadius = 5.0f;      // 追跡を諦める距離
    public float searchDuration = 3.0f;  // 探す時間
    private float searchTimer = 0f;      // 内部計算用タイマー

    private Vector3 searchCenter; // 円の中心（mi失った場所を指定）
    public float circleRadius = 3.0f; // 円の大きさ（半径）
    public float circleSpeed = 5.0f;  // 回る速さ

    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        player = GameObject.Find("Player");
        startPosition = transform.position;

        transform.localScale = new Vector3(5.0f, 5.0f, 1.0f);
    }
    
    void OnDestroy()
    {
        if(hitCoroutine != null) StopCoroutine(hitCoroutine);
    }
    
    void Update()
    {
        // 死亡チェック
        if (life <= 0) {
            animator.SetBool("IsDead", true);
            StartCoroutine(DestroyEnemy());
            return;
        }
        if(isHitted) return;

        // AIステートマシン処理
        float distance = Vector3.Distance(transform.position, player.transform.position);

        switch (currentState)
        {
            case BatState.Idle:
                //【待機状態】
                speed = Vector2.zero;
                spriteRenderer.color = Color.white;
                if (distance < detectionRadius)
                {
                    currentState = BatState.Chase;
                }
                break;

            case BatState.Chase:
                //【追跡状態】
                spriteRenderer.color = Color.red;
                if (player.transform.position.x > transform.position.x)
                    facingLeft = false;
                else
                    facingLeft = true;

                // 移動処理
                Vector3 direction = (player.transform.position - transform.position).normalized;
                speed.x = direction.x * chaseSpeed;
                speed.y = direction.y * chaseSpeed;

                // プレイヤーが一定距離離れたら「捜索」へ移行
                if (distance > lostRadius)
                {
                    currentState = BatState.Search;
                    searchTimer = searchDuration;
                    searchCenter = transform.position;
                }
                break;

            case BatState.Search:
                //【捜索状態】

                spriteRenderer.color = Color.yellow;
                searchTimer -= Time.deltaTime;
                // 円周上の目標位置を計算
                float angle = Time.time * circleSpeed;
                float x = Mathf.Cos(angle) * circleRadius;
                float y = Mathf.Sin(angle) * circleRadius;

                Vector3 targetPos = searchCenter + new Vector3(x, y, 0);

                // その目標位置に向かって移動させる
                Vector3 moveDir = (targetPos - transform.position);
                speed = moveDir * 5.0f;

                // 移動方向に合わせて体の向きを変える
                if (speed.x > 0.1f) facingLeft = false;
                if (speed.x < -0.1f) facingLeft = true;

                if (distance < detectionRadius) currentState = BatState.Chase;
                if (searchTimer <= 0) currentState = BatState.Idle;
                break;

                // ===================================================
                // プレイヤーに体当たりするAIを作りましょう

                // 例：プレイヤーの方を向く
                Vector3 player_position = player.transform.position;
                if (player_position.x > transform.position.x)
                {
                    facingLeft = false;
                }
                else
                {
                    facingLeft = true;
                }

                speed.x = 0.0f;    // 右が+、左が-になります
                speed.y = 0.0f;    // 上が+、下が-になります

                // ===================================================
        }
        Movement();
    }
    
    void Movement(){
        spriteRenderer.flipX = facingLeft;
        Vector3 pos = transform.position;
        pos.x += speed.x * Time.deltaTime;
        pos.y += speed.y * Time.deltaTime;
        if(pos.y < 0.8f){
            pos.y = 0.8f;
        }
        transform.position = pos;
    }

    // ダメージを受ける：プレイヤー側がコールする仕組みになっています
    public void ApplyDamage(float damage) {
        if (!isInvincible) {
            // 攻撃を受けた方向が取れる仕組みになっています
            float dir = damage / Mathf.Abs(damage);
            damage = Mathf.Abs(damage);
            life -= damage;
            if(hitCoroutine != null) StopCoroutine(hitCoroutine);
            hitCoroutine = StartCoroutine(HitTime());
        }
    }

    // 無敵時間の設定 : WaitForSecondsで設定している間、isHittedとisInvinsibleをtrueにする
    IEnumerator HitTime() {
        isHitted = true;
        isInvincible = true;
        yield return new WaitForSeconds(0.5f);
        isHitted = false;
        isInvincible = false;
        hitCoroutine = null;
    }

    void OnTriggerEnter2D(Collider2D collider) {
        if (collider.gameObject.tag == "Player" && life > 0)
        {
            collider.gameObject.GetComponent<CharacterController2D>().ApplyDamage(2f, transform.position);
        }
    }
    
    // 志望処理
    IEnumerator DestroyEnemy() {
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    // デバッグ用：シーンビューで範囲を可視化
    void OnDrawGizmos()
    {
        // 発見する距離（赤）
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 見失う距離（黄色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lostRadius);

        // 捜索範囲（青）
        if (currentState == BatState.Search)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(searchCenter, circleRadius);
        }
    }
}
