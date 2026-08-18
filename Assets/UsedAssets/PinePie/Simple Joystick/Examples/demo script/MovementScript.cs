using UnityEngine;
using UnityEngine.UI;
using YG;

namespace PinePie.SimpleJoystick.Examples.DemoScript
{
	public class MovementScript : MonoBehaviour
	{
		private JoystickController joystickController;
		public GameObject WithoutCamera;
		public float rotationSpeed = 10f;
		public float[] levelSpeeds = {6f, 6.89f, 7.78f, 8.67f, 9.56f, 10.44f, 13.83f, 15.22f, 20f, 25f};
		public Button[] boostButtons;
		private Button boostButton;
		
		private Vector3 movement;
		private Rigidbody rb;
		private bool holding = false;

		void Start()
		{
			if (YG2.envir.isMobile)
				boostButton = boostButtons[0];
			else
				boostButton = boostButtons[1];
			joystickController = GameObject.Find("JoystickRuler").GetComponent<JoystickController>();
			rb = GetComponent<Rigidbody>();
		}

		void FixedUpdate()
		{
			if (GamingManager.Instance == null || BlackHoleController.Player == null || BlackHoleController.Player.IsConsumed)
				return;

			if (boostButton != null)
			{
				bool showBoost = GamingManager.Instance.perc * 100f >= 70f;
				boostButton.gameObject.SetActive(showBoost);
				if (YG2.envir.isDesktop)
				{
					holding = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
					boostButton.image.color = holding
						? new Color32(0x38, 0xc8, 0x07, 0xFF)
						: new Color32(0x48, 0xFF, 0x09, 0xFF);
				}
			}

			movement = Vector3.zero;
			if (joystickController != null && joystickController.isPressed)
			{
				Vector2 joysticInput = joystickController.InputDirection;
				movement = new Vector3(joysticInput.x, 0f, joysticInput.y);
			}

			if (movement.magnitude > 1f)
				movement = movement.normalized;
			if (movement.magnitude > 0.01f && WithoutCamera != null)
			{
				Quaternion targetRotation = Quaternion.LookRotation(movement);
				WithoutCamera.transform.rotation = Quaternion.Slerp(WithoutCamera.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
			float k = 1f;
			BoostButton boost = boostButton != null ? boostButton.GetComponent<BoostButton>() : null;
			if (holding || (boost != null && boost.isHolding))
				k = 2f;

			int level = BlackHoleController.Player != null
				? BlackHoleController.Player.currentLevel
				: 0;
			float speed = (level >= 0 && level < levelSpeeds.Length) ? levelSpeeds[level] : levelSpeeds[^1];
			Vector3 newPosition = rb.position + 0.5f * k * movement * speed * Time.fixedDeltaTime;
			newPosition.x = Mathf.Clamp(newPosition.x, GamingManager.Instance.minX, GamingManager.Instance.maxX);
			newPosition.z = Mathf.Clamp(newPosition.z, GamingManager.Instance.minZ, GamingManager.Instance.maxZ);
			rb.MovePosition(newPosition);
		}
	}
}