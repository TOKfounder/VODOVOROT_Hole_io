using UnityEngine;
using System.Collections.Generic;
using YG;

public class BlackHoleController : HoleParent
{
	public static BlackHoleController Player { get; private set; }

	public static BlackHoleController Instance => Player;
	private bool eliminatedReported;

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
		HoleCameraFollow.Ensure(this);
		if (nickname == null)
			return;

		if (!string.IsNullOrEmpty(YG2.saves.nickName))
			nickname.text = YG2.saves.nickName;
		else
			nickname.text = YG2.saves.langRu ? "Легенда" : "Legend";
	}

	protected override void FixedUpdate()
	{
		base.FixedUpdate();

		if (ModeManager.currentMode == ModeManager.Mode.Boss)
			TryAbsorbActiveBoss();
		else if (ModeManager.currentMode == ModeManager.Mode.Hunting)
			TryAbsorbHuntingEnemies();
	}

	public void Eliminate()
	{
		if (eliminatedReported)
			return;

		eliminatedReported = true;
		MarkConsumed();
		enabled = false;
		PinePie.SimpleJoystick.Examples.DemoScript.MovementScript movement =
			GetComponent<PinePie.SimpleJoystick.Examples.DemoScript.MovementScript>();
		if (movement != null)
			movement.enabled = false;

		GamingManager.Instance?.OnPlayerEliminated();
	}

	private void TryAbsorbActiveBoss()
	{
		TryAbsorbEnemy(ModeManager.ActiveBoss);
	}

	private void TryAbsorbHuntingEnemies()
	{
		List<EnemyController> enemies = ModeManager.HuntingEnemies;
		for (int i = 0; i < enemies.Count; i++)
		{
			if (TryAbsorbEnemy(enemies[i]))
				return;
		}
	}

	private bool TryAbsorbEnemy(EnemyController enemy)
	{
		if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsConsumed)
			return false;

		if (!CanAbsorbOtherHole(enemy))
			return false;

		if (!IsOtherHoleFullyInside(enemy))
			return false;

		AbsorbEnemy(enemy);
		return true;
	}

	private void AbsorbEnemy(EnemyController enemy)
	{
		if (enemy == null || enemy.IsConsumed)
			return;

		enemy.MarkConsumed();
		int absorbedScore = enemy.score;
		if (absorbedScore > 0)
			AddScore(absorbedScore);

		enemy.OnAbsorbedByPlayer();
	}
}
