using UnityEngine;

public static class Tool
{
	public static string ConvertText(int cnt)
	{
		string st = "";
		if (cnt < 1000)
		{
			st = $"{(long)cnt}";
		}
		else if (cnt >= 1000 && cnt < 1000000)
		{
			st = $"{(long)cnt / 1000}.{(long)cnt % 1000 / 10:D2}K";
		}
		else if (cnt >= 1000000 && cnt < 1000000000)
		{
			st = $"{(long)cnt / 1000000}.{(long)cnt % 1000000 / 10000:D2}M";
		}
		return st;
	}

	public static bool CanFit2D(Vector3 sizeA, Vector3 sizeB)
	{
		return (sizeA.x <= sizeB.x && sizeA.z <= sizeB.z) || (sizeA.x <= sizeB.x && sizeA.y <= sizeB.z) 
		|| (sizeA.y <= sizeB.x && sizeA.z <= sizeB.z);
	}
	public static bool CanAbsorbHoleSize(Vector3 smallerHole, Vector3 playerHole)
	{
		return smallerHole.x <= playerHole.x && smallerHole.z <= playerHole.z;
	}

	public static bool IsCircleFullyInside(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB)
	{
		float dx = centerB.x - centerA.x;
		float dz = centerB.y - centerA.y;
		return dx * dx + dz * dz + radiusB * radiusB <= radiusA * radiusA + 0.01f;
	}

	public static bool CanFitForEnemies(Vector3 sizeA, Vector3 sizeB)
	{
		return (sizeA.x <= sizeB.x && sizeA.z <= sizeB.z) && (sizeA.x <= sizeB.x && sizeA.y <= sizeB.z) 
		&& (sizeA.y <= sizeB.x && sizeA.z <= sizeB.z);
	}
}
