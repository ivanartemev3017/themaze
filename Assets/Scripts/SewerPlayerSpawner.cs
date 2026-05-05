using UnityEngine;

public class SewerPlayerSpawner : MonoBehaviour
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] Vector3 spawnPosition = new Vector3(0f, 1f, 0f);

    void Start()
    {
        if (playerPrefab == null) { Debug.LogError("[SewerPlayerSpawner] playerPrefab not assigned."); return; }

        var player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        player.name = "Player";
        PlayerSpawner.FirePlayerSpawned(player);
    }
}
