using UnityEngine;
using UnityEngine.UI;
using Vodovorot.Stage3;
using YG;

namespace Vodovorot.RoundExperience
{
    /// <summary>Compares saved portraits from the real catalogue. Purchase/equip routes stay in the shop.</summary>
    public sealed class SkinComparisonView : MonoBehaviour
    {
        [SerializeField] private HorizontalLayout3D[] shops;
        [SerializeField] private Button[] openButtons;
        [SerializeField] private GameObject sheet;
        [SerializeField] private Button previous, next, close;
        [SerializeField] private Text title, promise, equippedName, selectedName, equippedDescription, selectedDescription;
        [SerializeField] private RawImage equippedImage, selectedImage;
        [SerializeField] private Texture2D[] portraitsRu, portraitsEn;
        private int selected = -1, equipped = -1;
        private bool russian;
        private HorizontalLayout3D activeShop;

        public bool IsOpen => sheet != null && sheet.activeSelf;
        public int SelectedIndex => selected;
        public int EquippedIndex => equipped;

        private void Awake()
        {
            foreach (Button button in openButtons)
                button.onClick.AddListener(Open);
            previous.onClick.AddListener(Previous);
            next.onClick.AddListener(Next);
            close.onClick.AddListener(Close);
            Close();
            RefreshLanguage();
        }

        private void OnDestroy()
        {
            foreach (Button button in openButtons)
                if (button != null) button.onClick.RemoveListener(Open);
            if (previous != null) previous.onClick.RemoveListener(Previous);
            if (next != null) next.onClick.RemoveListener(Next);
            if (close != null) close.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            activeShop = FindShop();
            if (activeShop == null || YG2.saves == null)
                return;
            sheet.SetActive(true);
            selected = equipped = -1;
            RefreshComparison();
        }

        public void Close()
        {
            if (sheet != null)
                sheet.SetActive(false);
        }

        private void Previous() { activeShop?.RotateOnDeg(false); RefreshComparison(); }
        private void Next() { activeShop?.RotateOnDeg(true); RefreshComparison(); }

        private void LateUpdate()
        {
            bool changedLanguage = russian != (YG2.saves == null || YG2.saves.langRu);
            if (changedLanguage)
                RefreshLanguage();
            if (!IsOpen)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            activeShop = FindShop();
            if (activeShop == null)
            {
                Close();
                return;
            }
            if (changedLanguage || selected != activeShop.ChosenLegacyIndex || equipped != YG2.saves.equipedMaterial)
                RefreshComparison();
        }

        private HorizontalLayout3D FindShop()
        {
            foreach (HorizontalLayout3D shop in shops)
                if (shop != null && shop.isActiveAndEnabled)
                    return shop;
            return null;
        }

        private void RefreshLanguage()
        {
            russian = YG2.saves == null || YG2.saves.langRu;
            foreach (Button button in openButtons)
                button.GetComponentInChildren<Text>(true).text = russian ? "Сравнить с надетым" : "Compare with equipped";
            title.text = russian ? "Сравнение скинов" : "Compare skins";
            promise.text = SkinAppearanceText.CosmeticPromise(russian);
            close.GetComponentInChildren<Text>(true).text = russian ? "В магазин" : "Back to shop";
        }

        private void RefreshComparison()
        {
            if (activeShop == null || YG2.saves == null)
                return;
            selected = Mathf.Clamp(activeShop.ChosenLegacyIndex, 0, 4);
            equipped = Mathf.Clamp(YG2.saves.equipedMaterial, 0, 4);
            SkinVisualCatalog catalog = SkinVisualCatalog.Load();
            SkinVisualCatalog.Entry left = catalog.GetByLegacyIndex(equipped);
            SkinVisualCatalog.Entry right = catalog.GetByLegacyIndex(selected);
            equippedName.text = (russian ? "Надето · " : "Equipped · ") + (russian ? left.nameRu : left.nameEn).Replace('\n', ' ');
            selectedName.text = (russian ? "Выбрано · " : "Selected · ") + (russian ? right.nameRu : right.nameEn).Replace('\n', ' ');
            equippedDescription.text = SkinAppearanceText.Description(equipped, russian);
            selectedDescription.text = SkinAppearanceText.Description(selected, russian);
            Texture2D[] images = russian ? portraitsRu : portraitsEn;
            equippedImage.texture = images[equipped];
            selectedImage.texture = images[selected];
        }
    }
}
