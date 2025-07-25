using UnityEngine;

public class ObstacleTriggerZone : MonoBehaviour
{
    public bool obstacleDetected = false;
    public float steerCorrection = 0f;

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            obstacleDetected = true;

            // Compute direction from car to obstacle
            Vector3 toObstacle = other.transform.position - transform.position;
            steerCorrection = -Mathf.Sign(toObstacle.x) * 0.5f; // steer away (left/right)
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            obstacleDetected = false;
            steerCorrection = 0f;
        }
    }
}
