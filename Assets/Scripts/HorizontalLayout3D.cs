using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Vodovorot.Stage3;
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
    private SkinVisualFactory.ShopVisual[] catalogVisuals;
    private bool warnedMissingTextBindings;
    private PurchaseYG purchaseWidget;
    private bool purchaseRouteConfigured, ownsPurchaseFallback;
    private PurchaseMedia purchaseMedia, currencyMedia;

    // ImageLoadYG creates a Sprite on every Load, even for its cached texture.
    // Keep media requests separate from the always-current SDK text and release
    // only sprites created after this controller takes ownership of that loader.
    private sealed class PurchaseMedia
    {
        private readonly ImageLoadYG loader;
        private readonly Sprite originalSprite;
        private Sprite displayedSprite;
        private Texture textureBeforeRequest;
        private string requestedUrl;
        private bool pending;

        public PurchaseMedia(ImageLoadYG loader)
        {
            this.loader = loader;
            originalSprite = displayedSprite = loader != null && loader.spriteImage != null ? loader.spriteImage.sprite : null;
        }

        public void Observe()
        {
            if (loader == null) return;
            if (loader.spriteImage != null && loader.spriteImage.sprite != displayedSprite)
            {
                if (displayedSprite != null && displayedSprite != originalSprite) Destroy(displayedSprite);
                displayedSprite = loader.spriteImage.sprite;
            }
            Texture current = CurrentTexture();
            if (pending && current != null && current != textureBeforeRequest) pending = false;
        }

        public void Refresh(string url)
        {
            Observe();
            if (loader == null || !loader.gameObject.activeInHierarchy)
            {
                Interrupted();
                return;
            }
            if (string.IsNullOrEmpty(url) || url == "null" || requestedUrl == url) return;
            requestedUrl = url;
            textureBeforeRequest = CurrentTexture();
            pending = true;
            loader.Load(url);
            Observe(); // Cached images finish synchronously.
        }

        public void Interrupted()
        {
            Observe();
            // Unity stops the loader's coroutine when Buying/the panel is hidden.
            // A completed image stays cached; an interrupted request can retry.
            if (pending) { requestedUrl = null; pending = false; }
        }

        public void Dispose()
        {
            Observe();
            if (displayedSprite != null && displayedSprite != originalSprite) Destroy(displayedSprite);
        }

        private Texture CurrentTexture() => loader == null ? null : loader.rawImage != null ? loader.rawImage.texture
            : loader.spriteImage != null && loader.spriteImage.sprite != null ? loader.spriteImage.sprite.texture : null;
    }

    // Selection is the historical save/payment index. Physical sibling order stays
    // 2,3,4,0,1 and is used only by the existing carousel placement animation.
    public int ChosenLegacyIndex => chosenObj;
    public string ChosenSkinId => SkinVisualCatalog.IdForLegacyIndex(chosenObj);
    public bool UsesCatalogVisuals => catalogVisuals != null;
    public Transform SelectedVisualRoot => catalogVisuals != null ? catalogVisuals[chosenObj].root : null;
    public bool HasCompleteShopTextBindings => featureText != null && necessaryLevelText != null && costForCoinsText != null && costForDonateText != null;
    public bool UsesPersistentPurchaseRoute => purchaseRouteConfigured && purchaseWidget != null && !ownsPurchaseFallback;
    public string ActivePurchaseId => purchaseWidget != null ? purchaseWidget.id : ChosenSkinId;

    private void Awake()
    {
        if (VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.HorizontalLayout3D = this;
        ConfigurePurchaseRoute(); // Suppress the child SDK widget's Start before it can refresh an old SKU.
    }

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        InitializeCatalogVisuals();
        ConfigurePurchaseRoute();
        ArrangeChildren();
        UpdateForChosen();
    }

    private void OnEnable()
    {
        if (VodovorotGameManager.Instance != null)
            VodovorotGameManager.Instance.HorizontalLayout3D = this;
        YG2.onGetPayments += UpdateForChosen;
        UpdateForChosen();
    }

    private void OnDisable()
    {
        YG2.onGetPayments -= UpdateForChosen;
        purchaseMedia?.Interrupted(); currencyMedia?.Interrupted();
    }

    private void OnDestroy()
    {
        if (ownsPurchaseFallback && donateButton != null) donateButton.onClick.RemoveListener(BuyCurrentItem);
        purchaseMedia?.Dispose(); currencyMedia?.Dispose();
    }

    private void ConfigurePurchaseRoute()
    {
        if (purchaseRouteConfigured) return;
        // Preserve the serialized SDK handler and its separate sound listener. Do
        // not add another payment dispatch on top of that existing route.
        for (int index = 0; index < donateButton.onClick.GetPersistentEventCount(); index++)
        {
            if (donateButton.onClick.GetPersistentMethodName(index) != nameof(PurchaseYG.BuyPurchase) ||
                !(donateButton.onClick.GetPersistentTarget(index) is PurchaseYG candidate) ||
                donateButton.onClick.GetPersistentListenerState(index) == UnityEventCallState.Off) continue;
            if (purchaseWidget == null) purchaseWidget = candidate;
            else donateButton.onClick.SetPersistentListenerState(index, UnityEventCallState.Off);
        }
        if (purchaseWidget == null)
        {
            donateButton.onClick.AddListener(BuyCurrentItem);
            ownsPurchaseFallback = true;
        }
        else
        {
            // Only the controller refreshes the selected SKU. Disabling this
            // component suppresses its one-shot Start refresh, while the existing
            // persistent BuyPurchase event remains the sole purchase route.
            purchaseWidget.enabled = false;
            purchaseMedia = new PurchaseMedia(purchaseWidget.purchaseImageLoad);
            currencyMedia = new PurchaseMedia(purchaseWidget.currencyImageLoad);
        }
        purchaseRouteConfigured = true;
    }

    private void SynchronizeSelectedPurchase()
    {
        if (!purchaseRouteConfigured || purchaseWidget == null) return;
        purchaseWidget.id = ChosenSkinId;
        YG.Utils.Pay.Purchase product = YG2.PurchaseByID(ChosenSkinId);
        if (product != null)
        {
            // Prices and localization must refresh even while Buying is hidden.
            // Keep the SDK's text formatting, but update its media independently.
            var text = purchaseWidget.textLegasy;
            if (text.title != null) text.title.text = product.title;
            if (text.description != null) text.description.text = product.description;
            if (text.priceValue != null) text.priceValue.text = purchaseWidget.showCurrencyCode ? product.price : product.priceValue;
#if TMP_YG2
            var textMP = purchaseWidget.textMP;
            if (textMP.title != null) textMP.title.text = product.title;
            if (textMP.description != null) textMP.description.text = product.description;
            if (textMP.priceValue != null) textMP.priceValue.text = purchaseWidget.showCurrencyCode ? product.price : product.priceValue;
#endif
            string purchaseUrl = product.imageURI;
#if UNITY_EDITOR
            if (purchaseUrl == YG.InfoYG.DEMO_IMAGE) purchaseUrl = YG.EditorScr.ServerInfo.saveInfo.purchaseImage;
#endif
            purchaseMedia?.Refresh(purchaseUrl);
            currencyMedia?.Refresh(product.currencyImageURL);
        }
        if (chosenObj != 1 && YG2.saves.massiveOfObtaining[chosenObj] == 0)
            donateButton.interactable = product != null;
    }

    public void InitializeCatalogVisuals()
    {
        if (catalogVisuals != null) { RefreshCatalogCaptions(); return; }
        SkinVisualCatalog catalog = SkinVisualCatalog.Load();
        var originalSlots = new Transform[5];
        for (int legacy = 0; legacy < 5; legacy++)
        {
            if (toiletIDs == null || toiletIDs.Length != 5 || toiletIDs[legacy] != SkinVisualCatalog.IdForLegacyIndex(legacy))
                throw new System.InvalidOperationException("Shop catalog identity differs from the existing payment ID contract.");
            string expectedName = "SkinManicen (" + legacy + ")";
            foreach (Transform child in transform)
                if (child.name == expectedName)
                {
                    if (originalSlots[legacy] != null) throw new System.InvalidOperationException("Duplicate authored shop slot: " + expectedName);
                    originalSlots[legacy] = child;
                }
            if (originalSlots[legacy] == null || originalSlots[legacy].Find("Cap")?.GetComponent<Text>() == null)
                throw new System.InvalidOperationException("Missing full authored shop slot: " + expectedName);
        }
        var installed = new SkinVisualFactory.ShopVisual[5];
        bool russian = YG2.saves == null || YG2.saves.langRu;
        for (int legacy = 0; legacy < 5; legacy++)
            installed[legacy] = SkinVisualFactory.ReplaceShopVisual(catalog.GetByLegacyIndex(legacy), originalSlots[legacy], russian, RemapCaption);
        catalogVisuals = installed;
    }

    public bool TryGetVisualById(string skinId, out Transform visual)
    {
        if (catalogVisuals != null) foreach (SkinVisualFactory.ShopVisual item in catalogVisuals)
            if (item.entry.id == skinId) { visual = item.root; return visual != null; }
        visual = null; return false;
    }

    private void RefreshCatalogCaptions()
    {
        if (catalogVisuals == null) return;
        bool russian = YG2.saves == null || YG2.saves.langRu;
        foreach (SkinVisualFactory.ShopVisual visual in catalogVisuals) SkinVisualFactory.ApplyCaptionLanguage(visual, russian);
    }

    private void RemapCaption(Text original, Text replacement)
    {
        if (captions != null) for (int index = 0; index < captions.Length; index++)
            if (captions[index] == original.gameObject) captions[index] = replacement.gameObject;
        // These are runtime references only. The controller's localization arrays
        // must continue pointing to live captions after the visual source changes.
        foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
            foreach (MainMenuController menu in sceneRoot.GetComponentsInChildren<MainMenuController>(true))
            {
                RemapTextArray(menu.PanelOfSkins, original, replacement);
                RemapTextArray(menu.DPanelOfSkins, original, replacement);
            }
    }

    private static void RemapTextArray(Text[] array, Text original, Text replacement)
    {
        if (array == null) return;
        for (int index = 0; index < array.Length; index++) if (array[index] == original) array[index] = replacement;
    }

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

    /// <summary>Transfers only the selected visual pose when MainMenu changes its authored Canvas.</summary>
    public void CopySelectionForLayout(HorizontalLayout3D source)
    {
        if (source == null || source == this) return;
        chosenObj = source.chosenObj;
        startAngle = source.isRotating ? source.targetAngle : source.startAngle;
        initialAngle = targetAngle = startAngle;
        timeElapsed = 0; isRotating = false;
        if (targetCamera != null) ArrangeChildren();
        UpdateForChosen();
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
        UpdateForChosen(); // The visible price and SKU follow the same newly selected legacy ID immediately.

        targetAngle = startAngle + angle;
        timeElapsed = 0f;
        isRotating = true;
    }

    private void Update()
    {
        purchaseMedia?.Observe(); currencyMedia?.Observe();
        RefreshCatalogCaptions();
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
        RefreshCatalogCaptions();
        if (!HasCompleteShopTextBindings && !warnedMissingTextBindings)
        {
            warnedMissingTextBindings = true;
            Debug.LogWarning("[SkinVisualCatalog] Existing shop price/description Text references are missing. Full models remain available; economic UI bindings need inspection.", this);
        }
        if (YG2.saves.massiveOfObtaining[chosenObj] == 0)
        {
            // Не куплено
            buttonOfBuying.SetActive(true);
            buttonOfEquiping.SetActive(false);

            if (necessaryLevelText != null) necessaryLevelText.text = YG2.saves.langRu
                ? $"{necessaryLevels[chosenObj]} уровень"
                : $"{necessaryLevels[chosenObj]} level";

            if (costForCoinsText != null) costForCoinsText.text = costsForCoins[chosenObj].ToString();

            if (chosenObj == 1)
            {
                if (costForDonateText != null) costForDonateText.text = "";
                donateButton.interactable = false;
                currencyImage.color = new Color(1, 1, 1, 0);
            }
            else
            {
                if (costForDonateText != null) costForDonateText.text = costsForDonate[chosenObj].ToString();
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

            Button equipButton = buttonOfEquiping.GetComponent<Button>();
            Graphic equipGraphic = equipButton != null ? equipButton.targetGraphic : buttonOfEquiping.GetComponent<Image>();
            if (equipGraphic != null) equipGraphic.color = isEquipped ? RankingVisualStyle.Load().blue : RankingVisualStyle.Load().green;

            buttonOfEquiping.GetComponentInChildren<Text>().text = isEquipped 
                ? (YG2.saves.langRu ? "Надето" : "equipped") 
                : (YG2.saves.langRu ? "Одеть" : "equip");
        }

        if (featureText != null) featureText.text = YG2.saves.langRu ? featuresRu[chosenObj] : featuresEn[chosenObj];
        SynchronizeSelectedPurchase();
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
