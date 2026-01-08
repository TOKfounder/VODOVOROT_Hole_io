using UnityEngine;
using System.Collections;

public class FallingObject : MonoBehaviour
{
	public int value;  // Сколько очков даёт этот объект
	public Vector3 size;
	public float V3;

	private Vector3 startPosition;
	private Quaternion startRotation;
	private Rigidbody rb;
	private Collider col;
	public Renderer rend;

	public bool isTriggered = false;
	public HoleParent CurrentHole {get; set; }

	void Awake()
	{
		col = GetComponent<Collider>();
		if (col == null)
		{
			col = gameObject.AddComponent<BoxCollider>();
		}
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
		gameObject.layer = 7;
		size = GetVisualSize();
		V3 = size.x * size.y * size.z;
		startPosition = GetComponent<Transform>().position;
		startRotation = GetComponent<Transform>().rotation;
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
		GamingManager.Instance.AllValues += value; //!!!
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.CompareTag("Player"))
		{
			if (isTriggered)
			{
				var otherHole = other.GetComponentInParent<HoleParent>();
				if (otherHole.nickname != CurrentHole.nickname)
				{
					isTriggered = false;
					CurrentHole = otherHole;
				}
				else
					return;
			}
			else
				CurrentHole = other.GetComponentInParent<HoleParent>();

			if (Tool.CanFit2D(size, CurrentHole.size))
			{
				isTriggered = true;
				rb.isKinematic = false;
			}
		}
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
		{
			totalBounds.Encapsulate(renderer.bounds);
		}
		return totalBounds.size;
	}


	public void ResetToStart()
	{
		print("ResetToStart");
		transform.position = startPosition;
		transform.rotation = startRotation;
		rb.isKinematic = true;
		isTriggered = false;
		// col.enabled = true;
		// rend.enabled = true;
		CurrentHole = null;
	}

	public void OnScored()
	{
		print("OnScored");
		CurrentHole.AddScore(value);
		rb.isKinematic = true;
		col.enabled = false;
		rend.enabled = false;
		CurrentHole = null;
	}
}