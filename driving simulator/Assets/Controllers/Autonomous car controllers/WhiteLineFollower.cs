using UnityEngine;
using System.Collections;

public class WhiteLineFollower : MonoBehaviour
{
    private bool isAutodrive = true;
    private float manualSteering = 0f;
    private float manualSpeed = 0f;

    public Camera carCamera;
    private Texture2D tex;
    private float steeringAngle;
    public float Kp = 0.25f, Ki = 0.006f, Kd = 2f;

    public float speed = 5f;
    private float integral;
    private float lastError;
    private Rigidbody rb;
    public ObstacleTriggerZone triggerZone;
    
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
        if (triggerZone.obstacleDetected)
        {
            Debug.Log($"🛑 Steering away from obstacle with correction: {triggerZone.steerCorrection}");

            transform.Rotate(0f, triggerZone.steerCorrection, 0f);
            speed = Mathf.Max(speed - 2f, 5f);
            return;
        }

        if (isAutodrive)
        {
            StartCoroutine(ProcessCameraImage());
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
        {
            Debug.Log("No white line detected");
            yield break;
        }

        float laneCenter;
        if (leftCount == 0)
            laneCenter = rightSum / (float)rightCount - width / 4f;
        else if (rightCount == 0)
            laneCenter = leftSum / (float)leftCount + width / 4f;
        else
            laneCenter = (leftSum + rightSum) / (float)(leftCount + rightCount);

        float error = (laneCenter - centerX) / centerX; // -1 to 1
        float steer = ApplyPID(error);
        float turnStrength = Mathf.Clamp(steer * 1f, -0.5f, 0.5f); // scaled

        transform.Rotate(0f, turnStrength, 0f);
        //Debug.Log($"Steer: {turnStrength:F2} | Error: {error:F2}");
    }

    float ApplyPID(float error)
    {
        integral += error;
        float derivative = error - lastError;
        lastError = error;

        return (Kp * error + Ki * integral + Kd * derivative);
    }
}
