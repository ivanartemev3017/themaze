using UnityEngine;

public class SewerExitTrigger : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            SewerLevelManager.Instance?.PlayerReachedExit();
    }
}
