using UnityEngine;
using System.Collections;

public class EnemyMovement : MonoBehaviour
{
	[SerializeField] private GameObject withoutCamera;
	[SerializeField] private float rotationSpeed = 0.1f;
	[SerializeField] private float detectionRadius = 50f;
	[SerializeField] private float searchInterval = 0.75f;
	[SerializeField] private LayerMask fallableObjects;

	[Header("Hunting")]
	[SerializeField] private float huntSightRadius = 45f;
	[SerializeField] private float huntPlayerSeconds = 10f;
	[SerializeField] private float farmAfterHuntSeconds = 18f;

	private float bossFarmSeconds = 30f;
	private float bossProbeSeconds = 120f;
	private float bossSpeedMul = 0.56f;
	private float enemySpeedMul = 0.25f;

	private Transform currentTarget;
	private float[] levelSpeeds = { 3.6f, 4.134f, 4.668f, 5.202f, 5.736f, 6.264f, 8.298f, 9.132f, 12f, 15f, 16.8f };
	private Rigidbody rb;
	private float stuckTimer;
	private Transform ignoredTarget;
	private float ignoreCooldown;
	private bool fleeFromTarget;
	private EnemyController enemyController;
	private float huntPlayerUntil = -1f;
	private float farmUntil;
	private float edgeRecoverUntil;
	public int FarmSectorIndex { get; set; } = -1;

	public bool BossMayAbsorbPlayer
	{
		get
		{
			if (ModeManager.currentMode != ModeManager.Mode.Boss)
				return true;
			return GetBossPhase() != BossPhase.Farm;
		}
	}

	private enum BossPhase
	{
		Farm,
		Probe,
		Hunt
	}

	void Start()
	{
		ApplyBalanceConfig();
		rb = GetComponent<Rigidbody>();
		enemyController = GetComponentInParent<EnemyController>();
		if (withoutCamera == null && enemyController != null)
			withoutCamera = enemyController.WithoutCamera;
		StartCoroutine(SearchRoutine());
	}

	void ApplyBalanceConfig()
	{
		GameBalanceConfig config = GameBalance.Current;
		if (config != null)
			levelSpeeds = GameBalance.CopyOr(config.levelSpeeds, levelSpeeds);

		ModeConfig current = GameBalance.Mode(ModeManager.currentMode);
		if (current != null)
		{
			if (current.huntSightRadius > 0f)
				huntSightRadius = current.huntSightRadius;
			if (current.huntPlayerSeconds > 0f)
				huntPlayerSeconds = current.huntPlayerSeconds;
			if (current.farmAfterHuntSeconds > 0f)
				farmAfterHuntSeconds = current.farmAfterHuntSeconds;
			if (current.enemySpeedMul > 0f)
				enemySpeedMul = current.enemySpeedMul;
		}

		ModeConfig boss = GameBalance.Mode(ModeManager.Mode.Boss);
		if (boss != null)
		{
			if (boss.bossFarmSeconds > 0f)
				bossFarmSeconds = boss.bossFarmSeconds;
			if (boss.bossProbeSeconds > 0f)
				bossProbeSeconds = boss.bossProbeSeconds;
			if (boss.bossSpeedMul > 0f)
				bossSpeedMul = boss.bossSpeedMul;
		}
	}

	IEnumerator SearchRoutine()
	{
		while (true)
		{
			FindClosestObject();
			yield return new WaitForSeconds(searchInterval);
		}
	}

	void FixedUpdate()
	{
		if (enemyController == null || MatchPause.IsPaused)
			return;

		if (!IsCurrentTargetValid())
			currentTarget = null;

		if (currentTarget != null)
			MoveToTarget();
		else
			SmallWander();

		if (ignoredTarget != null)
		{
			ignoreCooldown += Time.fixedDeltaTime;
			if (ignoreCooldown > 10f)
			{
				ignoredTarget = null;
				ignoreCooldown = 0f;
			}
		}
	}

	void FindClosestObject()
	{
		if (enemyController == null)
			return;

		fleeFromTarget = false;

		if (TrySetModeTarget())
			return;

		Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, fallableObjects);
		if (hitColliders.Length == 0)
		{
			SetTarget(null);
			return;
		}

		int sector = FarmSectorIndex;
		if (sector >= 0 && FarmSectors.Count > 0)
			sector = FarmSectors.FindSectorWithFood(sector, enemyController.size, FarmSectors.SectorRadius);

