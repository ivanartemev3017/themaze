using UnityEngine;

public class SewerPlayerSpawner : MonoBehaviour
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] SewerMazeGenerator mazeGenerator;

    void Start()
    {
        if (mazeGenerator != null)
            mazeGenerator.GenerateMaze();

        var spawnPos = mazeGenerator != null
            ? mazeGenerator.StartWorldPosition + Vector3.up * 1f
            : new Vector3(2f, 1f, 2f);

        if (playerPrefab == null) { Debug.LogError("[SewerPlayerSpawner] playerPrefab not assigned."); return; }

        var player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        player.name = "Player";
        player.tag  = "Player";
        PlayerSpawner.FirePlayerSpawned(player);

        // Place exit trigger
        if (mazeGenerator != null)
        {
            var exitGO = new GameObject("SewerExit");
            exitGO.transform.position = mazeGenerator.ExitWorldPosition + Vector3.up * 0.5f;
            var col = exitGO.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 1.8f;
            exitGO.AddComponent<SewerExitTrigger>();
        }
    }
}
