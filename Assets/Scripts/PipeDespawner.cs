using UnityEngine;

public class PipeDespawner : MonoBehaviour
{
    [Tooltip("Seconds before the pipe is automatically destroyed.")]
    public float lifetime = 3f;

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
