using UnityEngine;

public class PipeSpawnerScript : MonoBehaviour
{
    [Header("Refs")]
    public Transform planet;
    public Transform bird;
    public GameObject pipePrefab;

    [Header("Sphere layout")]
    public float planetRadius = 25f;
    public float altitudeOffset = 0.33f;      // match bird clearance or whatever looks right
    public Vector3 worldAxis = Vector3.up;     // must match PlanetSpinner/BirdSideOrbit
    public float obstacleClearance = -5f;

    [Header("Placement")]
    [Tooltip("How many degrees of arc ahead of the bird to spawn.")]
    public float aheadAngleDeg = 25f;  // try 12–25
    [Tooltip("Lateral offset so it's not dead center (+/-).")]
    public float minSideAngleDeg = 10f;
    public float maxSideAngleDeg = 25f;

    [Tooltip("Latitudes in degrees away from the equator (0 = equator, +/- moves toward poles).")]
    public float[] latitudeDeg = new float[] { -30f, 0f, 30f };

    [Header("Timing")]
    public float spawnInterval = 5f;
    public int   burstPerInterval = 1;         // more than 1 if you want clusters

    public static readonly System.Collections.Generic.List<Transform> ActivePipes = new();

    float timer;

    void Start()
    {
        SpawnOne();
    }

    void FixedUpdate()
    {
        timer += Time.fixedDeltaTime;
        if (timer >= spawnInterval)
        {
            for (int i = 0; i < burstPerInterval; i++)
                SpawnOne();
            timer = 0f;
        }
    }

    void SpawnOne()
    {
        // 1) Build the local tangent frame at the bird
        Vector3 n = (bird.position - planet.position).normalized;                 // surface up
        Vector3 fwd = Vector3.Cross(worldAxis.normalized, n).normalized;          // forward along ground
        if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(bird.forward, n).normalized;
        Vector3 right = Vector3.Cross(n, fwd).normalized;

        // 2) Move 'ahead' by rotating the normal around the RIGHT axis
        Quaternion aheadRot = Quaternion.AngleAxis(aheadAngleDeg, right);
        Vector3 aheadDir = (aheadRot * n).normalized;

        // 3) Offset sideways around the normal so it's not directly in front
        float side = (Random.value < 0.5f ? -1f : 1f) * Random.Range(minSideAngleDeg, maxSideAngleDeg);
        Quaternion sideRot = Quaternion.AngleAxis(side, n);
        Vector3 finalDir = (sideRot * aheadDir).normalized;

        // 4) Position at same altitude as the bird uses
        float r = planetRadius + obstacleClearance;
        Vector3 pos = planet.position + finalDir * r;

        // 5) Orient: up = radial; forward = tangent toward bird
        Vector3 up = finalDir;
        Vector3 forwardToBird = Vector3.ProjectOnPlane((bird.position - pos), up).normalized;
        if (forwardToBird.sqrMagnitude < 1e-6f) forwardToBird = Vector3.Cross(worldAxis, up).normalized;
        Quaternion rot = Quaternion.LookRotation(forwardToBird, up);

        // 6) Instantiate as child of the planet but KEEP world scale (avoid giant pipes)
        var go = Instantiate(pipePrefab, pos, rot);
        go.transform.SetParent(planet, true); // true = keep world transform
        ActivePipes.Add(go.transform);

        var align = go.GetComponent<PipeScript>();
        if (!align) align = go.AddComponent<PipeScript>();
        align.SetPlanet(planet, worldAxis: Vector3.up, lockForwardToSpin: false);

        var despawn = go.AddComponent<PipeDespawner>();
    }
    
    void OnDestroy() { 
        ActivePipes.Clear(); 
    }

    public void Reset()
    {
        timer = 0f;
        foreach (var pipe in ActivePipes)
        {
            if (pipe) Destroy(pipe.gameObject);
        }
        ActivePipes.Clear();
    }
}
