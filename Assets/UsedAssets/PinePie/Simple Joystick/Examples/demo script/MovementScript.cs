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

        private JoystickController joystickController;
        private Rigidbody rb;
        private bool isBoosting = false;

        private BlackHoleController playerHole;   // ссылка на нашу дыру игрока

        void Start()
        {
            // Выбираем кнопку буста в зависимости от платформы
            boostButton = YG2.envir.isMobile ? boostButtons[0] : boostButtons[1];

            joystickController = GameObject.Find("JoystickRuler").GetComponent<JoystickController>();
            rb = GetComponent<Rigidbody>();

            // Находим BlackHoleController один раз в Start
            playerHole = GetComponentInParent<BlackHoleController>();
            if (playerHole == null)
                Debug.LogError("MovementScript: Не найден BlackHoleController на родителе!");

            // Регистрируемся в GamingManager (если нужно)
        }

        void FixedUpdate()
        {
            if (playerHole == null) return;

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
            if (isBoosting || (boostButton != null && boostButton.GetComponent<BoostButton>().isHolding))
                boostMultiplier = 2f;

            // Берём текущий уровень игрока из BlackHoleController
            int currentLevel = playerHole.currentLevel;
            float currentSpeed = (currentLevel < levelSpeeds.Length) ? levelSpeeds[currentLevel] : levelSpeeds[^1];

            // Финальное движение
            Vector3 newPosition = rb.position 
                + 0.5f * boostMultiplier * movement 
                * currentSpeed 
                * Time.fixedDeltaTime;

            // Ограничение по границам карты
            newPosition.x = Mathf.Clamp(newPosition.x, 
                VodovorotGameManager.Instance.GamingManager.minX, 
                VodovorotGameManager.Instance.GamingManager.maxX);

            newPosition.z = Mathf.Clamp(newPosition.z, 
                VodovorotGameManager.Instance.GamingManager.minZ, 
                VodovorotGameManager.Instance.GamingManager.maxZ);

            rb.MovePosition(newPosition);
        }
    }
}