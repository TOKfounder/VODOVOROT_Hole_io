using UnityEngine;
using YG;

public class HoleCameraFollow : MonoBehaviour
{
	[Header("Mobile")]
	[SerializeField] private float mobileBaseHeight = 1.48f;
	[SerializeField] private float mobileBaseBack = 3.9f;
	[SerializeField] private float mobileHeightPerRadius = 0.77f;
	[SerializeField] private float mobileBackPerRadius = 1.6f;
	[SerializeField] private float mobileMaxHeight = 12f;
	[SerializeField] private float mobileMaxBack = 21.5f;

	[Header("Desktop")]
	[SerializeField] private float desktopBaseHeight = 2.14f;
	[SerializeField] private float desktopBaseBack = 4.8f;
	[SerializeField] private float desktopHeightPerRadius = 0.9f;
	[SerializeField] private float desktopBackPerRadius = 1.87f;
	[SerializeField] private float desktopMaxHeight = 14.7f;
	[SerializeField] private float desktopMaxBack = 25.5f;

	[Header("Tuning")]
	[SerializeField] [Range(0.5f, 1.5f)] private float distanceScale = 1f;
	[SerializeField] private float smoothSpeed = 4f;
	[SerializeField] private float minHeight = 1.07f;
	[SerializeField] private float minBack = 3f;
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
		float height = mobile ? mobileBaseHeight : desktopBaseHeight;
		float back = mobile ? mobileBaseBack : desktopBaseBack;
		height += radius * (mobile ? mobileHeightPerRadius : desktopHeightPerRadius);
		back += radius * (mobile ? mobileBackPerRadius : desktopBackPerRadius);
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
