using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-160)]
public class MapAbsorbableSetup : MonoBehaviour
{
	[SerializeField] private int fallableLayer = 7;

	void Awake()
	{
		Scene scene = gameObject.scene;
		if (!scene.IsValid())
			return;

		GameObject[] roots = scene.GetRootGameObjects();
		for (int r = 0; r < roots.Length; r++)
			StripFarmPoints(roots[r].transform);

		roots = scene.GetRootGameObjects();
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
		return n == "Plane" || n == "MainPlatform" || n == "MapPlayableGround";
	}

	private static void StripFarmPoints(Transform root)
	{
		Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
		for (int i = transforms.Length - 1; i >= 0; i--)
		{
			Transform t = transforms[i];
			if (t == null || !t.name.StartsWith("FarmPoint"))
				continue;
			if (!IsEmptyMarker(t))
				continue;
			Destroy(t.gameObject);
		}
	}

	private static bool IsEmptyMarker(Transform t)
	{
		Renderer[] renderers = t.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			if (renderers[i] is MeshRenderer || renderers[i] is SkinnedMeshRenderer)
				return false;
		}
		return true;
	}

	private void PrepareRenderers(GameObject root)
	{
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

			if (go.GetComponent<FallingObject>() == null)
			{
				if (go.GetComponent<Collider>() == null)
					go.AddComponent<BoxCollider>();
				else
				{
					MeshCollider meshCol = go.GetComponent<MeshCollider>();
					if (meshCol != null && !meshCol.convex)
					{
						meshCol.enabled = false;
						if (go.GetComponent<BoxCollider>() == null)
							go.AddComponent<BoxCollider>();
					}
				}
				go.AddComponent<FallingObject>();
			}

			go.layer = fallableLayer;
		}
	}
}
