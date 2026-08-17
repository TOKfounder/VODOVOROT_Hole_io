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

	protected override void FixedUpdate()
	{
		base.FixedUpdate();

		if (ModeManager.currentMode != ModeManager.Mode.Boss)
			return;

		TryAbsorbEnemyHoles();
	}

	private void TryAbsorbEnemyHoles()
	{
		for (int i = holeList.Count - 1; i >= 0; i--)
		{
			HoleParent candidate = holeList[i];
			if (candidate == null || candidate == this || candidate.holeType != TypeOfHole.enemy)
				continue;

			if (!CanAbsorbHole(candidate))
				continue;

			if (!IsInHole(candidate.transform.position))
				continue;

			AbsorbEnemy(candidate as EnemyController);
			break;
		}
	}

	private bool CanAbsorbHole(HoleParent enemy)
	{
		if (enemy == null || enemy.size == Vector3.zero || size == Vector3.zero)
			return false;

		if (!Tool.CanFit2D(enemy.size, size))
			return false;

		if (currentLevel > enemy.currentLevel)
			return true;

		return currentLevel == enemy.currentLevel && score > enemy.score;
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
