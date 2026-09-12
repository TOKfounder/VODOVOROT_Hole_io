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
	[SerializeField] private GameObject pointsPref;
	public GameObject WithoutCamera;
	public Vector3 size;
	public GameObject hole;
	[SerializeField] private float baseRadius = 0.2f;
	public int currentLevel;
	[SerializeField] private Canvas mainCanvas;
	public Collider platform;
	public int TeamId { get; private set; } = -1;
	public bool IsConsumed { get; private set; }
	public bool NickAssigned { get; private set; }

	public List<FallingObject> nearbyFallingObjects = new List<FallingObject>(1000);
	bool isUpdated = false;

	protected float[] scoreRequired = {
			0,
			44,
			128,
			293,
			624,
			1209,
			2243,
			3998,
			6825,
			10725,
			15990
	};
	protected float[] levelScales = { 0.41f, 0.45f, 0.62f, 1.12f, 2f, 3.34f, 5.11f, 7.57f, 11.48f, 17.06f, 18.7f };
	public int score;
	protected Vector3 targetScale;
	protected float scaleLerpSpeed = 1.15f;
	[SerializeField] private float birthLerpSpeed = 5f;
	private bool birthIntro;
	public bool IsBirthIntro => birthIntro;
	private float radius;
	private float radiusPerScale;

	protected virtual bool UseBirthIntro => false;

	public int ScoreRequiredForLevel(int level)
	{
		if (scoreRequired == null || scoreRequired.Length == 0)
			return 0;
		int index = Mathf.Clamp(level, 0, scoreRequired.Length - 1);
		return (int)scoreRequired[index];
	}

	private ObjectPool<GameObject> pointsPool;

	protected virtual void Awake()
	{
		ApplyBalanceConfig();
		if (platform != null)
			GamingManager.allPlatforms.Add(platform);
	}

	private void ApplyBalanceConfig()
	{
		GameBalanceConfig config = GameBalance.Current;
		if (config == null)
			return;
		scoreRequired = GameBalance.CopyOr(config.scoreRequired, scoreRequired);
		levelScales = GameBalance.CopyOr(config.levelScales, levelScales);
		if (config.scaleLerpSpeed > 0f)
			scaleLerpSpeed = config.scaleLerpSpeed;
		if (config.birthLerpSpeed > 0f)
			birthLerpSpeed = config.birthLerpSpeed;
	}

	public virtual void Start()
	{
		holeList.Add(this);
		score = 0;
		if (GameController.Instance != null)
			mainCanvas = GameController.Instance.currentCanvas;

		InitPointsPool();
		if (UseBirthIntro)
		{
			birthIntro = true;
			transform.localScale = Vector3.one * 0.02f;
		}
		UpdateSize();
		if (nickname != null && nickname.GetComponent<NickBillboard>() == null)
			nickname.gameObject.AddComponent<NickBillboard>();
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

			float floorY = GetScoreFloorY();
			bool belowFloor = (!obj.isColon && obj.rend.bounds.center.y <= floorY)
				|| (obj.isColon && obj.rend.bounds.max.y <= floorY);

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

		float lerp = birthIntro ? birthLerpSpeed : scaleLerpSpeed;
		transform.localScale = Vector3.Lerp(transform.localScale, targetScale, lerp * Time.fixedDeltaTime);
		if (birthIntro && (transform.localScale - targetScale).sqrMagnitude < 0.0004f)
			birthIntro = false;
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
			{
				nickname.text = nickOverride;
				NickAssigned = true;
			}
			nickname.color = color;
		}

		if (border != null)
			border.color = color;
	}

	public void SetNickname(string nick)
	{
		if (nickname == null || string.IsNullOrEmpty(nick))
			return;

		nickname.text = nick;
		NickAssigned = true;
	}

	public void MarkConsumed()
	{
		IsConsumed = true;
	}

	public int GetCurrentLevel(float[] required)
	{
		for (int i = required.Length - 1; i >= 0; i--)
		{
			if (score >= required[i])
				return i;
		}
		return 0;
	}

	public void AddScore(int amount)
	{
		AddScoreInternal(amount, PointsScript.GoldPopup, false);
	}

	public void AddScoreFromHole(int amount, Color popupColor)
	{
		AddScoreInternal(amount, popupColor, true);
	}

	private void AddScoreInternal(int amount, Color popupColor, bool fromHole)
	{
		if (amount <= 0) return;
		score += amount;
		totalScore += amount;
		if (this is BlackHoleController)
		{
			PointEffect(amount, popupColor);
			if (fromHole)
				HoleFeedback.ForPlayer?.PlayAbsorb(popupColor);
			else
				HoleFeedback.ForPlayer?.PlayGulp();
		}
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

	private void PointEffect(int amount, Color popupColor)
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
			screenPos = ScorePopupZone.Instance.GetRandomScreenPosition();
		else
			screenPos = Camera.main.WorldToScreenPoint(hole.transform.position);

		RectTransform rect = points.GetComponent<RectTransform>();
		if (rect != null)
			rect.position = screenPos;

		PointsScript ps = points.GetComponent<PointsScript>();
		if (ps != null)
			ps.OnSpawn(amount, popupColor);
		else
		{
			Text pointsText = points.GetComponent<Text>();
			if (pointsText != null)
			{
				pointsText.text = $"+{amount}";
				pointsText.color = popupColor;
			}
		}
	}

	public void ReturnPointsToPool(GameObject pointsObject)
	{
		if (pointsObject == null || !pointsObject.activeSelf)
			return;

		if (pointsPool != null)
			pointsPool.Release(pointsObject);
		else
			Destroy(pointsObject);
	}

	public static void ClearAllScorePopups()
	{
		PointsScript[] popups = Object.FindObjectsByType<PointsScript>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		for (int i = 0; i < popups.Length; i++)
		{
			if (popups[i] != null)
				popups[i].HideNow();
		}
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
			int maxLevel = scoreRequired.Length - 1;
			if (currentLevel >= maxLevel)
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

		int scaleIndex = Mathf.Clamp(currentLevel, 0, levelScales.Length - 1);
		float scale = levelScales[scaleIndex];
		if (this is BlackHoleController)
			scale *= SkinStats.StartScaleMultiplier;
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
		float sx = transform.localScale.x;
		if (radiusPerScale <= 0f && radius > 0.02f && sx > 0.08f && !birthIntro)
			radiusPerScale = radius / sx;
	}

	public float GetHoleRadius() => radius;

	public float GetStableHoleRadius()
	{
		if (radiusPerScale > 0f)
			return Mathf.Max(0.08f, targetScale.x * radiusPerScale);
		return Mathf.Max(0.08f, radius);
	}

	public float GetScoreFloorY()
	{
		if (platform != null)
			return platform.bounds.min.y + 0.04f;
		return transform.position.y;
	}

	public bool IsInHole(Vector3 objPos)
	{
		float dx = objPos.x - transform.position.x;
		float dz = objPos.z - transform.position.z;
		return dx * dx + dz * dz <= radius * radius;
	}

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

		EnemyController enemy = other as EnemyController;
		if (enemy != null)
		{
			if (this is BlackHoleController)
				HoleFeedback.ForPlayer?.PlayAbsorb(PopupColorForHole(other));
			enemy.OnAbsorbedByPlayer(this);
			return;
		}

		BlackHoleController player = other as BlackHoleController;
		if (player != null)
			player.Eliminate();
	}

	private static Color PopupColorForHole(HoleParent other)
	{
		if (other != null && other.TeamId == ModeManager.TeamBlue)
			return PointsScript.CyanPopup;
		return PointsScript.RedPopup;
	}
}
