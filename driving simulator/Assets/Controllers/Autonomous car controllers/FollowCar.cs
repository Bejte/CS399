using UnityEngine;

public class FollowCar : MonoBehaviour
{
    public Transform target;           // The car to follow
    public Vector3 offset = new Vector3(0f, 5f, -10f);  // Behind and above the car
    public float followSpeed = 5f;     // How fast the camera follows

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position based on offset
        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        // Look at the car
        transform.LookAt(target);
    }
}
