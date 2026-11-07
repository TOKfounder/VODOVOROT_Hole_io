using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YG;

namespace Vodovorot.Stage3
{
    /// <summary>
    /// Reuses the authored wide MainMenu on a landscape phone. Installed only in
    /// MainMenu; gameplay keeps its original mobile Canvas and joystick routing.
    /// </summary>
    public sealed class MobileMenuLayout : MonoBehaviour
    {
        private static readonly string[] PanelPaths =
        {
            "PanelOfSkins", "PanelOfMaps", "PanelOfModes", "PanelOfProgress", "PanelOfValute", "PanelOfLeaders"
        };

        private GameController controller;
        private Canvas desktop, mobile;
        private HorizontalLayout3D desktopShop, mobileShop;
        private InputField desktopNickname, mobileNickname;
        private Transform[] desktopPanels, mobilePanels;
        private int lastWidth, lastHeight;
        private bool lastMobileDevice, initialized;
#if UNITY_EDITOR
        private bool? reviewMobileDevice;
#endif

        public bool UsingDesktopMenu => initialized && controller.currentCanvas == desktop;
        public HorizontalLayout3D ActiveShop => UsingDesktopMenu ? desktopShop : mobileShop;

        /// <summary>No gameplay Canvas, profile or SDK environment fields are changed.</summary>
        public static MobileMenuLayout Install(GameController owner)
        {
            if (owner == null || owner.gameObject.scene.name != "MainMenu") return null;
            MobileMenuLayout layout = owner.GetComponent<MobileMenuLayout>();
            if (layout == null) layout = owner.gameObject.AddComponent<MobileMenuLayout>();
            layout.controller = owner;
            layout.InitializeBindings();
            layout.RefreshLayout();
            return layout;
        }

        public static bool UsesDesktopMenu(bool mobileDevice, int width, int height) =>
            !mobileDevice || (width > 0 && height > 0 && width > height);

        private bool IsMobileDevice
        {
            get
            {
#if UNITY_EDITOR
                if (reviewMobileDevice.HasValue) return reviewMobileDevice.Value;
#endif
                return YG2.envir.isMobile;
            }
        }

#if UNITY_EDITOR
        /// <summary>Scene-local QA override; null returns to the real SDK device flag.</summary>
        public void SetMobileDeviceOverrideForReview(bool? mobileDevice)
        {
            reviewMobileDevice = mobileDevice;
            RefreshLayout();
        }
#endif

        private void InitializeBindings()
        {
            if (initialized || controller == null || controller.CanvasForDesktop == null || controller.CanvasForMobile == null) return;
            desktop = controller.CanvasForDesktop.GetComponent<Canvas>();
            mobile = controller.CanvasForMobile.GetComponent<Canvas>();
            if (desktop == null || mobile == null) return;
            // These are the two explicit authored shop and nickname paths, never
            // a scene-wide controller, font or sprite search.
            desktopShop = desktop.transform.Find("PanelOfSkins/LayoutGroup3D")?.GetComponent<HorizontalLayout3D>();
            mobileShop = mobile.transform.Find("PanelOfSkins/LayoutGroup3D")?.GetComponent<HorizontalLayout3D>();
            desktopNickname = desktop.transform.Find("InputFieldForNick")?.GetComponent<InputField>();
            mobileNickname = mobile.transform.Find("InputFieldForNick")?.GetComponent<InputField>();
            desktopPanels = new Transform[PanelPaths.Length + 1];
            mobilePanels = new Transform[PanelPaths.Length + 1];
            for (int index = 0; index < PanelPaths.Length; index++)
            {
                desktopPanels[index] = desktop.transform.Find(PanelPaths[index]);
                mobilePanels[index] = mobile.transform.Find(PanelPaths[index]);
            }
            desktopPanels[PanelPaths.Length] = desktop.transform.Find("DesktopPanelOfSettings");
            mobilePanels[PanelPaths.Length] = mobile.transform.Find("MobilePanelOfSettings");
            initialized = true;
        }

