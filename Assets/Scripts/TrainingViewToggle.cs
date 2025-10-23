using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

public class TrainingViewToggle : MonoBehaviour
{
    [Tooltip("Time scale during training. Use 1 to watch, >1 to speed up.")]
    public float trainingTimeScale = 1f;

    [Tooltip("Normal time scale when *not* training.")]
    public float normalTimeScale = 1f;

    float baseFixed;

    void Awake()
    {
        baseFixed = Time.fixedDeltaTime; // usually 0.02
    }

    void Start()
    {
        bool communicatorOn = Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;

        // Are there any agents set to 'Default' (i.e., trainable)?
        var allBps = Object.FindObjectsByType<BehaviorParameters>(FindObjectsSortMode.None);
        bool anyDefault = allBps.Any(bp => bp.BehaviorType == BehaviorType.Default);

        // Effective training only if communicator is on AND some agent is in Default
        bool effectiveTraining = communicatorOn && anyDefault;

        float targetScale = effectiveTraining ? trainingTimeScale : normalTimeScale;
        
        // During training, always use 1.0 for Unity's Time.timeScale to respect Academy stepping
        // if (effectiveTraining)
        // {
        //     targetScale = 1.0f;
        // }

        // Set both timeScale and fixedDeltaTime together to keep physics cadence stable
        Time.timeScale = targetScale;
        Time.fixedDeltaTime = baseFixed * targetScale;
    }
}
