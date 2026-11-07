using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BoostButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
	public bool isHolding = false;  // ← Это и есть "удерживается ли кнопка"

	public void OnPointerDown(PointerEventData eventData)
	{
			isHolding = true;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
			isHolding = false;
	}

	// Опционально: если палец ушёл за пределы кнопки
	public void OnPointerExit(PointerEventData eventData)
	{
			if (isHolding) isHolding = false;
	}

    private void OnDisable() => isHolding = false;

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) isHolding = false;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) isHolding = false;
    }
}
