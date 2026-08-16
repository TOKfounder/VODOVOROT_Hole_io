using UnityEngine;
using UnityEngine.EventSystems;

public class BoostButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
	public bool isHolding = false;

	public void OnPointerDown(PointerEventData eventData)
	{
		isHolding = true;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		isHolding = false;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (isHolding)
			isHolding = false;
	}
}
