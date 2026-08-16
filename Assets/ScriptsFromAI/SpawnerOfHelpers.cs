using UnityEngine;

public class SpawnerOfHelpers : MonoBehaviour
{
	[SerializeField] private GameObject colonPrefab;
	[SerializeField] private GameObject helperPrefab;
	[SerializeField] private int countOfColon = 0;
	[Tooltip("Фича хелперов ещё сырая — по умолчанию выключена")]
	[SerializeField] private bool enableColonHelpers = false;

	void Start()
	{
		if (!enableColonHelpers || colonPrefab == null || GamingManager.Instance == null)
			return;

		for (int i = 0; i < countOfColon; i++)
		{
			float randomX = Random.Range(GamingManager.Instance.minX, GamingManager.Instance.maxX);
			float randomZ = Random.Range(GamingManager.Instance.minZ, GamingManager.Instance.maxZ);
			GameObject colon = Instantiate(colonPrefab, new Vector3(randomX, 0, randomZ), Quaternion.identity, transform);
			FallingObject fo = colon.GetComponentInChildren<FallingObject>(true);
			if (fo != null)
				fo.isColon = true;
		}
	}

	public void TrySpawnHelper(Transform colonTransform)
	{
		if (!enableColonHelpers || helperPrefab == null || colonTransform == null)
			return;

		Instantiate(helperPrefab, colonTransform.position, Quaternion.identity, transform);
	}

	// Старое API
	public void SpawnHelper(Transform colonTransform) => TrySpawnHelper(colonTransform);
}
