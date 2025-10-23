using UnityEngine;
using Unity.MLAgents;

public enum ControlMode { Human, AI }

[RequireComponent(typeof(CharacterController))]
public class BirdPlayerScript : MonoBehaviour
{
    [Header("Control")]
    public ControlMode controlMode = ControlMode.Human;
    [HideInInspector] public float aiStrafe = 0f;   // -1..1
    [HideInInspector] public bool aiFlap = false;

    [Header("Planet")]
    public Transform planet;
    public float planetRadius = 25f;
    public Vector3 worldAxis = Vector3.up;
    public PlanetSpinner spinner;

    [Header("Side movement")]
    public float sideSpeedDeg = 90f;        // degrees/sec around the planet

    [Header("Jump (radial)")]
    public float groundClearance = 0.05f;    // base height above the surface (min)
    public float jumpHeight = 3f;            // how high a tap lifts you (in world units)
    public float gravity = 3f;            // inward acceleration (positive value)
    public float radialDamping = 1.5f;       // light damping for smooth arcs

    [Header("Animation / Logic")]
    public Animator anim;
    public LogicScript logic;

    [Header("Ceiling")]
    public float maxAltitude = 15f;     // extra height above groundClearance allowed
    public float ceilingGrace = 0.15f;   // seconds above ceiling before game over (prevents jitter)
    private float overCeilingTimer = 0f;

    bool isAlive;
    // internal radial state (scalar, outward is positive)
    float radialVel = 0f;                    // m/s along the surface normal
    float altitude = 0f;                     // extra height above surface beyond clearance

    public bool IsAlive => isAlive;        // expose read-only
    public float Altitude => altitude;     // expose read-only
    public float RadialVel => radialVel;   // expose read-only

    bool _humanFlapDown;

    bool IsTraining() =>
        Academy.IsInitialized && (Academy.Instance.IsCommunicatorOn || Application.isBatchMode);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // controller = GetComponent<CharacterController>();
        if (!logic) logic = GameObject.FindGameObjectWithTag("Logic").GetComponent<LogicScript>();
        if (anim && !IsTraining() && controlMode == ControlMode.Human)
            anim.Play("Fly");

        altitude = 0f;
        SnapToSurface();
        AlignToSurface();
        isAlive = true;

        // Debug.Log($"Start - Altitude: {altitude:F2}, Alive: {isAlive}, Position: {transform.position}, Planet: {planet.position}");

