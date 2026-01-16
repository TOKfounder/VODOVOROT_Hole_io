using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerOfHelpers : MonoBehaviour
{
	[SerializeField] private GameObject colonPrefab;
	[SerializeField] private GameObject helperPrefab;
	[SerializeField] private int countOfColon;

	void Start()
	{
		for (int i = 0; i < countOfColon; i ++)
		{
			float randomX = Random.Range(GamingManager.Instance.minX, GamingManager.Instance.maxX);
			float randomZ = Random.Range(GamingManager.Instance.minZ, GamingManager.Instance.maxZ);
			Instantiate(colonPrefab, new Vector3(randomX, 0, randomZ), Quaternion.identity, transform);
		}
	}	

	public void SpawnHelper(Transform colonTransform)
	{
		Instantiate(helperPrefab, colonTransform.position, Quaternion.identity, transform);
	}
}
