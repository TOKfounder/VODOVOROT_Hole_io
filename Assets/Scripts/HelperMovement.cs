using UnityEngine;

public class HelperMovement : HoleCollectorMovement
{
    [Header("References")]
    public GameObject withoutCamera;

    [Header("Movement Settings")]
    public float rotationSpeed = 30f;
    public float detectionRadius = 500f;
    public float searchInterval = 0.5f;
    public LayerMask fallableObjects;

    [Header("Follow Settings")]
    public float minDistanceToOwner = 3f;

    [Header("Speeds")]
    public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f };

    private HelperController helperController;

    protected override GameObject MovementRoot => withoutCamera != null ? withoutCamera : gameObject;
    protected override float RotationSpeed => rotationSpeed;
    protected override float DetectionRadius => detectionRadius;
    protected override float SearchInterval => searchInterval;
    protected override LayerMask FallableObjects => fallableObjects;
    protected override float[] LevelSpeeds => levelSpeeds;
    protected override HoleParent ControlledHole => helperController;

    protected override void Start()
    {
        helperController = GetComponentInParent<HelperController>();
        base.Start();
    }

    protected override bool CanCollectTarget(FallingObject target)
    {
        return helperController != null && Tool.CanFit2D(target.size, helperController.size);
    }

    protected override void HandleIdleMovement()
    {
        if (helperController == null || helperController.Owner == null)
            return;

        Vector3 direction = helperController.Owner.transform.position - transform.position;
        direction.y = 0f;

        if (direction.magnitude > minDistanceToOwner)
            MoveInDirection(direction);
    }
}
