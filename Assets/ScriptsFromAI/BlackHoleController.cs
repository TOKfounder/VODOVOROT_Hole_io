using UnityEngine;
using YG;

public class BlackHoleController : HoleParent
{
	public static BlackHoleController Player { get; private set; }

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

	protected override void FixedUpdate()
	{
		base.FixedUpdate();

		if (ModeManager.currentMode != ModeManager.Mode.Boss)
			return;

		TryAbsorbActiveBoss();
	}

	private void TryAbsorbActiveBoss()
	{
		EnemyController enemy = ModeManager.ActiveBoss;
		if (enemy == null || !enemy.isActiveAndEnabled)
			return;

		if (!CanAbsorbOtherHole(enemy))
			return;

		if (!IsOtherHoleFullyInside(enemy))
			return;

		AbsorbEnemy(enemy);
	}

	private void AbsorbEnemy(EnemyController enemy)
	{
		if (enemy == null)
			return;

		int absorbedScore = enemy.score;
		if (absorbedScore > 0)
			AddScore(absorbedScore);

		enemy.OnAbsorbedByPlayer();
		GamingManager.Instance?.OnBossDefeated();
	}
}
