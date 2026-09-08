using UnityEngine;
using UnityEngine.UI;
using YG;

namespace PinePie.SimpleJoystick.Examples.DemoScript
{
	public class MovementScript : MonoBehaviour
	{
		private JoystickController joystickController;
		private CanvasGroup joystickGroup;
		public GameObject WithoutCamera;
		public float rotationSpeed = 10f;
		public float[] levelSpeeds = { 6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f, 28f };
		public Button[] boostButtons;
		private Button boostButton;
		private BoostButton boostHold;

		private Vector3 movement;
		private Rigidbody rb;
		private bool holding;
		private bool keyboardOwnsInput;

		void Start()
		{
			if (YG2.envir.isMobile)
				boostButton = boostButtons != null && boostButtons.Length > 0 ? boostButtons[0] : null;
			else
				boostButton = boostButtons != null && boostButtons.Length > 1 ? boostButtons[1] : null;
			if (boostButton != null)
			{
				boostHold = boostButton.GetComponent<BoostButton>();
				boostButton.gameObject.SetActive(true);
			}

			ResolveJoystick();
			rb = GetComponent<Rigidbody>();
		}

		void ResolveJoystick()
		{
			JoystickController[] sticks = FindObjectsByType<JoystickController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < sticks.Length; i++)
			{
				if (sticks[i] == null)
					continue;
				if (!sticks[i].gameObject.activeInHierarchy && YG2.envir.isDesktop)
					continue;
				joystickController = sticks[i];
				EnsureJoystickGroup(sticks[i].gameObject);
				break;
			}

			if (joystickController == null)
			{
				GameObject found = GameObject.Find("JoystickRuler");
				if (found != null)
				{
					joystickController = found.GetComponent<JoystickController>();
					EnsureJoystickGroup(found);
				}
			}
		}

		void EnsureJoystickGroup(GameObject root)
		{
			if (root == null)
				return;
			joystickGroup = root.GetComponent<CanvasGroup>();
			if (joystickGroup == null)
				joystickGroup = root.AddComponent<CanvasGroup>();
			joystickGroup.blocksRaycasts = true;
			joystickGroup.interactable = true;
		}

		void FixedUpdate()
		{
			if (GamingManager.Instance == null || BlackHoleController.Player == null || BlackHoleController.Player.IsConsumed)
				return;
			if (MatchPause.IsPaused)
				return;

			if (boostButton != null && !boostButton.gameObject.activeSelf)
				boostButton.gameObject.SetActive(true);

			if (YG2.envir.isDesktop && boostButton != null && boostButton.image != null)
			{
				holding = Input.GetKey(KeyCode.LeftShift)
					|| Input.GetKey(KeyCode.RightShift)
					|| Input.GetKey(KeyCode.Space);
				boostButton.image.color = holding
					? new Color32(0x38, 0xc8, 0x07, 0xFF)
					: new Color32(0x48, 0xFF, 0x09, 0xFF);
			}

			Vector2 key = Vector2.zero;
			if (YG2.envir.isDesktop)
			{
				key.x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
					- (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
				key.y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
					- (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
			}

			bool stickPressed = joystickController != null && joystickController.isPressed && joystickController.InputDirection.sqrMagnitude > 0.0001f;
			if (stickPressed)
				keyboardOwnsInput = false;
			else if (key.sqrMagnitude > 0.01f)
				keyboardOwnsInput = true;

			if (YG2.envir.isDesktop && joystickGroup != null)
				joystickGroup.alpha = keyboardOwnsInput ? 0f : 1f;

			movement = Vector3.zero;
			if (keyboardOwnsInput && key.sqrMagnitude > 0.01f)
				movement = new Vector3(key.x, 0f, key.y);
			else if (stickPressed)
			{
				Vector2 joysticInput = joystickController.InputDirection;
				movement = new Vector3(joysticInput.x, 0f, joysticInput.y);
			}

			if (movement.sqrMagnitude > 1f)
				movement.Normalize();
			if (movement.sqrMagnitude > 0.0001f && WithoutCamera != null)
			{
				Quaternion targetRotation = Quaternion.LookRotation(movement);
				WithoutCamera.transform.rotation = Quaternion.Slerp(
					WithoutCamera.transform.rotation,
					targetRotation,
					rotationSpeed * Time.deltaTime);
			}

			float k = 1f;
			if (holding || (boostHold != null && boostHold.isHolding))
				k = 2f;

			int level = BlackHoleController.Player.currentLevel;
			float speed = (level >= 0 && level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];
			speed *= SkinStats.SpeedMultiplier;
			Vector3 newPosition = rb.position + 0.5f * k * movement * speed * Time.fixedDeltaTime;
			newPosition.x = Mathf.Clamp(newPosition.x, GamingManager.Instance.minX, GamingManager.Instance.maxX);
			newPosition.z = Mathf.Clamp(newPosition.z, GamingManager.Instance.minZ, GamingManager.Instance.maxZ);
			rb.MovePosition(newPosition);
		}
	}
}