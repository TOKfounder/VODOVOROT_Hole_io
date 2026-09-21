using UnityEngine;
using UnityEngine.UI;

public class ProgressController : MonoBehaviour
{
	[SerializeField] private Image progressFill;
	[SerializeField] private RectTransform nodesRoot;

	private RectTransform[] nodeRects;
	private Text[] nodeLevelLabels;
	private Image[] nodeDots;
	private RectTransform[] nodePanels;
	private Text[] panelTitles;
	private Text[] panelDetails;
	private bool built;

	void OnEnable()
	{
		ProgressPresenter.Instance.Sync();
		EnsureNodes();
		Refresh();
	}

	public void Refresh()
	{
		ProgressModel model = ProgressPresenter.Instance.Model;
		if (model == null)
			return;

		if (progressFill != null)
			progressFill.fillAmount = model.OverallFill;

		ProgressRoadNode[] nodes = model.BuildRoad();
		if (nodeRects == null || nodeRects.Length != nodes.Length)
			return;

		for (int i = 0; i < nodes.Length; i++)
		{
			ProgressRoadNode node = nodes[i];
			if (nodeLevelLabels[i] != null)
				nodeLevelLabels[i].text = node.level.ToString();
			if (nodeDots[i] != null)
			{
				if (node.current)
					nodeDots[i].color = new Color(0.35f, 0.85f, 0.45f, 1f);
				else if (node.reached)
					nodeDots[i].color = new Color(1f, 0.85f, 0.25f, 1f);
				else
					nodeDots[i].color = new Color(0.35f, 0.35f, 0.38f, 0.85f);
			}

			bool showPanel = node.milestone && nodePanels[i] != null;
			if (nodePanels[i] != null)
				nodePanels[i].gameObject.SetActive(showPanel);
			if (!showPanel)
				continue;

			if (panelTitles[i] != null)
				panelTitles[i].text = node.title;
			if (panelDetails[i] != null)
				panelDetails[i].text = node.detail;
		}
	}

	private void EnsureNodes()
	{
		if (built)
			return;

		if (nodesRoot == null)
			nodesRoot = FindContent();
		if (nodesRoot == null)
			return;

		HideLegacyRoad(nodesRoot);
		ClearGeneratedNodes(nodesRoot);

		int count = ProgressPresenter.Instance.Model.Runtime.maxLevel;
		nodeRects = new RectTransform[count];
		nodeLevelLabels = new Text[count];
		nodeDots = new Image[count];
		nodePanels = new RectTransform[count];
		panelTitles = new Text[count];
		panelDetails = new Text[count];

		const float spacing = 128f;
		const float lineY = 40f;
		float width = spacing * count;
		nodesRoot.sizeDelta = new Vector2(width, 220f);
		if (progressFill == null || !progressFill.isActiveAndEnabled)
			progressFill = CreateFill(nodesRoot, width, lineY);
		else
		{
			RectTransform fillRect = progressFill.rectTransform;
			fillRect.anchoredPosition = new Vector2(40f, lineY);
			fillRect.sizeDelta = new Vector2(width - 80f, fillRect.sizeDelta.y);
		}

		for (int i = 0; i < count; i++)
		{
			GameObject nodeGo = new GameObject("RoadNode_" + (i + 1), typeof(RectTransform));
			nodeGo.transform.SetParent(nodesRoot, false);
			RectTransform rect = nodeGo.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0.5f);
			rect.anchorMax = new Vector2(0f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(80f, 40f);
			rect.anchoredPosition = new Vector2(40f + i * spacing, lineY);
			nodeRects[i] = rect;

			nodeDots[i] = CreateDot(rect);
			nodeLevelLabels[i] = CreateLabel(rect, "Level", new Vector2(0f, 28f), 18);
			nodePanels[i] = CreateMilestonePanel(rect, out panelTitles[i], out panelDetails[i]);
			nodePanels[i].gameObject.SetActive(false);
		}

		built = true;
	}

	private RectTransform FindContent()
	{
		Transform[] all = GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < all.Length; i++)
		{
			if (all[i] != null && all[i].name == "Content")
				return all[i] as RectTransform;
		}
		return null;
	}

	private static void HideLegacyRoad(Transform root)
	{
		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (child != null && !child.name.StartsWith("RoadNode_") && child.name != "RoadFill")
				child.gameObject.SetActive(false);
		}
	}

	private static void ClearGeneratedNodes(Transform root)
	{
		for (int i = root.childCount - 1; i >= 0; i--)
		{
			Transform child = root.GetChild(i);
			if (child != null && child.name.StartsWith("RoadNode_"))
				Object.Destroy(child.gameObject);
		}
	}

	private static Image CreateFill(Transform parent, float width, float lineY)
	{
		GameObject go = new GameObject("RoadFill", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, false);
		go.transform.SetAsFirstSibling();
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 0.5f);
		rect.anchorMax = new Vector2(0f, 0.5f);
		rect.pivot = new Vector2(0f, 0.5f);
		rect.anchoredPosition = new Vector2(40f, lineY);
		rect.sizeDelta = new Vector2(width - 80f, 18f);
		Image image = go.GetComponent<Image>();
		image.color = new Color(1f, 0.85f, 0.25f, 0.85f);
		image.type = Image.Type.Filled;
		image.fillMethod = Image.FillMethod.Horizontal;
		image.raycastTarget = false;
		return image;
	}

	private static Image CreateDot(Transform parent)
	{
		GameObject go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(22f, 22f);
		Image image = go.GetComponent<Image>();
		image.color = Color.white;
		image.raycastTarget = false;
		return image;
	}

	private static Text CreateLabel(Transform parent, string name, Vector2 pos, int fontSize)
	{
		Text text = ActiveCanvas.CreateText(parent, name, pos, new Vector2(78f, 36f));
		if (text == null)
			return null;
		text.fontSize = fontSize;
		text.alignment = TextAnchor.MiddleCenter;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		RectTransform rect = text.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = pos;
		return text;
	}

	private static RectTransform CreateMilestonePanel(Transform parent, out Text title, out Text detail)
	{
		GameObject go = new GameObject("RewardPanel", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0f, -16f);
		rect.sizeDelta = new Vector2(120f, 120f);

		Image image = go.GetComponent<Image>();
		ActiveCanvas.ApplyRoundedPanel(image);
		image.raycastTarget = false;

		title = ActiveCanvas.CreateText(go.transform, "Title", new Vector2(0f, -8f), new Vector2(108f, 54f));
		if (title != null)
		{
			title.fontSize = 22;
			title.alignment = TextAnchor.MiddleCenter;
			title.horizontalOverflow = HorizontalWrapMode.Wrap;
			title.verticalOverflow = VerticalWrapMode.Overflow;
			title.resizeTextForBestFit = true;
			title.resizeTextMinSize = 12;
			title.resizeTextMaxSize = 22;
			title.raycastTarget = false;
		}

		detail = ActiveCanvas.CreateText(go.transform, "Detail", new Vector2(0f, -64f), new Vector2(108f, 48f));
		if (detail != null)
		{
			detail.fontSize = 20;
			detail.alignment = TextAnchor.MiddleCenter;
			detail.horizontalOverflow = HorizontalWrapMode.Wrap;
			detail.verticalOverflow = VerticalWrapMode.Overflow;
			detail.resizeTextForBestFit = true;
			detail.resizeTextMinSize = 12;
			detail.resizeTextMaxSize = 20;
			detail.raycastTarget = false;
		}

		return rect;
	}
}
