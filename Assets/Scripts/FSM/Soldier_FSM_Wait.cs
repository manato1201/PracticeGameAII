using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier_FSM_Wait : Soldier_FSM_Base
{
	public Soldier_FSM_Wait(Soldier s) : base(s)
	{
		
	}
	
	public override void OnEnter()
	{
		soldier.WaitAction();
		soldier.speed.x = 0.0f;    // 右が+。左が-になります
		soldier.speed.y = 0.0f;    // 上が+、下が-になります
	}
	
	public override void OnUpdate()
	{
		// 例えば
		if(soldier.tool.DistanceToPlayer() < 3f){
			// ブラックボードにメッセージを書く
			GameManager.instance.blackBoard.SetValue(BlackBoardKey.Move, 1);
			// 以下、書き込みの例
			GameManager.instance.blackBoard.SetValue(BlackBoardKey.Time, 3f);
			GameManager.instance.blackBoard.SetValue(BlackBoardKey.Target, new Vector2(1f,0f));
		}
		
		// 以下、読み込みの例
		{
			Vector2 value;
			GameManager.instance.blackBoard.GetValue(BlackBoardKey.Target, out value);
			float waitTime;
			GameManager.instance.blackBoard.GetValue(BlackBoardKey.Time, out waitTime);
		}
		
	}
	
	public override void OnExit()
	{
		
	}
	
	public override Soldier.State CheckTransitions()
	{
		if(soldier.isHitted){
			return Soldier.State.DAMAGE;
		}
			
		// 移動サインを誰かが書きこんでいたら移動開始
		int check = 0;
		GameManager.instance.blackBoard.GetValue(BlackBoardKey.Move, out check);
		if(check > 0){
			return Soldier.State.RUN;
		}
		return Soldier.State.WAIT;
	}
	
}

