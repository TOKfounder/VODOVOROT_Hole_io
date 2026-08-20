using UnityEngine;

public class NickBillboard : MonoBehaviour
{
	void LateUpdate()
	{
		Camera cam = Camera.main;
		if (cam == null)
			return;

		Vector3 toCamera = cam.transform.position - transform.position;
		toCamera.y = 0f;
		if (toCamera.sqrMagnitude < 0.0001f)
			return;

		transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
	}
}
