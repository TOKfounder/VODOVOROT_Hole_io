using UnityEngine;
using YG;

public class BlackHoleController : HoleParent
{
	public static BlackHoleController Player { get; private set; }

	// Совместимость со старым кодом MovementScript
	public static BlackHoleController Instance => Player;

	protected override void Awake()
	{
		base.Awake();
		Player = this;
	}

	protected override void OnDestroy()
	{
		if (Player == this)
			Player = null;
		base.OnDestroy();
	}

	public override void Start()
	{
		base.Start();
		holeType = TypeOfHole.player;
		if (nickname == null)
			return;

		if (!string.IsNullOrEmpty(YG2.saves.nickName))
			nickname.text = YG2.saves.nickName;
		else
			nickname.text = YG2.saves.langRu ? "Легенда" : "Legend";
	}
}
