using UnityEngine;

/// <summary>
/// Placed on the ExitTrigger GameObject created at runtime by GameManager.
/// Calls GameManager.Win() when the Player enters the trigger sphere.
/// </summary>
public class ExitTriggerHandler : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // PlayerSpawner sets the spawned instance name to "Player"
        if (other.name == "Player" || other.CompareTag("Player"))
            GameManager.Instance?.Win();
    }
}
