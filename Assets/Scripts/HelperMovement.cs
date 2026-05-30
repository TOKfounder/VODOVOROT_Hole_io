using UnityEngine;
using System.Collections;

public class HelperMovement : MonoBehaviour
{
    [Header("References")]
    public GameObject withoutCamera;

    [Header("Movement Settings")]
    public float rotationSpeed = 0.1f;
    public float detectionRadius = 500f;
    public float searchInterval = 1.25f;
    public LayerMask fallableObjects;

    [Header("Target Switching")]
    public float targetHoldTimeout = 4f;

    [Header("Aim Settings")]
    public float visualTurnSpeed = 240f;

    [Header("Debug")]
    public bool debugMovement = false;

    [Header("Speeds")]
    public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f };

    private Transform currentTarget;
    private FallingObject currentTargetObject;
    private Rigidbody rb;
    private HelperController helperController;
    private Vector3 currentWaypoint;
    private bool hasWaypoint;
    private bool isPassingThroughTarget;
    private bool passThroughWaypointExtended;
    private float lastWaypointDistance = Mathf.Infinity;

    private Transform ignoredTarget;
    private float ignoreCooldown;
    private float stuckTimer;
    private int searchTick;

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
            searchTick++;
            if (debugMovement)
                Debug.Log($"[HelperMovement] search tick #{searchTick} on {name}", this);

            FindClosestObject();
            yield return new WaitForSeconds(searchInterval);
        }
    }

    private void FixedUpdate()
    {
        if (currentTargetObject != null && !currentTargetObject.HasHelperReservation(this))
        {
            if (debugMovement)
                Debug.Log($"[HelperMovement] lost reservation, releasing target on {name}", this);
            ReleaseCurrentTarget();
        }

        if (currentTarget != null)
        {
            UpdateLockedWaypointIfNeeded();
            MoveToTarget();
            UpdateWaypointProgress();
        }
        else
        {
            currentTargetObject = null;
            hasWaypoint = false;
            isPassingThroughTarget = false;
            passThroughWaypointExtended = false;
            lastWaypointDistance = Mathf.Infinity;
        }

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
        if (currentTargetObject != null)
            return;

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

        if (debugMovement)
        {
            string bestName = bestTargetObject != null ? bestTargetObject.name : "none";
            Debug.Log($"[HelperMovement] search result on {name}: best={bestName}, dist={closestDist:0.00}, target={(bestTargetObject != null ? "yes" : "no")}", this);
        }

        SwitchTarget(bestTarget, bestTargetObject);
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
            currentWaypoint = GetWaypointForTarget(newTargetObject);
            hasWaypoint = true;
            isPassingThroughTarget = false;
            passThroughWaypointExtended = false;
            lastWaypointDistance = Vector3.Distance(rb.position, currentWaypoint);
            stuckTimer = 0f;

            if (debugMovement)
            {
                Vector3 center = GetTargetCenter(newTargetObject);
                Debug.Log(
                    $"[HelperMovement] target locked on {name}: target={newTargetObject.name}, centerDist={Vector3.Distance(transform.position, center):0.00}, waypoint={currentWaypoint}, waypointDist={lastWaypointDistance:0.00}",
                    this);
            }
            return;
        }

        if (debugMovement)
            Debug.Log($"[HelperMovement] failed to reserve target on {name}", this);

        stuckTimer = 0f;
    }

    private bool CanCollectTarget(FallingObject target)
    {
        return helperController != null && Tool.CanFitForHelperAndEnemies(target.size, helperController.GetFitSize());
    }

    private void MoveToTarget()
    {
        Vector3 targetPoint = hasWaypoint ? currentWaypoint : GetWaypointForTarget(currentTargetObject);
        Vector3 dir = targetPoint - transform.position;
        dir.y = 0;

        if (dir.magnitude <= GetStopDistance(currentTargetObject))
        {
            if (debugMovement)
            {
                Debug.Log(
                    $"[HelperMovement] stop on {name}: target={currentTargetObject.name}, waypointDist={dir.magnitude:0.00}, stopDist={GetStopDistance(currentTargetObject):0.00}, passThrough={isPassingThroughTarget}",
                    this);
            }

            currentTarget = null;
            currentTargetObject = null;
            hasWaypoint = false;
            lastWaypointDistance = Mathf.Infinity;
            return;
        }

        Vector3 moveDir = dir.normalized;
        SmoothVisualRotation(moveDir);

        int level = helperController != null ? helperController.currentLevel : 0;
        float speed = (level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];

        Vector3 newPosition = rb.position + moveDir * speed * 0.15f * Time.fixedDeltaTime;
        ClampToBounds(ref newPosition);
        rb.MovePosition(newPosition);

        if (debugMovement)
        {
            Debug.Log(
                $"[HelperMovement] move on {name}: target={currentTargetObject.name}, pos={rb.position}, waypoint={targetPoint}, dir={dir.magnitude:0.00}, speed={speed:0.00}",
                this);
        }
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

    private void ReleaseCurrentTarget()
    {
        if (currentTargetObject != null)
        {
            if (debugMovement)
                Debug.Log($"[HelperMovement] release target on {name}: {currentTargetObject.name}", this);

            currentTargetObject.ReleaseHelperReservation(this);
        }

        currentTarget = null;
        currentTargetObject = null;
        hasWaypoint = false;
        isPassingThroughTarget = false;
        passThroughWaypointExtended = false;
        lastWaypointDistance = Mathf.Infinity;
    }

    private void UpdateLockedWaypointIfNeeded()
    {
        if (!hasWaypoint || passThroughWaypointExtended || currentTargetObject == null || helperController == null)
            return;

        Vector3 center = GetTargetCenter(currentTargetObject);
        Vector3 moveDir = center - transform.position;
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector3 helperSize = helperController.size;
        float helperDiameter = Mathf.Max(helperSize.x, helperSize.z);

        if (moveDir.magnitude > helperDiameter * 2f)
            return;

        currentWaypoint = center + moveDir.normalized * helperDiameter * 2f;
        isPassingThroughTarget = true;
        passThroughWaypointExtended = true;
        lastWaypointDistance = Vector3.Distance(rb.position, currentWaypoint);
        stuckTimer = 0f;

        if (debugMovement)
        {
            Debug.Log(
                $"[HelperMovement] pass-through enabled on {name}: target={currentTargetObject.name}, centerDist={moveDir.magnitude:0.00}, helperDiameter={helperDiameter:0.00}, newWaypoint={currentWaypoint}",
                this);
        }
    }

    private Vector3 GetTargetCenter(FallingObject target)
    {
        if (target != null && target.rend != null)
            return target.rend.bounds.center;

        return target != null ? target.transform.position : transform.position;
    }

    private Vector3 GetWaypointForTarget(FallingObject target)
    {
        Vector3 center = GetTargetCenter(target);
        if (helperController == null || target == null)
            return center;

        Vector3 moveDir = (center - transform.position);
        moveDir.y = 0f;
        if (moveDir.sqrMagnitude <= Mathf.Epsilon)
            return center;

        return center;
    }

    private float GetStopDistance(FallingObject target)
    {
        if (helperController == null || target == null)
            return transform.localScale.x * 0.25f;

        Vector3 helperSize = helperController.size;
        float helperDiameter = Mathf.Max(helperSize.x, helperSize.z);
        float targetRadius = Mathf.Max(target.size.x, target.size.z) * 0.5f;

        if (isPassingThroughTarget)
            return helperDiameter * 0.2f;

        // До пересечения центра helper должен продолжать идти к цели,
        // а не останавливаться заранее.
        return Mathf.Max(helperDiameter * 0.05f, targetRadius * 0.05f);
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

    private void UpdateWaypointProgress()
    {
        if (!hasWaypoint)
            return;

        float currentDistance = Vector3.Distance(rb.position, currentWaypoint);

        if (currentDistance + 0.01f < lastWaypointDistance)
        {
            stuckTimer = 0f;
        }
        else
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= targetHoldTimeout)
            {
                ignoredTarget = currentTarget;
                ignoreCooldown = 0f;
                ReleaseCurrentTarget();
                stuckTimer = 0f;
                return;
            }
        }

        lastWaypointDistance = currentDistance;

        if (isPassingThroughTarget && currentTargetObject != null && helperController != null)
        {
            Vector3 center = GetTargetCenter(currentTargetObject);
            Vector3 forward = (currentWaypoint - center);
            forward.y = 0f;
            if (forward.sqrMagnitude > Mathf.Epsilon)
            {
                float helperDiameter = Mathf.Max(helperController.size.x, helperController.size.z);
                float passedDistance = Vector3.Dot(rb.position - center, forward.normalized);
                if (passedDistance >= helperDiameter * 2f)
                {
                    ignoredTarget = currentTarget;
                    ignoreCooldown = 0f;

                    if (debugMovement)
                    {
                        Debug.Log(
                            $"[HelperMovement] passed target on {name}: target={currentTargetObject.name}, passedDistance={passedDistance:0.00}, threshold={(helperDiameter * 2f):0.00}",
                            this);
                    }

                    ReleaseCurrentTarget();
                }
            }
        }
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
