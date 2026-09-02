using System.Collections.Generic;
using UnityEngine;

public class ScoreOrbSpawner : MonoBehaviour
{
	private static readonly int[] Denoms = { 100, 50, 25, 10, 5, 1 };
	private static readonly Color GoldBright = new Color(1f, 0.86f, 0.28f, 1f);
	private static readonly Color OrbWhite = new Color(0.96f, 0.97f, 1f, 1f);

	[SerializeField] private int poolSize = 32;
	[SerializeField] private float burstSpeed = 3.2f;
	[SerializeField] private float burstUp = 2.8f;
	[SerializeField] private float ringPadding = 2.8f;
	[SerializeField] private float groundOffset = 0.18f;
	[SerializeField] private float hopHeight = 0.55f;

	private readonly List<ScoreOrb> pool = new List<ScoreOrb>(32);
	private Material orbMaterial;
	private Mesh sphereMesh;
	private static ScoreOrbSpawner instance;

	public static void Burst(Vector3 holeCenter, float holeRadius, int totalScore, Color tint)
	{
		if (totalScore <= 0)
			return;

		Ensure().SpawnBurst(holeCenter, holeRadius, totalScore, tint);
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

	private void SpawnBurst(Vector3 holeCenter, float holeRadius, int totalScore, Color tint)
	{
		List<int> parts = SplitScore(totalScore);
		int count = parts.Count;
		if (count == 0)
			return;

		float ring = Mathf.Max(1.5f, holeRadius + ringPadding);
		float groundY = holeCenter.y;

		for (int i = 0; i < count; i++)
		{
			ScoreOrb orb = GetOrb();
			if (orb == null)
				continue;

			float angle = (i + 0.5f) / count * Mathf.PI * 2f;
			Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
			Vector3 rest = holeCenter + dir * ring;
			rest.y = groundY + groundOffset;
			Vector3 origin = rest + Vector3.up * hopHeight;
			Vector3 impulse = dir * burstSpeed;
			impulse.y = burstUp;
			orb.gameObject.SetActive(true);
			Color orbColor = ColorForValue(parts[i]);
			float orbScale = ScaleForValue(parts[i]);
			orb.Launch(this, parts[i], orbColor, origin, rest, impulse, orbScale);
		}
	}

	private static float ScaleForValue(int value)
	{
		if (value >= 100)
			return 0.85f;
		if (value >= 50)
			return 0.68f;
		if (value >= 25)
			return 0.55f;
		if (value >= 10)
			return 0.44f;
		if (value >= 5)
			return 0.36f;
		return 0.28f;
	}

	private static Color ColorForValue(int value)
	{
		float t = Mathf.InverseLerp(1f, 100f, value);
		Color color = Color.Lerp(OrbWhite, GoldBright, t);
		color.a = 1f;
		return color;
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
		orbInstanceMaterial.color = OrbWhite;
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
			orbMaterial.color = Color.white;
		}
	}

	private static List<int> SplitScore(int total)
	{
		List<int> parts = new List<int>(16);
		int remaining = Mathf.Max(0, total);
		for (int d = 0; d < Denoms.Length && remaining > 0 && parts.Count < 16; d++)
		{
			int denom = Denoms[d];
			while (remaining >= denom && parts.Count < 16)
			{
				parts.Add(denom);
				remaining -= denom;
			}
		}

		if (remaining > 0)
		{
			if (parts.Count < 16)
				parts.Add(remaining);
			else
				parts[parts.Count - 1] += remaining;
		}

		return parts;
	}
}
