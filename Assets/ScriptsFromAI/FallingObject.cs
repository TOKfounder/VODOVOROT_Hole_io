using UnityEngine;
using System.Collections;

public class FallingObject : MonoBehaviour
{
	private const int MaxConvexTriangles = 255;
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
		col = GetComponent<Collider>();
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
			? (config != null ? config.suctionDrag : 0.25f)
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

	private void PrepareSuctionCollider()
	{
		if (preparedMesh)
			return;

		meshCol = col as MeshCollider;
		if (meshCol == null)
			meshCol = GetComponent<MeshCollider>();
		if (meshCol == null || meshCol.convex)
			return;

		preparedMesh = true;
		meshWasConvex = meshCol.convex;
		meshWasEnabled = meshCol.enabled;

		int tris = 0;
		if (meshCol.sharedMesh != null)
			tris = meshCol.sharedMesh.triangles.Length / 3;

		if (tris > 0 && tris <= MaxConvexTriangles)
		{
			meshCol.convex = true;
			col = meshCol;
			return;
		}

		meshCol.enabled = false;
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
		float pull = config != null ? config.suctionPull : 28f;
		float down = config != null ? config.suctionDownForce : 38f;
		Vector3 holePos = CurrentHole.transform.position;
		Vector3 toHole = holePos - rb.position;
		toHole.y = 0f;
		Vector3 force = Vector3.down * down;
		if (toHole.sqrMagnitude > 0.0001f)
			force += toHole.normalized * pull;
		rb.AddForce(force, ForceMode.Acceleration);

		transform.localScale = Vector3.Lerp(transform.localScale, squeezeFrom * VisualSqueeze, 4f * Time.fixedDeltaTime);

		Vector3 vel = rb.velocity;
		bool towardHole = toHole.sqrMagnitude > 0.0001f && Vector3.Dot(vel, toHole.normalized) > 0.12f;
		if (vel.y < -0.12f || towardHole)
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

		if (CurrentHole.holeType == HoleParent.TypeOfHole.enemy
			|| CurrentHole.holeType == HoleParent.TypeOfHole.enemyHelper)
		{
			if (Tool.CanFitForEnemies(size, CurrentHole.size))
			{
				BeginSuctionPhysics(CurrentHole);
				beganSuction = true;
			}
		}
		else if (Tool.CanFit2D(size, CurrentHole.size))
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
