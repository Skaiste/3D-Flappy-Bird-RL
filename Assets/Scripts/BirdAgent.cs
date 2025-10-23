using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;
using System.Collections.Generic;

public class BirdAgent : Agent
{
    [Header("Refs")]
    public BirdPlayerScript bird;
    public Transform planet;
    public PipeSpawnerScript spawner;
    public LogicScript logic;
    public PlanetSpinner spinner;

    public int numObservablePipes = 3; // should match branch 2 size

    public enum TrainingStage { Survival, SimplePipes, FullGame }
    
    [Header("Training Stages")]
    public TrainingStage currentStage = TrainingStage.Survival;
    
    [Header("Stage Settings")]
    [SerializeField] bool enablePipeSpawning = false;
    [SerializeField] float simplePipeSpawnInterval = 8f; // Slower for simple stage
    [SerializeField] float fullPipeSpawnInterval = 5f;  // Normal speed for full stage

    // Tunables
    public float maxArcLookaheadDeg = 60f;

    // Progress shaping state
    float _prevAlong = 0f;
    bool _havePrevAlong = false;

    // Step/time pressure (discourages camping)
    [SerializeField] float _stepPenalty = -0.001f;

    // Flapping tracking (for potential rewards, not constraints)
    int _consecutiveFlaps = 0;

    bool _havePrevObs;
    float _prevAlongObs;
    float _lastAltitude = 0f;
    
    // Episode timing for survival stage
    public float _episodeStartTime;
    [SerializeField] float survivalEpisodeDuration = 15f; // 15 seconds for survival stage

    Transform NextPipe()
    {
        if (PipeSpawnerScript.ActivePipes.Count == 0) return null;

        Vector3 n = (bird.transform.position - planet.position).normalized;
        Vector3 A = (spinner ? spinner.CurrentAxisWorld : Vector3.up).normalized;
        Vector3 forwardRun = -Vector3.Cross(A, n).normalized;

        Transform best = null;
        float bestAlong = float.PositiveInfinity;

        foreach (var t in PipeSpawnerScript.ActivePipes.ToList())
        {
            if (!t) continue;

            // Prefer a child named "GapCenter" if present
            Transform gate = t.Find("MiddlePipe");
            if (!gate)
            {
                var mid = t.GetComponentInChildren<MiddlePipeScript>();
                if (mid) gate = mid.transform;
            }
            if (!gate) gate = t;

            // only consider ahead
            Vector3 toGate = gate.position - bird.transform.position;
            float along = Vector3.Dot(toGate, forwardRun);
            if (along <= 0f) continue; // behind us

            // optionally also filter by arc window like you had, but along is the real priority
            if (along < bestAlong) { bestAlong = along; best = gate; }
        }
        return best;
    }


    // Returns up to k pipes that are ahead and within arc window, sorted by along-distance (closest first)
    System.Collections.Generic.List<Transform> TopPipes(int k)
    {
        var result = new System.Collections.Generic.List<Transform>();
        if (PipeSpawnerScript.ActivePipes.Count == 0) return result;

        Vector3 n = (bird.transform.position - planet.position).normalized;
        Vector3 A = (spinner ? spinner.CurrentAxisWorld : Vector3.up).normalized;
        Vector3 forwardRun = -Vector3.Cross(A, n).normalized;

        // Gather candidates with their "along" distance and arc
        var candidates = new System.Collections.Generic.List<(Transform t, float along, float arc)>();
        foreach (var t in PipeSpawnerScript.ActivePipes.ToList())
        {
            if (!t) continue;

            Transform gate = t.Find("MiddlePipe");
            if (!gate)
            {
                var mid = t.GetComponentInChildren<MiddlePipeScript>();
                if (mid) gate = mid.transform;
            }
            if (!gate) gate = t;

            Vector3 toGate = gate.position - bird.transform.position;
            float along = Vector3.Dot(toGate, forwardRun);
            if (along <= 0f) continue; // behind us

            float arc = Mathf.Acos(Mathf.Clamp(Vector3.Dot(toGate.normalized, forwardRun), -1f, 1f)) * Mathf.Rad2Deg;
            if (arc <= maxArcLookaheadDeg)
            {
                candidates.Add((gate, along, arc));
            }
        }

        // Sort by along-distance (closest first)
        candidates.Sort((a, b) => a.along.CompareTo(b.along));

        // Take up to k
        for (int i = 0; i < Mathf.Min(k, candidates.Count); i++)
        {
            result.Add(candidates[i].t);
        }

        return result;
    }