        var agent = GetComponent<BirdAgent>();
        if (agent != null) agent.enabled = (controlMode == ControlMode.AI);
    }

    void Update()
    {
        if (!isAlive && Input.GetKeyDown(KeyCode.Return)) logic.restartGame();
        if (controlMode == ControlMode.Human && Input.GetKeyDown(KeyCode.Space))
            _humanFlapDown = true;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // --- SIDE-ONLY (strafe): rotate around the local normal at the bird
        Vector3 n = (transform.position - planet.position).normalized;
        float input = (controlMode == ControlMode.Human)
            ? Input.GetAxis("Horizontal") // A/D or arrows
            : Mathf.Clamp(aiStrafe, -1f, 1f);
        float sideAngle = input * sideSpeedDeg * dt;
        if (Mathf.Abs(sideAngle) > Mathf.Epsilon)
        {
            transform.RotateAround(planet.position, n, sideAngle);
        }

        // --- JUMP (radial)
        bool flapPressed = (controlMode == ControlMode.Human) ? _humanFlapDown : aiFlap;
        if (flapPressed && isAlive)
        {
            float v = Mathf.Sqrt(2f * Mathf.Abs(gravity) * Mathf.Max(0f, jumpHeight));
            radialVel += v; // outward impulse
        }
        _humanFlapDown = false;
        aiFlap = false; // consume once per decision when AI drives

        radialVel -= Mathf.Abs(gravity) * dt;   // inward pull
        altitude  += radialVel * dt;

        if (altitude < 0f)
        {
            altitude = 0f;
            if (radialVel < 0f) radialVel = 0f; // cancel inward on contact
        }
        radialVel -= radialVel * radialDamping * dt;

        // --- APPLY POSITION at constant radius + clearance + altitude
        n = (transform.position - planet.position).normalized;
        float r = planetRadius + groundClearance + altitude;
        transform.position = planet.position + n * r;

        // --- ORIENT to face the running direction from the planet spinner
        Vector3 A = (spinner ? spinner.CurrentAxisWorld : Vector3.up).normalized;
        if (A.sqrMagnitude < 1e-6f) A = Vector3.up;

        Vector3 forwardRun = -Vector3.Cross(A, n); // ground moves toward bird along this
        if (forwardRun.sqrMagnitude < 1e-6f)
            forwardRun = Vector3.ProjectOnPlane(transform.forward, n).normalized;

        transform.rotation = Quaternion.LookRotation(forwardRun.normalized, n);

        // --- CEILING CHECK
        // Base radius = planetRadius + groundClearance (your normal running height at 0 altitude)
        // Max allowed radius = base + maxAltitude
        float baseRadius = planetRadius + groundClearance;
        float maxRadius  = baseRadius + maxAltitude;

        r = (transform.position - planet.position).magnitude;
        if (r > maxRadius)
        {
            overCeilingTimer += Time.fixedDeltaTime;
            if (overCeilingTimer >= ceilingGrace)
            {
                // Debug.Log($"Bird hit ceiling! r={r:F2}, maxRadius={maxRadius:F2}, altitude={altitude:F2}");
                OnGameOver();
            }
            
        }
        else
        {
            overCeilingTimer = 0f; // back under the ceiling; reset grace timer
        }
    }

    void SnapToSurface()
    {
        Vector3 dir = transform.position - planet.position;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
        dir.Normalize();
        float r = planetRadius + groundClearance + altitude;
        transform.position = planet.position + dir * r;
    }

    void AlignToSurface()
    {
        Vector3 normal = (transform.position - planet.position).normalized;
        Vector3 forwardTangent = Vector3.Cross(worldAxis.normalized, normal);
        if (forwardTangent.sqrMagnitude < 1e-6f)
            forwardTangent = Vector3.ProjectOnPlane(transform.forward, normal).normalized; // fallback

        transform.rotation = Quaternion.LookRotation(forwardTangent.normalized, normal);
    }

    private void OnCollisionEnter(Collision collision)
    {
        OnGameOver();
    }

    public void OnGameOver()
    {
        if (!isAlive) return;
        
        // Debug.Log($"OnGameOver called - Altitude: {altitude:F2}, Position: {transform.position}, Distance from planet: {(transform.position - planet.position).magnitude:F2}");
        
        if (controlMode == ControlMode.Human)
            anim.Play("Dead");
        logic.gameOver(controlMode == ControlMode.AI);
        isAlive = false;
        
        // For AI training, notify the agent that the bird died
        if (controlMode == ControlMode.AI)
        {
            var agent = GetComponent<BirdAgent>();
            if (agent != null)
            {
                // Just notify the agent - let it handle episode ending and rewards
                agent.OnBirdDied();
            }
        }
    }

    void OnValidate()
    {
        planetRadius = Mathf.Max(0f, planetRadius);
        groundClearance = Mathf.Max(0f, groundClearance);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        gravity = Mathf.Abs(gravity);
        radialDamping = Mathf.Max(0f, radialDamping);
    }

    public void ResetForEpisode()
    {
        // Force reset all state
        altitude = 0f;
        radialVel = 0f;
        overCeilingTimer = 0f;
        isAlive = true;

        // Force position to safe location
        SnapToSurface();
        AlignToSurface();
        
        // Double-check position is safe
        float currentRadius = (transform.position - planet.position).magnitude;
        float maxAllowedRadius = planetRadius + groundClearance + maxAltitude;
        
        if (currentRadius > maxAllowedRadius)
        {
            // Debug.Log($"Position unsafe after reset! r={currentRadius:F2}, max={maxAllowedRadius:F2}, forcing to surface");
            // Force to surface if still unsafe
            Vector3 dir = (transform.position - planet.position).normalized;
            transform.position = planet.position + dir * (planetRadius + groundClearance);
        }

        // Debug.Log($"ResetForEpisode - Altitude: {altitude:F2}, Alive: {isAlive}, Position: {transform.position}, Radius: {currentRadius:F2}");

        if (controlMode == ControlMode.Human)
            if (anim) anim.Play("Fly");
    }
}
