using UnityEngine;
using System.Collections;

public class FallingObject : MonoBehaviour
{
	public int value;
	public Vector3 size;
	public float V3;
	public Renderer rend;
	public bool isTriggered = false;
	public bool isColon = false;

	private Vector3 startPosition;
	private Quaternion startRotation;
	private Rigidbody rb;
	private Collider col;
	private Collider[] objectColliders;
	private Coroutine myCoroutine;
	private readonly System.Collections.Generic.List<HoleParent> candidateHoles =
		new System.Collections.Generic.List<HoleParent>(4);

	private const float SwitchImprovementFactorSqr = 0.8464f; // 0.92 * 0.92 => новая дыра ближе хотя бы на 8%

	public HoleParent CurrentHole { get; set; }

	void Awake()
	{
		col = GetComponent<Collider>();
		if (col == null)
		{
			col = gameObject.AddComponent<BoxCollider>();
		}
		rb = GetComponent<Rigidbody>();
		if (rb == null)
		{
			rb = gameObject.AddComponent<Rigidbody>();
		}
		rb.isKinematic = true;
		rend = GetComponent<Renderer>();
		if (rend == null)
		{
			Destroy(GetComponent<FallingObject>());
		}

		CacheObjectColliders();
}

	void Start()
	{
		gameObject.layer = 7;
		IgnoreAllRegisteredPlatforms();
		RefreshSpawnStats();
		VodovorotGameManager.Instance.GamingManager.AllValues += value;
	}

	void FixedUpdate()
	{
		if (!rb.isKinematic)
			return;

		CleanupCandidateHoles();
		EvaluateBestHole();
	}

	IEnumerator DelayForUpdateCurrentHole()
	{
		yield return new WaitForSeconds(3.5f);
		if (!rb.isKinematic)
			ResetToStart();
	}

	private void OnTriggerEnter(Collider other)
	{
		RegisterCandidateHole(other);
	}

	private void OnTriggerStay(Collider other)
	{
		RegisterCandidateHole(other);
	}

	private void OnTriggerExit(Collider other)
	{
		if (!other.CompareTag("Player"))
			return;

		HoleParent hole = other.GetComponentInParent<HoleParent>();
		if (hole == null)
			return;

		candidateHoles.Remove(hole);
	}

	public void PrepareForSpawn(bool colon)
	{
		isColon = colon;
		ResetRuntimeState();
		RefreshSpawnStats();
		SetCurrentHole(null);
	}

	private Vector3 GetVisualSize()
	{
		Bounds totalBounds = new Bounds(transform.position, Vector3.zero);
		Collider collider = GetComponent<Collider>();
		if (collider != null && collider.enabled)
		{
			totalBounds.Encapsulate(collider.bounds);
			return totalBounds.size;
		}
		Renderer renderer = GetComponent<Renderer>();
		if (renderer != null && renderer.enabled)
		{
			totalBounds.Encapsulate(renderer.bounds);
		}
		return totalBounds.size;
	}

	private void ResetRuntimeState()
	{
		rb.isKinematic = true;
		rb.useGravity = true;
		rb.detectCollisions = true;
		rb.linearVelocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;
		isTriggered = false;
		col.enabled = true;
		rend.enabled = true;
		candidateHoles.Clear();

		if (myCoroutine != null)
		{
			StopCoroutine(myCoroutine);
			myCoroutine = null;
		}
	}

	private void RefreshSpawnStats()
	{
		size = GetVisualSize();
		V3 = size.x * size.y * size.z;
		startPosition = transform.position;
		startRotation = transform.rotation;

		if (V3 <= 0.087f)
		{
			value = 1;
		}
		else if (V3 <= 0.51f)
		{
			value = 2;
		}
		else if (V3 <= 10.63f)
		{
			value = 3;
		}
		else if (V3 <= 20f)
		{
			value = 5;
		}
		else if (V3 <= 60f)
		{
			value = 10;
		}
		else if (V3 <= 100f)
		{
			value = 25;
		}
		else if (V3 <= 250f)
		{
			value = 40;
		}
		else if (V3 <= 860f)
		{
			value = 60;
		}
		else
		{
			value = 100;
		}

		rb.mass = V3 * 50;
		rb.linearDamping = 4;
		rb.angularDamping = 4;
	}


	public void ResetToStart()
	{
		transform.position = startPosition;
		transform.rotation = startRotation;
		rb.isKinematic = true;
		isTriggered = false;
		col.enabled = true;
		rend.enabled = true;
		candidateHoles.Clear();
		if (myCoroutine != null) StopCoroutine(myCoroutine);

		SetCurrentHole(null);
	}

	public void OnScored(HoleParent hole)
	{
		hole.AddScore(value);
		value = 0;

		rb.isKinematic = true;
		col.enabled = false;
		rend.enabled = false;
		candidateHoles.Clear();
		SetCurrentHole(null);

		if (myCoroutine != null) StopCoroutine(myCoroutine);

		// Если это колón и его съел игрок — спавним Helper
		if (isColon && hole is BlackHoleController playerHole)
		{
			playerHole.OnColonAbsorbed(transform);
		}
	}

	private void RegisterCandidateHole(Collider other)
	{
		if (!other.CompareTag("Player"))
			return;

		HoleParent hole = other.GetComponentInParent<HoleParent>();
		if (hole == null || candidateHoles.Contains(hole))
			return;

		candidateHoles.Add(hole);
	}

