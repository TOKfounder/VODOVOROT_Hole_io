using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using YG;
using System.Linq;

public class EnemyController : HoleParent
{
	public static List<EnemyController> enemyList = new List<EnemyController>();
	public static EnemyController[] enemies => enemyList.ToArray();

	public override void Start()
	{
		base.Start();
		enemyList.Add(this);
		nickname.text = $"Enemy{enemies.Length}";
	}

	// public override void AddScore(int amount){
	// 	base.AddScore(amount);
	// 	GetComponentInParent<EnemyMovement>().haveGoal = false;
	// }
}
