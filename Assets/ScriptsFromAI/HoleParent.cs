using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using YG;

public class HoleParent : MonoBehaviour
{
	public static HoleParent Instance;
	// public static List<HoleParent> holeList = new List<HoleParent>();
	// public static HoleParent[] holes => holeList.ToArray();

	public Text nickname;
	public Image border;
	public GameObject pointsPref;
	public GameObject WithoutCamera;
	public Vector3 size;
	public GameObject hole;
	public float baseRadius = 0.2f;
	public int currentLevel;
	public Canvas mainCanvas;

	[SerializeField] float detectionRadius = 25f;
	[SerializeField] LayerMask fallingObjectsLayer = 7;
	[SerializeField] float updateNearbyInterval = 0.1f;

	List<FallingObject> nearbyFallingObjects = new List<FallingObject>(256);
	Collider[] overlapBuffer;
	Coroutine updateNearbyCoroutine;

	protected float[] scoreRequired = {
			0,       // Level 0
			26,       // Level 1
			163,    // Level 2
			795,    // Level 3
			1779,    // Level 4
			3703,    // Level 5
			5127,    // Level 6
			8751,    // Level 7
			11375,   // Level 8
			13000,    // Level 9 (максимум)
			15000
	};
	protected float[] levelScales = { 0.41f, 0.81f, 1.61f, 2.41f, 4.1f, 6f, 8f, 10f, 12f, 13.5f, 67f }; // scale на каждом уровне
	public int score;
	protected Vector3 targetScale;   // Куда хотим прийти
	protected float scaleLerpSpeed = 2f; // Насколько быстро "растёт" (чем больше, тем быстрее)

	protected void Awake()
	{
		Instance = this;
		overlapBuffer = new Collider[512];
	}

	public virtual void Start()
	{
		updateNearbyCoroutine = StartCoroutine(UpdateNearbyObjectsRoutine());
		// holeList.Add(this);
		score = 0;
		UpdateSize();
		// size *= 5;
		if (YG2.envir.isMobile)
			Camera.main.transform.localPosition = new Vector3(0, 2.21199989f, -5.85099983f);
		mainCanvas = GameController.Instance.currentCanvas;
	}

	void OnDrawGizmos()
	{
		Gizmos.DrawWireSphere(transform.position, detectionRadius);
		Gizmos.DrawSphere(transform.position, Mathf.Max(size.x, size.z) * 1.5f);
	}

	IEnumerator UpdateNearbyObjectsRoutine()
	{
		while (true)
		{
			UpdateNearbyObjectsOnce();
			yield return new WaitForSeconds(updateNearbyInterval);
		}
	}

	void UpdateNearbyObjectsOnce()
	{
		Vector3 center = transform.position;
		int hitCount = Physics.OverlapSphereNonAlloc(center, detectionRadius, overlapBuffer, fallingObjectsLayer);

		nearbyFallingObjects.Clear();
		for (int i = 0; i < hitCount; i++)
		{
			Collider col = overlapBuffer[i];
			FallingObject obj = col.GetComponent<FallingObject>();
			if (obj != null && obj.isTriggered)
			{
				nearbyFallingObjects.Add(obj);
			}
		}
	}

	void OnDestroy()
	{
		if (updateNearbyCoroutine != null)
		{
			StopCoroutine(updateNearbyCoroutine);
		}
	}

	void Update()
	{
		for (int i = nearbyFallingObjects.Count - 1; i >= 0; i--)
		{
			FallingObject obj = nearbyFallingObjects[i];
			if (obj == null || obj.CurrentHole != this || !obj.isTriggered)
			{
				nearbyFallingObjects.RemoveAt(i);
				continue;
			}

			// print(obj.rend.bounds.center.y);
			if (obj.rend.bounds.center.y <= 0f)
			{
				print("isUnderground");
				if (IsInHole(obj.transform.position))
				{
					obj.OnScored();
				}
				else
				{
					obj.ResetToStart();
				}
				nearbyFallingObjects.RemoveAt(i);
			}
		}

		transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleLerpSpeed * Time.deltaTime);
	}

	public int GetCurrentLevel(float[] scoreRequired)
	{
		for (int i = scoreRequired.Length - 2; i >= 0; i--)
		{
			if (score >= scoreRequired[i])
				return i;
		}
		return 0;
	}

	public void AddScore(int amount)
	{
		score += amount;
		PointEffect(amount);
		YG2.SaveProgress();
		UpdateSize();
	}

	private void PointEffect(int amount)
	{
		Vector3 worldPos = hole.transform.position;
		Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
		var points = Instantiate(pointsPref, mainCanvas.transform);
		points.GetComponent<RectTransform>().position = screenPos;
		points.GetComponent<Text>().text = $"+{amount}";
	}

	public Vector3 GetVisualSizeOfHole()
	{
		Renderer renderer = hole.GetComponent<Renderer>();
		Bounds totalBound = new Bounds(hole.transform.position, Vector3.zero);

		totalBound.Encapsulate(renderer.bounds);

		return totalBound.size;
	}
	
	private void UpdateSize()
	{
		currentLevel = GetCurrentLevel(scoreRequired);
		if (currentLevel == 10)
		{
			border.fillAmount = 1f;
		}
		else
		{
			float prev = scoreRequired[currentLevel];
			float next = scoreRequired[currentLevel+1];
			border.fillAmount = (float)((score - prev) / (next - prev));
		}
		float scale = levelScales[currentLevel];
		targetScale = new Vector3(scale, scale * 4.508031f, scale);
		size = GetVisualSizeOfHole();
	}

	public bool IsInHole(Vector3 objPos)
	{
		float dx = objPos.x - transform.position.x;
		float dy = objPos.z - transform.position.z;
		float radius = Mathf.Max(size.x, size.z) * 1.5f;
		return dx * dx + dy * dy <= radius * radius;
	}
}
