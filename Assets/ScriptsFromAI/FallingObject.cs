using UnityEngine;
using System.Collections;

public class FallingObject : MonoBehaviour
{
	public int value;
	public Vector3 size;
	public float V3;
	public Renderer rend;

	private Vector3 startPosition;
	private Quaternion startRotation;
	private Rigidbody rb;
	private Collider col;
	private Coroutine myCoroutine;

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
		foreach (var plat in GamingManager.allPlatforms)
		{
			Physics.IgnoreCollision(plat, col, true);
		}
		size = GetVisualSize();
		V3 = size.x * size.y * size.z;
		startPosition = GetComponent<Transform>().position;
		startRotation = GetComponent<Transform>().rotation;
		// Physics.IgnoreLayerCollision(7, 0, true);
		if (V3 <= 1.69f)
		{
			value = 1;
		}
		else if (V3 <= 19.63f)
		{
			value = 2;
		}
		else if (V3 <= 42.7f)
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

	IEnumerator DelayForUpdateCurrentHole()
	{
		yield return new WaitForSeconds(4f);
		if (!rb.isKinematic)
			ResetToStart();
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
					Physics.IgnoreCollision(CurrentHole.platform, col, true);
					isTriggered = false;
					CurrentHole = otherHole;
					Physics.IgnoreCollision(CurrentHole.platform, col, false);
				}
				else
					return;
			}
			else
			{
				CurrentHole = other.GetComponentInParent<HoleParent>();
				Physics.IgnoreCollision(CurrentHole.platform, col, false);
			}
			if (myCoroutine != null) StopCoroutine(myCoroutine);
			myCoroutine = StartCoroutine(DelayForUpdateCurrentHole());
				
			if (CurrentHole.isEnemy)
			{
				if (Tool.CanFitForEnemies(size, CurrentHole.size))
				{
					isTriggered = true;
					Physics.IgnoreCollision(CurrentHole.platform, col, true);
					rb.isKinematic = false;
					CurrentHole.nearbyFallingObjects.Add(this);
				}
			} else {
				if (Tool.CanFit2D(size, CurrentHole.size))
				{
					isTriggered = true;
					rb.isKinematic = false;
					CurrentHole.nearbyFallingObjects.Add(this);
				}
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
		col.enabled = true;
		rend.enabled = true;
		CurrentHole = null;
		foreach (var plat in GamingManager.allPlatforms)
		{
			Physics.IgnoreCollision(plat, col, true);
		}
		if (myCoroutine != null) StopCoroutine(myCoroutine);
	}

	public void OnScored(HoleParent hole)
	{
		hole.AddScore(value);
		rb.isKinematic = true;
		col.enabled = false;
		rend.enabled = false;
		CurrentHole = null;
		if (myCoroutine != null) StopCoroutine(myCoroutine);
	}
}