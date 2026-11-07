using UnityEngine;
using UnityEngine.UI;
using YG;

namespace PinePie.SimpleJoystick.Examples.DemoScript
{
    public class MovementScript : MonoBehaviour
    {
        [Header("References")]
        public GameObject WithoutCamera;
        public Button[] boostButtons;
        private Button boostButton;

        [Header("Movement Settings")]
        public float rotationSpeed = 10f;
        public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f };

        [SerializeField] private JoystickController mobileJoystick;
        [SerializeField] private JoystickController desktopJoystick;
        private JoystickController joystickController;
        [SerializeField] private GamingManager gamingManager;
        private BoostButton boostControl;
        private Rigidbody rb;
        private bool isBoosting = false;
        private bool focused = true;
        private bool inputSuspended;

        private BlackHoleController playerHole;   // ссылка на нашу дыру игрока

        void Start()
        {
            // Выбираем кнопку буста в зависимости от платформы
            boostButton = YG2.envir.isMobile ? boostButtons[0] : boostButtons[1];
            joystickController = YG2.envir.isMobile ? mobileJoystick : desktopJoystick;

            rb = GetComponent<Rigidbody>();
            if (boostButton != null) boostControl = boostButton.GetComponent<BoostButton>();

            // Находим BlackHoleController один раз в Start
            playerHole = GetComponentInParent<BlackHoleController>();
            if (playerHole == null)
                Debug.LogError("MovementScript: Не найден BlackHoleController на родителе!");

            // Регистрируемся в GamingManager (если нужно)
        }

        void FixedUpdate()
        {
            if (playerHole == null || gamingManager == null || !focused || YG2.nowAdsShow) return;

            HandleBoostUI();
            HandleInput();
            MovePlayer();
        }

        private void HandleBoostUI()
        {
            if (YG2.envir.isDesktop)
            {
                isBoosting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                boostButton.image.color = isBoosting
                    ? new Color32(0x38, 0xC8, 0x07, 0xFF)
                    : new Color32(0x48, 0xFF, 0x09, 0xFF);
            }
        }

        private void HandleInput()
        {
            // Для десктопа используем Shift
            if (YG2.envir.isDesktop)
            {
                isBoosting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }
            // Для мобильного буст берётся из BoostButton компонента
        }

        private void MovePlayer()
        {
            Vector3 movement = Vector3.zero;

            if (joystickController != null && joystickController.isPressed)
            {
                Vector2 input = joystickController.InputDirection;
                movement = new Vector3(input.x, 0f, input.y);
            }

            if (YG2.envir.isDesktop)
            {
                float horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                    (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                float vertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                    (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                if (horizontal != 0f || vertical != 0f)
                    movement = new Vector3(horizontal, 0f, vertical);
            }

            if (movement.magnitude > 1f)
                movement = movement.normalized;

            // Поворот WithoutCamera
            if (movement.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movement);
                WithoutCamera.transform.rotation = Quaternion.Slerp(
                    WithoutCamera.transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime);
            }

            // Множитель скорости от буста
            float boostMultiplier = 1f;
            if (isBoosting || (boostControl != null && boostControl.isHolding))
                boostMultiplier = 2f;

            // Берём текущий уровень игрока из BlackHoleController
            int currentLevel = playerHole.currentLevel;
            float currentSpeed = (currentLevel < levelSpeeds.Length) ? levelSpeeds[currentLevel] : levelSpeeds[^1];

            // Финальное движение
            Vector3 newPosition = rb.position 
                + 0.5f * boostMultiplier * movement 
                * currentSpeed 
                * Time.fixedDeltaTime;

            rb.MovePosition(ClampPositionToMap(newPosition));
        }

        private Vector3 ClampPositionToMap(Vector3 position)
        {
            // The whole visible rim stays on the floor; clamping only its center
            // allowed part of the hole to float beyond the outer road at a wall.
            float radius = Mathf.Max(playerHole.size.x, playerHole.size.z) * .5f + .5f;
            float insetX = Mathf.Min(radius, (gamingManager.maxX - gamingManager.minX) * .5f);
            float insetZ = Mathf.Min(radius, (gamingManager.maxZ - gamingManager.minZ) * .5f);
            position.x = Mathf.Clamp(position.x, gamingManager.minX + insetX, gamingManager.maxX - insetX);
            position.z = Mathf.Clamp(position.z, gamingManager.minZ + insetZ, gamingManager.maxZ - insetZ);
            return position;
        }

        private void OnEnable()
        {
            YG2.onFocusWindowGame += OnApplicationFocus;
            YG2.onOpenAnyAdv += ResetHeldInput;
        }

        private void OnDisable()
        {
            YG2.onFocusWindowGame -= OnApplicationFocus;
            YG2.onOpenAnyAdv -= ResetHeldInput;
            ResetHeldInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) ResetHeldInput();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) ResetHeldInput();
        }

        private void Update()
        {
            bool suspended = Time.timeScale <= 0f || YG2.nowAdsShow;
            if (suspended && !inputSuspended) ResetHeldInput();
            inputSuspended = suspended;
        }

        private void ResetHeldInput()
        {
            isBoosting = false;
            if (boostControl != null) boostControl.isHolding = false;
            if (joystickController != null) joystickController.ResetInput();
        }
    }
}
