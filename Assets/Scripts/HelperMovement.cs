using UnityEngine;
using System.Collections;

public class HelperMovement : MonoBehaviour
{
    [Header("References")]
    public GameObject withoutCamera;

    [Header("Movement Settings")]
    public float rotationSpeed = 0.1f;
    public float detectionRadius = 500f;
    public float searchInterval = 0.5f;
    public LayerMask fallableObjects;

    [Header("Target Switching")]
    public float targetHoldTimeout = 4f;

    [Header("Aim Settings")]
    public float visualTurnSpeed = 240f;

    [Header("Speeds")]
    public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f };

    private Transform currentTarget;
    private FallingObject currentTargetObject;
    private Rigidbody rb;
    private HelperController helperController;

    private Transform ignoredTarget;
    private float ignoreCooldown;
    private float stuckTimer;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        helperController = GetComponentInParent<HelperController>();

        rb.angularDamping = 3f;
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
        if (currentTargetObject != null && !currentTargetObject.HasHelperReservation(this))
        {
            ReleaseCurrentTarget();
        }

        if (currentTarget != null)
            MoveToTarget();
        else
            currentTargetObject = null;

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

            if (!CanCollectTarget(fo)) continue;
            if (fo.IsReservedByAnotherHelper(this))
                continue;

            float dist = Vector3.Distance(transform.position, GetTargetCenter(fo));
            if (dist < closestDist)
            {
                closestDist = dist;
                bestTarget = fo.transform;
                bestTargetObject = fo;
            }
        }

        if (currentTarget == bestTarget)
        {
            CheckStuckStatus();
        }
        else
        {
            SwitchTarget(bestTarget, bestTargetObject);
        }
    }

    private void SwitchTarget(Transform newTarget, FallingObject newTargetObject)
    {
        if (newTargetObject == currentTargetObject)
            return;

        if (newTargetObject != null && newTargetObject.TryReserveForHelper(this))
        {
            ReleaseCurrentTarget();
            currentTarget = newTarget;
            currentTargetObject = newTargetObject;
            stuckTimer = 0f;
            return;
        }

        stuckTimer = 0f;
    }

    private bool CanCollectTarget(FallingObject target)
    {
        return helperController != null && Tool.CanFitForHelperAndEnemies(target.size, helperController.GetFitSize());
    }

    private void MoveToTarget()
    {
        Vector3 targetPoint = GetOvershootPoint(currentTargetObject);
        Vector3 dir = targetPoint - transform.position;
        dir.y = 0;

        float distanceToWaypoint = dir.magnitude;
        if (distanceToWaypoint <= Mathf.Epsilon)
        {
            currentTarget = null;
            currentTargetObject = null;
            return;
        }

        Vector3 moveDir = dir.normalized;
        SmoothVisualRotation(moveDir);

        int level = helperController != null ? helperController.currentLevel : 0;
        float speed = (level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];
        float stepDistance = Mathf.Min(speed * 0.15f * Time.fixedDeltaTime, distanceToWaypoint);

        Vector3 newPosition = rb.position + moveDir * stepDistance;
        ClampToBounds(ref newPosition);
        rb.MovePosition(newPosition);
    }

    private void ClampToBounds(ref Vector3 newPosition)
    {
        if (VodovorotGameManager.Instance == null || VodovorotGameManager.Instance.GamingManager == null)
            return;

        newPosition.x = Mathf.Clamp(newPosition.x,
            VodovorotGameManager.Instance.GamingManager.minX,
            VodovorotGameManager.Instance.GamingManager.maxX);
        newPosition.z = Mathf.Clamp(newPosition.z,
            VodovorotGameManager.Instance.GamingManager.minZ,
            VodovorotGameManager.Instance.GamingManager.maxZ);
    }

    private void CheckStuckStatus()
    {
        stuckTimer += searchInterval;

        if (stuckTimer >= targetHoldTimeout)
        {
            ignoredTarget = currentTarget;
            ignoreCooldown = 0f;
            ReleaseCurrentTarget();
            stuckTimer = 0f;
        }
    }

    private void ReleaseCurrentTarget()
    {
        if (currentTargetObject != null)
            currentTargetObject.ReleaseHelperReservation(this);

        currentTarget = null;
        currentTargetObject = null;
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
        if (helperController == null || target == null)
            return center;

        Vector3 helperSize = helperController.GetFitSize();
        float helperDiameter = Mathf.Max(helperSize.x, helperSize.z);
        float waypointDistance = helperDiameter * 0.2f;
        Vector3 moveDir = (center - transform.position);
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude <= Mathf.Epsilon)
            return center;

        float distanceToCenter = moveDir.magnitude;
        if (distanceToCenter <= waypointDistance)
            return center;

        return transform.position + moveDir.normalized * waypointDistance;
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

    private void OnDisable()
    {
        ReleaseCurrentTarget();
    }

    private void OnDestroy()
    {
        ReleaseCurrentTarget();
    }
}
