using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soldier_FSM_RangeAttack : Soldier_FSM_Base
{
	float timer;
	
	public Soldier_FSM_RangeAttack(Soldier s) : base(s)
	{
		
	}
	
	public override void OnEnter()
	{
		soldier.RangeAttackAction();
		timer = 5f;
	}
	
	public override void OnUpdate()
	{
		timer -= Time.deltaTime;
	}
	
	public override void OnExit()
	{
	}
	
	public override Soldier.State CheckTransitions()
	{
		if(soldier.isHitted){
			return Soldier.State.DAMAGE;
		}
		if(timer < 0){
			return Soldier.State.WAIT;
		}
		return Soldier.State.RANGE_ATTACK;
	}
	
}

