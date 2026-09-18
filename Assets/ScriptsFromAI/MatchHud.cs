using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class MatchHud : MonoBehaviour
{
	private const int MaxArrows = 6;
	private const float ArrowDistance = 220f;
	private const float ArrowAlpha = 0.7f;
	private const float AllyArrowScale = 0.62f;
	private static readonly Color BossDangerColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
	private static readonly Color EnemyArrowColor = new Color(1f, 0.35f, 0.25f, 0.9f);
	private static readonly Color AllyArrowColor = new Color(0.3f, 0.9f, 1f, 0.85f);
	private static readonly Color FoodArrowColor = new Color(1f, 0.85f, 0.25f, 0.88f);
	private readonly List<FallingObject> largeFoodBuffer = new List<FallingObject>(32);

	[SerializeField] [Tooltip("Стрелка гаснет, когда цель ближе этого радиуса по XZ")]
	[Min(1f)] private float arrowHideRadius = 20f;

	private Text timerText;
	private Text statusText;
	private readonly List<RectTransform> arrows = new List<RectTransform>(MaxArrows);
	private readonly List<Image> arrowImages = new List<Image>(MaxArrows);
	private RectTransform minimapRoot;
	private readonly List<RectTransform> minimapDots = new List<RectTransform>(12);
	private readonly List<Image> minimapDotImages = new List<Image>(12);
	private Sprite minimapSprite;

	public Text TimerText => timerText;
	public RectTransform MinimapRect => minimapRoot;

	public RectTransform FirstActiveArrow()
	{
		for (int i = 0; i < arrows.Count; i++)
		{
			if (arrows[i] != null && arrows[i].gameObject.activeSelf)
				return arrows[i];
		}
		return arrows.Count > 0 ? arrows[0] : null;
	}

	public void SetVisible(bool visible)
	{
		gameObject.SetActive(visible);
	}

	public static MatchHud Ensure()
	{
		MatchHud hud = FindAnyObjectByType<MatchHud>();
		if (hud != null)
			return hud;

		Canvas canvas = ActiveCanvas.Get();
		if (canvas == null)
			return null;

		GameObject go = new GameObject("MatchHud", typeof(RectTransform), typeof(MatchHud));
		go.transform.SetParent(canvas.transform, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		return go.GetComponent<MatchHud>();
	}

	void Awake()
	{
		Build();
	}

	void LateUpdate()
	{
		UpdateStatus();
		UpdateArrows();
		UpdateMinimap();
	}

	private void Build()
	{
		timerText = ActiveCanvas.CreateText(transform, "TimerText", new Vector2(0f, -36f), new Vector2(240f, 52f));
		if (timerText != null)
			timerText.fontSize = 36;

		statusText = ActiveCanvas.CreateText(transform, "MatchStatusText", new Vector2(0f, -100f), new Vector2(760f, 44f));
		if (statusText != null)
			statusText.fontSize = 26;

		for (int i = 0; i < MaxArrows; i++)
			arrows.Add(CreateArrow("TargetArrow_" + i));

		BuildMinimap();
	}

	private RectTransform CreateArrow(string name)
	{
		GameObject arrowGo = new GameObject(name, typeof(RectTransform), typeof(Image));
		arrowGo.transform.SetParent(transform, false);
		RectTransform rect = arrowGo.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(56f, 84f);

		Image image = arrowGo.GetComponent<Image>();
		image.sprite = ActiveCanvas.GetHudPointerSprite();
		image.preserveAspect = true;
		image.color = EnemyArrowColor;
		image.raycastTarget = false;
		arrowGo.SetActive(false);
		arrowImages.Add(image);
		return rect;
	}

	private void UpdateStatus()
	{
		if (statusText == null)
			return;

		bool ru = YG2.saves.langRu;
		if (ModeManager.currentMode == ModeManager.Mode.Boss)
		{
			HoleParent player = BlackHoleController.Player;
			EnemyController boss = ModeManager.ActiveBoss;
			int playerLevel = player != null ? player.currentLevel : 0;
			int bossLevel = boss != null ? boss.currentLevel : 0;
			statusText.gameObject.SetActive(true);
			statusText.text = ru
				? $"Игрок Lv {playerLevel}  /  Босс Lv {bossLevel}"
				: $"Player Lv {playerLevel}  /  Boss Lv {bossLevel}";
			return;
		}

		if (ModeManager.currentMode == ModeManager.Mode.Hunting)
		{
			statusText.gameObject.SetActive(true);
			statusText.text = ru
				? $"Осталось врагов: {ModeManager.RemainingHunters}"
				: $"Enemies left: {ModeManager.RemainingHunters}";
			return;
		}

		if (ModeManager.currentMode == ModeManager.Mode.TeamMode)
		{
			if (GamingManager.Instance != null && GamingManager.Instance.PlayerEliminated)
			{
				statusText.gameObject.SetActive(true);
				statusText.text = ru ? "Вы поглощены!" : "Eliminated!";
				return;
			}

			int blue = ModeManager.GetTeamScore(ModeManager.TeamBlue);
			int red = ModeManager.GetTeamScore(ModeManager.TeamRed);
			int total = Mathf.Max(1, blue + red);
			int bluePct = Mathf.RoundToInt(100f * blue / total);
			int redPct = 100 - bluePct;
			statusText.gameObject.SetActive(true);
			statusText.text = ru
				? $"Синие {bluePct}%  /  Красные {redPct}%"
				: $"Blue {bluePct}%  /  Red {redPct}%";
			return;
		}

		if (ModeManager.currentMode == ModeManager.Mode.TotalCleaning)
		{
			int percent = GamingManager.Instance != null
				? Mathf.RoundToInt(GamingManager.Instance.GetCapturePercent() * 100f)
				: 0;
			statusText.gameObject.SetActive(true);
			statusText.text = ru ? $"Зачистка {percent}%" : $"Clear {percent}%";
			return;
		}

		statusText.gameObject.SetActive(false);
	}

	private void UpdateArrows()
	{
		HideUnusedArrows(0);

		if (BlackHoleController.Player == null || Camera.main == null)
			return;

		if (ModeManager.currentMode == ModeManager.Mode.Boss)
		{
			PlaceArrow(0, ModeManager.ActiveBoss != null ? ModeManager.ActiveBoss.transform : null, BossDangerColor, 1f, arrowHideRadius);
			HideUnusedArrows(1);
			return;
		}

		if (ModeManager.currentMode == ModeManager.Mode.Hunting)
		{
			int shown = PlaceListArrows(ModeManager.HuntingEnemies, 0, EnemyArrowColor, 1f);
			HideUnusedArrows(shown);
			return;
		}

		if (ModeManager.currentMode == ModeManager.Mode.TeamMode)
		{
			int shown = PlaceListArrows(ModeManager.TeamEnemies, 0, EnemyArrowColor, 1f);
			shown = PlaceListArrows(ModeManager.TeamAllies, shown, AllyArrowColor, AllyArrowScale);
			HideUnusedArrows(shown);
			return;
		}

		if (ModeManager.currentMode == ModeManager.Mode.TotalCleaning)
		{
			HideUnusedArrows(0);
			return;
		}

		HideUnusedArrows(0);
	}

	private void BuildMinimap()
	{
		minimapSprite = CreateWhiteSprite();
		GameObject rootGo = new GameObject("Minimap", typeof(RectTransform), typeof(Image));
		rootGo.transform.SetParent(transform, false);
		minimapRoot = rootGo.GetComponent<RectTransform>();
		minimapRoot.anchorMin = new Vector2(1f, 0f);
		minimapRoot.anchorMax = new Vector2(1f, 0f);
		minimapRoot.pivot = new Vector2(1f, 0f);
		minimapRoot.anchoredPosition = new Vector2(-18f, 18f);
		minimapRoot.sizeDelta = new Vector2(148f, 148f);

		Image bg = rootGo.GetComponent<Image>();
		bg.sprite = minimapSprite;
		bg.color = new Color(0.05f, 0.07f, 0.1f, 0.62f);
		bg.raycastTarget = false;

		for (int i = 0; i < 12; i++)
		{
			GameObject dotGo = new GameObject("MinimapDot_" + i, typeof(RectTransform), typeof(Image));
			dotGo.transform.SetParent(minimapRoot, false);
			RectTransform rect = dotGo.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(10f, 10f);
			Image image = dotGo.GetComponent<Image>();
			image.sprite = minimapSprite;
			image.raycastTarget = false;
			dotGo.SetActive(false);
			minimapDots.Add(rect);
			minimapDotImages.Add(image);
		}
	}

	private void UpdateMinimap()
	{
		if (minimapRoot == null || GamingManager.Instance == null)
			return;

		if (!minimapRoot.gameObject.activeSelf)
			minimapRoot.gameObject.SetActive(true);

		int used = 0;
		used = PlaceMinimapDot(used, BlackHoleController.Player != null ? BlackHoleController.Player.transform : null, Color.white, 12f);
		if (ModeManager.currentMode == ModeManager.Mode.Boss)
			used = PlaceMinimapDot(used, ModeManager.ActiveBoss != null ? ModeManager.ActiveBoss.transform : null, BossDangerColor, 11f);
		else if (ModeManager.currentMode == ModeManager.Mode.Hunting)
			used = PlaceMinimapDots(used, ModeManager.HuntingEnemies, EnemyArrowColor, 9f);
		else if (ModeManager.currentMode == ModeManager.Mode.TeamMode)
		{
			used = PlaceMinimapDots(used, ModeManager.TeamAllies, AllyArrowColor, 9f);
			used = PlaceMinimapDots(used, ModeManager.TeamEnemies, EnemyArrowColor, 9f);
		}
		else if (ModeManager.currentMode == ModeManager.Mode.TotalCleaning)
			used = PlaceLargeFoodMinimapDots(used);

		for (int i = used; i < minimapDots.Count; i++)
		{
			if (minimapDots[i] != null)
				minimapDots[i].gameObject.SetActive(false);
		}
	}

	private int PlaceMinimapDots(int start, List<EnemyController> list, Color color, float size)
	{
		int used = start;
		for (int i = 0; i < list.Count && used < minimapDots.Count; i++)
		{
			EnemyController enemy = list[i];
			if (enemy == null || enemy.IsConsumed)
				continue;
			used = PlaceMinimapDot(used, enemy.transform, color, size);
		}
		return used;
	}

	private int PlaceMinimapDot(int index, Transform target, Color color, float size)
	{
		if (index >= minimapDots.Count || target == null || GamingManager.Instance == null)
			return index;

		RectTransform rect = minimapDots[index];
		rect.gameObject.SetActive(true);
		rect.sizeDelta = new Vector2(size, size);
		rect.anchoredPosition = WorldToMinimap(target.position);
		if (index < minimapDotImages.Count && minimapDotImages[index] != null)
			minimapDotImages[index].color = color;
		return index + 1;
	}

	private Vector2 WorldToMinimap(Vector3 world)
	{
		float minX = GamingManager.Instance.minX;
		float maxX = GamingManager.Instance.maxX;
		float minZ = GamingManager.Instance.minZ;
		float maxZ = GamingManager.Instance.maxZ;
		float nx = Mathf.InverseLerp(minX, maxX, world.x);
		float nz = Mathf.InverseLerp(minZ, maxZ, world.z);
		float half = 64f;
		return new Vector2((nx - 0.5f) * 2f * half, (nz - 0.5f) * 2f * half);
	}

	private static Sprite CreateWhiteSprite()
	{
		Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
		tex.Apply();
		return Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
	}

	private int PlaceLargeFoodArrows()
	{
		CollectLargeFood(largeFoodBuffer);
		SortFoodByPlayerDistance(largeFoodBuffer);
		int shown = 0;
		for (int i = 0; i < largeFoodBuffer.Count && shown < MaxArrows; i++)
		{
			FallingObject food = largeFoodBuffer[i];
			if (food == null)
				continue;
			if (PlaceArrow(shown, food.transform, FoodArrowColor, 0.85f, arrowHideRadius * 0.65f))
				shown++;
		}
		return shown;
	}

	private int PlaceLargeFoodMinimapDots(int start)
	{
		CollectLargeFood(largeFoodBuffer);
		int used = start;
		for (int i = 0; i < largeFoodBuffer.Count && used < minimapDots.Count; i++)
		{
			FallingObject food = largeFoodBuffer[i];
			if (food == null)
				continue;
			used = PlaceMinimapDot(used, food.transform, FoodArrowColor, 8f);
		}
		return used;
	}

	private static void CollectLargeFood(List<FallingObject> dest)
	{
		dest.Clear();
		List<FallingObject> foods = FallingObject.Active;
		for (int i = 0; i < foods.Count; i++)
		{
			FallingObject food = foods[i];
			if (food == null || !food.isActiveAndEnabled || food.isTriggered || food.value <= 1)
				continue;
			dest.Add(food);
		}
	}

	private static void SortFoodByPlayerDistance(List<FallingObject> foods)
	{
		if (BlackHoleController.Player == null || foods.Count < 2)
			return;

		Vector3 player = BlackHoleController.Player.transform.position;
		foods.Sort((a, b) =>
		{
			float da = a == null ? float.MaxValue : (a.transform.position - player).sqrMagnitude;
			float db = b == null ? float.MaxValue : (b.transform.position - player).sqrMagnitude;
			return da.CompareTo(db);
		});
	}

	private int PlaceListArrows(List<EnemyController> list, int startIndex, Color color, float scale)
	{
		int shown = startIndex;
		for (int i = 0; i < list.Count && shown < MaxArrows; i++)
		{
			EnemyController enemy = list[i];
			if (enemy == null || enemy.IsConsumed)
				continue;
			if (PlaceArrow(shown, enemy.transform, color, scale, arrowHideRadius))
				shown++;
		}
		return shown;
	}

	private bool PlaceArrow(int index, Transform target, Color color, float scale, float hideRadius)
	{
		if (index < 0 || index >= arrows.Count || arrows[index] == null || target == null)
			return false;

		if (BlackHoleController.Player == null || Camera.main == null)
			return false;

		if (!IsFarFromPlayer(target.position, hideRadius))
		{
			arrows[index].gameObject.SetActive(false);
			return false;
		}

		Vector3 world = target.position - BlackHoleController.Player.transform.position;
		world.y = 0f;
		Transform cam = Camera.main.transform;
		Vector3 right = cam.right;
		right.y = 0f;
		Vector3 forward = cam.forward;
		forward.y = 0f;
		if (right.sqrMagnitude < 0.0001f)
			right = Vector3.right;
		if (forward.sqrMagnitude < 0.0001f)
			forward = Vector3.forward;
		right.Normalize();
		forward.Normalize();

		Vector2 dir = new Vector2(Vector3.Dot(world, right), Vector3.Dot(world, forward));
		if (dir.sqrMagnitude < 0.001f)
		{
			arrows[index].gameObject.SetActive(false);
			return false;
		}

		dir.Normalize();
		Vector3 playerScreen = Camera.main.WorldToScreenPoint(BlackHoleController.Player.transform.position);
		Vector3 targetScreen = Camera.main.WorldToScreenPoint(target.position);
		if (targetScreen.z < 0f)
		{
			arrows[index].gameObject.SetActive(false);
			return false;
		}

		RectTransform arrow = arrows[index];
		arrow.sizeDelta = new Vector2(56f, 84f) * scale;
		float tipOffset = ArrowDistance * scale + arrow.sizeDelta.y * 0.5f;
		Vector2 screenDelta = (Vector2)targetScreen - (Vector2)playerScreen;
		if (screenDelta.magnitude <= tipOffset)
		{
			arrow.gameObject.SetActive(false);
			return false;
		}

		arrow.gameObject.SetActive(true);
		float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
		arrow.localEulerAngles = new Vector3(0f, 0f, angle - 90f);
		arrow.position = playerScreen + (Vector3)(dir * ArrowDistance * scale);
		if (index < arrowImages.Count && arrowImages[index] != null)
		{
			Color tint = color;
			tint.a = ArrowAlpha;
			arrowImages[index].color = tint;
		}
		return true;
	}

	private bool IsFarFromPlayer(Vector3 worldPos, float hideRadius)
	{
		if (BlackHoleController.Player == null)
			return false;

		Vector3 delta = worldPos - BlackHoleController.Player.transform.position;
		delta.y = 0f;
		return delta.sqrMagnitude > hideRadius * hideRadius;
	}

	private void HideUnusedArrows(int usedCount)
	{
		for (int i = usedCount; i < arrows.Count; i++)
		{
			if (arrows[i] != null)
				arrows[i].gameObject.SetActive(false);
		}
	}
}
