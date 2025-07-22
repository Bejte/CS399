using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class LineFollowerPID : MonoBehaviour
{
    public Camera carCamera;
    public RenderTexture camRT;
    public RawImage debugImageUI;
    private Texture2D debugTex;
    public float Kp = 0.5f, Ki = 0.0f, Kd = 0.1f;

    private Texture2D tex;
    private float integral;
    private float lastError;

    public float speed = 10f;
    public float steeringSensitivity = 1.0f;

    void Start()
    {
        tex = new Texture2D(camRT.width, camRT.height, TextureFormat.RGB24, false);
        if (!camRT.IsCreated())
        {
            Debug.LogWarning("RenderTexture not created. Creating now.");
            camRT.Create();
        }
        debugTex = new Texture2D(camRT.width, camRT.height, TextureFormat.RGB24, false);
        if (debugImageUI != null)
            debugImageUI.texture = debugTex;
    }

    void FixedUpdate()
    {
        // Make sure the camera renders into camRT
        carCamera.targetTexture = camRT;

        // Force the camera to render
        carCamera.Render();

        // Read pixels from that RenderTexture
        RenderTexture.active = camRT;
        tex.ReadPixels(new Rect(0, 0, camRT.width, camRT.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        int testX = tex.width / 4;      // somewhere on the left lane
        int testY = tex.height / 6;     // low row, near car
        Color testPixel = tex.GetPixel(testX, testY);
        Debug.Log($"[Lane Line Check] R: {testPixel.r:F2}, G: {testPixel.g:F2}, B: {testPixel.b:F2}");


        // Continue with your pipeline
        VisualizeWhitePixels(tex);
        float error = CalculateCenterLineError(tex);
        float steering = PIDControl(error);

        transform.Translate(Vector3.forward * speed * Time.fixedDeltaTime);
        transform.Rotate(Vector3.up, steering * steeringSensitivity);
    }


    float PIDControl(float error)
    {
        integral += error * Time.fixedDeltaTime;
        float derivative = (error - lastError) / Time.fixedDeltaTime;
        lastError = error;
        return Kp * error + Ki * integral + Kd * derivative;
    }

    void VisualizeWhitePixels(Texture2D source)
    {
        int width = source.width;
        int height = source.height;

        Color[] debugPixels = new Color[width * height];
        Color[] srcPixels = source.GetPixels();

        int whitePixelCount = 0;

        // Only scan bottom 1/3 of image (adjust as needed)
        int scanStartY = 0;
        int scanEndY = height / 3;

        for (int y = scanStartY; y < scanEndY; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color pixel = srcPixels[index];

                if (IsWhite(pixel))
                {
                    debugPixels[index] = Color.red;
                    whitePixelCount++;
                }
                else
                {
                    debugPixels[index] = Color.black;
                }
            }
        }

        debugTex.SetPixels(debugPixels);
        debugTex.Apply();

        Debug.Log($"[Visual Debug] White pixels (bottom only): {whitePixelCount}");
    }


    float CalculateCenterLineError(Texture2D frame)
    {
        int width = frame.width;
        int height = frame.height;

        Color[] pixels = frame.GetPixels();
        List<int> laneCenters = new List<int>();

        for (int offset = 0; offset < 3; offset++)  // scan 3 rows
        {
            int scanY = height / 2 + offset * 10;  // near bottom
            int leftEdge = -1, rightEdge = -1;

            for (int x = 0; x < width; x++)
            {
                Color pixel = pixels[scanY * width + x];
                if (IsWhite(pixel))
                {
                    if (leftEdge == -1)
                        leftEdge = x;
                    rightEdge = x;
                }
            }

            if (leftEdge != -1 && rightEdge != -1)
            {
                int laneCenter = (leftEdge + rightEdge) / 2;
                laneCenters.Add(laneCenter);
            }
        }

        if (laneCenters.Count == 0)
            return 0f;

        int avgLaneCenter = (int)laneCenters.Average();
        int imageCenter = width / 2;

        return (float)(imageCenter - avgLaneCenter) / imageCenter;
    }

    bool IsWhite(Color pixel)
    {
        float brightness = (pixel.r + pixel.g + pixel.b) / 3f;
        return brightness > 0.4f;
    }

}
