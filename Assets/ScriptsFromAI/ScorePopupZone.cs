using UnityEngine;
using UnityEngine.UI;

public class ScorePopupZone : MonoBehaviour
{
	public static ScorePopupZone Instance { get; private set; }

	[SerializeField] private RectTransform zone;
	[SerializeField] private float idleAlpha = 0.03f;
	[SerializeField] private float pulseAlpha = 0.18f;
	[SerializeField] private float pulseDuration = 0.2f;

	private Image image;
	private float pulseTimer;

	void Awake()
	{
		Instance = this;
		if (zone == null)
			zone = GetComponent<RectTransform>();
		image = GetComponent<Image>();
		if (image != null)
			image.raycastTarget = false;
		ApplyAlpha(idleAlpha);
	}

	void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	void Update()
	{
		if (image == null || pulseTimer <= 0f)
			return;

		pulseTimer -= Time.unscaledDeltaTime;
		float t = Mathf.Clamp01(pulseTimer / pulseDuration);
		ApplyAlpha(Mathf.Lerp(idleAlpha, pulseAlpha, t));
	}

	public void Pulse()
	{
		pulseTimer = pulseDuration;
		ApplyAlpha(pulseAlpha);
	}

	public Vector3 GetRandomScreenPosition()
	{
		if (zone == null)
			return Vector3.zero;

		Vector3[] corners = new Vector3[4];
		zone.GetWorldCorners(corners);
		float x = Random.Range(corners[0].x, corners[2].x);
		float y = Random.Range(corners[0].y, corners[2].y);
		return new Vector3(x, y, 0f);
	}

	public static void EnsureZone(Canvas canvas)
	{
		if (canvas == null)
			return;

		if (Instance != null && Instance.transform.IsChildOf(canvas.transform))
			return;

		if (Instance != null)
			Destroy(Instance.gameObject);

		GameObject go = new GameObject("ScorePopupZone", typeof(RectTransform), typeof(Image), typeof(ScorePopupZone));
		go.transform.SetParent(canvas.transform, false);

		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0f, -150f);
		rect.sizeDelta = new Vector2(320f, 140f);

		ScorePopupZone zone = go.GetComponent<ScorePopupZone>();
		Image image = go.GetComponent<Image>();
		image.raycastTarget = false;
		zone.ApplyAlpha(zone.idleAlpha);
	}

	private void ApplyAlpha(float alpha)
	{
		if (image == null)
			image = GetComponent<Image>();
		if (image == null)
			return;

		Color color = image.color;
		color.r = 1f;
		color.g = 1f;
		color.b = 1f;
		color.a = alpha;
		image.color = color;
	}
}
