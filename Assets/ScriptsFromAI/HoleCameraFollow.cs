using UnityEngine;
using YG;

public class HoleCameraFollow : MonoBehaviour
{
	[Header("Mobile")]
	[SerializeField] private float mobileReferenceRadius = 0.35f;
	[SerializeField] private float mobileBackAtReference = 2.97f;
	[SerializeField] private float mobileHeightAtReference = 1.17f;
	[SerializeField] private float mobileMaxHeight = 8f;
	[SerializeField] private float mobileMaxBack = 14.33f;

	[Header("Desktop")]
	[SerializeField] private float desktopReferenceRadius = 0.35f;
	[SerializeField] private float desktopBackAtReference = 3.67f;
	[SerializeField] private float desktopHeightAtReference = 1.68f;
	[SerializeField] private float desktopMaxHeight = 9.8f;
	[SerializeField] private float desktopMaxBack = 17f;

	[Header("Tuning")]
	[SerializeField] [Range(0.5f, 1.5f)] private float distanceScale = 1f;
	[SerializeField] private float smoothSpeed = 4f;
	[SerializeField] private float minHeight = 0.71f;
	[SerializeField] private float minBack = 2f;
	[SerializeField] private float punchDuration = 0.12f;

	private HoleParent target;
	private Transform camTransform;
	private float punchTimer;
	private float punchStrength;
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

		instance.punchStrength = Mathf.Max(instance.punchStrength, strength);
		instance.punchTimer = instance.punchDuration;
	}

	public void Bind(HoleParent player)
	{
		target = player;
		camTransform = transform;
		instance = this;
	}

	void OnDestroy()
	{
		if (instance == this)
			instance = null;
	}

	void LateUpdate()
	{
		if (target == null || camTransform == null)
			return;

		float radius = Mathf.Max(0.01f, target.GetHoleRadius());
		bool mobile = YG2.envir.isMobile;
		float refRadius = mobile ? mobileReferenceRadius : desktopReferenceRadius;
		float backAtRef = mobile ? mobileBackAtReference : desktopBackAtReference;
		float heightAtRef = mobile ? mobileHeightAtReference : desktopHeightAtReference;

		float back = radius * (backAtRef / refRadius);
		float height = radius * (heightAtRef / refRadius);
		height = Mathf.Clamp(height, minHeight, mobile ? mobileMaxHeight : desktopMaxHeight);
		back = Mathf.Clamp(back, minBack, mobile ? mobileMaxBack : desktopMaxBack);
		height *= distanceScale;
		back *= distanceScale;

		if (punchTimer > 0f)
		{
			punchTimer -= Time.deltaTime;
			float t = Mathf.Clamp01(punchTimer / punchDuration);
			back += punchStrength * t;
			if (punchTimer <= 0f)
				punchStrength = 0f;
		}

		Vector3 desired = new Vector3(0f, height, -back);
		camTransform.localPosition = Vector3.Lerp(camTransform.localPosition, desired, smoothSpeed * Time.deltaTime);
	}
}
