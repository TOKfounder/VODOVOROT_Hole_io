using UnityEngine;
using System.Collections;

public class FallingObject : MonoBehaviour
{
	private const float IdleResetSeconds = 4f;
	private const float VisualSqueeze = 0.38f;

	public int value;
	public Vector3 size;
	protected float V3;
	public Renderer rend;
	public bool isTriggered = false;
	public bool isColon = false;

	private Vector3 startPosition;
	private Quaternion startRotation;
	private Vector3 startScale = Vector3.one;
	private Vector3 squeezeFrom = Vector3.one;
	protected Rigidbody rb;
	protected Collider col;
	private Coroutine myCoroutine;
	private bool suctionStillMoving;
	private MeshCollider meshCol;
	private bool meshWasConvex;
	private bool meshWasEnabled;
	private bool preparedMesh;
	private BoxCollider suctionBox;
	private static Collider cachedGround;

	public HoleParent CurrentHole { get; set; }

	protected virtual bool AssignValueFromVolume => true;
	protected virtual bool CountsTowardMapTotal => true;
	protected virtual bool ResetsIfNotScored => true;
	protected virtual bool IgnoresMapOnStart => true;
	protected virtual bool NeedsRigidbodyAtStart => false;
	protected virtual int ObjectLayer => 7;

	void Awake()
	{
		CacheEnabledCollider();
		if (col == null)
			col = gameObject.AddComponent<BoxCollider>();
		rb = GetComponent<Rigidbody>();
		if (NeedsRigidbodyAtStart)
			EnsureFallingBody(true);
		rend = GetComponent<Renderer>();
		if (rend == null)
			Destroy(GetComponent<FallingObject>());
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
		startScale = transform.localScale;
		if (AssignValueFromVolume)
			AssignDefaultValue();
		if (rb != null)
			ApplyBodyTuning(false);
		if (CountsTowardMapTotal && value > 1 && GamingManager.Instance != null)
			GamingManager.Instance.AllValues += value;
	}

	protected void EnsureFallingBody(bool kinematic)
	{
		if (rb == null)
			rb = GetComponent<Rigidbody>();
		if (rb == null)
			rb = gameObject.AddComponent<Rigidbody>();
		ApplyBodyTuning(!kinematic);
		rb.isKinematic = kinematic;
		if (!kinematic)
			rb.useGravity = true;
	}

	protected void ApplyBodyTuning(bool suction)
	{
		if (rb == null)
			return;

		GameBalanceConfig config = GameBalance.Current;
		float minMass = config != null ? config.suctionMassMin : 0.4f;
		float maxMass = config != null ? config.suctionMassMax : 3f;
		float drag = suction
			? (config != null ? config.suctionDrag : 0.8f)
			: 4f;
		rb.mass = Mathf.Clamp(V3 > 0f ? V3 * 0.35f : minMass, minMass, maxMass);
		rb.drag = drag;
		rb.angularDrag = suction ? 0.4f : 4f;
	}

	protected void BeginSuctionPhysics(HoleParent hole)
	{
		CurrentHole = hole;
		isTriggered = true;
		squeezeFrom = transform.localScale;
		PrepareSuctionCollider();
		EnsureFallingBody(false);
		IgnoreMapPlatforms();
		IgnorePlayableGround();
		if (hole != null && !hole.nearbyFallingObjects.Contains(this))
			hole.nearbyFallingObjects.Add(this);
	}

	private void CacheEnabledCollider()
	{
		Collider[] cols = GetComponents<Collider>();
		col = null;
		for (int i = 0; i < cols.Length; i++)
		{
			Collider candidate = cols[i];
			if (candidate == null || !candidate.enabled)
				continue;
			MeshCollider mesh = candidate as MeshCollider;
			if (mesh != null && !mesh.convex)
				continue;
			col = candidate;
			return;
		}
	}

	private void PrepareSuctionCollider()
	{
		if (preparedMesh)
			return;

		meshCol = GetComponent<MeshCollider>();
		if (meshCol == null || meshCol.convex)
		{
			CacheEnabledCollider();
			return;
		}

		preparedMesh = true;
		meshWasConvex = meshCol.convex;
		meshWasEnabled = meshCol.enabled;
		meshCol.enabled = false;

		if (suctionBox == null)
			suctionBox = gameObject.AddComponent<BoxCollider>();
		Bounds world = rend != null ? rend.bounds : meshCol.bounds;
		suctionBox.center = transform.InverseTransformPoint(world.center);
		Vector3 lossy = transform.lossyScale;
		suctionBox.size = new Vector3(
			SafeDiv(world.size.x, lossy.x),
			SafeDiv(world.size.y, lossy.y),
			SafeDiv(world.size.z, lossy.z));
		col = suctionBox;
	}

	private static float SafeDiv(float value, float scale)
	{
		return Mathf.Abs(scale) > 0.0001f ? value / Mathf.Abs(scale) : value;
	}

