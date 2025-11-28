using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Soldier_FSM_Dead : Soldier_FSM_Base
{
	int step;
	float timer;
	
	public Soldier_FSM_Dead(Soldier s) : base(s)
	{
		
	}
	
	public override void OnEnter()
	{
		soldier.DeadAction();
		step = 0;
		timer = 0.5f;
	}
	
	public override void OnUpdate()
	{
		switch(step){
			case 0:
				timer = 0.5f;
				step++;
				break;
			case 1:
				timer -= Time.deltaTime;
				if(timer < 0){
					soldier.speed.x = 0;
					timer = 3f;
					step++;
				}
				break;
			case 2:
				timer -= Time.deltaTime;
				if(timer < 0){
					soldier.Delete();
					step++;
				}
				break;
			case 3:
				// 何もしない
			break;
		}
		
	}
	
	public override void OnExit()
	{
		
	}
	
	public override Soldier.State CheckTransitions()
	{
		return Soldier.State.DEAD;
	}
	
}

