using UnityEngine;

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
		if (nickname != null)
			nickname.text = $"Enemy{count}";
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
