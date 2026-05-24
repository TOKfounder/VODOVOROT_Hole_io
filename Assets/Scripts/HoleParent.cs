using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using YG;
using UnityEngine.Pool;

public class HoleParent : MonoBehaviour
{
    public static List<HoleParent> holeList = new List<HoleParent>();
    public static int totalScore;

    public Text nickname;
    public enum TypeOfHole { player, enemy, playerHelper, enemyHelper }
    public TypeOfHole holeType;

    public Image border;
    public GameObject pointsPref;           // префаб +1
    public GameObject WithoutCamera;
    public Vector3 size;
    public GameObject hole;
    public float baseRadius = 0.2f;
    public int currentLevel;
    public Canvas mainCanvas;
    public Collider platform;
    public Transform pointsContainer;

    // Новый пул для эффектов очков (чтобы не плодить Instantiate)
    private ObjectPool<GameObject> pointsPool;

    public List<FallingObject> nearbyFallingObjects = new List<FallingObject>(500);

    protected float[] scoreRequired = { 0, 26, 163, 795, 1779, 3703, 5127, 8751, 11375, 13000, 15000 };
    protected float[] levelScales = { 0.41f, 0.81f, 1.61f, 2.41f, 4.1f, 6f, 8f, 10f, 12f, 13.5f, 67f };

    public int score;

    protected Vector3 targetScale;
    protected float scaleLerpSpeed = 8f;             // увеличил скорость сглаживания
    private float radius;

    private bool isInitialized = false;
    private bool keepScoreOnStart = false;

    protected virtual void Awake()
    {
        if (border == null)
            border = GetComponentInChildren<Image>(true);

		if (VodovorotGameManager.Instance != null)
		    // можно добавить ссылку, если нужно
		
        VodovorotGameManager.allPlatforms.Add(platform);

        // Создаём пул для +очков (максимум 50 одновременно)
        pointsPool = new ObjectPool<GameObject>(
            () => {
                GameObject obj = Instantiate(pointsPref);
                obj.transform.SetParent(pointsContainer ? pointsContainer : mainCanvas.transform);
                return obj;
            },
            obj => obj.SetActive(true),
            obj => obj.SetActive(false),
            obj => Destroy(obj),
            false, 30, 60);
    }

    public virtual void Start()
    {
        holeList.Add(this);
        keepScoreOnStart = false;
        isInitialized = true;

        if (YG2.envir.isMobile)
            Camera.main.transform.localPosition = new Vector3(0, 2.21199989f, -5.85099983f);

		if (VodovorotGameManager.Instance?.GameController != null)
        	mainCanvas = VodovorotGameManager.Instance.GameController.currentCanvas;
		else
			mainCanvas = FindAnyObjectByType<Canvas>();
        
        UpdateSize();

        if (pointsContainer == null && mainCanvas != null)
            pointsContainer = mainCanvas.transform.Find("PointsContainer");
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        if (hole != null)
            UpdateSize(true);
    }

    private void OnDestroy()
    {
        holeList.Remove(this);
        pointsPool?.Clear();

		if (platform != null)
			VodovorotGameManager.allPlatforms.Remove(platform);
    }

