using UnityEngine;

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
			RefreshSizeFromScore();
		}
		count += 1;
		if (nickname != null)
			nickname.text = $"Enemy{count}";
	}
}
