using UnityEngine;
using System.Collections;

public class EnemyMovement : MonoBehaviour
{
	public GameObject withoutCamera;
	public float rotationSpeed = 0.1f;
	public float detectionRadius = 80f;
	public float searchInterval = 0.5f;
	public LayerMask fallableObjects;

	private Transform currentTarget;
	private float[] levelSpeeds = {6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f};
	private Rigidbody rb;
	private float stuckTimer;
	private Transform ignoredTarget;
	private float ignoreCooldown;

	private EnemyController enemyController;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
		enemyController = GetComponentInParent<EnemyController>();
		StartCoroutine(SearchRoutine());
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
		if (enemyController == null)
			return;

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

		Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, fallableObjects);
		if (hitColliders.Length == 0)
			return;

		float closestDist = Mathf.Infinity;
		Transform bestTarget = null;
		foreach (var hit in hitColliders)
		{
			if (hit.transform == ignoredTarget) continue;

			var fo = hit.GetComponentInParent<FallingObject>();
			if (fo == null) continue;

			if (Tool.CanFitForEnemies(fo.size, enemyController.size))
			{
				float dist = Vector3.Distance(transform.position, hit.transform.position);
				if (closestDist > dist)
				{
					closestDist = dist;
					bestTarget = hit.transform;
				}
			}
		}
		if (currentTarget == bestTarget)
		{
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
		if (dir.magnitude < transform.localScale.x * 0.5f)
		{
			currentTarget = null;
			return;
		}
		Vector3 moveDir = dir.normalized;

		Quaternion targetRotation = Quaternion.LookRotation(moveDir);
		withoutCamera.transform.rotation = Quaternion.Slerp(withoutCamera.transform.rotation,
			targetRotation, rotationSpeed * Time.fixedDeltaTime);

		int level = enemyController.currentLevel;
		float speed = (level >= 0 && level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];
		Vector3 newPosition = rb.position + moveDir * speed * 0.25f * Time.fixedDeltaTime;
		ClampToBounds(ref newPosition);
		rb.MovePosition(newPosition);
	}

	void SmallWander()
	{
		if (withoutCamera == null)
			return;

		int level = enemyController.currentLevel;
		float speed = (level >= 0 && level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];
		Vector3 newPosition = rb.position + withoutCamera.transform.forward * speed * 0.25f * Time.fixedDeltaTime;
		ClampToBounds(ref newPosition);
		rb.MovePosition(newPosition);
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
}
