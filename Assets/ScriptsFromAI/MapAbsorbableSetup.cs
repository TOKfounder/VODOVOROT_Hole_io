using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-160)]
public class MapAbsorbableSetup : MonoBehaviour
{
	[SerializeField] private int fallableLayer = 7;

	private static readonly string[] GroundParts =
	{
		"Grass Tile", "Natures_Grass Tile", "Asphalt", "Sidewalk", "Pavement",
		"Street", "Crosswalk", "Curb", " Road", "Road "
	};

	private static readonly string[] DecorParts =
	{
		"Window", "Lamp", "Glow", "Torch",
		"Point Light", "Spot Light", "Directional Light", "Area Light"
	};

	void Awake()
	{
		Scene scene = gameObject.scene;
		if (!scene.IsValid())
			return;

		GameObject[] roots = scene.GetRootGameObjects();
		for (int r = 0; r < roots.Length; r++)
		{
			if (ShouldSkipRoot(roots[r]))
				continue;
			PrepareRenderers(roots[r]);
		}
	}

	private static bool ShouldSkipRoot(GameObject root)
	{
		string n = root.name;
		return n == "ImmersivePack" || n == "MapPlayableGround" || n == "EventSystem";
	}

	private static bool ShouldSkipAbsorbable(GameObject go)
	{
		string n = go.name;
		if (n == "Plane" || n == "MainPlatform" || n == "MapPlayableGround")
			return true;
		return IsGround(n) || IsDecor(n);
	}

	private static bool IsGround(string name)
	{
		if (name.Contains("Tile") && (name.Contains("Grass") || name.Contains("Road") || name.Contains("Asphalt")))
			return true;
		for (int i = 0; i < GroundParts.Length; i++)
		{
			if (name.Contains(GroundParts[i]))
				return true;
		}
		return name.StartsWith("Road") || name.EndsWith("Road");
	}

	public static bool IsDecorName(string name)
	{
		return IsDecor(name);
	}

	private static bool IsDecor(string name)
	{
		for (int i = 0; i < DecorParts.Length; i++)
		{
			if (name.Contains(DecorParts[i]))
				return true;
		}
		return false;
	}

	private static void DisableNonConvexMesh(GameObject go)
	{
		MeshCollider meshCol = go.GetComponent<MeshCollider>();
		if (meshCol == null || meshCol.convex)
			return;
		meshCol.enabled = false;
		if (go.GetComponent<BoxCollider>() == null)
			go.AddComponent<BoxCollider>();
	}

	private static void FitBoxToRenderers(GameObject go)
	{
		Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
		bool any = false;
		Bounds world = new Bounds(go.transform.position, Vector3.zero);
		for (int i = 0; i < renderers.Length; i++)
		{
			Renderer rend = renderers[i];
			if (rend == null || !rend.enabled)
				continue;
			if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer))
				continue;
			if (IsDecor(rend.gameObject.name))
				continue;
			if (!any)
			{
				world = rend.bounds;
				any = true;
			}
			else
				world.Encapsulate(rend.bounds);
		}
		if (!any)
			return;

		BoxCollider box = go.GetComponent<BoxCollider>();
		if (box == null)
			box = go.AddComponent<BoxCollider>();
		Transform t = go.transform;
		box.center = t.InverseTransformPoint(world.center);
		Vector3 lossy = t.lossyScale;
		box.size = new Vector3(
			SafeDiv(world.size.x, lossy.x),
			SafeDiv(world.size.y, lossy.y),
			SafeDiv(world.size.z, lossy.z));
	}

	private static float SafeDiv(float value, float scale)
	{
		return Mathf.Abs(scale) > 0.0001f ? value / Mathf.Abs(scale) : value;
	}

	private void PrepareRenderers(GameObject root)
	{
		HashSet<GameObject> prepared = new HashSet<GameObject>();
		Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			Renderer rend = renderers[i];
			if (rend == null)
				continue;
			if (!(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer))
				continue;

			GameObject go = rend.gameObject;
			if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null)
				continue;
			if (ShouldSkipAbsorbable(go))
				continue;

			GameObject host = ResolveHost(go);
			if (host == null || ShouldSkipAbsorbable(host))
				continue;
			if (!prepared.Add(host))
			{
				host.layer = fallableLayer;
				continue;
			}

			ConsolidateFallingObject(host);
			host.layer = fallableLayer;
		}
	}

	private static void ConsolidateFallingObject(GameObject host)
	{
		FallingObject onHost = host.GetComponent<FallingObject>();
		FallingObject[] found = host.GetComponentsInChildren<FallingObject>(true);
		bool extras = false;
		for (int i = 0; i < found.Length; i++)
		{
			FallingObject fo = found[i];
			if (fo == null || fo.gameObject == host)
				continue;
			extras = true;
			fo.enabled = false;
			Object.DestroyImmediate(fo);
		}

		if (onHost != null && !extras)
			return;

		DisableNonConvexMesh(host);
		FitBoxToRenderers(host);
		if (host.GetComponent<Collider>() == null)
			host.AddComponent<BoxCollider>();
		if (host.GetComponent<FallingObject>() == null)
			host.AddComponent<FallingObject>();
	}

	private static GameObject ResolveHost(GameObject go)
	{
		Transform parent = go.transform.parent;
		if (parent != null && parent.GetComponent<Renderer>() == null && !IsDecor(parent.name))
		{
			int meshKids = 0;
			for (int i = 0; i < parent.childCount; i++)
			{
				if (parent.GetChild(i).GetComponent<Renderer>() != null)
					meshKids++;
			}
			if (meshKids == 1)
				return parent.gameObject;
		}
		return go;
	}
}
