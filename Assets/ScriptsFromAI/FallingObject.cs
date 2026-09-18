using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class FallingObject : MonoBehaviour
{
	private const float IdleResetSeconds = 4f;
	private const float VisualSqueeze = 0.38f;
	private const float GulpMoveSpeed = 4.5f;
	private const float DefaultAttractMul = 1.1f;
	private const float DefaultLiftDiameterFactor = 0.3f;

	private enum SuctionPhase
	{
		Slide,
		Lift,
		Drop
	}

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
	private static bool sceneHooked;
	public static readonly List<FallingObject> Active = new List<FallingObject>(512);
	private SuctionPhase suctionPhase;
	private bool gulpArmed;

	public HoleParent CurrentHole { get; set; }
	public bool IsAirborneGulp => isTriggered && suctionPhase == SuctionPhase.Lift;

	protected virtual bool AssignValueFromVolume => true;
	protected virtual bool CountsTowardMapTotal => true;
	protected virtual bool ResetsIfNotScored => true;
	protected virtual bool IgnoresMapOnStart => true;
	protected virtual bool NeedsRigidbodyAtStart => false;
	protected virtual int ObjectLayer => 7;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticState()
	{
		cachedGround = null;
		sceneHooked = false;
		Active.Clear();
	}

	private static void EnsureSceneHook()
	{
		if (sceneHooked)
			return;
		sceneHooked = true;
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		cachedGround = null;
	}

	void Awake()
	{
		EnsureSceneHook();
		CacheEnabledCollider();
		if (col == null)
			col = gameObject.AddComponent<BoxCollider>();
		rb = GetComponent<Rigidbody>();
		if (NeedsRigidbodyAtStart)
			EnsureFallingBody(true);
		rend = GetComponent<Renderer>();
		if (rend == null)
			rend = GetComponentInChildren<Renderer>();
		if (rend == null)
			Destroy(GetComponent<FallingObject>());
	}

	void OnEnable()
	{
		if (!Active.Contains(this))
			Active.Add(this);
	}

	void OnDisable()
	{
		Active.Remove(this);
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
		suctionPhase = SuctionPhase.Slide;
		gulpArmed = false;
		squeezeFrom = transform.localScale;
		PrepareSuctionCollider();
		EnsureFallingBody(false);
		IgnoreMapPlatforms();
		IgnorePlayableGround();
		AttachToHoleList(hole);
		BeginGulp();
	}

	private void BindToHole(HoleParent hole)
	{
		if (CurrentHole != null && CurrentHole != hole)
		{
			if (CurrentHole.platform != null && col != null)
				Physics.IgnoreCollision(CurrentHole.platform, col, true);
			CurrentHole.nearbyFallingObjects.Remove(this);
		}

		CurrentHole = hole;
		if (hole != null && hole.platform != null && col != null)
			Physics.IgnoreCollision(hole.platform, col, false);
	}

	private void AttachToHoleList(HoleParent hole)
	{
		if (hole != null && !hole.nearbyFallingObjects.Contains(this))
			hole.nearbyFallingObjects.Add(this);
	}

	private void DetachFromHoleList()
	{
		if (CurrentHole != null)
			CurrentHole.nearbyFallingObjects.Remove(this);
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
		if (!isTriggered || rb == null || CurrentHole == null)
			return;
		if (CurrentHole.IsConsumed || !CurrentHole.isActiveAndEnabled)
		{
			ResetToStart();
			return;
		}

		GameBalanceConfig config = GameBalance.Current;
		float down = config != null ? config.suctionDownForce : 17f;
		float squeezeSpeed = config != null ? config.suctionSqueezeSpeed : 1.2f;
		float attractMul = config != null ? config.suctionAttractRadius : DefaultAttractMul;

		Vector3 holePos = CurrentHole.transform.position;
		Vector3 toHole = holePos - rb.position;
		toHole.y = 0f;
		float dist = toHole.magnitude;
		float attractR = CurrentHole.GetStableHoleRadius() * attractMul;

		if (suctionPhase == SuctionPhase.Slide)
			BeginGulp();

		if (suctionPhase == SuctionPhase.Lift)
			StepGulp();
		else if (suctionPhase == SuctionPhase.Drop && !rb.isKinematic)
		{
			float dt = Time.fixedDeltaTime;
			Vector3 vel = rb.velocity;
			float nextX = Mathf.MoveTowards(rb.position.x, holePos.x, GulpMoveSpeed * dt);
			float nextZ = Mathf.MoveTowards(rb.position.z, holePos.z, GulpMoveSpeed * dt);
			if (dt > 0.0001f)
			{
				vel.x = (nextX - rb.position.x) / dt;
				vel.z = (nextZ - rb.position.z) / dt;
			}
			rb.velocity = vel;
			rb.AddForce(Vector3.down * down, ForceMode.Acceleration);
		}

		if (suctionPhase != SuctionPhase.Slide)
			transform.localScale = Vector3.Lerp(transform.localScale, squeezeFrom * VisualSqueeze, squeezeSpeed * Time.fixedDeltaTime);

		if (IsAirborneGulp || suctionPhase == SuctionPhase.Drop || (suctionPhase == SuctionPhase.Slide && dist <= attractR))
			suctionStillMoving = true;
		else if (!rb.isKinematic)
		{
			Vector3 vel = rb.velocity;
			Vector3 velXZ = new Vector3(vel.x, 0f, vel.z);
			bool towardHole = dist > 0.0001f && Vector3.Dot(velXZ, toHole) > 0f;
			if (vel.y < -0.08f || towardHole || velXZ.sqrMagnitude > 0.014f)
				suctionStillMoving = true;
		}
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
		if (otherHole == null || otherHole.IsConsumed || !otherHole.isActiveAndEnabled)
			return;

		if (isTriggered && CurrentHole != null && !CurrentHole.IsConsumed && CurrentHole.isActiveAndEnabled)
			return;

		BindToHole(otherHole);

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

	public void TryAttract(HoleParent hole)
	{
		if (hole == null || hole.IsConsumed || !hole.isActiveAndEnabled || isTriggered || value <= 0)
			return;
		if (!Tool.CanFitFootprint(size, hole.size))
			return;

		float attractMul = GameBalance.Current != null ? GameBalance.Current.suctionAttractRadius : DefaultAttractMul;
		Vector3 delta = hole.transform.position - transform.position;
		delta.y = 0f;
		float limit = hole.GetStableHoleRadius() * attractMul;
		if (delta.sqrMagnitude > limit * limit)
			return;

		BindToHole(hole);
		if (!isColon && ResetsIfNotScored)
		{
			if (myCoroutine != null)
				StopCoroutine(myCoroutine);
			myCoroutine = StartCoroutine(DelayForUpdateCurrentHole());
		}
		BeginSuctionPhysics(hole);
		OnSuctionBegan(hole);
	}

	private void BeginGulp()
	{
		if (gulpArmed)
			return;
		gulpArmed = true;
		suctionPhase = SuctionPhase.Lift;
		if (rb != null)
		{
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
			rb.useGravity = false;
			rb.isKinematic = true;
		}
	}

	private void StepGulp()
	{
		if (rb == null || CurrentHole == null)
			return;

		GameBalanceConfig config = GameBalance.Current;
		float liftFactor = config != null ? config.suctionLiftDiameterFactor : DefaultLiftDiameterFactor;
		Vector3 holePos = CurrentHole.transform.position;
		float holeRadius = CurrentHole.GetStableHoleRadius();
		float liftY = holePos.y + holeRadius * 2f * liftFactor;
		Vector3 target = new Vector3(holePos.x, liftY, holePos.z);
		Vector3 next = Vector3.MoveTowards(rb.position, target, GulpMoveSpeed * Time.fixedDeltaTime);
		rb.MovePosition(next);

		float dx = next.x - holePos.x;
		float dz = next.z - holePos.z;
		bool overHole = dx * dx + dz * dz <= holeRadius * holeRadius;
		bool atHeight = next.y >= liftY - 0.05f;
		if (overHole && atHeight)
			BeginDrop();
	}

	private void BeginDrop()
	{
		suctionPhase = SuctionPhase.Drop;
		if (rb == null)
			return;
		rb.isKinematic = false;
		rb.useGravity = true;
		rb.velocity = Vector3.zero;
		rb.angularVelocity = Vector3.zero;
		ApplyBodyTuning(true);
	}

	private Vector3 GetVisualSize()
	{
		Renderer[] renderers = GetComponentsInChildren<Renderer>();
		bool any = false;
		Bounds totalBounds = new Bounds(transform.position, Vector3.zero);
		for (int i = 0; i < renderers.Length; i++)
		{
			Renderer mesh = renderers[i];
			if (mesh == null || !mesh.enabled)
				continue;
			if (!(mesh is MeshRenderer) && !(mesh is SkinnedMeshRenderer))
				continue;
			if (MapAbsorbableSetup.IsDecorName(mesh.gameObject.name))
				continue;
			if (!any)
			{
				totalBounds = mesh.bounds;
				any = true;
			}
			else
				totalBounds.Encapsulate(mesh.bounds);
		}
		return any ? totalBounds.size : Vector3.zero;
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
		DetachFromHoleList();
		transform.position = startPosition;
		transform.rotation = startRotation;
		if (startScale.sqrMagnitude > 0.0001f)
			transform.localScale = startScale;
		ReleaseFallingBody();
		isTriggered = false;
		suctionPhase = SuctionPhase.Slide;
		gulpArmed = false;
		if (col != null)
			col.enabled = true;
		Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			if (renderers[i] != null)
				renderers[i].enabled = true;
		}
		CurrentHole = null;
		IgnoreMapPlatforms();
		if (myCoroutine != null) StopCoroutine(myCoroutine);
	}

	public virtual void OnScored(HoleParent hole)
	{
		DetachFromHoleList();
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
		HideVisuals();
		CurrentHole = null;
		suctionPhase = SuctionPhase.Slide;
		gulpArmed = false;
		if (myCoroutine != null) StopCoroutine(myCoroutine);
		gameObject.SetActive(false);
	}

	private void HideVisuals()
	{
		Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			if (renderers[i] != null)
				renderers[i].enabled = false;
		}
		if (col != null)
			col.enabled = false;
	}
}
