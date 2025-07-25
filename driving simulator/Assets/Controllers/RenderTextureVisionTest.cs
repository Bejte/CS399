using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenshotVisionTest : MonoBehaviour
{
    [Header("Scene Setup")]
    public Camera carCamera;
    public RawImage debugRawImage;

    void Start()
    {
        // Setup the scene manually
        carCamera.transform.position = new Vector3(0, 2, 0);
        carCamera.transform.rotation = Quaternion.Euler(30, 0, 0);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.position = new Vector3(0, 0, 5);
        floor.GetComponent<Renderer>().material.color = Color.gray;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(0, 0.5f, 5);
        cube.GetComponent<Renderer>().material.color = Color.white;
    }

    void LateUpdate()
    {
        StartCoroutine(CaptureScreenshotPixel());
    }

    IEnumerator CaptureScreenshotPixel()
    {
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();

        if (screenshot != null)
        {
            // Show the screenshot in the UI
            if (debugRawImage != null)
                debugRawImage.texture = screenshot;

            // Read the center pixel
            Color c = screenshot.GetPixel(screenshot.width / 2, screenshot.height / 2);
            Debug.Log($"📸 Screenshot center: R:{c.r:F2}, G:{c.g:F2}, B:{c.b:F2}");
        }
        else
        {
            Debug.LogError("❌ Screenshot failed to capture.");
        }
    }
}