    // ВСЯ ЛОГИКА ОБРАБОТКИ ОБЪЕКТОВ ПЕРЕНЕСЕНА СЮДА
    protected virtual void FixedUpdate()
    {
        if (!isInitialized) return;

        // Обработка nearby объектов (теперь в FixedUpdate — безопасно)
        for (int i = nearbyFallingObjects.Count - 1; i >= 0; i--)
        {
            FallingObject obj = nearbyFallingObjects[i];

            if (obj == null || !obj.isTriggered)
            {
                nearbyFallingObjects.RemoveAt(i);
                continue;
            }

            // Проверяем, упал ли объект ниже уровня пола
            if ((!obj.isColon && obj.rend.bounds.center.y <= 0f) ||
                (obj.isColon && obj.rend.bounds.max.y <= 0f))
            {
                if (IsInHole(obj.transform.position))
                {
                    obj.OnScored(this);
                }
                else
                {
                    obj.ResetToStart();
                }

                // Объект может уже сам удалиться из списка внутри FallingObject.SetCurrentHole(null)
                if (i < nearbyFallingObjects.Count && nearbyFallingObjects[i] == obj)
                    nearbyFallingObjects.RemoveAt(i);
                else
                    nearbyFallingObjects.Remove(obj);
            }
        }

        // Сглаживание масштаба (было в Update — теперь здесь)
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleLerpSpeed * Time.fixedDeltaTime);
    }

    // Вызывается только когда score реально изменился
    public virtual void AddScore(int amount)
    {
        ApplyScoreChange(amount, true, ShouldShowPointEffect(), true);
    }

    protected void ApplyScoreChange(int amount, bool addToWorldProgress, bool showPointEffect, bool saveProgress)
    {
        if (amount <= 0)
            return;

        score += amount;

        if (addToWorldProgress)
            totalScore += amount;

        if (showPointEffect)
            CreatePointEffect(amount);

        UpdateSize();
        OnScoreChanged();

        if (saveProgress && VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.SaveProgress();
    }

    public void InitializeScore(int initialScore, bool snapScale = true)
    {
        score = Mathf.Max(0, initialScore);
        keepScoreOnStart = true;
        UpdateSize(snapScale);
        OnScoreChanged();
    }

    private void CreatePointEffect(int amount)
    {
        GameObject points = pointsPool.Get();// === НОВАЯ ЛОГИКА: появление в центре экрана ===
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2.5f, 0f);
        
        // Небольшая рандомизация, чтобы цифры не накладывались друг на друга
        screenCenter.x += Random.Range(-90f, 90f);
        screenCenter.y += Random.Range(-45f, 60f);

        RectTransform rect = points.GetComponent<RectTransform>();
        rect.position = screenCenter;

        // Сначала вызываем OnSpawn с нужным количеством
        PointsScript ps = points.GetComponent<PointsScript>();
        if (ps != null)
            ps.OnSpawn(this, amount);
        else
            Debug.LogError("PointsScript не найден на префабе +очков!");
    }

    protected void UpdateSize(bool snapScale = false)
    {
        currentLevel = GetCurrentLevel(scoreRequired);

        if (border != null)
        {
            if (currentLevel == 10)
            {
                border.fillAmount = 1f;
            }
            else
            {
                float prev = scoreRequired[currentLevel];
                float next = scoreRequired[currentLevel + 1];
                border.fillAmount = (score - prev) / (next - prev);
            }
        }

        float scale = levelScales[currentLevel];
        targetScale = new Vector3(scale, scale * 4.508031f, scale);

        if (snapScale)
            transform.localScale = targetScale;

        size = GetVisualSizeOfHole();
        radius = (size.x + size.z) / 2f;
    }

    public int GetCurrentLevel(float[] scoreRequired)
    {
        for (int i = scoreRequired.Length - 2; i >= 0; i--)
        {
            if (score >= scoreRequired[i])
                return i;
        }
        return 0;
    }

    public Vector3 GetVisualSizeOfHole()
    {
        Renderer renderer = hole.GetComponent<Renderer>();
        Bounds totalBound = new Bounds(hole.transform.position, Vector3.zero);
        totalBound.Encapsulate(renderer.bounds);
        return totalBound.size;
    }

    public Vector3 GetFitSize()
    {
        // if (holeType == TypeOfHole.player)
        //     return size;

        return size * 0.9f;
    }

    public bool IsInHole(Vector3 objPos)
    {
        float dx = objPos.x - transform.position.x;
        float dy = objPos.z - transform.position.z;
        return dx * dx + dy * dy <= radius * radius;
    }

    // Для дочерних классов (BlackHoleController, EnemyController)
    protected virtual void OnScoreChanged() { }

    protected virtual bool ShouldShowPointEffect()
    {
        return holeType == TypeOfHole.player || holeType == TypeOfHole.playerHelper;
    }

    public void ReturnPointsToPool(GameObject pointsObject)
    {
        if (pointsPool != null)
            pointsPool.Release(pointsObject);
    }
}
