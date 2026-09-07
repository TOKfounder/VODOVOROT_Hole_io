using System.Collections.Generic;
using UnityEngine;

public class ScoreOrbSpawner : MonoBehaviour
{
	private static readonly Color TierGreen = new Color(0.2f, 0.95f, 0.35f, 1f);
	private static readonly Color TierBlue = new Color(0.25f, 0.55f, 1f, 1f);
	private static readonly Color TierYellow = new Color(1f, 0.92f, 0.15f, 1f);
	private static readonly Color TierOrange = new Color(1f, 0.45f, 0.1f, 1f);
	private static readonly Color TierRed = new Color(1f, 0.25f, 0.2f, 1f);
	private static readonly Color TierGold = new Color(1f, 0.85f, 0.2f, 1f);

	[SerializeField] private int poolSize = 64;
	[SerializeField] private float popHeight = 1.4f;
	[SerializeField] private float ringPadding = 2.24f;
	[SerializeField] private float groundOffset = 0.18f;

	private readonly List<ScoreOrb> pool = new List<ScoreOrb>(64);
	private Material orbMaterial;
	private Mesh sphereMesh;
	private static ScoreOrbSpawner instance;

	public static void Burst(HoleParent absorber, int totalScore, bool isBoss)
	{
		if (totalScore <= 0 || absorber == null)
			return;

		Ensure().SpawnBurst(absorber, totalScore, isBoss);
	}

	public static ScoreOrbSpawner Ensure()
	{
		if (instance != null)
			return instance;

		instance = FindAnyObjectByType<ScoreOrbSpawner>();
		if (instance == null)
		{
			GameObject go = new GameObject("ScoreOrbSpawner");
			instance = go.AddComponent<ScoreOrbSpawner>();
		}
		instance.WarmPool();
		return instance;
	}

	void Awake()
	{
		instance = this;
		WarmPool();
	}

	void OnDestroy()
	{
		if (instance == this)
			instance = null;
		if (orbMaterial != null)
			Destroy(orbMaterial);
	}

	public void Despawn(ScoreOrb orb)
	{
		if (orb == null)
			return;

		if (orb.CurrentHole != null)
			orb.CurrentHole.nearbyFallingObjects.Remove(orb);

		orb.SleepInPool();
		if (!pool.Contains(orb))
			pool.Add(orb);
	}

	private void SpawnBurst(HoleParent absorber, int totalScore, bool isBoss)
	{
		List<int> parts = SplitScoreRandom(totalScore, isBoss);
		int count = parts.Count;
		if (count == 0)
			return;

		Vector3 holeCenter = absorber.transform.position;
		float holeRadius = absorber.GetHoleRadius();
		float ring = Mathf.Max(1.2f, holeRadius + ringPadding);
		float groundY = holeCenter.y;
		Vector3 holeOrigin = holeCenter;
		holeOrigin.y = groundY + groundOffset * 0.5f;

		for (int i = 0; i < count; i++)
		{
			ScoreOrb orb = GetOrb();
			if (orb == null)
				continue;

			float angle = (i + 0.5f) / count * Mathf.PI * 2f;
			Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
			Vector3 rest = holeCenter + dir * ring;
			rest.y = groundY + groundOffset;
			orb.gameObject.SetActive(true);
			Color orbColor = ColorForValue(parts[i]);
			float orbScale = ScaleForValue(parts[i]);
			orb.Launch(this, parts[i], orbColor, holeOrigin, rest, popHeight, orbScale);
		}
	}

	private static float ScaleForValue(int value)
	{
		if (value >= 100)
			return 0.85f;
		if (value >= 50)
			return 0.72f;
		if (value >= 25)
			return 0.58f;
		if (value >= 10)
			return 0.46f;
		if (value >= 5)
			return 0.36f;
		return 0.28f;
	}

	private static Color ColorForValue(int value)
	{
		if (value >= 100)
		{
			float t = Mathf.InverseLerp(100f, 200f, value);
			return Color.Lerp(TierRed, TierGold, t);
		}
		if (value >= 50)
			return Color.Lerp(TierOrange, TierRed, Mathf.InverseLerp(50f, 99f, value));
		if (value >= 25)
			return TierOrange;
		if (value >= 10)
			return TierYellow;
		if (value >= 5)
			return TierBlue;
		return TierGreen;
	}

	private ScoreOrb GetOrb()
	{
		for (int i = pool.Count - 1; i >= 0; i--)
		{
			ScoreOrb orb = pool[i];
			pool.RemoveAt(i);
			if (orb != null)
				return orb;
		}
		return CreateOrb();
	}

	private void WarmPool()
	{
		EnsureAssets();
		int missing = poolSize - pool.Count;
		for (int i = 0; i < missing; i++)
		{
			ScoreOrb orb = CreateOrb();
			if (orb != null)
			{
				orb.SleepInPool();
				pool.Add(orb);
			}
		}
	}

	private ScoreOrb CreateOrb()
	{
		EnsureAssets();
		GameObject go = new GameObject("ScoreOrb");
		go.transform.SetParent(transform, false);
		go.layer = 7;

		MeshFilter filter = go.AddComponent<MeshFilter>();
		filter.sharedMesh = sphereMesh;
		MeshRenderer renderer = go.AddComponent<MeshRenderer>();
		Material orbInstanceMaterial = new Material(orbMaterial);
		orbInstanceMaterial.color = TierGreen;
		renderer.sharedMaterial = orbInstanceMaterial;
		SphereCollider sphere = go.AddComponent<SphereCollider>();
		sphere.radius = 0.5f;
		Rigidbody body = go.AddComponent<Rigidbody>();
		body.interpolation = RigidbodyInterpolation.Interpolate;
		body.collisionDetectionMode = CollisionDetectionMode.Continuous;
		body.useGravity = true;
		go.transform.localScale = Vector3.one * 0.22f;

		return go.AddComponent<ScoreOrb>();
	}

	private void EnsureAssets()
	{
		if (sphereMesh == null)
			sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
		if (sphereMesh == null)
		{
			GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
			Destroy(temp);
		}

		if (orbMaterial == null)
		{
			Shader shader = Shader.Find("Unlit/Color");
			if (shader == null)
				shader = Shader.Find("Standard");
			orbMaterial = new Material(shader);
			orbMaterial.color = TierGreen;
		}
	}

	private static List<int> SplitScoreRandom(int total, bool isBoss)
	{
		int targetMin = isBoss ? 20 : 10;
		int targetMax = isBoss ? 50 : 30;
		int count = Mathf.Min(total, Random.Range(targetMin, targetMax + 1));
		if (count <= 0)
			return new List<int>(0);

		int[] parts = new int[count];
		for (int i = 0; i < count; i++)
			parts[i] = 1;

		int remaining = total - count;
		for (int r = 0; r < remaining; r++)
			parts[Random.Range(0, count)]++;

		List<int> result = new List<int>(count);
		for (int i = 0; i < count; i++)
			result.Add(parts[i]);
		return result;
	}
}
