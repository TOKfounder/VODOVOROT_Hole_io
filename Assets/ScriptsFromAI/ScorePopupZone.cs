using UnityEngine;
using UnityEngine.UI;

public class ScorePopupZone : MonoBehaviour
{
	public static ScorePopupZone Instance { get; private set; }

	[SerializeField] private RectTransform zone;

	void Awake()
	{
		Instance = this;
		if (zone == null)
			zone = GetComponent<RectTransform>();

		Image image = GetComponent<Image>();
		if (image != null)
			image.enabled = false;
	}

	void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	public void Pulse()
	{
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

		GameObject go = new GameObject("ScorePopupZone", typeof(RectTransform), typeof(ScorePopupZone));
		go.transform.SetParent(canvas.transform, false);

		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 1f);
		rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0f, -150f);
		rect.sizeDelta = new Vector2(320f, 140f);
	}
}
