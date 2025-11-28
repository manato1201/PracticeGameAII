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
		
	}
	
	public override void OnExit()
	{
		
	}
	
	public override Soldier.State CheckTransitions()
	{
		if(soldier.isHitted){
			return Soldier.State.DAMAGE;
		}
		
		if(soldier.tool.DistanceToPlayer() < 2f){
			return Soldier.State.RUN;
		}
		return Soldier.State.WAIT;
	}
	
}

