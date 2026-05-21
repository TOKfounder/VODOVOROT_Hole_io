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

    [Header("Follow Settings")]
    public float followSpeed = 8f;
    public float minDistanceToOwner = 3f;

    [Header("Speeds")]
    public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f };

    private Transform currentTarget;
    private Rigidbody rb;
    private HelperController helperController;
    private Transform ownerTransform;

    private Transform ignoredTarget;
    private float ignoreCooldown;
    private float stuckTimer;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        helperController = GetComponentInParent<HelperController>();
        ownerTransform = helperController != null && helperController.Owner != null
            ? helperController.Owner.transform
            : null;

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
        if (ownerTransform == null && helperController != null && helperController.Owner != null)
            ownerTransform = helperController.Owner.transform;

        if (currentTarget != null)
            MoveToTarget();
        else
            FollowOwner();

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

        foreach (var hit in hitColliders)
        {
            if (hit.transform == ignoredTarget) continue;

            var fo = hit.GetComponentInParent<FallingObject>();
            if (fo == null) continue;

            if (!CanCollectTarget(fo)) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
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

    private bool CanCollectTarget(FallingObject target)
    {
        return helperController != null && Tool.CanFit2D(target.size, helperController.size);
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
        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        if (withoutCamera != null)
        {
            withoutCamera.transform.rotation = Quaternion.Slerp(
                withoutCamera.transform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime);
        }

        int level = helperController != null ? helperController.currentLevel : 0;
        float speed = (level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];

        Vector3 newPosition = rb.position + moveDir * speed * 0.15f * Time.fixedDeltaTime;
        ClampToBounds(ref newPosition);
        rb.MovePosition(newPosition);
    }

    private void FollowOwner()
    {
        if (ownerTransform == null)
            return;

        Vector3 direction = ownerTransform.position - transform.position;
        direction.y = 0f;

        if (direction.magnitude <= minDistanceToOwner)
            return;

        Vector3 moveDir = direction.normalized;
        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
        if (withoutCamera != null)
        {
            withoutCamera.transform.rotation = Quaternion.Slerp(
                withoutCamera.transform.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime);
        }

        rb.MovePosition(rb.position + moveDir * followSpeed * Time.fixedDeltaTime);
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

        if (stuckTimer >= 3.5f)
        {
            ignoredTarget = currentTarget;
            ignoreCooldown = 0f;
            currentTarget = null;
            stuckTimer = 0f;
        }
    }
}
