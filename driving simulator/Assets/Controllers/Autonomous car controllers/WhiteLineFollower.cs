using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using System; // <<< NEW

public class WhiteLineFollower : MonoBehaviour
{
    // ------------ G29 / Input System ------------
    [Header("Input System()")]
    public InputActionAsset controls;                       // drag your .inputactions here
    private InputAction steerAction, throttleAction, brakeAction, toggleAutoDriveAction;

    [Header("Manual (G29) driving params")]
    public float maxManualSpeed = 100f;
    public float accelPerSec = 40f;
    public float brakeDecelPerSec = 60f;
    public float maxSteerPerFixedStep = 0.5f;               // how much you rotate per FixedUpdate for |steer| = 1
    [Header("Wheel Colliders")]
    public WheelCollider frontRight;
    public WheelCollider frontLeft;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Wheel Transforms")]
    public Transform frontRightTransform;
    public Transform frontLeftTransform;
    public Transform rearLeftTransform;
    public Transform rearRightTransform;
    [Header("Keyboard Control")]
    public float keyboardSteerSpeed = 1.5f;
    public float keyboardThrottle = 1000f;
    public float keyboardBrake = 500f;
    [Header("Keyboard Toggle")]
    public KeyCode toggleKey = KeyCode.T;

    // ------------ Auto drive (your original fields) ------------
    private bool isAutodrive = true;
    public float maxAutonomousSpeed = 50f;
    public Camera carCamera;
    private Texture2D tex;
    public float Kp = 0.35f, Ki = 0.001f, Kd = 0.2f;
    public float speed = 15f;
    private float steeringAngle;
    public float maxSteer = 25f;
    private float integral;
    private float lastError;
    private Rigidbody rb;
    public ObstacleTriggerZone triggerZone;
    private bool recoveringFromObstacle = false;
    private float recoveryTimer = 0f;
    private float recoveryDuration = 0.9f; //TODO: find a better way that works for every obstacle
    private bool obstaclePassed = true; // flag to track if we passed the obstacle
    private float avoidanceTimer = 1.5f; //TODO: find a better way that works for every obstacle
    private float timer = 0f;
    private float blendWeightLine = 1f;
    private float blendWeightObstacle = 0f;

    private float firstAvoidanceSteer = 0f; // initial avoidance steer


    private int flag = 0;

    void Awake()
    {
        // wire up input actions
        var driving = controls.FindActionMap("Driving", throwIfNotFound: true);
        steerAction = driving.FindAction("Steer", throwIfNotFound: true);
        throttleAction = driving.FindAction("Throttle", throwIfNotFound: true);
        brakeAction = driving.FindAction("Brake", throwIfNotFound: true);
        toggleAutoDriveAction = driving.FindAction("ToggleAD", throwIfNotFound: true);

        driving.Enable();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        //prevent rolling over
        //rb.centerOfMass = new Vector3(0f, -1f, 0f);
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = 0.2f;  // slows down linear velocity
        rb.angularDamping = 1.5f;    // slows down rotation due to oversteer


        int width = Screen.width;
        int height = Screen.height;
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        if (carCamera.targetTexture != null)
            carCamera.targetTexture = null; // make sure it's null to read screen
        WheelFrictionCurve friction = frontLeft.sidewaysFriction;
        friction.extremumValue = 2.0f;
        friction.asymptoteValue = 1.5f;
        friction.stiffness = 1.5f;

        frontLeft.sidewaysFriction = friction;
        frontRight.sidewaysFriction = friction;
        rearLeft.sidewaysFriction = friction;
        rearRight.sidewaysFriction = friction;

    }

