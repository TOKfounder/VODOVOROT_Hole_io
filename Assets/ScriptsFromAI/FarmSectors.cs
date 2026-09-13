using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class FarmSectors
{
	public const float SectorRadius = 32f;

	private static readonly List<Vector3> centers = new List<Vector3>(8);
	private static readonly List<Vector3> cachedFarmPoints = new List<Vector3>(8);
	private static int cachedFarmSceneHandle = int.MinValue;

	public static int Count => centers.Count;

	public static void Build(int count)
	{
		centers.Clear();
		if (count <= 0)
			return;

		List<Vector3> markers = CollectFarmPoints();
		if (markers.Count >= count)
		{
			for (int i = 0; i < count; i++)
				centers.Add(markers[i]);
			return;
		}

		List<Vector3> foods = CollectFoodPositions();
		if (foods.Count == 0)
		{
			for (int i = 0; i < count; i++)
				centers.Add(markers.Count > 0 ? markers[i % markers.Count] : Vector3.zero);
			return;
		}

		if (markers.Count > 0)
		{
			for (int i = 0; i < markers.Count && centers.Count < count; i++)
				centers.Add(markers[i]);
		}

		int remain = count - centers.Count;
		if (remain > 0)
			AddAngleClusters(foods, remain);
	}

	public static Vector3 GetCenter(int index)
	{
		if (centers.Count == 0)
			return Vector3.zero;
		index = ((index % centers.Count) + centers.Count) % centers.Count;
		return centers[index];
	}

	public static bool IsInSector(int index, Vector3 worldPos, float radius)
	{
		if (centers.Count == 0)
			return true;
		Vector3 center = GetCenter(index);
		float dx = worldPos.x - center.x;
		float dz = worldPos.z - center.z;
		return dx * dx + dz * dz <= radius * radius;
	}

	public static int FindSectorWithFood(int preferred, Vector3 holeSize, float radius)
	{
		if (centers.Count == 0)
			return preferred;

		bool preferredOk = false;
		int other = -1;
		List<FallingObject> foods = FallingObject.Active;
		for (int i = 0; i < foods.Count; i++)
		{
			FallingObject fo = foods[i];
			if (!IsEatable(fo, holeSize))
				continue;

			if (IsInSector(preferred, fo.transform.position, radius))
			{
				preferredOk = true;
				continue;
			}

			if (other >= 0)
				continue;

			for (int s = 0; s < centers.Count; s++)
			{
				if (s == preferred)
					continue;
				if (!IsInSector(s, fo.transform.position, radius))
					continue;
				other = s;
				break;
			}
		}

		if (preferredOk)
			return preferred;
		return other >= 0 ? other : preferred;
	}

	private static bool IsEatable(FallingObject fo, Vector3 holeSize)
	{
		if (fo == null || !fo.isActiveAndEnabled || fo.isTriggered || fo.value <= 0)
			return false;
		return Tool.CanFitFootprint(fo.size, holeSize);
	}

	private static List<Vector3> CollectFarmPoints()
	{
		RefreshFarmPointCache();
		return cachedFarmPoints;
	}

	private static void RefreshFarmPointCache()
	{
		Scene scene = SceneManager.GetActiveScene();
		if (cachedFarmSceneHandle == scene.handle)
			return;

		cachedFarmSceneHandle = scene.handle;
		cachedFarmPoints.Clear();
		if (!scene.IsValid())
			return;

		GameObject[] roots = scene.GetRootGameObjects();
		for (int i = 0; i < roots.Length; i++)
		{
			if (roots[i] != null)
				CollectFarmPointsUnder(roots[i].transform);
		}
	}

	private static void CollectFarmPointsUnder(Transform t)
	{
		if (t.name.StartsWith("FarmPoint"))
			cachedFarmPoints.Add(t.position);
		for (int i = 0; i < t.childCount; i++)
			CollectFarmPointsUnder(t.GetChild(i));
	}

	private static List<Vector3> CollectFoodPositions()
	{
		List<Vector3> foods = new List<Vector3>(256);
		List<FallingObject> found = FallingObject.Active;
		for (int i = 0; i < found.Count; i++)
		{
			if (found[i] == null)
				continue;
			foods.Add(found[i].transform.position);
		}
		return foods;
	}

	private static void AddAngleClusters(List<Vector3> foods, int count)
	{
		Vector3 mid = Vector3.zero;
		for (int i = 0; i < foods.Count; i++)
			mid += foods[i];
		mid /= foods.Count;

		Vector3[] sums = new Vector3[count];
		int[] hits = new int[count];
		for (int i = 0; i < foods.Count; i++)
		{
			Vector3 delta = foods[i] - mid;
			float angle = Mathf.Atan2(delta.z, delta.x);
			if (angle < 0f)
				angle += Mathf.PI * 2f;
			int bucket = Mathf.Clamp(Mathf.FloorToInt(angle / (Mathf.PI * 2f) * count), 0, count - 1);
			sums[bucket] += foods[i];
			hits[bucket]++;
		}

		for (int i = 0; i < count; i++)
		{
			int source = i;
			if (hits[source] <= 0)
			{
				source = -1;
				for (int step = 1; step < count; step++)
				{
					int next = (i + step) % count;
					if (hits[next] <= 0)
						continue;
					source = next;
					break;
				}
			}

			if (source >= 0)
				centers.Add(sums[source] / hits[source]);
		}
	}
}
