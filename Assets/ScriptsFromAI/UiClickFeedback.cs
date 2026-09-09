using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiClickFeedback : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
	private const float PunchScale = 0.94f;
	private const float PunchSeconds = 0.08f;

	private Vector3 restScale = Vector3.one;
	private float punchTime;

	public static void EnsureOnScene()
	{
		Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < canvases.Length; i++)
			EnsureOnCanvas(canvases[i]);
	}

	public static void EnsureOnCanvas(Canvas canvas)
	{
		if (canvas == null)
			return;

		Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
			EnsureOn(buttons[i]);
	}

	public static void EnsureOn(Button button)
	{
		if (button == null || button.GetComponent<BoostButton>() != null)
			return;
		if (button.GetComponent<UiClickFeedback>() == null)
			button.gameObject.AddComponent<UiClickFeedback>();
	}

	void Awake()
	{
		restScale = transform.localScale;
	}

	void OnDisable()
	{
		punchTime = 0f;
		transform.localScale = restScale;
	}

	void LateUpdate()
	{
		if (punchTime <= 0f)
			return;

		punchTime -= Time.unscaledDeltaTime;
		float u = 1f - Mathf.Clamp01(punchTime / PunchSeconds);
		float punch = u < 0.5f
			? Mathf.Lerp(1f, PunchScale, u * 2f)
			: Mathf.Lerp(PunchScale, 1f, (u - 0.5f) * 2f);
		transform.localScale = restScale * punch;
		if (punchTime <= 0f)
			transform.localScale = restScale;
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		punchTime = PunchSeconds;
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		AudioManager.PlayUiClick();
	}
}
