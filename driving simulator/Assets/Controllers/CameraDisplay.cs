using UnityEngine;
using UnityEngine.UI;

public class CameraDisplay : MonoBehaviour
{
    public Camera carCamera;
    public RawImage rawImage;

    void Start()
    {
        RenderTexture renderTex = new RenderTexture(256, 144, 16);
        carCamera.targetTexture = renderTex;
        rawImage.texture = renderTex;
    }
}
