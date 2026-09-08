using System.Collections;
using UnityEngine;
using YG;

public class EnemyController : HoleParent
{
	private int bossScore = 163;
	public static int count;
	private bool absorptionHandled;
	private EnemyMovement cachedMovement;

	[SerializeField] private float sinkDuration = 0.65f;
	[SerializeField] private float sinkDepth = 1.5f;

	public override void Start()
	{
		base.Start();
		holeType = TypeOfHole.enemy;
		cachedMovement = GetComponentInChildren<EnemyMovement>();
		if (ModeManager.currentMode == ModeManager.Mode.Boss)
		{
			score = ScoreRequiredForLevel(2);
			if (score <= 0)
				score = bossScore;
			RefreshSizeFromScore();
		}
		count += 1;
		if (!NickAssigned)
			ApplyDefaultNick();
	}

	protected override void FixedUpdate()
	{
		base.FixedUpdate();
		TryAbsorbPlayer();
	}

	private void TryAbsorbPlayer()
	{
		if (IsConsumed)
			return;

		BlackHoleController player = BlackHoleController.Player;
		if (player == null || player.IsConsumed)
			return;

		if (ModeManager.currentMode == ModeManager.Mode.TeamMode)
			return;

		if (ModeManager.currentMode == ModeManager.Mode.Boss
			&& cachedMovement != null
			&& !cachedMovement.BossMayAbsorbPlayer)
			return;

		if (!CanAbsorbOtherHole(player) || !IsOtherHoleFullyInside(player))
			return;

		player.Eliminate();
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

	public void OnAbsorbedByPlayer(HoleParent absorber)
	{
		if (absorptionHandled)
			return;

		absorptionHandled = true;
		MarkConsumed();

		EnemyMovement movement = cachedMovement;
		if (movement == null)
			movement = GetComponentInChildren<EnemyMovement>();
		if (movement != null)
			movement.enabled = false;

		Rigidbody body = GetComponent<Rigidbody>();
		if (body == null)
			body = GetComponentInChildren<Rigidbody>();
		if (body != null)
		{
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.isKinematic = true;
		}

		int loot = score;
		score = 0;
		bool isBoss = this == ModeManager.ActiveBoss;
		if (absorber != null && loot > 0)
			ScoreOrbSpawner.Burst(absorber, loot, isBoss);

		StartCoroutine(SinkThenDestroy());
	}

	private IEnumerator SinkThenDestroy()
	{
		Collider[] colliders = GetComponentsInChildren<Collider>();
		for (int i = 0; i < colliders.Length; i++)
		{
			if (colliders[i] != null)
				colliders[i].enabled = false;
		}

		Vector3 start = transform.position;
		Vector3 end = start + Vector3.down * sinkDepth;
		float elapsed = 0f;
		float duration = Mathf.Max(0.15f, sinkDuration);
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			transform.position = Vector3.Lerp(start, end, t * t);
			yield return null;
		}

		ModeManager.NotifyEnemyAbsorbed(this);
		Destroy(gameObject);
	}
}
