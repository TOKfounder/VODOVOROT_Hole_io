using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapFallingObjectBaker
{
	const int FallableLayer = 7;

	[MenuItem("Tools/VODOVOROT/Bake FallingObjects In Active Scene")]
	public static void BakeActiveSceneMenu()
	{
		Debug.Log(BakeActiveScene());
	}

	public static string BakeActiveScene()
	{
		Scene scene = EditorSceneManager.GetActiveScene();
		return BakeScene(scene);
	}

	public static string BakeScene(Scene scene)
	{
		if (!scene.IsValid() || !scene.isLoaded)
			return "scene not loaded";

		if (scene.path.IndexOf("Low Poly City") >= 0)
			return "refused: do not editor-bake Low Poly City (SaveScene strips the map). Use MapAbsorbableSetup at runtime.";

		int addedFalling = 0;
		int addedBox = 0;
		int skipped = 0;
		int farmDeleted = 0;
		int already = 0;

		GameObject[] roots = scene.GetRootGameObjects();
		var farmPoints = new System.Collections.Generic.List<GameObject>();
		for (int r = 0; r < roots.Length; r++)
		{
			Transform[] transforms = roots[r].GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < transforms.Length; i++)
			{
				Transform t = transforms[i];
				if (t != null && t.name.StartsWith("FarmPoint"))
					farmPoints.Add(t.gameObject);
			}
		}
		for (int i = 0; i < farmPoints.Count; i++)
		{
			if (farmPoints[i] != null)
			{
				Object.DestroyImmediate(farmPoints[i]);
				farmDeleted++;
			}
		}

		roots = scene.GetRootGameObjects();
		for (int r = 0; r < roots.Length; r++)
		{
			Renderer[] renderers = roots[r].GetComponentsInChildren<Renderer>(true);
			for (int i = 0; i < renderers.Length; i++)
			{
				Renderer rend = renderers[i];
				if (rend == null || !(rend is MeshRenderer) && !(rend is SkinnedMeshRenderer))
				{
					skipped++;
					continue;
				}

				GameObject go = rend.gameObject;
				if (ShouldSkip(go) || HasChildMesh(go.transform))
				{
					skipped++;
					continue;
				}

				if (go.GetComponent<FallingObject>() != null)
				{
					already++;
					continue;
				}

				MeshCollider meshCol = go.GetComponent<MeshCollider>();
				if (meshCol != null && !meshCol.convex)
				{
					meshCol.enabled = false;
					if (go.GetComponent<BoxCollider>() == null)
					{
						go.AddComponent<BoxCollider>();
						addedBox++;
					}
				}
				else if (go.GetComponent<Collider>() == null)
				{
					go.AddComponent<BoxCollider>();
					addedBox++;
				}

				go.AddComponent<FallingObject>();
				go.layer = FallableLayer;
				addedFalling++;
			}
		}

		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		return scene.name + " falling=" + addedFalling + " box=" + addedBox + " already=" + already + " skipped=" + skipped + " farmDeleted=" + farmDeleted;
	}

	static bool ShouldSkip(GameObject go)
	{
		if (go.GetComponent<Camera>() != null)
			return true;
		if (go.GetComponent<Light>() != null)
			return true;
		if (go.GetComponent<Canvas>() != null || go.GetComponent<UnityEngine.UI.Graphic>() != null)
			return true;
		if (go.name == "MapPlayableGround" || go.name == "EventSystem")
			return true;

		Transform t = go.transform;
		while (t != null)
		{
			string n = t.name;
			if (n == "ImmersivePack" || n == "Bounds" || n == "MapPlayableGround")
				return true;
			if (n.StartsWith("wall"))
				return true;
			if (n.StartsWith("Canvas"))
				return true;
			t = t.parent;
		}
		return false;
	}

	static bool HasChildMesh(Transform root)
	{
		Renderer[] children = root.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < children.Length; i++)
		{
			Renderer child = children[i];
			if (child == null || child.transform == root)
				continue;
			if (child is MeshRenderer || child is SkinnedMeshRenderer)
				return true;
		}
		return false;
	}
}