    public override void OnEpisodeBegin()
    {
        // Debug.Log($"OnEpisodeBegin called - Before reset: Alive={bird.IsAlive}, Altitude={bird.Altitude:F2}");
        
        bird.ResetForEpisode();
        // spawner.Reset();
        
        // Debug.Log($"OnEpisodeBegin called - After reset: Alive={bird.IsAlive}, Altitude={bird.Altitude:F2}");
        
        // Clear all active pipes
        foreach (var pipe in PipeSpawnerScript.ActivePipes.ToList())
        {
            if (pipe) Destroy(pipe.gameObject);
        }
        PipeSpawnerScript.ActivePipes.Clear();
        
        // Configure training stage
        ConfigureTrainingStage();
        
        // Reset score manually
        logic.playerScore = 0;
        if (logic.scoreText) logic.scoreText.text = "0";
        _prevAlong = 0f;
        _havePrevAlong = false;
        _lastAltitude = 0f;
        _consecutiveFlaps = 0;
        _episodeStartTime = Time.time;
        
        // Debug: Log episode start
        // Debug.Log($"Episode Begin - Stage: {currentStage}, Bird Alive: {bird.IsAlive}, Altitude: {bird.Altitude:F2}");
    }
    
    void ConfigureTrainingStage()
    {
        if (spawner == null) return;
        // currentStage = TrainingStage.SimplePipes;
        
        switch (currentStage)
        {
            case TrainingStage.Survival:
                // Disable pipe spawning for survival training
                spawner.enabled = false;
                enablePipeSpawning = false;
                break;
                
            case TrainingStage.SimplePipes:
                // Enable pipes with easier settings
                spawner.enabled = true;
                spawner.spawnInterval = simplePipeSpawnInterval;
                enablePipeSpawning = true;
                break;
                
            case TrainingStage.FullGame:
                // Full complexity
                spawner.enabled = true;
                spawner.spawnInterval = fullPipeSpawnInterval;
                enablePipeSpawning = true;
                break;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Always use the bird's local tangent plane as reference
        Vector3 n = (bird.transform.position - planet.position).normalized;
        Vector3 A = (spinner ? spinner.CurrentAxisWorld : Vector3.up).normalized;
        Vector3 forwardRun = -Vector3.Cross(A, n).normalized;
        Vector3 right = Vector3.Cross(n, forwardRun).normalized;
        
        // Bird state in local plane coordinates
        float altitude = bird.Altitude;
        float radialVel = bird.RadialVel;
        
        sensor.AddObservation(altitude / 15f);           // Height above surface
        sensor.AddObservation(Mathf.Clamp(radialVel / 5f, -1f, 1f)); // Vertical velocity
        sensor.AddObservation((15f - altitude) / 15f);   // Distance to ceiling
        
        if (currentStage != TrainingStage.Survival)
        {
            // Pipe observations in local plane coordinates
            Transform next = NextPipe();
            if (next)
            {
                Vector3 toPipe = next.position - bird.transform.position;
                float along = Vector3.Dot(toPipe, forwardRun);    // Distance ahead
                float lateral = Vector3.Dot(toPipe, right);       // Side position
                float radialErr = Vector3.Dot(toPipe, n);          // Height difference
                
                // 1. Distance to pipe (normalized)
                sensor.AddObservation(Mathf.Clamp(along / 20f, -1f, 1f));
                
                // 2. Lateral offset from pipe center (normalized)
                sensor.AddObservation(Mathf.Clamp(lateral / 10f, -1f, 1f));
                
                // 3. Height difference from pipe middle (normalized)
                sensor.AddObservation(Mathf.Clamp(radialErr / 5f, -1f, 1f));
                
                // 4. Velocity change toward pipe (for smooth approach)
                float dAlong = _havePrevObs ? along - _prevAlongObs : 0f;
                sensor.AddObservation(Mathf.Clamp(dAlong / 5f, -1f, 1f));
                _prevAlongObs = along;
                _havePrevObs = true;
                
                // 5. Relative velocity (bird's velocity relative to pipe)
                float relativeVel = radialVel - 0f; // Pipe doesn't move vertically
                sensor.AddObservation(Mathf.Clamp(relativeVel / 5f, -1f, 1f));
                
                // // 6. Pipe approach urgency (closer = more urgent)
                // float urgency = Mathf.Clamp(1f - Mathf.Abs(along) / 20f, 0f, 1f);
                // sensor.AddObservation(urgency);
                
                // 7. Combined alignment score (lateral + height)
                float lateralAlignment = Mathf.Exp(-Mathf.Abs(lateral) / 5f);
                float heightAlignment = Mathf.Exp(-Mathf.Abs(radialErr) / 2f);
                float combinedAlignment = (lateralAlignment + heightAlignment) / 2f;
                sensor.AddObservation(combinedAlignment);
                
                // // 8. Time to collision (estimated)
                // float timeToCollision = along / Mathf.Max(Mathf.Abs(radialVel), 0.1f);
                // sensor.AddObservation(Mathf.Clamp(timeToCollision / 10f, 0f, 1f));
            }
            else
            {
                // No pipe - pad with zeros
                for (int i = 3; i < 9; i++)
                    sensor.AddObservation(0f);
            }
        }
        else
        {
            for (int i = 3; i < 9; i++)
                sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Debug: Log method entry
        // Debug.Log($"OnActionReceived called - Stage: {currentStage}, Alive: {bird.IsAlive}");
        
        // Discrete: [ flap(0/1), strafe(0=left,1=none,2=right) ]
        int flap = actions.DiscreteActions[0];
        int strafe = actions.DiscreteActions[1];

        // Pure model decision - no artificial constraints
        bool doFlap = (flap == 1);
        
        // Track consecutive flaps for potential rewards (but don't constrain)
        if (doFlap) {
            _consecutiveFlaps++;
        } else {
            _consecutiveFlaps = 0;
        }

        // Apply actions
        bird.aiFlap = doFlap;
        bird.aiStrafe = (strafe == 0 ? -1f : (strafe == 2 ? 1f : 0f));

        // Energy management removed - let model learn pure flapping behavior

        // === STAGE-SPECIFIC REWARD SYSTEM ===
                
        // Check if episode ended due to common rewards (e.g., ceiling hit)
        if (!bird.IsAlive)
        {
            // float deathTime = Time.time - _episodeStartTime;
            // Debug.Log($"Step Reward - Stage: {currentStage}, Time: {deathTime:F2}s, Altitude: {bird.Altitude:F2}, Energy: {_energy:F2}, Flap: {doFlap}, Reward: {GetCumulativeReward():F4}");
            return; // Episode already ended, don't continue with stage rewards
        }

        // Stage-specific rewards
        switch (currentStage)
        {
            case TrainingStage.Survival:
                AddReward(GetSurvivalReward(doFlap));
                break;
                
            case TrainingStage.SimplePipes:
                AddReward(GetSurvivalReward(doFlap) * 0.1f);
                AddReward(GetSimplePipeReward(doFlap));
                break;
                
            case TrainingStage.FullGame:
                AddReward(GetSurvivalReward(doFlap) * 0.2f);
                AddReward(GetSimplePipeReward(doFlap));
                // AddReward(GetFullGameReward(doFlap));
                break;
        }
    }
        
    float GetSurvivalReward(bool doFlap)
    {
        float reward = 0f;
        float currentAlt = bird.Altitude;
        float radialVel = bird.RadialVel;
        float maxAltitude = 15f;
        
        // 1. Altitude-based rewards
        float optimalAltitude = maxAltitude * 0.25f; // Stay at 25% of max height
        float altitudeError = Mathf.Abs(currentAlt - optimalAltitude);
        float altitudeReward = Mathf.Exp(-altitudeError / 1.5f) * 0.02f;
        reward += altitudeReward;
        
        // 2. Ceiling avoidance
        float ceilingDistance = maxAltitude - currentAlt;
        if (ceilingDistance < 4f) {
            float dangerPenalty = (4f - ceilingDistance) / 4f * -0.1f;
            reward += dangerPenalty;
        }
        
        // 3. Smooth flight encouragement
        float velocityReward = Mathf.Exp(-Mathf.Abs(radialVel) / 1.5f) * 0.01f;
        reward += velocityReward;
        
        // 4. Flapping efficiency
        if (!doFlap) {
            reward += 0.002f; // Small reward for not flapping
        }
        
        // 5. Penalty for excessive flapping
        if (_consecutiveFlaps > 2) {
            reward -= 0.005f * (_consecutiveFlaps - 2);
        }
        return reward;
    }
    
    float GetSimplePipeReward(bool doFlap)
    {
        float reward = 0f;
        Transform target = NextPipe();
        if (target)
        {
            Vector3 n = (bird.transform.position - planet.position).normalized;
            Vector3 A = (spinner ? spinner.CurrentAxisWorld : Vector3.up).normalized;
            Vector3 forwardRun = -Vector3.Cross(A, n).normalized;
            Vector3 right = Vector3.Cross(n, forwardRun).normalized;

            Vector3 toPipe = (target.position - bird.transform.position);
            float along = Vector3.Dot(toPipe, forwardRun);
            float lateral = Vector3.Dot(toPipe, right);
            float radialErr = Vector3.Dot(toPipe, n); // Height difference from pipe

            if (!_havePrevAlong) { _prevAlong = along; _havePrevAlong = true; }

            // Gentle progress rewards
            float progress = Mathf.Clamp(_prevAlong - along, -1f, 1f);
            reward += 0.002f * progress;
            _prevAlong = along;

            // Lateral alignment reward (side-to-side)
            float lateralAlignment = Mathf.Exp(-Mathf.Abs(lateral));
            reward += 0.001f * lateralAlignment;
            
            // Altitude alignment reward (height matching with pipe middle)
            float altitudeAlignment = Mathf.Exp(-Mathf.Abs(radialErr) / 2f);
            reward += 0.01f * altitudeAlignment; // Stronger reward for height matching
        }
        return reward;
    }
    
    float GetFullGameReward(bool doFlap)
    {
        float reward = 0f;
        
        Transform target = NextPipe();
        if (target)
        {
            Vector3 n = (bird.transform.position - planet.position).normalized;
            Vector3 A = (spinner ? spinner.CurrentAxisWorld : Vector3.up).normalized;
            Vector3 forwardRun = -Vector3.Cross(A, n).normalized;
            Vector3 right = Vector3.Cross(n, forwardRun).normalized;

            Vector3 toPipe = (target.position - bird.transform.position);
            float along = Vector3.Dot(toPipe, forwardRun);
            float lateral = Vector3.Dot(toPipe, right);
            float radialErr = Vector3.Dot(toPipe, n); // Height difference from pipe

            if (!_havePrevAlong) { _prevAlong = along; _havePrevAlong = true; }

            // Full progress rewards
            float progress = Mathf.Clamp(_prevAlong - along, -1f, 1f);
            reward += 0.005f * progress;
            _prevAlong = along;

            // Full lateral alignment rewards
            float closeness = 1f / (1f + Mathf.Abs(along));
            float lateralAlignment = Mathf.Exp(-Mathf.Abs(lateral));
            reward += Mathf.Clamp01(closeness) * lateralAlignment * 0.001f;
            
            // Strong altitude alignment reward (height matching with pipe middle)
            float altitudeAlignment = Mathf.Exp(-Mathf.Abs(radialErr) / 1.5f);
            reward += 0.02f * altitudeAlignment; // Even stronger reward for height matching
            
            // Full pipe navigation bonus
            if (along < 0f && _prevAlong > 0f) {
                reward += 0.1f;
            }
        }
        return reward;
    }

    public void OnBirdScored()
    {
        AddReward(3f);
    }
    
    // Called when the bird dies (from BirdPlayerScript)
    public void OnBirdDied()
    {
        // Give survival time reward based on how long the bird lived
        float survivalTime = Time.time - _episodeStartTime;
        // float survivalReward = survivalTime * 2f; // 0.1 reward per second survived
        // AddReward(survivalReward);
        AddReward(-1.0f); // Death penalty
        Debug.Log($"Bird died after {survivalTime:F2}s - Final Reward: {GetCumulativeReward():F4}");
        EndEpisode();
    }
    
    // Public methods for training stage management
    public void SetTrainingStage(TrainingStage stage)
    {
        currentStage = stage;
        ConfigureTrainingStage();
    }
    
    public void NextTrainingStage()
    {
        switch (currentStage)
        {
            case TrainingStage.Survival:
                SetTrainingStage(TrainingStage.SimplePipes);
                break;
            case TrainingStage.SimplePipes:
                SetTrainingStage(TrainingStage.FullGame);
                break;
            case TrainingStage.FullGame:
                // Already at final stage
                break;
        }
    }
    
    public string GetStageDescription()
    {
        switch (currentStage)
        {
            case TrainingStage.Survival:
                return "Survival: Learn to fly without hitting ceiling";
            case TrainingStage.SimplePipes:
                return "Simple Pipes: Learn basic pipe navigation";
            case TrainingStage.FullGame:
                return "Full Game: Complete complexity";
            default:
                return "Unknown Stage";
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var da = actionsOut.DiscreteActions;
        da[0] = Input.GetKeyDown(KeyCode.Space) ? 1 : 0;
        float h = Input.GetAxisRaw("Horizontal");
        da[1] = h < -0.2f ? 0 : (h > 0.2f ? 2 : 1);
    }
}
