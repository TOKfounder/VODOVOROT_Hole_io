using UnityEngine;
using System.Collections;

public class EnemyMovement : MonoBehaviour
{
    [Header("References")]
    public GameObject withoutCamera;

    [Header("Movement Settings")]
    public float rotationSpeed = 0.1f;
    public float detectionRadius = 500f;
    public float searchInterval = 0.5f;
    public LayerMask fallableObjects;

    [Header("Speeds")]
    public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f };

    private Transform currentTarget;
    private Rigidbody rb;
    private EnemyController enemyController;

    private Transform ignoredTarget;
    private float ignoreCooldown;
    private float stuckTimer;

	private void Start()
	{
		rb = GetComponent<Rigidbody>();
		enemyController = GetComponentInParent<EnemyController>();

		// Важно: уменьшаем сопротивление повороту
		rb.angularDamping = 3f;           // ← добавь
		rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

		StartCoroutine(SearchRoutine());
	}

    private IEnumerator SearchRoutine()
    {
        while (true)
        {
            FindClosestObject();
            yield return new WaitForSeconds(searchInterval);
        }
    }

    private void FixedUpdate()
    {
        if (currentTarget != null)
            MoveToTarget();
        else
            SmallWander();

        // Таймер игнорирования застрявшей цели
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

    private void FindClosestObject()
    {
        // Один OverlapSphere вместо двух
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius * 100, fallableObjects);

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
                if (dist < closestDist)
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

	private void MoveToTarget()
	{
		Vector3 dir = currentTarget.position - transform.position;
		dir.y = 0;

		if (dir.magnitude < transform.localScale.x * 0.5f)
		{
			currentTarget = null;
			return;
		}

		Vector3 moveDir = dir.normalized;

		// Улучшенный поворот
		Quaternion targetRotation = Quaternion.LookRotation(moveDir);
		withoutCamera.transform.rotation = Quaternion.Slerp(
			withoutCamera.transform.rotation, 
			targetRotation, 
			rotationSpeed * Time.fixedDeltaTime);

		// Движение
		int level = enemyController.currentLevel;
		float speed = (level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];

		Vector3 newPosition = rb.position + moveDir * speed * 0.15f * Time.fixedDeltaTime;

		newPosition.x = Mathf.Clamp(newPosition.x, 
			VodovorotGameManager.Instance.GamingManager.minX, 
			VodovorotGameManager.Instance.GamingManager.maxX);
		newPosition.z = Mathf.Clamp(newPosition.z, 
			VodovorotGameManager.Instance.GamingManager.minZ, 
			VodovorotGameManager.Instance.GamingManager.maxZ);

		rb.MovePosition(newPosition);
	}

    private void SmallWander()
    {
        int level = enemyController.currentLevel;
        float speed = (level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];

		rb.MovePosition(rb.position + withoutCamera.transform.forward * speed * 0.15f * Time.fixedDeltaTime);
    }

    private void CheckStuckStatus()
    {
        stuckTimer += searchInterval;

        if (stuckTimer >= 3.5f)
        {
            ignoredTarget = currentTarget;
            ignoreCooldown = 0f;
            currentTarget = null;
            stuckTimer = 0f;
        }
    }
}