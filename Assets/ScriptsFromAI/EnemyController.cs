using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using YG;
using System.Linq;

public class EnemyController : HoleParent
{
	public static int count;

	public override void Start()
	{
		base.Start();
		count += 1;
		nickname.text = $"Enemy{count}";
	}
}
