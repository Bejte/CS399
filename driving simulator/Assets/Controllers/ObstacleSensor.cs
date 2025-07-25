using UnityEngine;

public class ObstacleSensor : MonoBehaviour
{
    public Transform sensorLeft;
    public Transform sensorCenter;
    public Transform sensorRight;

    public float sensorLength = 10f;
    public LayerMask obstacleLayer;

    public float avoidSteerPower = 1.5f;

    public bool obstacleDetected;
    public float steerCorrection;

    void Update()
    {
        obstacleDetected = false;
        steerCorrection = 0f;

        RaycastHit hit;

        bool left = Physics.Raycast(sensorLeft.position, sensorLeft.forward, out hit, sensorLength, obstacleLayer);
        bool center = Physics.Raycast(sensorCenter.position, sensorCenter.forward, out hit, sensorLength, obstacleLayer);
        bool right = Physics.Raycast(sensorRight.position, sensorRight.forward, out hit, sensorLength, obstacleLayer);

        if (left || center || right)
        {
            obstacleDetected = true;
            Debug.Log("AAAA");
            if (left && !right)
                steerCorrection = avoidSteerPower;
            else if (right && !left)
                steerCorrection = -avoidSteerPower;
            else
                steerCorrection = avoidSteerPower; // default steer left
        }

        // Debug lines
        Debug.DrawRay(sensorLeft.position, sensorLeft.forward * sensorLength, left ? Color.red : Color.green);
        Debug.DrawRay(sensorCenter.position, sensorCenter.forward * sensorLength, center ? Color.red : Color.green);
        Debug.DrawRay(sensorRight.position, sensorRight.forward * sensorLength, right ? Color.red : Color.green);
    }
}
