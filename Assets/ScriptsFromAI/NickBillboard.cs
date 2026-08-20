using UnityEngine;

public class NickBillboard : MonoBehaviour
{
	private Transform camTransform;

	void Start()
	{
		CacheCamera();
	}

	void LateUpdate()
	{
		if (camTransform == null)
			CacheCamera();
		if (camTransform == null)
			return;

		Vector3 toCamera = camTransform.position - transform.position;
		toCamera.y = 0f;
		if (toCamera.sqrMagnitude < 0.0001f)
			return;

		transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
	}

	private void CacheCamera()
	{
		Camera cam = Camera.main;
		if (cam != null)
			camTransform = cam.transform;
	}
}
