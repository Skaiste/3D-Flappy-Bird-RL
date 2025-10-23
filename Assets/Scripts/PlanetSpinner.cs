// PlanetSpinner.cs
using UnityEngine;

public class PlanetSpinner : MonoBehaviour
{
   [Tooltip("Bird transform used to determine forward direction on the surface.")]
    public Transform bird;

    [Tooltip("Planet / world center.")]
    public Transform planet;

    [Tooltip("Degrees per second the planet spins to create 'forward' scrolling.")]
    public float angularSpeedDeg = 4f;

    // Expose current spin axis so other scripts (despawner, etc.) can use it.
    public Vector3 CurrentAxisWorld { get; private set; } = Vector3.up;

    void FixedUpdate()
    {
        if (!bird || !planet) return;

        // n: radial out at bird
        Vector3 n = (bird.position - planet.position).normalized;

        // f: bird’s forward projected onto tangent (robust against camera tilts)
        Vector3 f = Vector3.ProjectOnPlane(bird.forward, n);
        if (f.sqrMagnitude < 1e-6f)
        {
            // fallback if forward ~ normal
            f = Vector3.ProjectOnPlane(bird.right, n);
        }
        f.Normalize();

        // Axis so ground at bird moves opposite to f
        Vector3 A = Vector3.Cross(f, n).normalized;
        if (A.sqrMagnitude < 1e-6f) return;

        CurrentAxisWorld = A;

        float angle = angularSpeedDeg * Time.fixedDeltaTime;
        // rotate the entire planet (and its children, including pipes) around A
        transform.Rotate(A, angle, Space.World);
    }
}
