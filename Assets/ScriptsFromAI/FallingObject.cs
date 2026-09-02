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
	protected Rigidbody rb;
	protected Collider col;
	private Coroutine myCoroutine;

	public HoleParent CurrentHole { get; set; }

	protected virtual bool AssignValueFromVolume => true;
	protected virtual bool CountsTowardMapTotal => true;
	protected virtual bool ResetsIfNotScored => true;
	protected virtual bool IgnoresMapOnStart => true;
	protected virtual int ObjectLayer => 7;

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
		gameObject.layer = ObjectLayer;
		if (IgnoresMapOnStart)
			IgnoreMapPlatforms();
		size = GetVisualSize();
		V3 = size.x * size.y * size.z;
		startPosition = transform.position;
		startRotation = transform.rotation;
		if (AssignValueFromVolume)
			AssignDefaultValue();
		rb.mass = Mathf.Max(0.1f, V3 * 50f);
		rb.drag = 4;
		rb.angularDrag = 4;
		if (CountsTowardMapTotal && GamingManager.Instance != null)
			GamingManager.Instance.AllValues += value;
	}

	IEnumerator DelayForUpdateCurrentHole()
	{
		yield return new WaitForSeconds(4f);
		if (!rb.isKinematic)
			ResetToStart();
	}

	protected virtual void OnTriggerEnter(Collider other)
	{
		TryBeginFall(other);
	}

	protected virtual void TryBeginFall(Collider other)
	{
		if (!other.CompareTag("Player"))
			return;

		HoleParent otherHole = other.GetComponentInParent<HoleParent>();
		if (otherHole == null)
			return;

		if (isTriggered)
		{
			if (CurrentHole == null || otherHole != CurrentHole)
			{
				if (CurrentHole != null && CurrentHole.platform != null && col != null)
					Physics.IgnoreCollision(CurrentHole.platform, col, true);
				isTriggered = false;
				CurrentHole = otherHole;
				if (CurrentHole.platform != null && col != null)
					Physics.IgnoreCollision(CurrentHole.platform, col, false);
			}
			else
				return;
		}
		else
		{
			CurrentHole = otherHole;
			if (CurrentHole.platform != null && col != null)
				Physics.IgnoreCollision(CurrentHole.platform, col, false);
		}

		bool beganSuction = false;

		if (!isColon && ResetsIfNotScored)
		{
			if (myCoroutine != null) StopCoroutine(myCoroutine);
			myCoroutine = StartCoroutine(DelayForUpdateCurrentHole());
		}

		if (CurrentHole.holeType == HoleParent.TypeOfHole.enemy
			|| CurrentHole.holeType == HoleParent.TypeOfHole.enemyHelper)
		{
			if (Tool.CanFitForEnemies(size, CurrentHole.size))
			{
				isTriggered = true;
				beganSuction = true;
				rb.isKinematic = false;
				if (!CurrentHole.nearbyFallingObjects.Contains(this))
					CurrentHole.nearbyFallingObjects.Add(this);
			}
		}
		else
		{
			if (Tool.CanFit2D(size, CurrentHole.size))
			{
				isTriggered = true;
				beganSuction = true;
				rb.isKinematic = false;
				if (!CurrentHole.nearbyFallingObjects.Contains(this))
					CurrentHole.nearbyFallingObjects.Add(this);
			}
		}

		if (beganSuction)
			OnSuctionBegan(CurrentHole);
	}

	protected virtual void OnSuctionBegan(HoleParent hole)
	{
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


	private void AssignDefaultValue()
	{
		if (V3 <= 0.087f)
			value = 1;
		else if (V3 <= 0.51f)
			value = 2;
		else if (V3 <= 10.63f)
			value = 3;
		else if (V3 <= 20f)
			value = 5;
		else if (V3 <= 60f)
			value = 10;
		else if (V3 <= 100f)
			value = 25;
		else if (V3 <= 250f)
			value = 40;
		else if (V3 <= 860f)
			value = 60;
		else
			value = 100;
	}

	protected void IgnoreMapPlatforms()
	{
		foreach (var plat in GamingManager.allPlatforms)
		{
			if (plat == null || col == null)
				continue;
			Physics.IgnoreCollision(plat, col, true);
		}
	}

	public virtual void ResetToStart()
	{
		transform.position = startPosition;
		transform.rotation = startRotation;
		rb.isKinematic = true;
		isTriggered = false;
		col.enabled = true;
		rend.enabled = true;
		CurrentHole = null;
		IgnoreMapPlatforms();
		if (myCoroutine != null) StopCoroutine(myCoroutine);
	}

	public virtual void OnScored(HoleParent hole)
	{
		hole.AddScore(value);
		value = 0;

		rb.isKinematic = true;
		col.enabled = false;
		rend.enabled = false;
		CurrentHole = null;
		if (myCoroutine != null) StopCoroutine(myCoroutine);

		if (isColon && SpawnerOfHelpers.ColonEnabled && hole is BlackHoleController)
		{
			SpawnerOfHelpers spawner = FindAnyObjectByType<SpawnerOfHelpers>();
			spawner?.TrySpawnHelper(transform);
		}
	}
}