using UnityEngine;
public class SpiralRotation : MonoBehaviour
{
	[SerializeField] private float rotationSpeed = -100f;

	void Update()
	{
		transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
	}
}
