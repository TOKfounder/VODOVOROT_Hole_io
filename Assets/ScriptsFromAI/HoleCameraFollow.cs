using UnityEngine;
using YG;

public class HoleCameraFollow : MonoBehaviour
{
	[SerializeField] private float pitch = 55.682f;
	[SerializeField] [Range(4f, 16f)] private float distancePerRadius = 8.2f;
	[SerializeField] [Range(0.5f, 1.5f)] private float distanceScale = 1f;
	[SerializeField] private float smoothSpeed = 6f;
	[SerializeField] private float punchDuration = 0.12f;
	[SerializeField] private float lookHeight = 0.05f;

	private HoleParent target;
	private Transform camTransform;
	private float punchTimer;
	private float punchStrength;
	private Vector3 currentOffset;
	private bool offsetInitialized;
	private static HoleCameraFollow instance;

	public static HoleCameraFollow Ensure(HoleParent player)
	{
		Camera cam = Camera.main;
		if (cam == null)
			return null;

		HoleCameraFollow follow = cam.GetComponent<HoleCameraFollow>();
		if (follow == null)
			follow = cam.gameObject.AddComponent<HoleCameraFollow>();
		follow.Bind(player);
		return follow;
	}

	public static void Punch(float strength)
	{
		if (instance == null)
			instance = FindAnyObjectByType<HoleCameraFollow>();
		if (instance == null)
			return;

		instance.punchStrength = Mathf.Max(instance.punchStrength, strength * 0.45f);
		instance.punchTimer = instance.punchDuration;
	}

	public void Bind(HoleParent player)
	{
		target = player;
		camTransform = transform;
		instance = this;
		if (camTransform.parent != null)
			camTransform.SetParent(null, true);
		offsetInitialized = false;
		SnapNow();
	}

	void OnDestroy()
	{
		if (instance == this)
			instance = null;
	}

	void LateUpdate()
	{
		Follow(Time.deltaTime);
	}

	private void SnapNow()
	{
		Follow(1000f);
	}

	private void Follow(float dt)
	{
		if (target == null || camTransform == null)
			return;

		float radius = Mathf.Max(0.08f, target.GetHoleRadius());
		float distance = radius * distancePerRadius * distanceScale;
		if (punchTimer > 0f)
		{
			punchTimer -= dt;
			float t = Mathf.Clamp01(punchTimer / punchDuration);
			distance += punchStrength * t;
			if (punchTimer <= 0f)
				punchStrength = 0f;
		}

		Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
		Vector3 look = GetLookPoint();
		Vector3 desiredOffset = rot * new Vector3(0f, 0f, -distance);
		if (!offsetInitialized)
		{
			currentOffset = desiredOffset;
			offsetInitialized = true;
		}
		else
		{
			float blend = 1f - Mathf.Exp(-smoothSpeed * Mathf.Max(0f, dt));
			currentOffset = Vector3.Lerp(currentOffset, desiredOffset, blend);
		}

		camTransform.position = look + currentOffset;
		camTransform.rotation = rot;
	}

	private Vector3 GetLookPoint()
	{
		if (target.hole != null)
			return target.hole.transform.position + Vector3.up * lookHeight;
		return target.transform.position + Vector3.up * lookHeight;
	}
}