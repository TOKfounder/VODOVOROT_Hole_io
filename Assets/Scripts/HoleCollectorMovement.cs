using System.Collections;
using UnityEngine;

public abstract class HoleCollectorMovement : MonoBehaviour
{
    private Transform currentTarget;
    private Rigidbody rb;
    private Transform ignoredTarget;
    private float ignoreCooldown;
    private float stuckTimer;

    protected abstract GameObject MovementRoot { get; }
    protected abstract float RotationSpeed { get; }
    protected abstract float DetectionRadius { get; }
    protected abstract float SearchInterval { get; }
    protected abstract LayerMask FallableObjects { get; }
    protected abstract float[] LevelSpeeds { get; }
    protected abstract HoleParent ControlledHole { get; }

    protected virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.angularDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        StartCoroutine(SearchRoutine());
    }

    private IEnumerator SearchRoutine()
    {
        while (true)
        {
            FindClosestObject();
            yield return new WaitForSeconds(SearchInterval);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (rb == null || ControlledHole == null || MovementRoot == null)
            return;

        if (currentTarget != null)
            MoveToTarget();
        else
            HandleIdleMovement();

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

    protected abstract bool CanCollectTarget(FallingObject target);
    protected abstract void HandleIdleMovement();

    protected void MoveInDirection(Vector3 moveDirection)
    {
        if (rb == null || MovementRoot == null)
            return;

        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude <= 0.0001f)
            return;

        Vector3 normalizedDirection = moveDirection.normalized;

        Quaternion targetRotation = Quaternion.LookRotation(normalizedDirection);
        MovementRoot.transform.rotation = Quaternion.Slerp(
            MovementRoot.transform.rotation,
            targetRotation,
            RotationSpeed * Time.fixedDeltaTime);

        float speed = GetCurrentSpeed();
        Vector3 newPosition = rb.position + normalizedDirection * speed * 0.15f * Time.fixedDeltaTime;

        GamingManager gamingManager = VodovorotGameManager.Instance?.GamingManager;
        if (gamingManager != null)
        {
            newPosition.x = Mathf.Clamp(newPosition.x, gamingManager.minX, gamingManager.maxX);
            newPosition.z = Mathf.Clamp(newPosition.z, gamingManager.minZ, gamingManager.maxZ);
        }

        rb.MovePosition(newPosition);
    }

    private void FindClosestObject()
    {
        if (ControlledHole == null)
            return;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, DetectionRadius, FallableObjects);

        float closestDist = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (Collider hit in hitColliders)
        {
            if (hit.transform == ignoredTarget)
                continue;

            FallingObject fallingObject = hit.GetComponentInParent<FallingObject>();
            if (fallingObject == null || fallingObject.value <= 0 || !fallingObject.gameObject.activeInHierarchy)
                continue;

            if (fallingObject.isTriggered && fallingObject.CurrentHole != null && fallingObject.CurrentHole != ControlledHole)
                continue;

            if (!CanCollectTarget(fallingObject))
                continue;

            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance < closestDist)
            {
                closestDist = distance;
                bestTarget = hit.transform;
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
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;
            return;
        }

        Vector3 direction = currentTarget.position - transform.position;
        direction.y = 0f;

        if (direction.magnitude < transform.localScale.x * 0.5f)
        {
            currentTarget = null;
            return;
        }

        MoveInDirection(direction);
    }

    private float GetCurrentSpeed()
    {
        if (ControlledHole == null || LevelSpeeds == null || LevelSpeeds.Length == 0)
            return 0f;

        int level = ControlledHole.currentLevel;
        return level < LevelSpeeds.Length ? LevelSpeeds[level] : LevelSpeeds[^1];
    }

    private void CheckStuckStatus()
    {
        stuckTimer += SearchInterval;

        if (stuckTimer >= 3.5f)
        {
            ignoredTarget = currentTarget;
            ignoreCooldown = 0f;
            currentTarget = null;
            stuckTimer = 0f;
        }
    }
}
