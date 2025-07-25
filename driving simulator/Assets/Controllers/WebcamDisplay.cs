using UnityEngine;
using UnityEngine.UI;

public class WebcamDisplay : MonoBehaviour
{
    [Header("UI")]
    public RawImage rawImage;
    [Tooltip("0 = main monitor, 1 = second monitor, ...")]
    public int targetDisplay = 0;

    private WebCamTexture webcamTexture;

    void Awake()
    {
        // Activate all extra displays (if any)
        for (int i = 1; i < Display.displays.Length; i++)
            Display.displays[i].Activate();
    }

    void Start()
    {
        if (rawImage == null)
        {
            Debug.LogError("RawImage is not assigned on WebcamDisplay.");
            return;
        }

        // Assign the RawImage's Canvas to the chosen display
        var canvas = rawImage.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (targetDisplay >= Display.displays.Length)
            {
                Debug.LogWarning($"Requested display {targetDisplay} but only {Display.displays.Length} display(s) found. Falling back to 0.");
                targetDisplay = 0;
            }
            canvas.targetDisplay = targetDisplay;
        }
        else
        {
            Debug.LogWarning("No Canvas found above RawImage. UI won't be redirected to another display.");
        }

        // Start webcam
        if (WebCamTexture.devices.Length > 0)
        {
            string camName = WebCamTexture.devices[0].name;
            webcamTexture = new WebCamTexture(camName);
            rawImage.texture = webcamTexture;
            rawImage.material.mainTexture = webcamTexture;
            webcamTexture.Play();
        }
        else
        {
            Debug.LogWarning("No webcam detected!");
        }
    }

    void OnDisable()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
            webcamTexture.Stop();
    }
}
