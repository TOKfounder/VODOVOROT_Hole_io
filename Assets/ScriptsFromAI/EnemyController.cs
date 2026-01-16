using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using YG;
using System.Linq;

public class EnemyController : HoleParent
{
	private int bossScore = 1779;
	public static int count;

	public override void Start()
	{
		base.Start();
		holeType = TypeOfHole.enemy;
		if (ModeManager.currentMode == ModeManager.Mode.Boss)
		{
			score = bossScore;
		}
		count += 1;
		nickname.text = $"Enemy{count}";
	}
}