	private void RestoreSuctionCollider()
	{
		if (suctionBox != null)
		{
			Destroy(suctionBox);
			suctionBox = null;
		}

		if (meshCol != null && preparedMesh)
		{
			meshCol.convex = meshWasConvex;
			meshCol.enabled = meshWasEnabled;
			col = meshCol;
		}

		preparedMesh = false;
	}

	private void ReleaseFallingBody()
	{
		RestoreSuctionCollider();
		if (NeedsRigidbodyAtStart)
		{
			if (rb != null)
			{
				rb.isKinematic = true;
				rb.useGravity = false;
				ApplyBodyTuning(false);
			}
			return;
		}

		if (rb == null)
			return;
		Destroy(rb);
		rb = null;
	}

	void FixedUpdate()
	{
		if (!isTriggered || rb == null || rb.isKinematic || CurrentHole == null)
			return;

		GameBalanceConfig config = GameBalance.Current;
		float pull = config != null ? config.suctionPull : 13f;
		float down = config != null ? config.suctionDownForce : 17f;
		float orbit = config != null ? config.suctionOrbit : 10f;
		float inward = config != null ? config.suctionOrbitInward : 4f;
		float orbitDown = config != null ? config.suctionOrbitDown : 6f;
		float rimFactor = config != null ? config.suctionRimFactor : 0.55f;
		float squeezeSpeed = config != null ? config.suctionSqueezeSpeed : 1.2f;

		Vector3 toHole = CurrentHole.transform.position - rb.position;
		toHole.y = 0f;
		float dist = toHole.magnitude;
		float rim = CurrentHole.GetStableHoleRadius() * rimFactor;
		bool atRim = dist > rim;
		Vector3 force;
		if (atRim && dist > 0.0001f)
		{
			Vector3 radial = toHole / dist;
			Vector3 tangent = Vector3.Cross(Vector3.up, radial);
			force = tangent * orbit + radial * inward + Vector3.down * orbitDown;
		}
		else
		{
			force = Vector3.down * down;
			if (dist > 0.0001f)
				force += (toHole / dist) * pull;
		}
		rb.AddForce(force, ForceMode.Acceleration);

		transform.localScale = Vector3.Lerp(transform.localScale, squeezeFrom * VisualSqueeze, squeezeSpeed * Time.fixedDeltaTime);

		Vector3 vel = rb.velocity;
		Vector3 velXZ = new Vector3(vel.x, 0f, vel.z);
		bool towardHole = dist > 0.0001f && Vector3.Dot(velXZ, toHole) > 0f;
		if (vel.y < -0.08f || towardHole || velXZ.sqrMagnitude > 0.014f)
			suctionStillMoving = true;
	}

	IEnumerator DelayForUpdateCurrentHole()
	{
		float idle = 0f;
		suctionStillMoving = false;
		while (idle < IdleResetSeconds)
		{
			yield return null;
			if (!isTriggered)
				yield break;
			if (suctionStillMoving)
			{
				idle = 0f;
				suctionStillMoving = false;
			}
			else
				idle += Time.deltaTime;
		}

		if (rb != null && !rb.isKinematic)
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

		if (Tool.CanFitFootprint(size, CurrentHole.size))
		{
			BeginSuctionPhysics(CurrentHole);
			beganSuction = true;
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
			totalBounds.Encapsulate(renderer.bounds);
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

	protected void IgnorePlayableGround()
	{
		if (col == null)
			return;
		if (cachedGround == null)
		{
			GameObject ground = GameObject.Find("MapPlayableGround");
			if (ground != null)
				cachedGround = ground.GetComponent<Collider>();
		}
		if (cachedGround != null)
			Physics.IgnoreCollision(cachedGround, col, true);
	}

	public virtual void ResetToStart()
	{
		transform.position = startPosition;
		transform.rotation = startRotation;
		if (startScale.sqrMagnitude > 0.0001f)
			transform.localScale = startScale;
		ReleaseFallingBody();
		isTriggered = false;
		if (col != null)
			col.enabled = true;
		if (rend != null)
			rend.enabled = true;
		CurrentHole = null;
		IgnoreMapPlatforms();
		if (myCoroutine != null) StopCoroutine(myCoroutine);
	}

	public virtual void OnScored(HoleParent hole)
	{
		int gained = value;
		hole.AddScore(gained);
		if (gained > 1 && hole is BlackHoleController)
			GamingManager.Instance?.AddProgressScore(gained);
		value = 0;

		RestoreSuctionCollider();
		if (startScale.sqrMagnitude > 0.0001f)
			transform.localScale = startScale;
		if (rb != null)
			rb.isKinematic = true;
		if (col != null)
			col.enabled = false;
		if (rend != null)
			rend.enabled = false;
		CurrentHole = null;
		if (myCoroutine != null) StopCoroutine(myCoroutine);
	}
}
