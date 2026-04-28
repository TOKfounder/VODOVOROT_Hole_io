using UnityEngine;

public class HelperController : HoleParent
{
    [Header("Настройки Helper")]
    public float followSpeed = 8f;           // скорость движения за игроком
    public float minDistanceToPlayer = 3f;   // минимальное расстояние до игрока

    private BlackHoleController player;
    private Rigidbody rb;

    public override void Start()
    {
        base.Start();
        holeType = TypeOfHole.playerHelper;

        rb = GetComponent<Rigidbody>();
        player = FindAnyObjectByType<BlackHoleController>();

        // Helpers не должны иметь бордер и nickname
        if (border != null) border.gameObject.SetActive(false);
        if (nickname != null) nickname.gameObject.SetActive(false);
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        if (player == null) return;

        Vector3 direction = player.transform.position - transform.position;
        float distance = direction.magnitude;

        if (distance > minDistanceToPlayer)
        {
            Vector3 moveDir = direction.normalized;
            rb.MovePosition(rb.position + moveDir * followSpeed * Time.fixedDeltaTime);
        }
    }
}
