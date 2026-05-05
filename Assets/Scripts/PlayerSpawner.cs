// =============================================================================
// PlayerSpawner.cs — Stage 2 Setup Instructions
// =============================================================================
//
// PREFAB TO USE
//   Drag this into the "playerPrefab" field in the Inspector:
//   StarterAssets > ThirdPersonController > Prefabs > PlayerArmature
//
//   Before dragging it in, duplicate the original prefab (right-click >
//   Duplicate) and rename it "Player" so you have your own copy to modify.
//   On your copy, swap Arissa's SkinnedMeshRenderer in place of the default
//   one, and assign Arissa's Avatar to the Animator component.
//
// CINEMACHINE SETUP
//   1. In the Hierarchy, add: GameObject > Cinemachine > Cinemachine Camera
//      (Unity 6 / Cinemachine 3.x) — or use "Virtual Camera" for CM 2.x.
//   2. After the player is spawned at runtime this script sets the camera's
//      Follow and LookAt targets to the spawned player's transform.
//      Make sure your Virtual Camera has a CinemachineFollow (or Transposer)
//      and CinemachineRotationComposer (or Composer) configured.
//   3. Recommended third-person offsets (set on the Virtual Camera):
//        Follow offset : (0, 1.8, -5)
//        Vertical axis : min -20°, max 60°
//        Damping       : Body X/Y/Z = 0.1 / 0.2 / 0.1
//   4. Add a CinemachineCollider (CM 2.x) or Cinemachine Deoccluder (CM 3.x)
//      extension so the camera pushes in when close to maze walls.
//
// SCENE OBJECT SETUP
//   • Create an empty GameObject named "PlayerSpawner".
//   • Attach this script.
//   • The MazeManager GameObject must exist in the scene (from Stage 1).
//   • The Cinemachine Virtual Camera must exist in the scene before Play.
//
// =============================================================================

