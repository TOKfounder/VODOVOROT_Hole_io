using System.Collections;
using UnityEngine;
using YG;

public class EnemyController : HoleParent
{
	private int bossScore = 1779;
	public static int count;
	private bool absorptionHandled;

	[SerializeField] private float sinkDuration = 1.2f;
	[SerializeField] private float sinkDepth = 1.5f;

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
		Color tint = PopupTint();
		StartCoroutine(SinkThenBurst(loot, tint));
	}

	private IEnumerator SinkThenBurst(int loot, Color tint)
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

		Vector3 burstCenter = start;
		float burstRadius = 1f;
		HoleParent player = BlackHoleController.Player;
		if (player != null)
		{
			burstCenter = player.transform.position;
			burstRadius = player.GetHoleRadius();
		}
		ScoreOrbSpawner.Burst(burstCenter, burstRadius, loot, tint);
		Destroy(gameObject);
	}

	private Color PopupTint()
	{
		if (border != null)
			return border.color;
		if (TeamId == ModeManager.TeamBlue)
			return PointsScript.CyanPopup;
		if (TeamId == ModeManager.TeamRed)
			return PointsScript.RedPopup;
		return PointsScript.GoldPopup;
	}
}
