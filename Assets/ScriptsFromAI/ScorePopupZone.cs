using UnityEngine;

public class ScorePopupZone : MonoBehaviour
{
	public static ScorePopupZone Instance { get; private set; }

	[SerializeField] private RectTransform zone;

	void Awake()
	{
		Instance = this;
		if (zone == null)
			zone = GetComponent<RectTransform>();
	}

	void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
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
		if (canvas == null || Instance != null)
			return;

		GameObject go = new GameObject("ScorePopupZone", typeof(RectTransform), typeof(ScorePopupZone));
		go.transform.SetParent(canvas.transform, false);

		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0f, -120f);
		rect.sizeDelta = new Vector2(320f, 140f);
	}
}