using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    /// <summary>
    /// Fired after the player GameObject is instantiated and named.
    /// Subscribers receive the spawned player instance directly.
    /// </summary>
    public static event System.Action<GameObject> OnPlayerSpawned;

    [Tooltip("Drag your duplicated 'Player' prefab here (StarterAssets > ThirdPersonController > Prefabs > PlayerArmature).")]
    public GameObject playerPrefab;

    [Tooltip("Spawn height offset above the floor so the CharacterController does not intersect geometry.")]
    public float spawnHeightOffset = 0.1f;

    // The instance created at runtime — readable by other systems (e.g. GameManager, minimap camera).
    public GameObject SpawnedPlayer { get; private set; }

    private void OnEnable()
    {
        MazeGenerator.OnMazeReady += HandleMazeReady;
    }

    private void OnDisable()
    {
        MazeGenerator.OnMazeReady -= HandleMazeReady;
    }

    private void Start()
    {
        if (playerPrefab == null)
            Debug.LogError("[PlayerSpawner] playerPrefab is not assigned. " +
                           "Drag 'Player' (duplicate of PlayerArmature prefab) into this field.");

        // Note: actual spawning happens in HandleMazeReady, which fires after
        // MazeGenerator.GenerateMaze() completes — guaranteed to be after this Start().
    }

    private void HandleMazeReady()
    {
        if (playerPrefab == null) return;

        Vector3 spawnPosition = GetSpawnPosition();
        SpawnPlayer(spawnPosition);
    }

    // -------------------------------------------------------------------------
    // Internal
    // -------------------------------------------------------------------------

    private Vector3 GetSpawnPosition()
    {
        var mazeManager = FindAnyObjectByType<MazeManager>();
        if (mazeManager == null)
        {
            Debug.LogWarning("[PlayerSpawner] MazeManager not found in scene. Spawning at world origin.");
            return new Vector3(0f, spawnHeightOffset, 0f);
        }

        Vector3 pos = mazeManager.StartPosition;
        pos.y = spawnHeightOffset;
        return pos;
    }

    private void SpawnPlayer(Vector3 position)
    {
        // Try loading an override prefab from Resources/Characters/<id> when not default
        string charId   = PlayerPrefs.GetString("SelectedCharacter", "arissa").ToLower();
        var    prefab   = playerPrefab;

        if (charId != "arissa")
        {
            var loaded = Resources.Load<GameObject>("Characters/" + charId);
            if (loaded != null)
                prefab = loaded;
            else
                Debug.LogWarning($"[PlayerSpawner] Character prefab 'Resources/Characters/{charId}' not found — using default.");
        }

        SpawnedPlayer = Instantiate(prefab, position, Quaternion.identity);
        SpawnedPlayer.name = "Player";

        // Non-Arissa prefabs are created manually and may have wrong CharacterController defaults.
        // Force the same values Arissa's PlayerArmature uses so camera height and grounding match.
        if (charId != "arissa")
        {
            var cc = SpawnedPlayer.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.height = 1.8f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.9f, 0f);
            }
            // Reset rotation — some Mixamo FBX roots have a baked-in 180° Y offset
            SpawnedPlayer.transform.rotation = Quaternion.identity;
        }

        // Each step wrapped — if any throws (e.g. Eve's embedded FBX materials),
        // OnPlayerSpawned still fires and the camera still follows.
        try { DisableStarterAssetsInput(SpawnedPlayer); } catch (System.Exception e) { Debug.LogError("[PlayerSpawner] DisableStarterAssetsInput: " + e.Message); }
        try { DarkenBrightMaterials(SpawnedPlayer);     } catch (System.Exception e) { Debug.LogError("[PlayerSpawner] DarkenBrightMaterials: "     + e.Message); }
        try { InjectPlayerMovement(SpawnedPlayer);      } catch (System.Exception e) { Debug.LogError("[PlayerSpawner] InjectPlayerMovement: "      + e.Message); }

        OnPlayerSpawned?.Invoke(SpawnedPlayer);
    }

    /// <summary>
    /// Disables Starter Assets input components so they don't fight with
    /// our PlayerMovement script.
    /// </summary>
    private static void DisableStarterAssetsInput(GameObject player)
    {
        foreach (MonoBehaviour mb in player.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb == null) continue;
            string t = mb.GetType().Name;
            if (t == "ThirdPersonController" || t == "PlayerInput" || t == "StarterAssetsInputs")
            {
                mb.enabled = false;
                Debug.Log($"[PlayerSpawner] Disabled Starter Assets component: {t}");
            }
        }

        // Disable extra AudioListeners on children — two listeners mute all audio in Unity
        foreach (var al in player.GetComponentsInChildren<AudioListener>())
        {
            al.enabled = false;
            Debug.Log("[PlayerSpawner] Disabled child AudioListener to prevent double-listener silence.");
        }

        // Disable child cameras that aren't ours (Starter Assets follow cam)
        foreach (var cam in player.GetComponentsInChildren<Camera>())
        {
            cam.enabled = false;
            Debug.Log($"[PlayerSpawner] Disabled child Camera: {cam.gameObject.name}");
        }
    }

    /// <summary>
    /// Darkens any material on the character whose base color is too bright (> 0.72 luminance).
    /// Fixes the "white cloak" issue from Arissa.fbx embedded materials — the maze aesthetic
    /// requires dark, worn-looking clothing. Skin/hair typically have lower or more saturated
    /// colors and are not affected.
    /// </summary>
    private static void DarkenBrightMaterials(GameObject player)
    {
        foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mats = smr.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;

                // Try URP property first, fall back to legacy
                Color col = mats[i].HasProperty("_BaseColor")
                    ? mats[i].GetColor("_BaseColor")
                    : mats[i].HasProperty("_Color")
                        ? mats[i].GetColor("_Color")
                        : Color.black;

                float lum = col.r * 0.299f + col.g * 0.587f + col.b * 0.114f;
                if (lum > 0.72f)
                {
                    var m = new Material(mats[i]);
                    // Dark leather/cloth: keep hue, crush brightness to 15-20%
                    Color dark = new Color(col.r * 0.18f, col.g * 0.15f, col.b * 0.12f, col.a);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", dark);
                    if (m.HasProperty("_Color"))     m.SetColor("_Color",     dark);
                    mats[i] = m;
                    changed  = true;
                    Debug.Log($"[PlayerSpawner] Darkened bright material '{mats[i].name}' (lum={lum:F2}) on {smr.gameObject.name}");
                }
            }

            if (changed) smr.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// Adds PlayerMovement to the spawned player and wires the scene ScreenJoystick.
    /// </summary>
    private static void InjectPlayerMovement(GameObject player)
    {
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
            movement = player.AddComponent<PlayerMovement>();

        // Wire joystick if one exists in the scene
        FloatingJoystick joystick = FindAnyObjectByType<FloatingJoystick>();
        if (joystick != null)
            movement.SetJoystick(joystick);
        else
            Debug.Log("[PlayerSpawner] No FloatingJoystick found — keyboard input only.");

        Debug.Log("[PlayerSpawner] PlayerMovement injected.");
    }

}
