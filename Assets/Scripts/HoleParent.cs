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
    private Renderer holeRenderer;

    public event System.Action<HoleParent> GrowthProgressChanged;
    public event System.Action<FallingObject> TargetRejected;
    public int PointsToNextSize => currentLevel >= scoreRequired.Length - 1
        ? 0 : Mathf.Max(0, Mathf.CeilToInt(scoreRequired[currentLevel + 1] - score));

    private bool isInitialized = false;
    private bool keepScoreOnStart = false;

    protected virtual void Awake()
    {
        if (hole != null) holeRenderer = hole.GetComponent<Renderer>();
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
        RefreshFitSize();
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
            if (currentLevel == scoreRequired.Length - 1)
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

        RefreshFitSize();
        GrowthProgressChanged?.Invoke(this);
    }

    public int GetCurrentLevel(float[] scoreRequired)
    {
        for (int i = scoreRequired.Length - 1; i >= 0; i--)
        {
            if (score >= scoreRequired[i])
                return i;
        }
        return 0;
    }

    public Vector3 GetVisualSizeOfHole()
    {
        Renderer renderer = holeRenderer;
        if (!Application.isPlaying && hole != null)
            renderer = hole.GetComponent<Renderer>();
        if (renderer == null) return Vector3.zero;
        Bounds totalBound = new Bounds(hole.transform.position, Vector3.zero);
        totalBound.Encapsulate(renderer.bounds);
        return totalBound.size;
    }

    private void RefreshFitSize()
    {
        // The fit follows the visible interpolation, including a growth with no later score event.
        size = GetVisualSizeOfHole();
        radius = (size.x + size.z) / 2f;
    }

    public Vector3 GetFitSize()
    {
        // if (holeType == TypeOfHole.player)
        //     return size;

        return size * 0.8f;
    }

    public int RequiredSizeFor(Vector3 objectSize)
    {
        for (int level = 0; level < levelScales.Length; level++)
            if (Tool.CanFit2D(objectSize, GetFitSizeAtLevel(level)))
                return level + 1;
        return -1;
    }

    public Vector3 GetFitSizeAtLevel(int level)
    {
        Renderer visual = holeRenderer != null ? holeRenderer : hole != null ? hole.GetComponent<Renderer>() : null;
        if (visual == null || level < 0 || level >= levelScales.Length)
            return Vector3.zero;
        // The visible spiral rotates between physics ticks. Runtime fit uses the
        // cached physics dimensions, so derive future sizes from that same snapshot.
        // Every level scales all three axes by the same ratio (Y keeps4.508031).
        if (Application.isPlaying && size.x > 0f && transform.localScale.x > .0001f)
            return GetFitSize() * (levelScales[level] / transform.localScale.x);

        // Predict from the same authored mesh and level scales without changing
        // the live transform or relying on a renderer bounds cache during growth.
        float scale = levelScales[level];
        Matrix4x4 parent = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 rootAtLevel = parent * Matrix4x4.TRS(transform.localPosition, transform.localRotation,
            new Vector3(scale, scale * 4.508031f, scale));
        Matrix4x4 visualAtLevel = rootAtLevel * transform.worldToLocalMatrix * visual.localToWorldMatrix;
        Bounds local = visual.localBounds;
        Bounds predicted = new Bounds(visualAtLevel.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = new Vector3((corner & 1) == 0 ? local.min.x : local.max.x,
                (corner & 2) == 0 ? local.min.y : local.max.y,
                (corner & 4) == 0 ? local.min.z : local.max.z);
            predicted.Encapsulate(visualAtLevel.MultiplyPoint3x4(point));
        }
        return predicted.size * .8f;
    }

    internal void ReportRejectedTarget(FallingObject item) => TargetRejected?.Invoke(item);

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
