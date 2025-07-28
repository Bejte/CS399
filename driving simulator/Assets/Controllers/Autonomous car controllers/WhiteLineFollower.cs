using UnityEngine;
using System.Collections;

public class WhiteLineFollower : MonoBehaviour
{
    private bool isAutodrive = true;
    private float manualSteering = 0f; //Steering angle
    private float manualSpeed = 0f; //Speed of the car

    [Header("Camera / Vision")]
    public Camera carCamera;
    private Texture2D tex;

    [Header("PID")]
    public float Kp = 0.25f, Ki = 0.006f, Kd = 2f;

    [Header("Speed")]
    public float speed = 15f;
    public float maxSpeed = 20f;
    public float minSpeed = 5f;

    private float integral;
    private float lastError;
    private Rigidbody rb;

    [Header("Obstacle trigger")]
    public ObstacleTriggerZone triggerZone;

    [Header("Recovery")]
    public float recoveryDuration = 1.5f; // time to steer to opposite direction after avoidance
    private bool recoveringFromObstacle = false;
    private float recoveryTimer = 0f; // counter for time
    private float recoverySteer = 0f; // to mirror the same movement as the obstacle avoidance

    [Header("Safety during recovery")]
    public LayerMask obstacleLayer;
    public float collisionCheckDistance = 6f;
    public float collisionCheckAngle = 20f;
    public float emergencyAvoidTurn = 0.6f; // how hard to turn if something is still in front

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        int width = Screen.width;
        int height = Screen.height;
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        if (carCamera != null && carCamera.targetTexture != null)
            carCamera.targetTexture = null;
    }

    void FixedUpdate()
    {
        
        if (isAutodrive)
        {
            StartCoroutine(ProcessCameraImage());

            // 1) Obstacle avoidance has priority
            if (triggerZone != null && triggerZone.obstacleDetected)
            {
                Debug.Log($"Steering away from obstacle with correction: {triggerZone.steerCorrection}");
                transform.Rotate(0f, triggerZone.steerCorrection, 0f);

                recoveringFromObstacle = true;
                recoveryTimer = recoveryDuration;
                recoverySteer = -triggerZone.steerCorrection * 0.5f; // gentle opposite after avoidance

                speed = Mathf.Max(speed - 2f, minSpeed);
                return;
            }
            // 2) Recovery phase (now with collision prediction)
            else if (recoveringFromObstacle)
            {
                recoveryTimer -= Time.fixedDeltaTime;

                // Before applying the gentle return, check if you'd crash
                if (CheckImminentCollision(out float steerSign))
                {
                    // Abort gentle recovery, steer harder away, slow down
                    float turn = steerSign * emergencyAvoidTurn;
                    transform.Rotate(0f, turn, 0f);
                    speed = Mathf.Max(speed - 2f, minSpeed);

                    // Optionally restart recovery timer so we keep trying longer
                    recoveryTimer = Mathf.Max(recoveryTimer, 0.5f);
                    return;
                }

                // Safe: do the planned gentle return
                transform.Rotate(0f, recoverySteer, 0f);
                rb.linearVelocity = transform.forward * speed;

                if (recoveryTimer <= 0f)
                    recoveringFromObstacle = false;

                Debug.Log($"Recovering lane... ({recoveryTimer:F2}s left)");
                return;
            }

            if (triggerZone == null || !triggerZone.obstacleDetected)
                speed = Mathf.Min(speed + 0.5f, maxSpeed);
            else
                speed = 15f;

            rb.linearVelocity = transform.forward * speed;
        }
        else
        {
            HandleManualControl();
            rb.linearVelocity = transform.forward * manualSpeed;
            transform.Rotate(0f, manualSteering, 0f);
        }

        // Toggle mode with 'A' key
        if (Input.GetKeyDown(KeyCode.A))
        {
            isAutodrive = !isAutodrive;
            Debug.Log(isAutodrive ? "Switched to Auto-drive" : "Switched to Manual-drive");
        }

        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
        {
            if (isAutodrive)
            {
                isAutodrive = false;
                Debug.Log("Switched to Manual-drive (due to user input)");
            }
        }
    }

    void HandleManualControl()
    {
        if (Input.GetKey(KeyCode.UpArrow))
            manualSpeed = Mathf.Min(manualSpeed + 0.8f, 50f);
        else if (Input.GetKey(KeyCode.DownArrow))
            manualSpeed = Mathf.Max(manualSpeed - 0.8f, -3f); // allow light reverse
        else
            manualSpeed = Mathf.MoveTowards(manualSpeed, 0, 0.2f); // decelerate

        if (Input.GetKey(KeyCode.LeftArrow))
            manualSteering = -0.5f;
        else if (Input.GetKey(KeyCode.RightArrow))
            manualSteering = 0.5f;
        else
            manualSteering = 0f;
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
            yield break;

        float leftAvg = leftCount > 0 ? (float)leftSum / leftCount : -1f;
        float rightAvg = rightCount > 0 ? (float)rightSum / rightCount : -1f;

        float laneCenter;
        if (leftAvg < 0 && rightAvg < 0)
            yield break;
        else if (leftAvg < 0)
            laneCenter = rightAvg - width / 3f;
        else if (rightAvg < 0)
            laneCenter = leftAvg + width / 3f;
        else
        {
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

    // ----------- NEW -----------
    // Returns true if we see something in front, and gives a steerSign (+1 turn right, -1 turn left)
    bool CheckImminentCollision(out float steerSign)
    {
        steerSign = 0f;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 fwd = transform.forward;
        Vector3 leftDir = Quaternion.AngleAxis(-collisionCheckAngle, Vector3.up) * fwd;
        Vector3 rightDir = Quaternion.AngleAxis(collisionCheckAngle, Vector3.up) * fwd;

        float leftDist = RayDistance(origin, leftDir, collisionCheckDistance);
        float rightDist = RayDistance(origin, rightDir, collisionCheckDistance);
        float centerDist = RayDistance(origin, fwd, collisionCheckDistance);

        bool danger = centerDist < collisionCheckDistance ||
                      leftDist < collisionCheckDistance ||
                      rightDist < collisionCheckDistance;

        if (!danger) return false;

        // Prefer the freer side
        if (rightDist > leftDist) steerSign = +1f; else steerSign = -1f;

        Debug.DrawRay(origin, fwd * collisionCheckDistance, Color.red);
        Debug.DrawRay(origin, leftDir * collisionCheckDistance, Color.red);
        Debug.DrawRay(origin, rightDir * collisionCheckDistance, Color.red);

        return true;
    }

    float RayDistance(Vector3 origin, Vector3 dir, float maxDist)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, obstacleLayer, QueryTriggerInteraction.Ignore))
            return hit.distance;

        return Mathf.Infinity;
    }
}
