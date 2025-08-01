using UnityEngine;

public class ObstacleTriggerZone : MonoBehaviour
{
    //TODO: make the steer correction dynamic; that is it should change according to the obstacle
    public bool obstacleDetected = false;
    public float steerCorrection = 0f;

    private float lastSteer = 0f;
    private float steerCooldownTime = 0.2f;
    private float steerTimer = 0f;
    private float factor = 30f;

    private void Update()
    {
        if (obstacleDetected)
        {
            steerTimer += Time.deltaTime;
            if (steerTimer > steerCooldownTime)
            {
                steerCorrection = lastSteer;
                steerTimer = 0f;
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            obstacleDetected = true;

            Vector3 toObstacle = other.transform.position - transform.position;

            float direction;

            // If very close to center, bias left
            if (Mathf.Abs(toObstacle.x) < 0.2f)
                direction = -1f;
            else
                direction = Mathf.Sign(toObstacle.x);

            lastSteer = -direction * factor; // invert for avoidance
            steerCorrection = lastSteer;
            Debug.Log($"Obstacle detected! Steering correction: {steerCorrection}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            obstacleDetected = false;
            steerCorrection = 0f;
            lastSteer = 0f;
            steerTimer = 0f;
        }
    }
}
