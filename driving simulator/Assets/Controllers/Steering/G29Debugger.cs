using UnityEngine;

public class G29Debugger : MonoBehaviour
{
    // Common legacy Input Manager axes Unity often exposes by default.
    // If some of these don't exist in your project, they'll just stay at 0.
    private readonly string[] axisNames =
    {
        "Horizontal", "Vertical",
        "X axis", "Y axis", "Z axis",
        "3rd axis", "4th axis", "5th axis", "6th axis", "7th axis", "8th axis", "9th axis", "10th axis"
    };

    void Update()
    {
        // Buttons
        for (int i = 0; i < 20; i++)
        {
            if (Input.GetKey("joystick button " + i))
                Debug.Log($"Button {i} pressed");
        }

        // Axes
        foreach (var a in axisNames)
        {
            float v = 0f;
            try { v = Input.GetAxis(a); }
            catch { continue; } // axis not defined in Input Manager

            if (Mathf.Abs(v) > 0.05f)
                Debug.Log($"Axis \"{a}\": {v:F2}");
        }

        // Which joysticks Unity se
    }
}
