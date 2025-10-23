using UnityEngine;

public class PipeScript : MonoBehaviour
{
    public Transform planet;
    [Tooltip("Optional: a reference axis (your spinner's current axis). Only needed if you want a stable 'forward'.")]
    public Vector3 referenceAxis = Vector3.up;
    public bool lockForwardToSpin = false;

    public void SetPlanet(Transform planetRoot, Vector3 worldAxis, bool lockForwardToSpin)
    {
        planet = planetRoot;
        referenceAxis = worldAxis;
        this.lockForwardToSpin = lockForwardToSpin;
    }

    void LateUpdate()
    {
        if (!planet) return;

        // Radial "up"
        Vector3 n = (transform.position - planet.position).normalized;

        // Option A: just keep the pipe upright (simplest, looks static on planet)
        if (!lockForwardToSpin)
        {
            // Preserve current forward but make 'up' = n
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, n).normalized;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(referenceAxis, n).normalized;
            transform.rotation = Quaternion.LookRotation(fwd, n);
            return;
        }
        else
        {
            Vector3 f = Vector3.Cross(referenceAxis.normalized, n);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(transform.forward, n);
            transform.rotation = Quaternion.LookRotation(f.normalized, n);
        }
    }

    void OnDestroy()
    {
        PipeSpawnerScript.ActivePipes.Remove(transform);
    }
}