    void FixedUpdate()
    {

        CheckAutodriveToggle();

        if (isAutodrive)
        {
            ApplyAutonomousDriveLogic();
            return; 
        }

        else
        {
            ApplyManualDriveInput();
            return;
        }
    }
    void ApplyManualDriveInput()
    {
        //float steerVal = steerAction.ReadValue<float>();   // -1..1 (x)
        //float throttleVal = throttleAction.ReadValue<float>() * keyboardThrottle;   // 0..1  (z)
        //float brakeVal = brakeAction.ReadValue<float>() * keyboardBrake;      // 0..1  (rz)

        float steerVal = Input.GetAxis("Horizontal") * keyboardSteerSpeed; // -1..1 (x)

        float throttleVal = keyboardThrottle * Input.GetAxis("Vertical"); // 0..1

        float brakeVal;
        if (Input.GetKeyDown(KeyCode.Space))
            brakeVal = keyboardBrake;
        else
            brakeVal = 0f;

        float motorTorque = throttleVal * maxManualSpeed;
        float brakeTorque = brakeVal * brakeDecelPerSec;

        Debug.Log($"[Manual] steerVal: {steerVal:F2}, throttleVal: {throttleVal:F2}, brakeVal: {brakeVal:F2}");
        Debug.Log($"[Manual] motorTorque: {motorTorque:F2}, brakeTorque: {brakeTorque:F2}");

        frontLeft.motorTorque = motorTorque;
        frontRight.motorTorque = motorTorque;
        rearLeft.motorTorque = motorTorque;
        rearRight.motorTorque = motorTorque;

        frontLeft.brakeTorque = brakeTorque;
        frontRight.brakeTorque = brakeTorque;
        rearLeft.brakeTorque = brakeTorque;
        rearRight.brakeTorque = brakeTorque;

        float steerAngle = steerVal * maxSteerPerFixedStep * 100f;
        frontLeft.steerAngle = steerAngle;
        frontRight.steerAngle = steerAngle;

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
    void CheckAutodriveToggle()
    {
        float steerVal = steerAction.ReadValue<float>(); // -1..1 (x)
        if (isAutodrive && Mathf.Abs(steerVal) > 0.01f)
        {
            isAutodrive = false;
            Debug.Log("Switched to Manual-drive (G29) due to steering input");
        }

        if (toggleAutoDriveAction != null && toggleAutoDriveAction.triggered)
        {
            isAutodrive = !isAutodrive;
            Debug.Log(isAutodrive ? "Switched to Auto-drive" : "Switched to Manual-drive (G29)");
        }

        if (Input.GetKeyDown(toggleKey))
        {
            isAutodrive = !isAutodrive;
            Debug.Log(isAutodrive ? "Switched to Auto-drive" : "Switched to Manual-drive (G29)");
        }

        if (isAutodrive && (Input.GetKeyDown(KeyCode.A)))
        {
            isAutodrive = false;
            Debug.Log("Switched to Manual-drive due to steering input");
        }
    }
    void ApplyAutonomousDriveLogic()
    {
        float targetSteer = 0f;
        float targetSpeed = speed;

        if (triggerZone.obstacleDetected && flag == 0)
        {
            flag = 1; // set flag to avoid repeated corrections

            timer = avoidanceTimer;
            recoveryTimer = recoveryDuration;
            firstAvoidanceSteer = triggerZone.steerCorrection; // store initial avoidance steer
            obstaclePassed = false; // reset obstacle passed flag
            blendWeightLine = 0.3f;
            blendWeightObstacle = 0.7f;

        }
        else if (!triggerZone.obstacleDetected && !obstaclePassed)
        {
            timer -= Time.fixedDeltaTime;
            targetSpeed = Mathf.Max(speed - 5f, 20f); // slow down while avoiding

            if (timer <= 0f)
            {
                obstaclePassed = true; // mark as passed
                timer = avoidanceTimer; // reset timer for next avoidance
                recoveringFromObstacle = true;
            }

            Debug.Log($"Avoiding obstacle... ({timer:F2}s left) Speed: {targetSpeed:F1}, Steer: {targetSteer:F1}");
        }
        else if (recoveringFromObstacle && obstaclePassed)
        {
            recoveryTimer -= Time.fixedDeltaTime;
            targetSpeed = 30f;

            blendWeightLine = Mathf.Lerp(blendWeightLine, 1f, Time.fixedDeltaTime * 1.2f);
            blendWeightObstacle = Mathf.Lerp(blendWeightObstacle, 0f, Time.fixedDeltaTime * 1.2f);

            if (recoveryTimer <= 0f)
            {
                recoveringFromObstacle = false;
                flag = 0;
            }

            Debug.Log($"Recovering lane... ({recoveryTimer:F2}s left)");
        }
        else
        {
            blendWeightLine = 1f;
            blendWeightObstacle = 0f;
        }

        StartCoroutine(ProcessCameraImage());

        if (!triggerZone.obstacleDetected)
            speed = Mathf.Min(speed + 2f, maxAutonomousSpeed); // increase speed if no obstacle
        else
            speed = 15f;
        
        float steerToUse = steeringAngle; // always use latest PID result

        if (triggerZone.obstacleDetected || !obstaclePassed)
        {
            // Use avoidance steer if obstacle detected or not passed
            steerToUse = triggerZone.steerCorrection; // strong avoidance steer
            Debug.Log($"Obstacle detected! Using avoidance steer: {steerToUse:F1}");
        }
        else if (recoveringFromObstacle)
        {
            // Blend PID + recovery steer smoothly
            steerToUse = -firstAvoidanceSteer;
        }

        targetSteer = steerToUse;

        ApplyWheelPhysicsAutonomous(targetSpeed, steerToUse);
    }
    void ApplyWheelPhysicsAutonomous(float targetSpeed, float targetSteer)
    {
        float motor = targetSpeed * 90f;

        frontLeft.motorTorque = motor;
        frontRight.motorTorque = motor;
        rearLeft.motorTorque = motor;
        rearRight.motorTorque = motor;

        // Smooth steering application
        float currentSteer = frontLeft.steerAngle;
        float desiredSteer = Mathf.Clamp(targetSteer, -maxSteer, maxSteer);
        float steerSpeed = 8f; // adjust for responsiveness
        float newSteer = Mathf.Lerp(currentSteer, desiredSteer, Time.fixedDeltaTime * steerSpeed);

        frontLeft.steerAngle = newSteer;
        frontRight.steerAngle = newSteer;


        float brake = (targetSpeed < 0.1f) ? 100f : 0f;

        if (brake > 0f)
        {
            Debug.Log($"[Brake] Applying brake: {brake:F1}");
        }

        frontLeft.brakeTorque = brake;
        frontRight.brakeTorque = brake;
        rearLeft.brakeTorque = brake;
        rearRight.brakeTorque = brake;

        UpdateWheel(frontRight, frontRightTransform);
        UpdateWheel(frontLeft, frontLeftTransform);
        UpdateWheel(rearLeft, rearLeftTransform);
        UpdateWheel(rearRight, rearRightTransform);
        Debug.Log($"[Wheel] desiredSteer: {desiredSteer:F1}, currentSteer: {currentSteer:F1}, applied: {newSteer:F1}");

    }
    IEnumerator ProcessCameraImage()
    {
        yield return new WaitForEndOfFrame();

        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        int width = tex.width;
        int height = tex.height;
        int centerX = width / 2;
        int scanYStart = 0;
        int scanYEnd = 200; // scan bottom 100 rows

        int leftSum = 0, rightSum = 0, leftCount = 0, rightCount = 0;

        for (int y = scanYStart; y < scanYEnd; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = tex.GetPixel(x, y);
                float intensity = (pixel.r + pixel.g + pixel.b) / 3f;
                float saturation = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) - Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));