		Transform bestTarget = PickClosestFood(hitColliders, sector, true);
		if (bestTarget == null)
			bestTarget = PickClosestFood(hitColliders, sector, false);
		SetTarget(bestTarget);
	}

	private Transform PickClosestFood(Collider[] hits, int sector, bool sectorOnly)
	{
		float closestDist = Mathf.Infinity;
		Transform bestTarget = null;
		for (int i = 0; i < hits.Length; i++)
		{
			Collider hit = hits[i];
			if (hit == null || hit.transform == ignoredTarget)
				continue;

			FallingObject fo = hit.GetComponentInParent<FallingObject>();
			if (fo == null || !Tool.CanFitForEnemies(fo.size, enemyController.size))
				continue;
			if (sectorOnly && sector >= 0 && !FarmSectors.IsInSector(sector, fo.transform.position, FarmSectors.SectorRadius))
				continue;

			float dist = Vector3.Distance(transform.position, hit.transform.position);
			if (dist >= closestDist)
				continue;
			closestDist = dist;
			bestTarget = hit.transform;
		}
		return bestTarget;
	}

	private bool TrySetModeTarget()
	{
		if (ModeManager.currentMode == ModeManager.Mode.TeamMode)
		{
			Transform holeTarget = FindClosestOpponentHole();
			if (holeTarget == null)
				return false;

			float dx = holeTarget.position.x - transform.position.x;
			float dz = holeTarget.position.z - transform.position.z;
			float range = huntSightRadius * MatchRules.TeamHuntRangeFactor;
			if (dx * dx + dz * dz > range * range)
				return false;

			SetTarget(holeTarget);
			return true;
		}

		BlackHoleController player = BlackHoleController.Player;
		if (player == null || player.IsConsumed)
			return false;

		if (ModeManager.currentMode == ModeManager.Mode.Boss && IsThisBoss())
		{
			BossPhase phase = GetBossPhase();
			if (phase == BossPhase.Farm)
				return false;

			SetTarget(player.transform);
			return true;
		}

		if (ModeManager.currentMode == ModeManager.Mode.Hunting)
			return TrySetHuntingTarget(player);

		return false;
	}

	private bool TrySetHuntingTarget(BlackHoleController player)
	{
		float dx = player.transform.position.x - transform.position.x;
		float dz = player.transform.position.z - transform.position.z;
		bool inSight = dx * dx + dz * dz <= huntSightRadius * huntSightRadius;
		bool playerBigger = player.currentLevel > enemyController.currentLevel
			|| (player.currentLevel == enemyController.currentLevel && player.score > enemyController.score);
		bool meBigger = enemyController.currentLevel > player.currentLevel
			|| (player.currentLevel == enemyController.currentLevel && enemyController.score > player.score);

		if (inSight && playerBigger && Time.time >= edgeRecoverUntil)
		{
			huntPlayerUntil = -1f;
			fleeFromTarget = true;
			SetTarget(player.transform);
			return true;
		}

		float now = Time.time;
		if (meBigger && now >= farmUntil)
		{
			if (huntPlayerUntil < 0f)
				huntPlayerUntil = now + huntPlayerSeconds;
			if (now < huntPlayerUntil)
			{
				fleeFromTarget = false;
				SetTarget(player.transform);
				return true;
			}

			huntPlayerUntil = -1f;
			farmUntil = now + farmAfterHuntSeconds;
		}
		else if (!meBigger)
			huntPlayerUntil = -1f;

		return false;
	}

	private bool IsThisBoss()
	{
		return ModeManager.ActiveBoss == enemyController;
	}

	private BossPhase GetBossPhase()
	{
		float timer = GamingManager.Instance != null ? GamingManager.Instance.timer : 0f;
		if (timer < bossFarmSeconds)
			return BossPhase.Farm;
		if (timer < bossFarmSeconds + bossProbeSeconds)
			return BossPhase.Probe;
		return BossPhase.Hunt;
	}

	private Transform FindClosestOpponentHole()
	{
		float closestDist = Mathf.Infinity;
		Transform bestTarget = null;
		for (int i = 0; i < HoleParent.holeList.Count; i++)
		{
			HoleParent other = HoleParent.holeList[i];
			if (other == null || other == enemyController || other.IsConsumed)
				continue;
			if (other.transform == ignoredTarget)
				continue;
			if (!enemyController.IsOpponent(other) || !enemyController.CanAbsorbOtherHole(other))
				continue;

			float dist = Vector3.Distance(transform.position, other.transform.position);
			if (dist >= closestDist)
				continue;

			closestDist = dist;
			bestTarget = other.transform;
		}
		return bestTarget;
	}

	private void SetTarget(Transform bestTarget)
	{
		if (currentTarget == bestTarget)
		{
			if (!IsHoleTarget(currentTarget))
				CheckStuckStatus();
		}
		else
		{
			currentTarget = bestTarget;
			stuckTimer = 0f;
		}
	}

	void MoveToTarget()
	{
		if (withoutCamera == null || currentTarget == null)
			return;

		Vector3 dir = currentTarget.position - transform.position;
		dir.y = 0;
		if (fleeFromTarget)
			dir = -dir;

		HoleParent holeTarget = currentTarget.GetComponentInParent<HoleParent>();
		if (holeTarget == null && dir.magnitude < transform.localScale.x * 0.5f)
		{
			currentTarget = null;
			return;
		}

		if (dir.sqrMagnitude < 0.0001f)
			return;

		Vector3 moveDir = dir.normalized;

		Quaternion targetRotation = Quaternion.LookRotation(moveDir);
		withoutCamera.transform.rotation = Quaternion.Slerp(withoutCamera.transform.rotation,
			targetRotation, rotationSpeed * Time.fixedDeltaTime);

		ApplyMove(moveDir);
	}

	void SmallWander()
	{
		if (withoutCamera == null)
			return;

		ApplyMove(withoutCamera.transform.forward);
	}

	void ApplyMove(Vector3 moveDir)
	{
		if (rb == null || enemyController == null)
			return;

		int level = enemyController.currentLevel;
		float speed = (level >= 0 && level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];
		float mul = IsThisBoss() ? bossSpeedMul : enemySpeedMul;
		Vector3 delta = moveDir * speed * mul * Time.fixedDeltaTime;
		Vector3 newPosition = rb.position + delta;
		Vector3 clamped = newPosition;
		ClampToBounds(ref clamped);
		if ((clamped - newPosition).sqrMagnitude > 0.0001f)
		{
			edgeRecoverUntil = Time.time + 0.6f;
			FaceMapCenter();
			if (withoutCamera != null)
			{
				newPosition = rb.position + withoutCamera.transform.forward * speed * mul * Time.fixedDeltaTime;
				ClampToBounds(ref newPosition);
			}
			else
				newPosition = clamped;
		}
		else
			newPosition = clamped;
		rb.MovePosition(newPosition);
	}

	private void FaceMapCenter()
	{
		if (withoutCamera == null || GamingManager.Instance == null)
			return;

		Vector3 center = new Vector3(
			(GamingManager.Instance.minX + GamingManager.Instance.maxX) * 0.5f,
			transform.position.y,
			(GamingManager.Instance.minZ + GamingManager.Instance.maxZ) * 0.5f);
		Vector3 dir = center - transform.position;
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.0001f)
			return;
		withoutCamera.transform.rotation = Quaternion.LookRotation(dir.normalized);
	}

	void ClampToBounds(ref Vector3 newPosition)
	{
		if (GamingManager.Instance == null)
			return;

		newPosition.x = Mathf.Clamp(newPosition.x, GamingManager.Instance.minX, GamingManager.Instance.maxX);
		newPosition.z = Mathf.Clamp(newPosition.z, GamingManager.Instance.minZ, GamingManager.Instance.maxZ);
	}

	void CheckStuckStatus()
	{
		stuckTimer += searchInterval;

		if (stuckTimer >= 3.5f)
		{
			ignoredTarget = currentTarget;
			ignoreCooldown = 0;
			currentTarget = null;
			stuckTimer = 0;
		}
	}

	private bool IsCurrentTargetValid()
	{
		if (currentTarget == null)
			return false;

		HoleParent holeTarget = currentTarget.GetComponentInParent<HoleParent>();
		if (holeTarget == null)
			return true;

		if (holeTarget.IsConsumed)
			return false;

		if (holeTarget is BlackHoleController)
			return true;

		return enemyController.IsOpponent(holeTarget)
			&& enemyController.CanAbsorbOtherHole(holeTarget);
	}

	private static bool IsHoleTarget(Transform target)
	{
		return target != null && target.GetComponentInParent<HoleParent>() != null;
	}
}
