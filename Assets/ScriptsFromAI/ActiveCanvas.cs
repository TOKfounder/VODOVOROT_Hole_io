using UnityEngine;
using UnityEngine.UI;

public static class ActiveCanvas
{
	private static Font cachedFont;

	public static Canvas Get()
	{
		if (GameController.Instance != null && GameController.Instance.currentCanvas != null)
			return GameController.Instance.currentCanvas;
		return Object.FindAnyObjectByType<Canvas>();
	}

	public static Font GetUiFont()
	{
		if (cachedFont != null)
			return cachedFont;

		cachedFont = FindSceneFont("Comic Sans");
		if (cachedFont != null)
			return cachedFont;

		cachedFont = FindSceneFont(null);
		return cachedFont;
	}

	private static Font FindSceneFont(string nameContains)
	{
		Text[] texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < texts.Length; i++)
		{
			Font font = texts[i] != null ? texts[i].font : null;
			if (font == null)
				continue;
			if (string.IsNullOrEmpty(nameContains) || font.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
				return font;
		}
		return null;
	}

	public static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
	{
		if (parent == null)
			parent = Get() != null ? Get().transform : null;
		if (parent == null)
			return null;

		Transform existing = parent.Find(name);
		if (existing != null)
			return existing.GetComponent<Text>();

		Font font = GetUiFont();
		if (font == null)
			return null;

		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = size;

		Text text = go.GetComponent<Text>();
		text.alignment = TextAnchor.MiddleCenter;
		text.fontSize = 28;
		text.color = Color.white;
		text.font = font;
		text.raycastTarget = false;
		return text;
	}

	public static Text CreateText(string name, Vector2 anchoredPosition, Vector2 size)
	{
		Canvas canvas = Get();
		return canvas == null ? null : CreateText(canvas.transform, name, anchoredPosition, size);
	}
}
