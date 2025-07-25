using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AutonomousCarScreenshotController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft, frontRight, rearLeft, rearRight;
    public Transform frontLeftTransform, frontRightTransform, rearLeftTransform, rearRightTransform;

    [Header("Camera and UI")]
    public Camera carCamera;
    public RawImage debugRawImage;

    [Header("Speed and Steering")]
    public float maxSteeringAngle = 30f;
    public float baseTorque = 800f;
    public float maxTorque = 1200f;

    [Header("PID Settings")]
    public float KP = 0.4f, KI = 0.0f, KD = 1.5f;

    private float previousError = 0f;
    private float integral = 0f;

    void Start()
    {
        // Position camera if needed
        if (carCamera != null)
        {
            carCamera.transform.localPosition = new Vector3(0, 2f, 0f);
            carCamera.transform.localEulerAngles = new Vector3(30f, 0f, 0f);
        }
    }

    void FixedUpdate()
    {
        StartCoroutine(CaptureAndDrive());
    }

    IEnumerator CaptureAndDrive()
    {
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (screenshot == null)
        {
            Debug.LogError("❌ Screenshot failed.");
            yield break;
        }


        if (debugRawImage != null)
            debugRawImage.texture = screenshot;

        int width = screenshot.width;
        int height = screenshot.height;

        int leftX = -1;
        int rightX = -1;

        Color testPixel = screenshot.GetPixel(screenshot.width / 2, 5); // near bottom
Debug.Log($"Pixel: R:{testPixel.r:F2}, G:{testPixel.g:F2}, B:{testPixel.b:F2}");


       for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = screenshot.GetPixel(x, y);

                // Strict white detection using RGB thresholds
                if (pixel.r > 0.9f && pixel.g > 0.9f && pixel.b > 0.9f)
                {
                    if (leftX == -1 || x < leftX)
                        leftX = x;

                    if (rightX == -1 || x > rightX)
                        rightX = x;
                }
            }
        }

        if (leftX == -1 || rightX == -1 || leftX == rightX)
        {
            Debug.LogWarning("❌ Could not detect both left and right white lane lines.");
            yield break;
        }

        // Midpoint between white lanes
        float laneCenterX = (leftX + rightX) / 2f;
        float screenCenterX = width / 2f;
        float centerError = (laneCenterX - screenCenterX) / screenCenterX;

        float steeringInput = ApplyPID(centerError);
        float torque = ComputeTorque(centerError);

        // Apply to wheels
        frontLeft.steerAngle = steeringInput * maxSteeringAngle;
        frontRight.steerAngle = steeringInput * maxSteeringAngle;

        rearLeft.motorTorque = torque;
        rearRight.motorTorque = torque;

        rearLeft.brakeTorque = 0f;
        rearRight.brakeTorque = 0f;

        UpdateWheelPose(frontLeft, frontLeftTransform);
        UpdateWheelPose(frontRight, frontRightTransform);
        UpdateWheelPose(rearLeft, rearLeftTransform);
        UpdateWheelPose(rearRight, rearRightTransform);

        Debug.Log($"🧠 Steering: {steeringInput:F2}, Torque: {torque:F0}, Error: {centerError:F2} | LeftX: {leftX}, RightX: {rightX}");
    }



    float ApplyPID(float error)
    {
        float diff = error - previousError;
        integral += error;
        previousError = error;
        float output = KP * error + KI * integral + KD * diff;
        return Mathf.Clamp(output, -1f, 1f);
    }

    float ComputeTorque(float error)
    {
        float abs = Mathf.Abs(error);
        if (abs > 0.3f) return Mathf.Max(300f, baseTorque - 400f);
        return Mathf.Min(baseTorque + 200f, maxTorque);
    }

    void UpdateWheelPose(WheelCollider col, Transform trans)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        trans.position = pos;
        trans.rotation = rot;
    }
}
