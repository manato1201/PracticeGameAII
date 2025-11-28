using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using TMPro;

public class GameManager : MonoBehaviour
{
	public static GameManager instance;

	// デバッグ用文字表示 DispStr用
	GameObject debugText;
	
	StringBuilder buffer = new StringBuilder();
	
	void Awake(){
		instance = this;
	}
	
	// Start is called before the first frame update
	void Start()
	{
		debugText = GameObject.Find("DebugText");
	}

	// Update is called once per frame
	void Update()
	{
		
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
