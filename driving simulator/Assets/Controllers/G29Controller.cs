using UnityEngine;

public class G29Controller : MonoBehaviour
{
    public float maxSteerAngle = 30f;  // Maximum steering angle
    public float maxSpeed = 10f;       // Maximum forward speed
    public float reverseSpeed = -5f;   // Speed when braking
    public Rigidbody carRigidbody;

    void Update()
    {
        // Get steering value from -1 (left) to 1 (right)
        float steerInput = Input.GetAxis("Steering");
        float steerAngle = steerInput * maxSteerAngle;

        // Get throttle/brake input (value from 0 to 1)
        float gas = Input.GetAxis("Gas");
        float brake = Input.GetAxis("Brake");

        // Determine speed based on input
        float speed = 0f;
        if (gas > 0.05f)
            speed = gas * maxSpeed;
        else if (brake > 0.05f)
            speed = brake * reverseSpeed;

        // Apply movement
        if (carRigidbody != null)
        {
            carRigidbody.linearVelocity = transform.forward * speed;
            transform.Rotate(0f, steerAngle * Time.deltaTime, 0f);
        }

        Debug.Log($"Steering: {steerInput:F2}, Gas: {gas:F2}, Brake: {brake:F2}");
    }
}
