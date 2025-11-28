using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier_FSM_Damage : Soldier_FSM_Base
{
	public Soldier_FSM_Damage(Soldier s) : base(s)
	{
		
	}
	
	public override void OnEnter()
	{
		soldier.speed.x = 0f;	// 左右移動を止めて
	}
	
	public override void OnUpdate()
	{
	}
	
	public override void OnExit()
	{
	}
	
	public override Soldier.State CheckTransitions()
	{
		if(!soldier.isHitted){
			return Soldier.State.WAIT;
		}
		return Soldier.State.DAMAGE;
	}
	
}

