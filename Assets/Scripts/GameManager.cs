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
        allySpawned = true; // フラグを立てる

        if (allyPrefab != null)
        {
            // 出現位置を決める（プレイヤーの少し後ろ、上空など）
            Vector3 pos = playerScript.transform.position;
            pos.x -= 2.0f;
            pos.y += 1.0f;

            if (spawnPoint != null) pos = spawnPoint.position;

            Instantiate(allyPrefab, pos, Quaternion.identity);

            Debug.Log("<color=cyan>味方AIプレイヤーを守る！</color>");

            // ブラックボード、ここで「ピンチ状態」を書き込んで実装BGMを変えるなど
            blackBoard.SetValue(BlackBoardKey.IsPlayerPinch, 1);
        }
    }

    void LateUpdate()
	{
		if(debugText != null){
			debugText.GetComponent<TextMeshProUGUI>().text = buffer.ToString();
		}
		buffer.Clear();
	}
	
	public void DispStr(string str){
		buffer.Append(str + "\n");
	}
	
}
