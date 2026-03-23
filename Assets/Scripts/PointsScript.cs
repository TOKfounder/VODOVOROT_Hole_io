using UnityEngine;
using UnityEngine.UI;

public class PointsScript : MonoBehaviour
{
	private float moveSpeed = 50f;
	private float time;
	private float duration = 1f;
	private Text txt;
	private Color startColor;

	void Start()
	{
		time = 0f;
		txt = GetComponent<Text>();
		// startColor = txt.color;
	}
	
	void Update()
	{
		if (time < duration)
		{
			transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
			time += Time.deltaTime;
			txt.color = new Color(txt.color.r, txt.color.b, txt.color.b, 1f - time);
		}
		else
		{
			Destroy(gameObject);
		}
	}
}
