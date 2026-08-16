using UnityEngine;
using UnityEngine.UI;

public class PointsScript : MonoBehaviour
{
	[SerializeField] private float moveSpeed = 50f;
	[SerializeField] private float duration = 1f;

	private float time;
	private Text txt;
	private Color startColor;
	private HoleParent ownerHole;

	void Awake()
	{
		txt = GetComponent<Text>();
		if (txt != null)
			startColor = txt.color;
	}

	public void BindPool(HoleParent owner)
	{
		ownerHole = owner;
	}

	public void OnSpawn(int amount)
	{
		if (txt == null)
			txt = GetComponent<Text>();

		time = 0f;
		gameObject.SetActive(true);

		if (txt != null)
		{
			txt.text = $"+{amount}";
			txt.color = startColor;
		}
	}

	void Update()
	{
		if (txt == null)
			return;

		if (time < duration)
		{
			transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
			time += Time.deltaTime;
			float alpha = 1f - time / duration;
			txt.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
		}
		else
		{
			if (ownerHole != null)
				ownerHole.ReturnPointsToPool(gameObject);
			else
				Destroy(gameObject);
		}
	}
}
