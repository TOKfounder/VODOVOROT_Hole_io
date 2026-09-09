using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class MatchHud : MonoBehaviour
{
	private const int MaxArrows = 6;
	private const int CleaningArrowCount = 3;
	private const float CleaningHideRadius = 8f;
	private const float ArrowDistance = 120f;
	private const float AllyArrowScale = 0.62f;
	private static readonly Color BossDangerColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
	private static readonly Color EnemyArrowColor = new Color(1f, 0.35f, 0.25f, 0.9f);
	private static readonly Color AllyArrowColor = new Color(0.3f, 0.9f, 1f, 0.85f);

	[SerializeField] [Tooltip("Стрелка гаснет, когда цель ближе этого радиуса по XZ")]
	[Min(1f)] private float arrowHideRadius = 20f;

	private Text timerText;
	private Text statusText;
	private readonly List<RectTransform> arrows = new List<RectTransform>(MaxArrows);
	private readonly List<Text> arrowGlyphs = new List<Text>(MaxArrows);
	private RectTransform minimapRoot;
	private readonly List<RectTransform> minimapDots = new List<RectTransform>(12);
	private readonly List<Image> minimapDotImages = new List<Image>(12);
	private Sprite minimapSprite;
	private static readonly Color LandmarkArrowColor = new Color(1f, 0.92f, 0.45f, 0.88f);
	private readonly List<FallingObject> cleaningScratch = new List<FallingObject>(32);

	public Text TimerText => timerText;

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
		Font font = ActiveCanvas.GetUiFont();
		if (font == null)
		{
			arrowGlyphs.Add(null);
			return null;
		}

		GameObject arrowGo = new GameObject(name, typeof(RectTransform), typeof(Text));
		arrowGo.transform.SetParent(transform, false);
		RectTransform rect = arrowGo.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(80f, 80f);

		Text glyph = arrowGo.GetComponent<Text>();
		glyph.text = "▲";
		glyph.alignment = TextAnchor.MiddleCenter;
		glyph.fontSize = 48;
		glyph.color = EnemyArrowColor;
		glyph.font = font;
		glyph.raycastTarget = false;
		arrowGo.SetActive(false);
		arrowGlyphs.Add(glyph);
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
			PlaceArrow(0, ModeManager.ActiveBoss != null ? ModeManager.ActiveBoss.transform : null, 0, 1, BossDangerColor, 1f, arrowHideRadius);
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
			int shown = PlaceLandmarkArrows();
			HideUnusedArrows(shown);
			return;
		}

		HideUnusedArrows(0);
	}

	private int PlaceLandmarkArrows()
	{
		HoleParent player = BlackHoleController.Player;
		if (player == null)
			return 0;

		cleaningScratch.Clear();
		List<FallingObject> landmarks = FallingObject.LandmarkObjects;
		Vector3 playerPos = player.transform.position;
		for (int i = 0; i < landmarks.Count; i++)
		{
			FallingObject fo = landmarks[i];
			if (fo == null || fo.value <= 1 || fo.isTriggered)
				continue;
			if (!Tool.CanFit2D(fo.size, player.size))
				continue;
			cleaningScratch.Add(fo);
		}

		cleaningScratch.Sort((a, b) =>
		{
			float da = HorizontalSqr(playerPos, a.transform.position);
			float db = HorizontalSqr(playerPos, b.transform.position);
			return da.CompareTo(db);
		});

		int shown = 0;
		int limit = Mathf.Min(CleaningArrowCount, cleaningScratch.Count);
		for (int i = 0; i < limit && shown < MaxArrows; i++)
		{
			if (PlaceArrow(shown, cleaningScratch[i].transform, shown, limit, LandmarkArrowColor, 0.85f, CleaningHideRadius))
				shown++;
		}
		return shown;
	}

	private static float HorizontalSqr(Vector3 from, Vector3 to)
	{
		float dx = to.x - from.x;
		float dz = to.z - from.z;
		return dx * dx + dz * dz;
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

		for (int i = 0; i < 10; i++)
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

		bool showMap = ModeManager.currentMode != ModeManager.Mode.TotalCleaning;
		if (minimapRoot.gameObject.activeSelf != showMap)
			minimapRoot.gameObject.SetActive(showMap);
		if (!showMap)
			return;

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

	private int PlaceListArrows(List<EnemyController> list, int startIndex, Color color, float scale)
	{
		int shown = startIndex;
		int totalFar = CountFar(list);
		int spreadIndex = 0;
		for (int i = 0; i < list.Count && shown < MaxArrows; i++)
		{
			EnemyController enemy = list[i];
			if (enemy == null || enemy.IsConsumed)
				continue;
			if (PlaceArrow(shown, enemy.transform, spreadIndex, totalFar, color, scale, arrowHideRadius))
			{
				shown++;
				spreadIndex++;
			}
		}
		return shown;
	}

	private int CountFar(List<EnemyController> list)
	{
		int count = 0;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] != null && !list[i].IsConsumed && IsFarFromPlayer(list[i].transform.position, arrowHideRadius))
				count++;
		}
		return count;
	}

	private bool PlaceArrow(int index, Transform target, int spreadIndex, int spreadTotal, Color color, float scale, float hideRadius)
	{
		if (index < 0 || index >= arrows.Count || arrows[index] == null || target == null)
			return false;

		if (!IsFarFromPlayer(target.position, hideRadius))
		{
			arrows[index].gameObject.SetActive(false);
			return false;
		}

		Vector3 targetScreen = Camera.main.WorldToScreenPoint(target.position);
		Vector3 playerScreen = Camera.main.WorldToScreenPoint(BlackHoleController.Player.transform.position);
		Vector2 dir = (Vector2)(targetScreen - playerScreen);
		if (dir.sqrMagnitude < 0.001f)
		{
			arrows[index].gameObject.SetActive(false);
			return false;
		}

		dir.Normalize();
		if (spreadTotal > 1)
		{
			float fan = (spreadIndex - (spreadTotal - 1) * 0.5f) * 18f;
			dir = (Vector2)(Quaternion.Euler(0f, 0f, fan) * dir);
		}

		RectTransform arrow = arrows[index];
		arrow.gameObject.SetActive(true);
		arrow.sizeDelta = new Vector2(80f, 80f) * scale;
		float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
		arrow.localEulerAngles = new Vector3(0f, 0f, angle - 90f);
		arrow.position = playerScreen + (Vector3)(dir * ArrowDistance * scale);
		if (index < arrowGlyphs.Count && arrowGlyphs[index] != null)
		{
			arrowGlyphs[index].color = color;
			arrowGlyphs[index].fontSize = Mathf.RoundToInt(48f * scale);
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
