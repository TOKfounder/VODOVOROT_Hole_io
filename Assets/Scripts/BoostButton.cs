using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BoostButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
	public bool isHolding = false;  // ← Это и есть "удерживается ли кнопка"

	public void OnPointerDown(PointerEventData eventData)
	{
			isHolding = true;
			Debug.Log("Буст нажат!");
	}

	public void OnPointerUp(PointerEventData eventData)
	{
			isHolding = false;
			Debug.Log("Буст отпущен!");
	}

	// Опционально: если палец ушёл за пределы кнопки
	public void OnPointerExit(PointerEventData eventData)
	{
			if (isHolding) isHolding = false;
	}
}