                if (intensity > 0.5f && saturation < 0.3f)
                {
                    if (x < centerX)
                    {
                        leftSum += x;
                        leftCount++;
                    }
                    else
                    {
                        rightSum += x;
                        rightCount++;
                    }
                }
            }
        }

        if (leftCount == 0 && rightCount == 0)
        {
            Debug.Log("No white line detected");
            yield break;
        }

        float leftAvg = leftCount > 0 ? (float)leftSum / leftCount : -1f;
        float rightAvg = rightCount > 0 ? (float)rightSum / rightCount : -1f;

        float laneCenter;
        if (leftAvg < 0 && rightAvg < 0)
        {
            Debug.Log("No valid lines detected");
            yield break;
        }
        else if (leftAvg < 0)
        {
            laneCenter = rightAvg - width / 2f;
        }
        else if (rightAvg < 0)
        {
            laneCenter = leftAvg + width / 2f;
        }
        else
        {
            // Boost dashed (right) line's influence
            float leftWeight = 1.0f;  // more weight for dashed line
            float rightWeight = 1.0f;

            laneCenter = (leftAvg * leftWeight + rightAvg * rightWeight) / (leftWeight + rightWeight);
        }

        float error = (laneCenter - centerX) / centerX; // -1 to 1
        float steer = ApplyPID(error);
        float targetSteer = Mathf.Clamp(steer * maxSteer, -maxSteer, maxSteer);
        steeringAngle = Mathf.Lerp(steeringAngle, targetSteer, Time.fixedDeltaTime * 5f); // smooth turn

        Debug.Log($"leftAvg={leftAvg:F1}, rightAvg={rightAvg:F1}, laneCenter={laneCenter:F1}, error={error:F2}, PID={steer:F2}, steerAngle={steeringAngle:F1}");
        Debug.Log($"[PID] raw PID steer: {steer:F2}, clamped targetSteer: {targetSteer:F1}");

    }

    float ApplyPID(float error)
    {
        if (Mathf.Sign(error) != Mathf.Sign(lastError))
            integral = 0f;

        integral += error;
        float derivative = error - lastError;
        lastError = error;

        return (Kp * error + Ki * integral + Kd * derivative);
    }
}
