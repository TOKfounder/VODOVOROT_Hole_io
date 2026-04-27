using UnityEngine;
using System.Collections;

public class FallingObject : MonoBehaviour
{
	public int value;
	public Vector3 size;
	public float V3;
	public Renderer rend;
	public bool isTriggered = false;
	public bool isColon = false;

	private Vector3 startPosition;
	private Quaternion startRotation;
	private Rigidbody rb;
	private Collider col;
	private Coroutine myCoroutine;
	private float lastHoleChangeTime = 0f;

	public HoleParent CurrentHole { get; set; }

	private float lastPlatformUpdateTime = 0f;

	void Awake()
	{
		col = GetComponent<Collider>();
		if (col == null)
		{
			col = gameObject.AddComponent<BoxCollider>();
		}
		rb = GetComponent<Rigidbody>();
		if (rb == null)
		{
			rb = gameObject.AddComponent<Rigidbody>();
		}
		rb.isKinematic = true;
		rend = GetComponent<Renderer>();
		if (rend == null)
		{
			Destroy(GetComponent<FallingObject>());
		}
	}

	void Start()
	{
		gameObject.layer = 7;
		foreach (var plat in VodovorotGameManager.allPlatforms)
		{
			Physics.IgnoreCollision(plat, col, true);
		}
		size = GetVisualSize();
		V3 = size.x * size.y * size.z;
		startPosition = GetComponent<Transform>().position;
		startRotation = GetComponent<Transform>().rotation;
		// Physics.IgnoreLayerCollision(7, 0, true);
		if (V3 <= 0.087f)
		{
			value = 1;
		}
		else if (V3 <= 0.51f)
		{
			value = 2;
		}
		else if (V3 <= 10.63f)
		{
			value = 3;
		}
		else if (V3 <= 20f)
		{
			value = 5;
		}
		else if (V3 <= 60f)
		{
			value = 10;
		}
		else if (V3 <= 100f)
		{
			value = 25;
		}
		else if (V3 <= 250f)
		{
			value = 40;
		}
		else if (V3 <= 860f)
		{
			value = 60;
		}
		else
		{
			value = 100;
		}
		rb.mass = V3 * 50;
		rb.linearDamping = 4;
		rb.angularDamping = 4;
		VodovorotGameManager.Instance.GamingManager.AllValues += value;
	}

	IEnumerator DelayForUpdateCurrentHole()
	{
		yield return new WaitForSeconds(4f);
		if (!rb.isKinematic)
			ResetToStart();
	}

private void OnTriggerEnter(Collider other)
{
    if (!other.CompareTag("Player")) return;

    HoleParent newHole = other.GetComponentInParent<HoleParent>();
    if (newHole == null) return;

    // Жёсткая защита от слишком частой смены
    if (Time.time - lastPlatformUpdateTime < 0.25f) return;

    Debug.Log($"[FallingObject] Триггер с дырой: {newHole.name} | CurrentHole = {(CurrentHole ? CurrentHole.name : "null")}");

    // Смена дыры
    if (CurrentHole != null)
        Physics.IgnoreCollision(CurrentHole.platform, col, true);

    CurrentHole = newHole;
    lastPlatformUpdateTime = Time.time;

    // Жёсткое обновление коллизий
    ForceUpdatePlatformCollisions();

    // Перезапускаем таймер падения
    if (!isColon)
    {
        if (myCoroutine != null) StopCoroutine(myCoroutine);
        myCoroutine = StartCoroutine(DelayForUpdateCurrentHole());
    }

    // Активируем падение
    bool canBeEaten = (CurrentHole.holeType == HoleParent.TypeOfHole.enemy)
        ? Tool.CanFitForEnemies(size, CurrentHole.size)
        : Tool.CanFit2D(size, CurrentHole.size);

    if (canBeEaten)
    {
        isTriggered = true;
        rb.isKinematic = false;
        CurrentHole.nearbyFallingObjects.Add(this);
        Debug.Log($"[FallingObject] Активировано падение в {CurrentHole.name}");
    }
}

	private Vector3 GetVisualSize()
	{
		Bounds totalBounds = new Bounds(transform.position, Vector3.zero);
		Collider collider = GetComponent<Collider>();
		if (collider != null && collider.enabled)
		{
			totalBounds.Encapsulate(collider.bounds);
			return totalBounds.size;
		}
		Renderer renderer = GetComponent<Renderer>();
		if (renderer != null && renderer.enabled)
		{
			totalBounds.Encapsulate(renderer.bounds);
		}
		return totalBounds.size;
	}


	public void ResetToStart()
	{
		transform.position = startPosition;
		transform.rotation = startRotation;
		rb.isKinematic = true;
		isTriggered = false;
		col.enabled = true;
		rend.enabled = true;
		CurrentHole = null;
		foreach (var plat in VodovorotGameManager.allPlatforms)
		{
			Physics.IgnoreCollision(plat, col, true);
		}
		if (myCoroutine != null) StopCoroutine(myCoroutine);

		ForceUpdatePlatformCollisions();
	}

	public void OnScored(HoleParent hole)
	{
		hole.AddScore(value);
		value = 0;

		rb.isKinematic = true;
		col.enabled = false;
		rend.enabled = false;
		CurrentHole = null;

		if (myCoroutine != null) StopCoroutine(myCoroutine);

		// Если это колón и его съел игрок — спавним Helper
		if (isColon && hole is BlackHoleController playerHole)
		{
			playerHole.OnColonAbsorbed(transform);
		}
	}

    /// <summary>
    /// ЖЁСТКОЕ обновление коллизий — вызывается каждый раз при смене дыры
    /// </summary>
    private void ForceUpdatePlatformCollisions()
    {
        if (CurrentHole == null || CurrentHole.platform == null)
        {
            Debug.LogWarning("[FallingObject] CurrentHole или platform == null");
            return;
        }

        // Игнорируем ВСЕ платформы
        foreach (var plat in VodovorotGameManager.allPlatforms)
        {
            if (plat == null) continue;
            Physics.IgnoreCollision(plat, col, true);
        }

        // Включаем коллизию только с нужной платформой
        Physics.IgnoreCollision(CurrentHole.platform, col, false);

        Debug.Log($"[FallingObject] ЖЁСТКОЕ обновление: Игнорируем все платформы кроме {CurrentHole.platform.name}");
    }

}