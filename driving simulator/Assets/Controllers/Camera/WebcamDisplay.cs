using UnityEngine;
using UnityEngine.UI;

public class WebcamDisplay : MonoBehaviour
{
    public RawImage rawImage;  // assign in Inspector
    private WebCamTexture webcamTexture;

    void Start()
    {
        if (WebCamTexture.devices.Length > 0)
        {
            webcamTexture = new WebCamTexture();
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
