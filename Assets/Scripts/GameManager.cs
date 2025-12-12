using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using TMPro;

public class GameManager : MonoBehaviour
{
	public static GameManager instance;
	public BlackBoard blackBoard;

	public GameObject player;

    [Header("監視・召喚設定")]
    public GameObject allyPrefab;   // 味方AIのプレハブをここに入れる
    public Transform spawnPoint;    // 味方の出現位置（指定してないのでプレイヤーの近くに出す）

    // プレイヤーの参照
    private CharacterController2D playerScript;
    private bool allySpawned = false;

    // デバッグ用文字表示 DispStr用
    GameObject debugText;
	
	StringBuilder buffer = new StringBuilder();
	
	void Awake(){
		instance = this;

        // ゲーム開始時に設定したいことがあれば、ここで行いましょう
        instance = this;
        blackBoard = new BlackBoard();
	}
	
	// Start is called before the first frame update
	void Start()
	{
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerScript = playerObj.GetComponent<CharacterController2D>();
        }
    }

	// Update is called once per frame
	void Update()
	{
        // プレイヤーが見つかっていない場合は何もしない
        if (playerScript == null) return;

        // --- ここが実装したいコア機能 ---

        float currentHP = playerScript.life;
        float maxHP = playerScript.maxlife;

        // HPが半分以下になったら
        if (currentHP <= maxHP / 2.0f)
        {
            if (!allySpawned)
            {
                SpawnAlly();
            }
        }
    }

    void SpawnAlly()
    {
        allySpawned = true;

        if (allyPrefab != null)
        {
            // 3体出す
            for (int i = 0; i < 3; i++)
            {
                // 1. 出現位置をランダムにばらけさせる
                Vector3 basePos = (spawnPoint != null) ? spawnPoint.position : playerScript.transform.position;
                Vector3 spawnPos = basePos;
                spawnPos.x -= Random.Range(1.0f, 8.0f);
                spawnPos.y += Random.Range(0.0f, 2.0f);

                // 2. 生成する
                GameObject newAlly = Instantiate(allyPrefab, spawnPos, Quaternion.identity);

                // 3. サイズを小さくする（0.8倍）
                newAlly.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

                // 4. 全員「赤色」にする
                SpriteRenderer sr = newAlly.GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = Color.red;
                }
            }

            Debug.Log("味方部隊参戦！");
        }
    }

    /*void LateUpdate()
	{
		if(debugText != null){
			debugText.GetComponent<TextMeshProUGUI>().text = buffer.ToString();
		}
		buffer.Clear();
	}
	
	public void DispStr(string str){
		buffer.Append(str + "\n");
	}*/
	
}
