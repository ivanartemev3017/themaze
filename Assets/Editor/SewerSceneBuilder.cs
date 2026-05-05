using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SewerSceneBuilder
{
    const string SCENE_PATH   = "Assets/Scenes/SewerScene.unity";
    const string MAT_DIR      = "Assets/Sewer/Materials/";
    const string WATER_BASE   = "Assets/Sewer/Textures/Water/Swamp_Water_tgmjffbqx_1K_BaseColor.jpg";
    const string WATER_NORM   = "Assets/Sewer/Textures/Water/Swamp_Water_tgmjffbqx_1K_Normal.jpg";
    const string TEX_WALL     = "Assets/Sewer/Textures/Sewer/concrete_dirty.jpg";
    const string TEX_FLOOR    = "Assets/Sewer/Textures/Sewer/brick_pavement.jpg";
    const string TEX_CEILING  = "Assets/Sewer/Textures/Sewer/concrete_base_02.jpg";
    const string PLAYER_PREFAB= "Assets/StarterAssets/ThirdPersonController/Prefabs/Player_Arissa.prefab";

    // ── Main entry ────────────────────────────────────────────────────────────

    [MenuItem("Sewer/Create Sewer Scene")]
    static void CreateSewerScene()
    {
        System.IO.Directory.CreateDirectory(MAT_DIR);
        System.IO.Directory.CreateDirectory("Assets/Scenes");

        // Materials
        var wallMat    = MakeMat("SewerWall",    TEX_WALL,    new Color(0.32f, 0.34f, 0.34f), 0.04f);
        var floorMat   = MakeMat("SewerFloor",   TEX_FLOOR,   new Color(0.28f, 0.30f, 0.30f), 0.06f);
        var ceilMat    = MakeMat("SewerCeiling", TEX_CEILING, new Color(0.20f, 0.22f, 0.23f), 0.03f);
        var waterMat   = MakeWaterMat();
        var gateMat    = MakeGateMat();
        AssetDatabase.SaveAssets();

        // New scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Environment
        RenderSettings.fog         = true;
        RenderSettings.fogMode     = FogMode.Exponential;
        RenderSettings.fogDensity  = 0.018f;
        RenderSettings.fogColor    = new Color(0.03f, 0.05f, 0.06f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight= new Color(0.07f, 0.09f, 0.10f);

        // Faint directional light (nearly everything lit by point lights)
        var dLight = new GameObject("Directional Light").AddComponent<Light>();
        dLight.type      = LightType.Directional;
        dLight.intensity = 0.08f;
        dLight.color     = new Color(0.3f, 0.4f, 0.5f);
        dLight.shadows   = LightShadows.None;
        dLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Level Manager
        var managerGO = new GameObject("SewerLevelManager");
        managerGO.AddComponent<SewerLevelManager>();

        // Maze Generator
        var mazeGO  = new GameObject("SewerMazeGenerator");
        var mazeGen = mazeGO.AddComponent<SewerMazeGenerator>();

        var soMaze = new SerializedObject(mazeGen);
        soMaze.FindProperty("wallMaterial").objectReferenceValue    = wallMat;
        soMaze.FindProperty("floorMaterial").objectReferenceValue   = floorMat;
        soMaze.FindProperty("ceilingMaterial").objectReferenceValue = ceilMat;
        soMaze.FindProperty("waterMaterial").objectReferenceValue   = waterMat;
        soMaze.FindProperty("gateMaterial").objectReferenceValue    = gateMat;
        soMaze.ApplyModifiedProperties();

        // Spawner
        var spawnerGO  = new GameObject("SewerSpawner");
        var spawner    = spawnerGO.AddComponent<SewerPlayerSpawner>();
        spawnerGO.AddComponent<PlayerTorch>();

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);

        var soSpawner = new SerializedObject(spawner);
        soSpawner.FindProperty("playerPrefab").objectReferenceValue    = playerPrefab;
        soSpawner.FindProperty("mazeGenerator").objectReferenceValue   = mazeGen;
        soSpawner.ApplyModifiedProperties();

        // Enemy
        var enemyGO  = new GameObject("SewerEnemy");
        var enemy    = enemyGO.AddComponent<SewerEnemy>();
        // [RequireComponent] already added CharacterController — just configure it
        var enemyCC  = enemyGO.GetComponent<CharacterController>();
        enemyCC.height = 1.8f;
        enemyCC.radius = 0.4f;
        enemyCC.center = new Vector3(0, 0.9f, 0);

        // Enemy visual — capsule placeholder
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.SetParent(enemyGO.transform, false);
        capsule.transform.localPosition = new Vector3(0, 0.9f, 0);
        capsule.transform.localScale    = new Vector3(0.6f, 0.85f, 0.6f);
        var capMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        capMat.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.10f));
        capMat.SetFloat("_Metallic",  0.3f);
        capMat.SetFloat("_Smoothness",0.2f);
        capsule.GetComponent<MeshRenderer>().sharedMaterial = capMat;
        Object.DestroyImmediate(capsule.GetComponent<Collider>());
        // Enemy eyes — two dim red lights
        AddEyeLight(enemyGO.transform, new Vector3( 0.08f, 1.55f, 0.3f));
        AddEyeLight(enemyGO.transform, new Vector3(-0.08f, 1.55f, 0.3f));

        var soEnemy = new SerializedObject(enemy);
        soEnemy.FindProperty("maze").objectReferenceValue = mazeGen;
        soEnemy.ApplyModifiedProperties();

        // Main Camera + FollowCamera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        var fcType = System.Type.GetType("FollowCamera, Assembly-CSharp");
        if (fcType != null) camGO.AddComponent(fcType);

        // Post-processing volume
        var volGO  = new GameObject("Global Volume");
        volGO.AddComponent<UnityEngine.Rendering.Volume>();
        var atType = System.Type.GetType("AtmosphereSetup, Assembly-CSharp");
        if (atType != null) volGO.AddComponent(atType);

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();
        Debug.Log("[SewerSceneBuilder] Done → " + SCENE_PATH);
    }

    // ── Material helpers ──────────────────────────────────────────────────────

    static Material MakeMat(string name, string texPath, Color tint, float smooth)
    {
        var path = MAT_DIR + name + ".mat";
        AssetDatabase.DeleteAsset(path);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Metallic",   0f);
        mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex != null) mat.SetTexture("_BaseMap", tex);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material MakeWaterMat()
    {
        var path = MAT_DIR + "SewerWater.mat";
        AssetDatabase.DeleteAsset(path);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor",  new Color(0.15f, 0.28f, 0.20f));
        mat.SetFloat("_Smoothness", 0.88f);
        mat.SetFloat("_Metallic",   0f);
        mat.SetTextureScale("_BaseMap", new Vector2(5f, 5f));

        var tex  = AssetDatabase.LoadAssetAtPath<Texture2D>(WATER_BASE);
        var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(WATER_NORM);
        if (tex  != null) mat.SetTexture("_BaseMap", tex);
        if (norm != null) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 0.4f); }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material MakeGateMat()
    {
        var path = MAT_DIR + "SewerGate.mat";
        AssetDatabase.DeleteAsset(path);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor",  new Color(0.12f, 0.13f, 0.15f));
        mat.SetFloat("_Metallic",   0.85f);
        mat.SetFloat("_Smoothness", 0.25f);

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sewer/Textures/Fence/IronFenceAlbedo.png");
        if (albedo != null) mat.SetTexture("_BaseMap", albedo);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void AddEyeLight(Transform parent, Vector3 localPos)
    {
        var go = new GameObject("Eye");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(0.9f, 0.1f, 0.05f);
        l.intensity = 1.5f;
        l.range     = 3f;
        l.shadows   = LightShadows.None;
    }
}
