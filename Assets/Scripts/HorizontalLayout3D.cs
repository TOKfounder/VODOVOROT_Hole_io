using UnityEngine;
using UnityEngine.UI;
using YG;

public class HorizontalLayout3D : MonoBehaviour
{
    [Header("Настройки карусели")]
    public float radius = 120f;
    public float startAngle = 50f;
    public Camera targetCamera;

    [Header("UI элементы")]
    public GameObject[] captions;
    public Text featureText;
    public GameObject buttonOfBuying;
    public GameObject buttonOfEquiping;
    public Text necessaryLevelText;
    public Text costForCoinsText;
    public Text costForDonateText;
    public Button donateButton;
    public Image currencyImage;

    [Header("Данные скинов")]
    [SerializeField] private string[] toiletIDs = { "obodok", "white", "gold", "scrag", "lord" };

    private readonly string[] featuresRu = 
    {
        "Этот красный тазик – для тех, кто любит жить на скорости! Бросай вызов привычному, залетай в тазик!",
        "Этот унитаз готов поддержать тебя в любой трудной и странной ситуации!",
        "Блеск роскоши для истинных чемпионов! Стань королём туалетных побед.",
        "Сиди с комфортом и властвуй! Злые силы не пройдут через эту дыру...",
        "На этом троне даже проблемы исчезают! Почувствуй себя властелином стока."
    };

    private readonly string[] featuresEn = 
    {
        "This red basin is for those who like to live at speed! Challenge the familiar, fly into the basin!",
        "This toilet bowl is ready to support you in any difficult and strange situation!",
        "The splendor of luxury for true champions! Become the king of toilet victories.",
        "Sit comfortably and rule! Evil forces will not pass through this hole...",
        "On this throne, even problems disappear! Feel like the lord of the drain."
    };

    private readonly int[] necessaryLevels = { 0, 1, 4, 7, 10 };
    private readonly int[] costsForCoins = { 0, 20, 270, 800, 2400 };
    private readonly int[] costsForDonate = { 0, 10000, 10, 40, 100 };

    // Внутренние переменные
    private float initialAngle;
    private float targetAngle;
    private float timeElapsed;
    private bool isRotating = false;
    private int chosenObj = 0;

    private void Awake()
    {
        if (VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.HorizontalLayout3D = this;
    }

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        ArrangeChildren();
        UpdateForChosen();
        donateButton.onClick.AddListener(BuyCurrentItem);
    }

    private void OnEnable() => UpdateForChosen();

    public void ArrangeChildren()
    {
        int count = transform.childCount;
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;

            Transform child = transform.GetChild(i);
            child.localPosition = pos;

            Vector3 lookPos = targetCamera.transform.position;
            lookPos.y = 37.7f;
            child.LookAt(lookPos);
        }

        // Поворот подписей
        foreach (var caption in captions)
        {
            Vector3 direction = caption.transform.position - targetCamera.transform.position;
            caption.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public void RotateOnDeg(bool right)
    {
        if (isRotating) return;

        initialAngle = startAngle;
        int count = 5;
        float val = 360f / count;
        float angle = right ? -val : val;

        chosenObj = right ? chosenObj + 1 : chosenObj + count - 1;
        chosenObj %= count;

        targetAngle = startAngle + angle;
        timeElapsed = 0f;
        isRotating = true;
    }

    private void Update()
    {
        if (!isRotating) return;

        timeElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(timeElapsed / 1f);

        startAngle = Mathf.LerpAngle(initialAngle, targetAngle, t);
        ArrangeChildren();

        if (t >= 1f)
        {
            isRotating = false;
            startAngle = (targetAngle + 360) % 360;
            UpdateForChosen();
        }
    }

    public void UpdateForChosen()
    {
        if (YG2.saves.massiveOfObtaining[chosenObj] == 0)
        {
            // Не куплено
            buttonOfBuying.SetActive(true);
            buttonOfEquiping.SetActive(false);

            necessaryLevelText.text = YG2.saves.langRu 
                ? $"{necessaryLevels[chosenObj]} уровень" 
                : $"{necessaryLevels[chosenObj]} level";

            costForCoinsText.text = costsForCoins[chosenObj].ToString();

            if (chosenObj == 1)
            {
                costForDonateText.text = "";
                donateButton.interactable = false;
                currencyImage.color = new Color(1, 1, 1, 0);
            }
            else
            {
                costForDonateText.text = costsForDonate[chosenObj].ToString();
                donateButton.interactable = true;
                currencyImage.color = Color.white;
            }
        }
        else
        {
            // Уже куплено
            buttonOfBuying.SetActive(false);
            buttonOfEquiping.SetActive(true);

            bool isEquipped = chosenObj == YG2.saves.equipedMaterial;

            buttonOfEquiping.GetComponent<Image>().color = isEquipped 
                ? new Color32(50, 101, 182, 255) 
                : new Color32(120, 182, 50, 255);

            buttonOfEquiping.GetComponentInChildren<Text>().text = isEquipped 
                ? (YG2.saves.langRu ? "Надето" : "equipped") 
                : (YG2.saves.langRu ? "Одеть" : "equip");
        }

        featureText.text = YG2.saves.langRu ? featuresRu[chosenObj] : featuresEn[chosenObj];
    }

    private void BuyCurrentItem()
    {
        YG2.BuyPayments(toiletIDs[chosenObj]);
    }

    public void BuyForSomething(int id)
    {
        bool isBought = false;

        if (id == 1) // за уровень
        {
            if (YG2.saves.levelOfProgress >= necessaryLevels[chosenObj])
            {
                isBought = true;
                VodovorotGameManager.Instance.MainMenuController.dzyn.Play();
            }
            else
                VodovorotGameManager.Instance.MainMenuController.fart.Play();
        }
        else if (id == 2) // за монеты
        {
            if (YG2.saves.goldCoins >= costsForCoins[chosenObj])
            {
                isBought = true;
                YG2.saves.goldCoins -= costsForCoins[chosenObj];
                VodovorotGameManager.Instance.MainMenuController.dzyn.Play();
            }
            else
                VodovorotGameManager.Instance.MainMenuController.fart.Play();
        }
        else if (id == 3) // за донат
        {
            Debug.Log("Сработала обработка InApp покупки");
            isBought = true;
        }

        if (isBought)
        {
            YG2.saves.massiveOfObtaining[chosenObj] = 1;
            VodovorotGameManager.Instance.SaveProgress();
            UpdateForChosen();
            VodovorotGameManager.Instance.MainMenuController.UpdateTriggers();
        }
    }

    public void EquipMaterial()
    {
        VodovorotGameManager.Instance.GameController.ChangeMain(chosenObj);
        UpdateForChosen();
    }
}