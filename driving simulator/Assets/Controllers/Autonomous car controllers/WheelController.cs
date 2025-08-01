using UnityEngine;
using UnityEngine.InputSystem;

public class WheelController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    [SerializeField] WheelCollider frontRight;
    [SerializeField] WheelCollider frontLeft;
    [SerializeField] WheelCollider rearLeft;
    [SerializeField] WheelCollider rearRight;

    [Header("Wheel Transforms (Meshes)")]
    [SerializeField] Transform frontRightTransform;
    [SerializeField] Transform frontLeftTransform;
    [SerializeField] Transform rearLeftTransform;
    [SerializeField] Transform rearRightTransform;

    [Header("Driving Parameters")]
    public float acceleration = 2500f;
    public float brakingForce = 300f;
    public float maxTurnAngle = 40f;
    
    private float currentAcceleration = 0f;
    private float currentBrakingForce = 0f;
    private float currentTurnAngle = 0f;

    [Header("Input System")]
    public InputActionAsset controls;
    private InputAction steeringAction, throttleAction, brakeAction, toggleAutoDriveAction;

    [Header("Control Mode")]
    public bool useG29 = true;

    void Awake()
    {
        var driving = controls.FindActionMap("Driving", throwIfNotFound: true);
        steeringAction = driving.FindAction("Steer", throwIfNotFound: true);
        throttleAction = driving.FindAction("Throttle", throwIfNotFound: true);
        brakeAction = driving.FindAction("Brake", throwIfNotFound: true);

        driving.Enable();
    }
    private void FixedUpdate()
    {
        float steerInput = 0f, throttleInput = 0f, brakeInput = 0f;
        
        if (useG29)
        {
            steerInput = steeringAction.ReadValue<float>();
            throttleInput = throttleAction.ReadValue<float>();
            brakeInput = brakeAction.ReadValue<float>();
        }
        else
        {
            steerInput = Input.GetAxis("Horizontal");
            throttleInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
            brakeInput = Input.GetKey(KeyCode.Space) ? 1f : 0f;
        }

        currentAcceleration = acceleration * throttleInput;
        currentBrakingForce = brakingForce * brakeInput;
        currentTurnAngle = maxTurnAngle * steerInput;

        frontRight.motorTorque = currentAcceleration;
        frontLeft.motorTorque = currentAcceleration;

        frontRight.brakeTorque = currentBrakingForce;
        frontLeft.brakeTorque = currentBrakingForce;
        rearLeft.brakeTorque = currentBrakingForce;
        rearRight.brakeTorque = currentBrakingForce;

        frontLeft.steerAngle = currentTurnAngle;
        frontRight.steerAngle = currentTurnAngle;

        UpdateWheel(frontRight, frontRightTransform);
        UpdateWheel(frontLeft, frontLeftTransform);
        UpdateWheel(rearLeft, rearLeftTransform);
        UpdateWheel(rearRight, rearRightTransform);
    }

    void UpdateWheel(WheelCollider col, Transform trans)
    {
        Vector3 position;
        Quaternion rotation;
        col.GetWorldPose(out position, out rotation);

        trans.position = position;
        trans.rotation = rotation;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            useG29 = !useG29;
            Debug.Log(useG29 ? "Using G29 Wheel" : "Using Keyboard Controls");
         }    
    }
}
