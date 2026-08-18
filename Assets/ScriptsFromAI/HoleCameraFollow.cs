using UnityEngine;
using YG;

public class HoleCameraFollow : MonoBehaviour
{
	[Header("Mobile")]
	[SerializeField] private float mobileBaseHeight = 2.21f;
	[SerializeField] private float mobileBaseBack = 5.85f;
	[SerializeField] private float mobileHeightPerRadius = 1.15f;
	[SerializeField] private float mobileBackPerRadius = 2.4f;
	[SerializeField] private float mobileMaxHeight = 18f;
	[SerializeField] private float mobileMaxBack = 32f;

	[Header("Desktop")]
	[SerializeField] private float desktopBaseHeight = 3.2f;
	[SerializeField] private float desktopBaseBack = 7.2f;
	[SerializeField] private float desktopHeightPerRadius = 1.35f;
	[SerializeField] private float desktopBackPerRadius = 2.8f;
	[SerializeField] private float desktopMaxHeight = 22f;
	[SerializeField] private float desktopMaxBack = 38f;

	[SerializeField] private float smoothSpeed = 4f;
	[SerializeField] private float minHeight = 1.6f;
	[SerializeField] private float minBack = 4.5f;

	private HoleParent target;
	private Transform camTransform;

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

	public void Bind(HoleParent player)
	{
		target = player;
		camTransform = transform;
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

		Vector3 desired = new Vector3(0f, height, -back);
		camTransform.localPosition = Vector3.Lerp(camTransform.localPosition, desired, smoothSpeed * Time.deltaTime);
	}
}
