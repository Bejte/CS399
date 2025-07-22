using UnityEngine;

public class AutonomousCarController : MonoBehaviour
{
    public WheelCollider frontLeft, frontRight, rearLeft, rearRight;
    public Transform frontLeftTransform, frontRightTransform, rearLeftTransform, rearRightTransform;

    public Camera carCamera;
    public float maxSpeed = 1500f;
    public float baseSpeed = 1000f;
    public float maxSteering = 30f;

    private RenderTexture camRT;
    private Texture2D camTexture;

    // PID control parameters
    float prevError = 0f;
    float integral = 0f;
    const float KP = 0.25f, KI = 0.006f, KD = 2f;

    void Start()
    {
        camRT = new RenderTexture(128, 128, 16);
        if (!camRT.IsCreated())
        {
            Debug.Log("🛠 camRT not created, calling Create()");
            camRT.Create();
        }
        carCamera.targetTexture = camRT;
        Debug.Log(carCamera.targetTexture != null ? "✅ Camera targetTexture assigned successfully!" : "❌ Camera targetTexture is null!");
        if (carCamera.targetTexture == null)
            Debug.LogError("❌ Camera targetTexture is STILL null after assignment!");
        camTexture = new Texture2D(128, 128, TextureFormat.RGB24, false);
        GameObject whiteCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        whiteCube.transform.position = carCamera.transform.position + carCamera.transform.forward * 30f;
        whiteCube.transform.localScale = Vector3.one * 0.5f;
        whiteCube.GetComponent<Renderer>().material.color = Color.white;
    }

    void FixedUpdate()
    {
        StartCoroutine(CaptureAndProcessCamera());
    }

    private System.Collections.IEnumerator CaptureAndProcessCamera()
    {

        if (carCamera.targetTexture == null)
        {
            Debug.LogError("❌ carCamera.targetTexture is null at runtime! Something reset it?");
            yield break;
        }
        
        yield return new WaitForEndOfFrame();

        RenderTexture.active = camRT;
        carCamera.Render();
        camTexture.ReadPixels(new Rect(0, 0, camRT.width, camRT.height), 0, 0);
        camTexture.Apply();
        RenderTexture.active = null;

        float laneAngle = ProcessCameraImage();

        if (float.IsNaN(laneAngle))
        {
            Debug.LogWarning("❌ Lane not detected — camera sees nothing usable.");
            yield break;
        }

        float steering = ApplyPID(laneAngle);
        float torque = ComputeSpeed(laneAngle);

        Debug.Log($"✅ LaneAngle: {laneAngle:F3}, Steering: {steering:F3}, Torque: {torque:F1}");

        frontLeft.steerAngle = steering * maxSteering;
        frontRight.steerAngle = steering * maxSteering;

        rearLeft.motorTorque = torque;
        rearRight.motorTorque = torque;

        rearLeft.brakeTorque = 0f;
        rearRight.brakeTorque = 0f;

        UpdateWheelPose(frontLeft, frontLeftTransform);
        UpdateWheelPose(frontRight, frontRightTransform);
        UpdateWheelPose(rearLeft, rearLeftTransform);
        UpdateWheelPose(rearRight, rearRightTransform);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"Velocity: {rb.linearVelocity.magnitude:F2} m/s");
        }
    }

    float ProcessCameraImage()
    {
        Color[] pixels = camTexture.GetPixels();

        int width = camTexture.width;
        int height = camTexture.height;

        // Center pixel debug
        Color center = camTexture.GetPixel(width / 2, height - 5);
        Color left = camTexture.GetPixel(5, height - 5);
        Color right = camTexture.GetPixel(width - 5, height - 5);
        Debug.Log($"🎯 Pixels — Left: ({left.r:F2},{left.g:F2},{left.b:F2}) | Center: ({center.r:F2},{center.g:F2},{center.b:F2}) | Right: ({right.r:F2},{right.g:F2},{right.b:F2})");

        // White pixel debug
        int whitePixelCount = 0;
        foreach (Color pixel in pixels)
        {
            if (pixel.r > 0.9f && pixel.g > 0.9f && pixel.b > 0.9f)
                whitePixelCount++;
        }
        float whiteRatio = (float)whitePixelCount / pixels.Length * 100f;
        Debug.Log($"🟦 White pixels: {whitePixelCount} ({whiteRatio:F2}%)");

        // Scan bottom row for dark road pixels
        int darkSum = 0, darkCount = 0;
        for (int y = height - 10; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[y * width + x];
                float intensity = (pixel.r + pixel.g + pixel.b) / 3f;
                if (intensity < 0.3f)
                {
                    darkSum += x;
                    darkCount++;
                }
            }
        }

        if (darkCount == 0)
        {
            Debug.LogWarning("❌ No dark road pixels detected in bottom scan.");
            return float.NaN;
        }

        float avgX = (float)darkSum / darkCount;
        float centerX = width / 2f;
        float normalized = (avgX - centerX) / centerX; // -1 to +1
        return normalized;
    }

    float ApplyPID(float error)
    {
        float diff = error - prevError;
        integral += error;
        prevError = error;

        float pid = KP * error + KI * integral + KD * diff;
        return Mathf.Clamp(pid, -1f, 1f);
    }

    float ComputeSpeed(float angle)
    {
        float abs = Mathf.Abs(angle);
        if (abs > 0.25f) return Mathf.Max(400f, baseSpeed - 600f);
        if (abs > 0.1f) return Mathf.Max(600f, baseSpeed - 300f);
        return Mathf.Min(baseSpeed + 300f, maxSpeed);
    }

    void UpdateWheelPose(WheelCollider col, Transform trans)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        trans.position = pos;
        trans.rotation = rot;
    }
}
