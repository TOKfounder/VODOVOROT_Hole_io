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

    [Header("Aim Settings")]
    public float targetOvershootFactor = 0.75f;
    public float visualTurnSpeed = 240f;

    [Header("Speeds")]
    public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f };

    private Transform currentTarget;
    private FallingObject currentTargetObject;
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
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, fallableObjects);

        float closestDist = Mathf.Infinity;
        Transform bestTarget = null;
        FallingObject bestTargetObject = null;

        foreach (var hit in hitColliders)
        {
            var fo = hit.GetComponentInParent<FallingObject>();
            if (fo == null) continue;

            if (fo.transform == ignoredTarget) continue;

            if (Tool.CanFitForHelperAndEnemies(fo.size, enemyController.GetFitSize()))
            {
                float dist = Vector3.Distance(transform.position, GetTargetCenter(fo));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = fo.transform;
                    bestTargetObject = fo;
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
            currentTargetObject = bestTargetObject;
            stuckTimer = 0f;
        }
    }

	private void MoveToTarget()
	{
		Vector3 targetPoint = GetOvershootPoint(currentTargetObject);
		Vector3 dir = targetPoint - transform.position;
		dir.y = 0;

		if (dir.magnitude <= GetStopDistance(currentTargetObject))
		{
			currentTarget = null;
			currentTargetObject = null;
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

        Vector3 newPosition = rb.position + withoutCamera.transform.forward * speed * 0.15f * Time.fixedDeltaTime;
        newPosition.x = Mathf.Clamp(newPosition.x,
            VodovorotGameManager.Instance.GamingManager.minX,
            VodovorotGameManager.Instance.GamingManager.maxX);
        newPosition.z = Mathf.Clamp(newPosition.z,
            VodovorotGameManager.Instance.GamingManager.minZ,
            VodovorotGameManager.Instance.GamingManager.maxZ);

		rb.MovePosition(newPosition);
    }

    private void CheckStuckStatus()
    {
        stuckTimer += searchInterval;

        if (stuckTimer >= 3.5f)
        {
            ignoredTarget = currentTarget;
            ignoreCooldown = 0f;
            currentTarget = null;
            currentTargetObject = null;
            stuckTimer = 0f;
        }
    }

    private Vector3 GetTargetCenter(FallingObject target)
    {
        if (target != null && target.rend != null)
            return target.rend.bounds.center;

        return target != null ? target.transform.position : transform.position;
    }

    private Vector3 GetOvershootPoint(FallingObject target)
    {
        Vector3 center = GetTargetCenter(target);
        if (target == null)
            return center;

        Vector3 moveDir = center - transform.position;
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude <= Mathf.Epsilon)
            return center;

        float targetRadius = Mathf.Max(target.size.x, target.size.z) * 0.5f;
        float helperRadius = Mathf.Max(enemyController.GetFitSize().x, enemyController.GetFitSize().z) * 0.5f;
        return center + moveDir.normalized * Mathf.Max(targetRadius, helperRadius) * targetOvershootFactor;
    }

    private float GetStopDistance(FallingObject target)
    {
        if (target == null)
            return transform.localScale.x * 0.25f;

        float helperRadius = Mathf.Max(enemyController.GetFitSize().x, enemyController.GetFitSize().z) * 0.5f;
        return helperRadius * 0.35f;
    }

    private void SmoothVisualRotation(Vector3 moveDir)
    {
        if (withoutCamera == null)
            return;

        if (moveDir.sqrMagnitude <= Mathf.Epsilon)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(moveDir.normalized);
        withoutCamera.transform.rotation = Quaternion.RotateTowards(
            withoutCamera.transform.rotation,
            targetRotation,
            visualTurnSpeed * Time.fixedDeltaTime);
    }
}
