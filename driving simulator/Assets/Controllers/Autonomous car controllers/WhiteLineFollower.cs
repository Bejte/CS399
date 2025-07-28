using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem; // <<< NEW

public class WhiteLineFollower : MonoBehaviour
{
    // ------------ G29 / Input System ------------
    [Header("Input System")]
    public InputActionAsset controls;                       // drag your .inputactions here
    private InputAction steeringAction, throttleAction, brakeAction, toggleAutoDriveAction;

    [Header("Manual (G29) driving params")]
    public float maxManualSpeed = 50f;
    public float accelPerSec = 40f;
    public float brakeDecelPerSec = 60f;
    public float maxSteerPerFixedStep = 0.5f;               // how much you rotate per FixedUpdate for |steer| = 1
    private float manualSpeed = 0f;
    private float manualSteering = 0f;

    // ------------ Auto drive (your original fields) ------------
    private bool isAutodrive = true;

    public Camera carCamera;
    private Texture2D tex;
    private float steeringAngle;
    public float Kp = 0.25f, Ki = 0.006f, Kd = 2f;

    public float speed = 15f;
    private float integral;
    private float lastError;
    private Rigidbody rb;
    public ObstacleTriggerZone triggerZone;
    private bool recoveringFromObstacle = false;
    private float recoveryTimer = 0f;
    private float recoveryDuration = 1.2f; // seconds of gentle return
    private float recoverySteer = 0f;

    void Awake()
    {
        // wire up input actions
        var driving = controls.FindActionMap("Driving", throwIfNotFound: true);
        steeringAction     = driving.FindAction("Steering",  throwIfNotFound: true);
        throttleAction     = driving.FindAction("Throttle",  throwIfNotFound: true);
        brakeAction        = driving.FindAction("Brake",     throwIfNotFound: true);
        toggleAutoDriveAction = driving.FindAction("ToggleAD", throwIfNotFound: true); // optional

        driving.Enable();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        int width = Screen.width;
        int height = Screen.height;
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        if (carCamera.targetTexture != null)
            carCamera.targetTexture = null; // make sure it's null to read screen
    }

    void FixedUpdate()
    {
        // Optional: toggle auto/manual from the wheel button
        if (toggleAutoDriveAction != null && toggleAutoDriveAction.triggered)
        {
            isAutodrive = !isAutodrive;
            Debug.Log(isAutodrive ? "Switched to Auto-drive" : "Switched to Manual-drive (G29)");
        }

        if (isAutodrive)
        {
            if (triggerZone.obstacleDetected)
            {
                Debug.Log($"Steering away from obstacle with correction: {triggerZone.steerCorrection}");
                transform.Rotate(0f, triggerZone.steerCorrection, 0f);

                recoveringFromObstacle = true;
                recoveryTimer = recoveryDuration;
                recoverySteer = -triggerZone.steerCorrection * 0.5f; // steer slightly opposite after avoidance

                speed = Mathf.Max(speed - 2f, 5f);
                return;
            }
            else if (recoveringFromObstacle)
            {
                recoveryTimer -= Time.fixedDeltaTime;

                transform.Rotate(0f, recoverySteer, 0f);

                Debug.Log($"↩Recovering lane... ({recoveryTimer:F2}s left)");

                if (recoveryTimer <= 0f)
                    recoveringFromObstacle = false;

                return;
            }

            StartCoroutine(ProcessCameraImage());

            if (!triggerZone.obstacleDetected)
                speed = Mathf.Min(speed + 0.5f, 20f);
            else
                speed = 15f;

            rb.linearVelocity = transform.forward * speed; // keep your original API
        }
        else
        {
            HandleManualControlG29();                       // <<< uses G29, not WASD
            rb.linearVelocity = transform.forward * manualSpeed;
            transform.Rotate(0f, manualSteering, 0f);
        }

        // Keep your keyboard override if you still want it (optional)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame)
            {
                isAutodrive = !isAutodrive;
                Debug.Log(isAutodrive ? "Switched to Auto-drive" : "Switched to Manual-drive");
            }
        }
    }

    // ----------- NEW: Manual control via G29 axes -----------
    void HandleManualControlG29()
    {
        float steerVal    = steeringAction.ReadValue<float>();   // -1..1 (x)
        float throttleVal = throttleAction.ReadValue<float>();   // 0..1  (z)
        float brakeVal    = brakeAction.ReadValue<float>();      // 0..1  (rz)

        // If any are inverted, either fix in the Input Actions with the Invert processor,
        // or invert here, e.g. throttleVal = 1f - throttleVal;

        // Speed control:
        float targetSpeed = throttleVal * maxManualSpeed;
        // accelerate towards targetSpeed
        manualSpeed = Mathf.MoveTowards(manualSpeed, targetSpeed, accelPerSec * Time.fixedDeltaTime);
        // apply braking deceleration (stronger)
        manualSpeed = Mathf.MoveTowards(manualSpeed, 0f, brakeDecelPerSec * brakeVal * Time.fixedDeltaTime);

        // Steering per physics tick
        manualSteering = steerVal * maxSteerPerFixedStep;
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
        int scanYEnd = 100; // scan bottom 100 rows

        int leftSum = 0, rightSum = 0, leftCount = 0, rightCount = 0;

        for (int y = scanYStart; y < scanYEnd; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = tex.GetPixel(x, y);
                float intensity = (pixel.r + pixel.g + pixel.b) / 3f;
                float saturation = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) - Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));

                if (intensity > 0.5f && saturation < 0.3f) // very light and neutral: white
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
            Debug.Log("⚠️ No white line detected");
            yield break;
        }
        else if (leftAvg < 0)
        {
            laneCenter = rightAvg - width / 3f;
        }
        else if (rightAvg < 0)
        {
            laneCenter = leftAvg + width / 3f;
        }
        else
        {
            // Boost dashed (left) line's influence
            float leftWeight = 1.2f;  // more weight for dashed line
            float rightWeight = 1.0f;

            laneCenter = (leftAvg * leftWeight + rightAvg * rightWeight) / (leftWeight + rightWeight);
        }

        float error = (laneCenter - centerX) / centerX; // -1 to 1
        float steer = ApplyPID(error);
        float turnStrength = Mathf.Clamp(steer * 1f, -0.5f, 0.5f); // scaled

        transform.Rotate(0f, turnStrength, 0f);
    }

    float ApplyPID(float error)
    {
        integral += error;
        float derivative = error - lastError;
        lastError = error;

        return (Kp * error + Ki * integral + Kd * derivative);
    }
}
