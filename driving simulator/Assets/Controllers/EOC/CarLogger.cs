using UnityEngine;
using System.IO;

public class CarLogger : MonoBehaviour
{
    private StreamWriter logWriter;
    private string logPath;

    private Vector3 lastPosition;
    private float totalDistance = 0f;
    private bool isLogging = true;
    private int numOfPoints = 0;

    void Start()
    {
        logPath = Path.Combine(Application.dataPath, "Beste_Objective_Path.txt");
        logWriter = new StreamWriter(logPath, append: false);
    }

    void FixedUpdate()
    {
        if (!isLogging) return;

        Vector3 currentPosition = transform.position;

        // Calculate 2D distance (ignore Y)
        float distanceDelta = Vector2.Distance(
            new Vector2(lastPosition.x, lastPosition.z),
            new Vector2(currentPosition.x, currentPosition.z)
        );

        totalDistance += distanceDelta;
        lastPosition = currentPosition;
        numOfPoints++;

        if (totalDistance >= 500f)
        {
            logWriter.WriteLine($"{numOfPoints:F2} points logged.");
            isLogging = false;
            Debug.Log($"Logging stopped after reaching 500m total distance.");
            return;
        }
        Vector3 pos = transform.position;
        logWriter.WriteLine($"{pos.x:F2} {pos.z:F2}"); // Only X and Z
    }

    void OnDestroy()
    {
        if (logWriter != null)
        {
            logWriter.Flush();
            logWriter.Close();
            Debug.Log($"Doc saved to: {logPath}");
        }
    }
}
