using UnityEngine;
using System.Collections;

public class FallingObject : MonoBehaviour
{
	public int value;  // Сколько очков даёт этот объект
	public Vector3 size;
	public float V3;

	private Vector3 startPosition;
	private Quaternion startRotation;
	private bool isTriggered = false;
	// private BlackHoleController hole;
	private Rigidbody rb;
	private Renderer rend;
	private Collider col;

	void Awake()
	{
		col = GetComponent<Collider>();
		if (col == null)
		{
			// col = gameObject.AddComponent<MeshCollider>();
			col = gameObject.AddComponent<BoxCollider>();
		}
		// MeshCollider meshCol = col as MeshCollider;
		// if (meshCol != null && !meshCol.convex)
		// {
		// 	meshCol.convex = true;
		// }
		rb = GetComponent<Rigidbody>();
		if (rb == null)
		{
			rb = gameObject.AddComponent<Rigidbody>();
		}
		rb.isKinematic = true;
		rend = GetComponent<Renderer>();
		if (rend == null)
		{
			Destroy(GetComponent<FallingObject>());
		}
	}

	void Start()
	{
		// hole = BlackHoleController.Instance;
		gameObject.layer = 7;
		size = GetVisualSize();
		V3 = size.x * size.y * size.z;
		startPosition = GetComponent<Transform>().position;
		startRotation = GetComponent<Transform>().rotation;
		// Physics.IgnoreLayerCollision(7, 7, true);
		Physics.IgnoreLayerCollision(7, 0, true);
		if (V3 <= 1.69f)
		{
			value = 1;
		}
		else if (V3 <= 42.7f)
		{
			value = 2;
		}
		else if (V3 <= 19.63f)
		{
			value = 3;
		}
		else if (V3 <= 250f)
		{
			value = 10;
		}
		else if (V3 <= 860f)
		{
			value = 20;
		}
		else
		{
			value = 50;
		}
		rb.mass = V3 * 50;
		rb.drag = 4;
		rb.angularDrag = 4;
		GamingManager.Instance.AllValues += value;
	}

	void Update()
{
	if (rend.bounds.center.y <= 0f)
	{
		if (IsInHole(BlackHoleController.Instance.transform))
		{
			BlackHoleController.Instance.AddScore(value);
			rb.isKinematic = true;
			col.enabled = false;
			rend.enabled = false;
			// BlackHoleController.Instance.EnqueueForAbsorption(gameObject);
			enabled = false;
		}
		else
		{
			transform.position = startPosition;
			transform.rotation = startRotation;
			rb.isKinematic = true;
			isTriggered = false;
		}
		
	}
}

	// void Update()
	// {
	// 	// if (transform.position.y <= (BlackHoleController.Instance.currentLevel - 1) * 0.15f)
	// 	if (GetComponent<Renderer>().bounds.center.y <= 0f)
	// 	{
	// 		if (IsInHole(BlackHoleController.Instance.transform))
	// 		{
	// 			BlackHoleController.Instance.AddScore(value);
	// 			Debug.Log("Destroy");
	// 			// Destroy(gameObject);
	// 			rb.isKinematic = true;
	// 			col.enabled = false;
	// 			gameObject.SetActive(false);
	// 		}
	// 		else
	// 		{
	// 			transform.position = startPosition;
	// 			transform.rotation = startRotation;
	// 			rb.isKinematic = true;
	// 			isTriggered = false;
	// 			// Physics.IgnoreLayerCollision(7, 0, false);
	// 		}
	// 	}
	// }

	private bool IsInHole(Transform hole)
	{
		float dx = hole.transform.position.x - transform.position.x;
		float dy = hole.transform.position.z - transform.position.z;
		float radius = Mathf.Max(BlackHoleController.Instance.size.x, BlackHoleController.Instance.size.z);
		return dx * dx + dy * dy <= radius * radius;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (isTriggered) return;
		if (other.CompareTag("Player"))
		{
			if (CanFit2D(size, BlackHoleController.Instance.size))
			{
				isTriggered = true;
				rb.isKinematic = false;
			}
		}
	}

	public bool CanFit2D(Vector3 sizeA, Vector3 sizeB)
	{
		return (sizeA.x <= sizeB.x && sizeA.z <= sizeB.z) || (sizeA.x <= sizeB.x && sizeA.y <= sizeB.z) 
		|| (sizeA.y <= sizeB.x && sizeA.z <= sizeB.z);
	}
	
	public Vector3 GetVisualSize()
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
		{
			totalBounds.Encapsulate(renderer.bounds);
		}
		return totalBounds.size;
	}
}