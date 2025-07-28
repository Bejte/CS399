using UnityEngine;

public class DisplayManager : MonoBehaviour
{
    public Camera secondaryCamera; // Assign your webcam or UI camera here

    void Start()
    {
        if (Display.displays.Length > 1)
            Display.displays[1].Activate();
        if (Display.displays.Length > 2)
            Display.displays[2].Activate();
        if (Display.displays.Length > 3)
            Display.displays[3].Activate();
    }
}