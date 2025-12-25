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

		void Update()
		{
			float pointOfBoost = YG2.envir.isMobile ? 50f : 75f;
			if (GamingManager.Instance.perc * 100f >= 75f)
			{
				boostButton.gameObject.SetActive(true);
			} else {
				boostButton.gameObject.SetActive(false);
			}
			if (YG2.envir.isDesktop)
			{
				holding = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
				if (holding)
					boostButton.image.color = new Color32(0x38, 0xc8, 0x07, 0xFF);
				else
					boostButton.image.color = new Color32(0x48, 0xFF, 0x09, 0xFF);
			}

			float moveX = Input.GetAxis("Horizontal");
			float moveY = Input.GetAxis("Vertical");
			movement = new Vector3(moveX, 0f, moveY);
			if (joystickController.isPressed)
			{
				Vector2 joysticInput = joystickController.InputDirection;
				movement = new Vector3(joysticInput.x, 0f, joysticInput.y);
			}

			if (movement.magnitude > 1f)
				movement = movement.normalized;
			if (movement.magnitude > 0.01f)
			{
				Quaternion targetRotation = Quaternion.LookRotation(movement);
				WithoutCamera.transform.rotation = Quaternion.Slerp(WithoutCamera.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
			}
			float k = 1f;
			if (holding || boostButton.GetComponent<BoostButton>().isHolding)
				k = 2f;
			Vector3 newPosition = rb.position + k * movement * levelSpeeds[BlackHoleController.Instance.currentLevel] * Time.deltaTime;
			newPosition.x = Mathf.Clamp(newPosition.x, GamingManager.Instance.minX, GamingManager.Instance.maxX);
			newPosition.z = Mathf.Clamp(newPosition.z, GamingManager.Instance.minZ, GamingManager.Instance.maxZ);
			rb.MovePosition(newPosition);
		}
	}
}