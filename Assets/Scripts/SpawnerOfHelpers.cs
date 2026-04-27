using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Pool;

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
            () => Instantiate(helperPrefab, transform),
            obj => obj.SetActive(true),
            obj => obj.SetActive(false),
            obj => Destroy(obj),
            false, 10, 30);
    }

    private void Start()
    {
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
        FallingObject fo = colon.GetComponent<FallingObject>();
        if (fo != null) fo.isColon = true;
    }

    /// <summary>
    /// Вызывается из BlackHoleController, когда нужно заспавнить хелпера рядом с колоном
    /// </summary>
    public void SpawnHelper(Transform colonTransform)
    {
        GameObject helper = helperPool.Get();
        helper.transform.position = colonTransform.position + Vector3.up * 0.5f;
        helper.transform.rotation = Quaternion.identity;

        // Можно добавить логику хелпера позже (HelperController)
    }

    // Если понадобится вернуть объекты в пул вручную
    public void ReturnColon(GameObject colon) => colonPool.Release(colon);
    public void ReturnHelper(GameObject helper) => helperPool.Release(helper);
}