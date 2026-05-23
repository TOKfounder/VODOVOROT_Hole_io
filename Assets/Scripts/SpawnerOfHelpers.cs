using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;
using YG;

public class SpawnerOfHelpers : MonoBehaviour
{
    [Header("Префабы")]
    [SerializeField] private GameObject colonPrefab;
    [SerializeField] private GameObject helperPrefab;

    [Header("Настройки спавна")]
    [SerializeField] private int countOfColon = 20;

    // Пулы
    private ObjectPool<GameObject> colonPool;
    private ObjectPool<GameObject> helperPool;
    private int helperSpawnCount;

    private void Awake()
    {
        // Создаём пулы
        colonPool = new ObjectPool<GameObject>(
            () => Instantiate(colonPrefab, transform),
            obj => obj.SetActive(true),
            obj => obj.SetActive(false),
            obj => Destroy(obj),
            false, 30, 60);

        helperPool = new ObjectPool<GameObject>(
            () =>
            {
                GameObject obj = Instantiate(helperPrefab, transform);
                obj.SetActive(false);
                return obj;
            },
            obj => obj.SetActive(false),
            obj => obj.SetActive(false),
            obj => Destroy(obj),
            false, 10, 30);
    }

    private void Start()
    {
        helperSpawnCount = 0;

        // Предспавним колоны при старте уровня
        for (int i = 0; i < countOfColon; i++)
        {
            SpawnColon();
        }
    }

    private void SpawnColon()
    {
        GameObject colon = colonPool.Get();

        float randomX = Random.Range(VodovorotGameManager.Instance.GamingManager.minX,
                                     VodovorotGameManager.Instance.GamingManager.maxX);
        float randomZ = Random.Range(VodovorotGameManager.Instance.GamingManager.minZ,
                                     VodovorotGameManager.Instance.GamingManager.maxZ);

        colon.transform.position = new Vector3(randomX, 0f, randomZ);
        colon.transform.rotation = Quaternion.identity;

        // Добавляем FallingObject с флагом isColon = true
        FallingObject fo = colon.GetComponentInChildren<FallingObject>(true);
        if (fo != null)
            fo.PrepareForSpawn(true);
    }

    /// <summary>
    /// Вызывается из BlackHoleController, когда нужно заспавнить хелпера рядом с колоном
    /// </summary>
    public void SpawnHelper(Transform colonTransform, BlackHoleController owner, int startingScore)
    {
        helperSpawnCount++;
        GameObject helper = helperPool.Get();
        helper.transform.position = new Vector3(colonTransform.position.x, 0.164f, colonTransform.position.z);
        helper.transform.rotation = Quaternion.identity;

        FallingObject helperFallingObject = helper.GetComponentInChildren<FallingObject>(true);
        if (helperFallingObject != null)
            helperFallingObject.PrepareForSpawn(false);

        HelperController helperController = helper.GetComponent<HelperController>();
        if (helperController != null)
        {
            string helperName = YG2.saves.langRu
                ? $"Помощник {helperSpawnCount}"
                : $"Helper {helperSpawnCount}";
            helperController.Initialize(owner, startingScore, helperName);
        }

        helper.SetActive(true);
    }

    // Если понадобится вернуть объекты в пул вручную
    public void ReturnColon(GameObject colon) => colonPool.Release(colon);
    public void ReturnHelper(GameObject helper) => helperPool.Release(helper);
}
