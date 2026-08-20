using UnityEngine;
using YG;

public class EnemyController : HoleParent
{
	private int bossScore = 1779;
	public static int count;
	private bool absorptionHandled;

	public override void Start()
	{
		base.Start();
		holeType = TypeOfHole.enemy;
		if (ModeManager.currentMode == ModeManager.Mode.Boss)
		{
			score = bossScore;
			RefreshSizeFromScore();
		}
		count += 1;
		if (!NickAssigned)
			ApplyDefaultNick();
	}

	private void ApplyDefaultNick()
	{
		if (nickname == null)
			return;

		bool ru = YG2.saves.langRu;
		if (ModeManager.currentMode == ModeManager.Mode.Boss)
			SetNickname(ru ? "Босс" : "Boss");
		else
			SetNickname(ru ? $"Враг {count}" : $"Enemy {count}");
	}

	public void OnAbsorbedByPlayer()
	{
		if (absorptionHandled)
			return;

		absorptionHandled = true;
		MarkConsumed();
		ModeManager.NotifyEnemyAbsorbed(this);

		EnemyMovement movement = GetComponentInChildren<EnemyMovement>();
		if (movement != null)
			movement.enabled = false;

		Destroy(gameObject);
	}
}