        private void LateUpdate()
        {
            if (!initialized) InitializeBindings();
            if (!initialized) return;
            bool mobileDevice = IsMobileDevice;
            Canvas expected = UsesDesktopMenu(mobileDevice, Screen.width, Screen.height) ? desktop : mobile;
            Canvas other = expected == desktop ? mobile : desktop;
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastMobileDevice != mobileDevice ||
                controller.currentCanvas != expected || !expected.gameObject.activeSelf || other.gameObject.activeSelf)
                RefreshLayout();
            else SynchronizeShopPointer(expected == desktop ? desktopShop : mobileShop);
        }

        /// <summary>Re-evaluates real viewport dimensions and preserves the currently open authored form.</summary>
        public void RefreshLayout()
        {
            if (!initialized) InitializeBindings();
            if (!initialized || controller.gameObject.scene.name != "MainMenu") return;
            bool mobileDevice = IsMobileDevice;
            lastWidth = Screen.width; lastHeight = Screen.height; lastMobileDevice = mobileDevice;
            Canvas destination = UsesDesktopMenu(mobileDevice, lastWidth, lastHeight) ? desktop : mobile;
            Canvas source = controller.currentCanvas == desktop || controller.currentCanvas == mobile ? controller.currentCanvas :
                mobile.gameObject.activeSelf ? mobile : desktop;
            if (source != destination) TransferFormState(source == desktop);

            InputField sourceNickname = source == desktop ? desktopNickname : mobileNickname;
            InputField destinationNickname = destination == desktop ? desktopNickname : mobileNickname;
            bool editingNickname = source != destination && sourceNickname != null && sourceNickname.isFocused;
            InputField.EndEditEvent endEdit = editingNickname ? sourceNickname.onEndEdit : null;
            // InputField.OnDisable emits onEndEdit. Rotating the menu must not
            // commit a draft nickname or write the player's profile.
            if (editingNickname) sourceNickname.onEndEdit = new InputField.EndEditEvent();
            try
            {
                controller.currentCanvas = destination;
                (destination == desktop ? mobile : desktop).gameObject.SetActive(false);
                destination.gameObject.SetActive(true);
            }
            finally
            {
                if (editingNickname && sourceNickname != null) sourceNickname.onEndEdit = endEdit;
            }
            if (editingNickname && destinationNickname != null)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(destinationNickname.gameObject);
                destinationNickname.ActivateInputField();
            }
            // Inactive shop Awakes and the singleton's initial discovery can leave
            // this pointer on the other Canvas. Payments must refresh this menu.
            SynchronizeShopPointer(destination == desktop ? desktopShop : mobileShop);
        }

        private static void SynchronizeShopPointer(HorizontalLayout3D shop)
        {
            if (VodovorotGameManager.Instance != null && VodovorotGameManager.Instance.HorizontalLayout3D != shop)
                VodovorotGameManager.Instance.HorizontalLayout3D = shop;
        }

        private void TransferFormState(bool sourceIsDesktop)
        {
            Transform[] sources = sourceIsDesktop ? desktopPanels : mobilePanels;
            Transform[] destinations = sourceIsDesktop ? mobilePanels : desktopPanels;
            for (int index = 0; index < sources.Length; index++)
                if (sources[index] != null && destinations[index] != null)
                    destinations[index].gameObject.SetActive(sources[index].gameObject.activeSelf);
            InputField sourceNickname = sourceIsDesktop ? desktopNickname : mobileNickname;
            InputField destinationNickname = sourceIsDesktop ? mobileNickname : desktopNickname;
            if (sourceNickname != null && destinationNickname != null)
                destinationNickname.SetTextWithoutNotify(sourceNickname.text);
            HorizontalLayout3D sourceShop = sourceIsDesktop ? desktopShop : mobileShop;
            HorizontalLayout3D destinationShop = sourceIsDesktop ? mobileShop : desktopShop;
            if (destinationShop != null) destinationShop.CopySelectionForLayout(sourceShop);
        }
    }
}
