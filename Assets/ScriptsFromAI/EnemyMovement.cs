using UnityEngine;
using System.Collections;

public class EnemyMovement : MonoBehaviour
{
	public GameObject withoutCamera;
	public float rotationSpeed = 5f;
	// public float[] levelSpeeds = {6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f};
	public float moveSpeed = 3f;
	public float detectionRadius = 200f;
	public float searchInterval = 0.5f;
	public LayerMask fallableObjects;
	[HideInInspector] public bool haveGoal;

	private Transform currentTarget;
	private Rigidbody rb;
	private Vector3 lastPosition;
	private float stuckTimer;

	private Transform ignoredTarget;

	void Start()
	{
		haveGoal = false;
		StartCoroutine(SearchRoutine());
		rb = GetComponent<Rigidbody>();
		lastPosition = transform.position;
	}

	IEnumerator SearchRoutine()
	{
		while (true)
		{
			if (currentTarget == null)
			{
				haveGoal = false;
				FindClosestObject();
			}
			yield return new WaitForSeconds(searchInterval);
		}
	}

	void Update()
	{
		if (currentTarget != null)
		{
			MoveToTarget();
			CheckStuckStatus();
		}
		else
		{
			Wander();
		}
		
	}

	void FindClosestObject()
	{
		Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, fallableObjects);

		float closestDist = Mathf.Infinity;
		Transform bestTarget = null;
		foreach (var hit in hitColliders)
		{
			if (hit.transform == ignoredTarget) continue;
			Vector3 fallingSize = hit.GetComponentInParent<FallingObject>().size;
			Vector3 holeSize = GetComponentInParent<EnemyController>().size;
			if (Tool.CanFit2D(fallingSize, holeSize))
			{
				float dist = Vector3.Distance(transform.position, hit.transform.position);
				if (closestDist > dist)
				{
					closestDist = dist;
					bestTarget = hit.transform;
				}
			}
		}
		currentTarget = bestTarget;
		if (currentTarget != null) ignoredTarget = null;
	}

	void MoveToTarget()
	{
		Vector3 dir = (currentTarget.position - transform.position);
		dir.y = 0;
		if (dir.magnitude < 0.6f){
			currentTarget = null;
			return;
		}
		dir = dir.normalized;

		Quaternion targetRotation = Quaternion.LookRotation(dir);
		withoutCamera.transform.rotation = Quaternion.Slerp(withoutCamera.transform.rotation, 
		targetRotation, rotationSpeed * Time.deltaTime);

		Vector3 newPosition = rb.position + dir * moveSpeed * Time.deltaTime;

		rb.MovePosition(newPosition);
	}

	void CheckStuckStatus()
	{
		if (Vector3.Distance(transform.position, lastPosition) < 0.02f)
		{
			stuckTimer += Time.deltaTime;
		}
		else
		{
			stuckTimer = 0;
		}

		if (stuckTimer > 0.6f)
		{
			ignoredTarget = currentTarget;
			currentTarget = null;
			stuckTimer = 0;

			rb.MovePosition(rb.position - transform.forward * 1f);
		}
		lastPosition = transform.position;
	}

	void Wander()
	{
		withoutCamera.transform.Rotate(0, 30f * Time.deltaTime, 0);
		rb.MovePosition(rb.position + withoutCamera.transform.forward * moveSpeed * 0.5f * Time.deltaTime);
	}
}