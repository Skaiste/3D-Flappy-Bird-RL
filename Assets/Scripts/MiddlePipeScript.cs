using UnityEngine;

public class MiddlePipeScript : MonoBehaviour
{
    public LogicScript logic;
    void Start()
    {
        logic = GameObject.FindGameObjectWithTag("Logic").GetComponent<LogicScript>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == 6) {// Bird layer
            logic.AddScore(1);
            // reward the agent, if present
            var agent = other.GetComponent<BirdAgent>();
            if (agent) agent.OnBirdScored();
        }
    }
}
