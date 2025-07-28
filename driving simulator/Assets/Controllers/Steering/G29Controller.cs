using UnityEngine;
using UnityEngine.InputSystem;

public class G29Controller : MonoBehaviour
{
	
	public InputActionAsset controls;
	
	private InputAction steering;
	private InputAction throttle;
	private InputAction brake;
	
    void OnEnable()
	{
		var driving = controls.FindActionMap("Driving");
		
		steering = driving.FindAction("Steer");
		throttle = driving.FindAction("Throttle");
		brake = driving.FindAction("Brake");
		
		driving.Enable();
	}
	
	void Update()
	{
		float steerVal = steering.ReadValue<float>();
		float throttleVal = throttle.ReadValue<float>();
		float brakeVal = brake.ReadValue<float>();

		Debug.Log($"Steering: {steerVal:F2} | Throttle: {throttleVal:F2} | Brake: {brakeVal:F2}");
	}
}
