using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using YG;
using System.Data.Common;

public class BlackHoleController : HoleParent
{

	public override void Start()
	{
		base.Start();
		isEnemy = false;
		if (YG2.saves.nickName != "")
			nickname.text = YG2.saves.nickName;
		else
		{
			nickname.text = YG2.saves.langRu? "Легенда" : "Legend";
		}
	}
}