	private void CacheObjectColliders()
	{
		objectColliders = GetComponentsInChildren<Collider>(true);
	}

	private void IgnoreAllRegisteredPlatforms()
	{
		foreach (var plat in VodovorotGameManager.allPlatforms)
		{
			SetCollisionWithCollider(plat, true);
		}

		for (int i = 0; i < HoleParent.holeList.Count; i++)
		{
			SetHolePlatformCollisions(HoleParent.holeList[i], true);
		}
	}

	private void SetHolePlatformCollisions(HoleParent hole, bool ignore)
	{
		if (hole == null)
			return;

		Collider[] holeColliders = hole.GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < holeColliders.Length; i++)
		{
			Collider holeCollider = holeColliders[i];
			if (holeCollider == null || holeCollider.isTrigger)
				continue;

			SetCollisionWithCollider(holeCollider, ignore);
		}
	}

	private void SetCollisionWithCollider(Collider targetCollider, bool ignore)
	{
		if (targetCollider == null || objectColliders == null)
			return;

		for (int i = 0; i < objectColliders.Length; i++)
		{
			Collider objectCollider = objectColliders[i];
			if (objectCollider == null || objectCollider == targetCollider)
				continue;

			Physics.IgnoreCollision(targetCollider, objectCollider, ignore);
		}
	}

	private void CleanupCandidateHoles()
	{
		for (int i = candidateHoles.Count - 1; i >= 0; i--)
		{
			if (candidateHoles[i] == null)
				candidateHoles.RemoveAt(i);
		}
	}

	private void EvaluateBestHole()
	{
		HoleParent bestHole = null;
		float bestDistanceSqr = float.PositiveInfinity;
		int bestPriority = int.MaxValue;

		for (int i = 0; i < candidateHoles.Count; i++)
		{
			HoleParent hole = candidateHoles[i];
			if (!CanBeEatenBy(hole))
				continue;

			int holePriority = GetHolePriority(hole);
			float distanceSqr = GetDistanceToHoleCenterSqr(hole);
			if (holePriority < bestPriority || (holePriority == bestPriority && distanceSqr < bestDistanceSqr))
			{
				bestPriority = holePriority;
				bestDistanceSqr = distanceSqr;
				bestHole = hole;
			}
		}

		if (bestHole == null)
		{
			if (!isTriggered)
				SetCurrentHole(null);
			return;
		}

		if (ShouldSwitchTo(bestHole, bestDistanceSqr))
			SetCurrentHole(bestHole);

		TryActivateCurrentHole();
	}

	private bool ShouldSwitchTo(HoleParent bestHole, float bestDistanceSqr)
	{
		if (bestHole == CurrentHole)
			return false;

		if (CurrentHole == null)
			return true;

		if (!candidateHoles.Contains(CurrentHole))
			return true;

		if (!CanBeEatenBy(CurrentHole))
			return true;

		int bestPriority = GetHolePriority(bestHole);
		int currentPriority = GetHolePriority(CurrentHole);
		if (bestPriority < currentPriority)
			return true;
		if (bestPriority > currentPriority)
			return false;

		float currentDistanceSqr = GetDistanceToHoleCenterSqr(CurrentHole);
		return bestDistanceSqr <= currentDistanceSqr * SwitchImprovementFactorSqr;
	}

	private int GetHolePriority(HoleParent hole)
	{
		if (hole == null)
			return int.MaxValue;

		return hole.holeType == HoleParent.TypeOfHole.player ? 0 : 1;
	}

	private void SetCurrentHole(HoleParent newHole)
	{
		if (CurrentHole == newHole)
			return;

		if (CurrentHole != null)
			CurrentHole.nearbyFallingObjects.Remove(this);

		CurrentHole = newHole;
		UpdatePlatformCollisions();
	}

	private void TryActivateCurrentHole()
	{
		if (CurrentHole == null || isTriggered || !rb.isKinematic)
			return;

		isTriggered = true;
		rb.isKinematic = false;

		if (!CurrentHole.nearbyFallingObjects.Contains(this))
			CurrentHole.nearbyFallingObjects.Add(this);

		if (myCoroutine != null) StopCoroutine(myCoroutine);
		myCoroutine = StartCoroutine(DelayForUpdateCurrentHole());
	}

	private bool CanBeEatenBy(HoleParent hole)
	{
		if (hole == null)
			return false;

		if (isColon)
			return hole.holeType == HoleParent.TypeOfHole.player;

		return hole.holeType == HoleParent.TypeOfHole.enemy
			? Tool.CanFitForHelperAndEnemies(size, hole.GetFitSize())
			: Tool.CanFit2D(size, hole.GetFitSize());
	}

	private float GetDistanceToHoleCenterSqr(HoleParent hole)
	{
		Vector3 delta = transform.position - GetHoleReferencePoint(hole);
		delta.y = 0f;
		return delta.sqrMagnitude;
	}

	/// <summary>
	/// Обновляет коллизии так, чтобы объект взаимодействовал только с платформой текущей дыры
	/// </summary>
	private void UpdatePlatformCollisions()
	{
		IgnoreAllRegisteredPlatforms();
		SetHolePlatformCollisions(CurrentHole, false);
	}

	private Vector3 GetHoleReferencePoint(HoleParent hole)
	{
		if (hole != null && hole.platform != null)
			return hole.platform.bounds.center;

		return hole != null ? hole.transform.position : transform.position;
	}
}
