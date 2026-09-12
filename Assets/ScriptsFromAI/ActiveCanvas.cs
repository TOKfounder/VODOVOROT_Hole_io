using UnityEngine;
using UnityEngine.UI;

public static class ActiveCanvas
{
	private static Font cachedFont;
	private static Sprite cachedPointer;
	private static Sprite cachedRoundedPanel;

	private static readonly Color UiOutlineColor = new Color(0f, 0f, 0f, 0.92f);
	private static readonly Vector2 UiOutlineDistance = new Vector2(2.4f, -2.4f);

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

		cachedFont = FindLoadedFont("Comic Sans");
		if (cachedFont != null)
			return cachedFont;

		cachedFont = FindSceneFont("Comic Sans");
		if (cachedFont != null)
			return cachedFont;

		Font osFont = Font.CreateDynamicFontFromOSFont("Comic Sans MS", 28);
		if (osFont != null)
		{
			cachedFont = osFont;
			return cachedFont;
		}

		cachedFont = FindSceneFont(null);
		return cachedFont;
	}

	public static Sprite GetHudPointerSprite()
	{
		if (cachedPointer == null)
			cachedPointer = Resources.Load<Sprite>("HudPointer");
		return cachedPointer;
	}

	public static void ApplyUiFontEverywhere()
	{
		Font font = GetUiFont();
		if (font == null)
			return;

		Text[] texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < texts.Length; i++)
		{
			if (texts[i] == null)
				continue;
			texts[i].font = font;
			ApplyUiTextOutline(texts[i]);
		}
	}

	public static void ApplyUiTextOutline(Text text)
	{
		if (text == null)
			return;
		if (text.GetComponent<NickBillboard>() != null)
			return;

		Outline outline = text.GetComponent<Outline>();
		if (outline == null)
			outline = text.gameObject.AddComponent<Outline>();
		outline.effectColor = UiOutlineColor;
		outline.effectDistance = IsSkinCarouselCaption(text) ? UiOutlineDistance * 0.6f : UiOutlineDistance;
		outline.useGraphicAlpha = true;
	}

	private static bool IsSkinCarouselCaption(Text text)
	{
		if (text == null)
			return false;

		HorizontalLayout3D[] layouts = Object.FindObjectsByType<HorizontalLayout3D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < layouts.Length; i++)
		{
			GameObject[] captions = layouts[i] != null ? layouts[i].captions : null;
			if (captions == null)
				continue;
			for (int c = 0; c < captions.Length; c++)
			{
				GameObject caption = captions[c];
				if (caption == null)
					continue;
				if (caption == text.gameObject || text.transform.IsChildOf(caption.transform))
					return true;
			}
		}
		return false;
	}

	public static void ApplyRoundedPanel(Image image)
	{
		if (image == null)
			return;

		image.sprite = GetRoundedPanelSprite();
		image.type = Image.Type.Sliced;
		image.pixelsPerUnitMultiplier = 1f;
		image.color = Color.white;

		Outline outline = image.GetComponent<Outline>();
		if (outline == null)
			outline = image.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.03f, 0.02f, 0.04f, 0.95f);
		outline.effectDistance = new Vector2(2f, -2f);
		outline.useGraphicAlpha = true;
	}

	public static Sprite GetRoundedPanelSprite()
	{
		if (cachedRoundedPanel != null)
			return cachedRoundedPanel;

		const int size = 64;
		const float radius = 16f;
		const float stroke = 3.2f;
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Bilinear;
		tex.hideFlags = HideFlags.HideAndDontSave;

		Color fill = new Color(0.12f, 0.1f, 0.14f, 0.96f);
		Color border = new Color(0.05f, 0.04f, 0.06f, 1f);
		float cx = (size - 1) * 0.5f;
		Color[] pixels = new Color[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float sdf = SdRoundedBox(x - cx, y - cx, cx, radius);
				Color color;
				if (sdf > 0.6f)
					color = Color.clear;
				else if (sdf > -stroke)
					color = Color.Lerp(border, Color.clear, Mathf.Clamp01((sdf + 0.5f) / 1.1f));
				else
					color = fill;
				pixels[y * size + x] = color;
			}
		}

		tex.SetPixels(pixels);
		tex.Apply(false, false);
		cachedRoundedPanel = Sprite.Create(
			tex,
			new Rect(0f, 0f, size, size),
			new Vector2(0.5f, 0.5f),
			100f,
			0,
			SpriteMeshType.FullRect,
			new Vector4(radius, radius, radius, radius));
		cachedRoundedPanel.name = "TutorialRoundedPanel";
		return cachedRoundedPanel;
	}

	private static float SdRoundedBox(float px, float py, float half, float radius)
	{
		float qx = Mathf.Abs(px) - (half - radius);
		float qy = Mathf.Abs(py) - (half - radius);
		float ox = Mathf.Max(qx, 0f);
		float oy = Mathf.Max(qy, 0f);
		return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
	}

	private static Font FindLoadedFont(string nameContains)
	{
		Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
		for (int i = 0; i < fonts.Length; i++)
		{
			Font font = fonts[i];
			if (font == null)
				continue;
			if (font.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
				return font;
		}
		return null;
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
		{
			Text existingText = existing.GetComponent<Text>();
			ApplyUiTextOutline(existingText);
			return existingText;
		}

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
		ApplyUiTextOutline(text);
		return text;
	}

	public static Text CreateText(string name, Vector2 anchoredPosition, Vector2 size)
	{
		Canvas canvas = Get();
		return canvas == null ? null : CreateText(canvas.transform, name, anchoredPosition, size);
	}
}
