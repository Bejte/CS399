using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    public Camera secondaryCamera; // Assign your webcam or UI camera here

    void Start()
    {
        Debug.Log(Display.displays.Length);
        // Check if multiple displays are available
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate(); // Enable second display

            if (secondaryCamera != null)
                secondaryCamera.targetDisplay = 1; // Send camera to second screen
        }
        else
        {
            Debug.LogWarning("Only one display detected.");
        }
    }
}