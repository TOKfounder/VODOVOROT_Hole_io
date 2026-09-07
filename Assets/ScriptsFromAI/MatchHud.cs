using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YG;

public class MatchHud : MonoBehaviour
{
	private const int MaxArrows = 6;
	private const float ArrowDistance = 120f;
	private const float AllyArrowScale = 0.62f;
	private static readonly Color BossArrowColor = new Color(1f, 0.85f, 0.2f, 0.9f);
	private static readonly Color EnemyArrowColor = new Color(1f, 0.35f, 0.25f, 0.9f);
	private static readonly Color AllyArrowColor = new Color(0.3f, 0.9f, 1f, 0.85f);

	[SerializeField] [Tooltip("Стрелка гаснет, когда цель ближе этого радиуса по XZ")]
	[Min(1f)] private float arrowHideRadius = 20f;

	private Text timerText;
	private Text statusText;
	private readonly List<RectTransform> arrows = new List<RectTransform>(MaxArrows);
	private readonly List<Text> arrowGlyphs = new List<Text>(MaxArrows);

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
		glyph.color = BossArrowColor;
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
			statusText.gameObject.SetActive(true);
			statusText.text = ru
				? $"Синие {blue}  /  Красные {red}   Враги: {ModeManager.RemainingTeamEnemies}"
				: $"Blue {blue}  /  Red {red}   Enemies: {ModeManager.RemainingTeamEnemies}";
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
			PlaceArrow(0, ModeManager.ActiveBoss != null ? ModeManager.ActiveBoss.transform : null, 0, 1, BossArrowColor, 1f);
			HideUnusedArrows(1);
			return;
		}

		if (ModeManager.currentMode == ModeManager.Mode.Hunting)
		{
			int shown = PlaceListArrows(ModeManager.HuntingEnemies, 0, BossArrowColor, 1f);
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

		HideUnusedArrows(0);
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
			if (PlaceArrow(shown, enemy.transform, spreadIndex, totalFar, color, scale))
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
			if (list[i] != null && !list[i].IsConsumed && IsFarFromPlayer(list[i].transform.position))
				count++;
		}
		return count;
	}

	private bool PlaceArrow(int index, Transform target, int spreadIndex, int spreadTotal, Color color, float scale)
	{
		if (index < 0 || index >= arrows.Count || arrows[index] == null || target == null)
			return false;

		if (!IsFarFromPlayer(target.position))
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

	private bool IsFarFromPlayer(Vector3 worldPos)
	{
		if (BlackHoleController.Player == null)
			return false;

		Vector3 delta = worldPos - BlackHoleController.Player.transform.position;
		delta.y = 0f;
		return delta.sqrMagnitude > arrowHideRadius * arrowHideRadius;
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
