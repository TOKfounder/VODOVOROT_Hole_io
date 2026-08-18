using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Pool;

public class HoleParent : MonoBehaviour
{
	public static List<HoleParent> holeList = new List<HoleParent>();
	public static int totalScore;

	public Text nickname;
	public enum TypeOfHole
	{
		player, enemy, playerHelper, enemyHelper
	}

	public TypeOfHole holeType;
	public Image border;
	public GameObject pointsPref;
	public GameObject WithoutCamera;
	public Vector3 size;
	public GameObject hole;
	public float baseRadius = 0.2f;
	public int currentLevel;
	public Canvas mainCanvas;
	public Collider platform;
	public int TeamId { get; private set; } = -1;
	public bool IsConsumed { get; private set; }

	public List<FallingObject> nearbyFallingObjects = new List<FallingObject>(1000);
	bool isUpdated = false;

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
			13000,    // Level 9
			15000
	};
	protected float[] levelScales = { 0.41f, 0.81f, 1.61f, 2.41f, 4.1f, 6f, 8f, 10f, 12f, 13.5f, 67f };
	public int score;
	protected Vector3 targetScale;
	protected float scaleLerpSpeed = 2f;
	private float radius;

	private ObjectPool<GameObject> pointsPool;

	protected virtual void Awake()
	{
		if (platform != null)
			GamingManager.allPlatforms.Add(platform);
	}

	public virtual void Start()
	{
		holeList.Add(this);
		score = 0;
		if (GameController.Instance != null)
			mainCanvas = GameController.Instance.currentCanvas;

		InitPointsPool();
		UpdateSize();
	}

	protected virtual void OnDestroy()
	{
		holeList.Remove(this);
		if (platform != null)
			GamingManager.allPlatforms.Remove(platform);
		pointsPool?.Clear();
	}

	protected virtual void FixedUpdate()
	{
		for (int i = nearbyFallingObjects.Count - 1; i >= 0; i--)
		{
			FallingObject obj = nearbyFallingObjects[i];
			if (obj == null || !obj.isTriggered || obj.rend == null)
			{
				nearbyFallingObjects.RemoveAt(i);
				continue;
			}

			bool belowFloor = (!obj.isColon && obj.rend.bounds.center.y <= 0f)
				|| (obj.isColon && obj.rend.bounds.max.y <= 0f);

			if (!belowFloor)
				continue;

			if (IsInHole(obj.transform.position))
				obj.OnScored(this);
			else
				obj.ResetToStart();

			if (i < nearbyFallingObjects.Count && nearbyFallingObjects[i] == obj)
				nearbyFallingObjects.RemoveAt(i);
			else
				nearbyFallingObjects.Remove(obj);
		}

		transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleLerpSpeed * Time.fixedDeltaTime);
		if (!isUpdated)
			UpdateSize();
		else
			RefreshHoleMetrics();

		TryAbsorbOppositeTeamHoles();
	}

	public bool IsOpponent(HoleParent other)
	{
		return other != null && TeamId >= 0 && other.TeamId >= 0 && TeamId != other.TeamId;
	}

	public void ApplyTeamVisuals(int teamId, Color color, string nickOverride)
	{
		TeamId = teamId;
		if (nickname != null)
		{
			if (!string.IsNullOrEmpty(nickOverride))
				nickname.text = nickOverride;
			nickname.color = color;
		}

		if (border != null)
			border.color = color;

		if (hole == null)
			return;

		Renderer rend = hole.GetComponent<Renderer>();
		if (rend == null)
			return;

		Material mat = rend.material;
		Color tint = color;
		tint.a = mat.color.a;
		mat.color = tint;
	}

	public void MarkConsumed()
	{
		IsConsumed = true;
	}

	public int GetCurrentLevel(float[] required)
	{
		for (int i = required.Length - 2; i >= 0; i--)
		{
			if (score >= required[i])
				return i;
		}
		return 0;
	}

	public void AddScore(int amount)
	{
		if (amount <= 0) return;
		score += amount;
		totalScore += amount;
		if (this is BlackHoleController)
			PointEffect(amount);
		isUpdated = false;
	}

	private void InitPointsPool()
	{
		if (pointsPref == null || mainCanvas == null)
			return;

		pointsPool = new ObjectPool<GameObject>(
			() =>
			{
				GameObject obj = Instantiate(pointsPref, mainCanvas.transform);
				PointsScript ps = obj.GetComponent<PointsScript>();
				if (ps != null)
					ps.BindPool(this);
				return obj;
			},
			obj => obj.SetActive(true),
			obj => obj.SetActive(false),
			obj => Destroy(obj),
			false, 20, 40);
	}

	private void PointEffect(int amount)
	{
		if (hole == null || mainCanvas == null || Camera.main == null)
			return;

		if (pointsPool == null)
			InitPointsPool();
		if (pointsPool == null)
			return;

		GameObject points = pointsPool.Get();
		Vector3 screenPos;
		if (ScorePopupZone.Instance != null)
		{
			screenPos = ScorePopupZone.Instance.GetRandomScreenPosition();
			ScorePopupZone.Instance.Pulse();
		}
		else
			screenPos = Camera.main.WorldToScreenPoint(hole.transform.position);

		RectTransform rect = points.GetComponent<RectTransform>();
		if (rect != null)
			rect.position = screenPos;

		PointsScript ps = points.GetComponent<PointsScript>();
		if (ps != null)
			ps.OnSpawn(amount);
		else
		{
			Text pointsText = points.GetComponent<Text>();
			if (pointsText != null)
				pointsText.text = $"+{amount}";
		}
	}

	public void ReturnPointsToPool(GameObject pointsObject)
	{
		if (pointsPool != null && pointsObject != null)
			pointsPool.Release(pointsObject);
		else if (pointsObject != null)
			Destroy(pointsObject);
	}

	public Vector3 GetVisualSizeOfHole()
	{
		if (hole == null)
			return Vector3.zero;

		Renderer renderer = hole.GetComponent<Renderer>();
		if (renderer == null)
			return Vector3.zero;

		Bounds totalBound = new Bounds(hole.transform.position, Vector3.zero);
		totalBound.Encapsulate(renderer.bounds);
		return totalBound.size;
	}

	protected void RefreshSizeFromScore()
	{
		isUpdated = false;
		UpdateSize();
	}

	private void UpdateSize()
	{
		isUpdated = true;
		currentLevel = GetCurrentLevel(scoreRequired);
		if (border != null)
		{
			if (currentLevel == 10)
			{
				border.fillAmount = 1f;
			}
			else
			{
				float prev = scoreRequired[currentLevel];
				float next = scoreRequired[currentLevel + 1];
				border.fillAmount = (score - prev) / (next - prev);
			}
		}

		float scale = levelScales[currentLevel];
		targetScale = new Vector3(scale, scale * 4.508031f, scale);
		RefreshHoleMetrics();
	}

	private void RefreshHoleMetrics()
	{
		Vector3 refreshedSize = GetVisualSizeOfHole();
		if (refreshedSize == Vector3.zero)
			return;

		size = refreshedSize;
		radius = (size.x + size.z) / 2f;
	}

	public bool IsInHole(Vector3 objPos)
	{
		float dx = objPos.x - transform.position.x;
		float dz = objPos.z - transform.position.z;
		return dx * dx + dz * dz <= radius * radius;
	}

	public float GetHoleRadius() => radius;

	public bool CanAbsorbOtherHole(HoleParent other)
	{
		if (other == null || other.IsConsumed || IsConsumed || other.size == Vector3.zero || size == Vector3.zero)
			return false;

		if (ModeManager.currentMode == ModeManager.Mode.TeamMode && !IsOpponent(other))
			return false;

		if (!Tool.CanAbsorbHoleSize(other.size, size))
			return false;

		if (currentLevel > other.currentLevel)
			return true;

		return currentLevel == other.currentLevel && score > other.score;
	}

	public bool IsOtherHoleFullyInside(HoleParent other)
	{
		if (other == null)
			return false;

		Vector2 myCenter = new Vector2(transform.position.x, transform.position.z);
		Vector2 otherCenter = new Vector2(other.transform.position.x, other.transform.position.z);
		return Tool.IsCircleFullyInside(myCenter, radius, otherCenter, other.GetHoleRadius());
	}

	public static void ResetStaticMatchState()
	{
		totalScore = 0;
		holeList.Clear();
	}

	private void TryAbsorbOppositeTeamHoles()
	{
		if (ModeManager.currentMode != ModeManager.Mode.TeamMode || TeamId < 0 || IsConsumed)
			return;

		for (int i = 0; i < holeList.Count; i++)
		{
			HoleParent other = holeList[i];
			if (other == null || other == this || !other.isActiveAndEnabled || other.IsConsumed)
				continue;
			if (!CanAbsorbOtherHole(other) || !IsOtherHoleFullyInside(other))
				continue;

			AbsorbOtherHole(other);
			return;
		}
	}

	private void AbsorbOtherHole(HoleParent other)
	{
		if (other == null || other.IsConsumed)
			return;

		other.MarkConsumed();
		int absorbedScore = other.score;
		if (absorbedScore > 0)
			AddScore(absorbedScore);

		EnemyController enemy = other as EnemyController;
		if (enemy != null)
		{
			enemy.OnAbsorbedByPlayer();
			return;
		}

		BlackHoleController player = other as BlackHoleController;
		if (player != null)
			player.Eliminate();
	}
}
