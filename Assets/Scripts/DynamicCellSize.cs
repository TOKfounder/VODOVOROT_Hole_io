using UnityEngine;
using UnityEngine.UI;

public class DynamicCellSize : MonoBehaviour
{
	private GridLayoutGroup grid;
	private RectTransform containerRect;
	private int count;

	void Start()
	{
		grid = GetComponent<GridLayoutGroup>();
		containerRect = GetComponent<RectTransform>();
		count = grid.transform.childCount;
		float totalHeight = containerRect.rect.height - grid.padding.top - grid.padding.bottom;
		float spacingTotal = grid.spacing.y * (count - 1);
		float cellHeight = (totalHeight - spacingTotal) / count;
		grid.cellSize = new Vector2(grid.cellSize.x, cellHeight);
	}
}
