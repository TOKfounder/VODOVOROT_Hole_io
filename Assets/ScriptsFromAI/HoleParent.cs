using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using YG;

public class HoleParent : MonoBehaviour
{
	public static HoleParent Instance;
	public static List<HoleParent> holeList = new List<HoleParent>();
	public static HoleParent[] holes => holeList.ToArray();

	public Text nickname;
	public Image border;
	public GameObject pointsPref;
	public GameObject WithoutCamera;
	public Vector3 size;
	public GameObject hole;
	public float baseRadius = 0.2f;
	public int currentLevel;
	public Canvas mainCanvas;

	protected float[] scoreRequired = {
			0,       // Level 0
			26,       // Level 1
			163,    // Level 2
			795,    // Level 3
			1779,    // Level 4
			3703,    // Level 5
			5127,    // Level 6
			8751,    // Level 7
			11375,   // Level 8
			13000,    // Level 9 (максимум)
			15000
	};
	protected float[] levelScales = { 0.41f, 0.81f, 1.61f, 2.41f, 4.1f, 6f, 8f, 10f, 12f, 13.5f, 67f }; // scale на каждом уровне
	public int score;
	protected Vector3 targetScale;   // Куда хотим прийти
	protected float scaleLerpSpeed = 2f; // Насколько быстро "растёт" (чем больше, тем быстрее)

	protected void Awake()
	{
		Instance = this;
	}

	public virtual void Start()
	{
		holeList.Add(this);
		score = 0;
		UpdateSize();
		size *= 5;
		if (YG2.envir.isMobile)
			Camera.main.transform.localPosition = new Vector3(0, 2.21199989f, -5.85099983f);
		mainCanvas = GameController.Instance.currentCanvas;
		// if (YG2.saves.nickName != "")
		// 	nickname.text = YG2.saves.nickName;
		// else
		// {
		// 	nickname.text = YG2.saves.langRu? "Легенда" : "Legend";
		// }
	}

	protected void Update()
	{
		transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleLerpSpeed * Time.deltaTime);
	}

	public int GetCurrentLevel(float[] scoreRequired)
	{
		for (int i = scoreRequired.Length - 2; i >= 0; i--)
		{
			if (score >= scoreRequired[i])
				return i;
		}
		return 0;
	}

	public void AddScore(int amount)
	{
		score += amount;
		PointEffect(amount);
		YG2.SaveProgress();
		UpdateSize();
	}

	protected void PointEffect(int amount)
	{
		Vector3 worldPos = hole.transform.position;
		Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
		var points = Instantiate(pointsPref, mainCanvas.transform);
		points.GetComponent<RectTransform>().position = screenPos;
		points.GetComponent<Text>().text = $"+{amount}";
	}

	public Vector3 GetVisualSizeOfHole()
	{
		Renderer renderer = hole.GetComponent<Renderer>();
		Bounds totalBound = new Bounds(hole.transform.position, Vector3.zero);

		totalBound.Encapsulate(renderer.bounds);

		return totalBound.size;
	}
	
	protected void UpdateSize()
	{
		currentLevel = GetCurrentLevel(scoreRequired);
		if (currentLevel == 10)
		{
			border.fillAmount = 1f;
		}
		else
		{
			float prev = scoreRequired[currentLevel];
			float next = scoreRequired[currentLevel+1];
			border.fillAmount = (float)((score - prev) / (next - prev));
		}
		float scale = levelScales[currentLevel];
		targetScale = new Vector3(scale, scale * 4.508031f, scale);
		size = GetVisualSizeOfHole();
	}
}
