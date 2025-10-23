using UnityEngine;

public class FollowPlayerScript : MonoBehaviour
{
    public Transform bird;        // the player
    public Transform planet;      // your sphere
    public float distanceBack = 6f;
    public float distanceUp = 2f;
    public float smooth = 6f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (!bird || !planet) return;

        // --- Surface normal (planet center → bird)
        Vector3 normal = (bird.position - planet.position).normalized;

        // --- Forward tangent = bird.forward
        Vector3 forward = bird.forward;

        // --- Desired camera position (behind and above the bird)
        Vector3 desiredPos =
            bird.position
            - forward * distanceBack
            + normal * distanceUp;

        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, desiredPos, smooth * Time.deltaTime);

        // --- Look at the bird, with the normal as the camera's "up"
        Quaternion desiredRot = Quaternion.LookRotation(bird.position - transform.position, normal);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, smooth * Time.deltaTime);
    }
}
