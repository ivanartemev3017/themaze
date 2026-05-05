using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SewerSceneBuilder
{
    const string SEWER_MODEL = "Assets/Sewer/Models/Sewers.fbx";
    const string WATER_BASE  = "Assets/Sewer/Textures/Water/Swamp_Water_tgmjffbqx_1K_BaseColor.jpg";
    const string WATER_NORM  = "Assets/Sewer/Textures/Water/Swamp_Water_tgmjffbqx_1K_Normal.jpg";
    const string SCENE_PATH  = "Assets/Scenes/SewerTest.unity";
    const string MAT_PATH    = "Assets/Sewer/Materials/SewerWater.mat";

    [MenuItem("Sewer/Build Test Scene")]
    static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        SetupEnvironment();
        PlaceSewerModel();
        var waterMat = BuildWaterMaterial();
        PlaceWater(waterMat);
        SetupCamera();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.Refresh();
        Debug.Log("[SewerSceneBuilder] Saved: " + SCENE_PATH);
    }

    static void SetupEnvironment()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.05f;
        RenderSettings.fogColor = new Color(0.08f, 0.10f, 0.12f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.22f);

        var go = new GameObject("Directional Light");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.5f;
        light.color = new Color(0.5f, 0.55f, 0.65f);
        light.shadows = LightShadows.None;
        go.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
    }

    static void PlaceSewerModel()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SEWER_MODEL);
        if (prefab == null)
        {
            Debug.LogWarning("[SewerSceneBuilder] Model not found: " + SEWER_MODEL);
            return;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "SewerLevel";
        go.transform.position = Vector3.zero;
        go.transform.localScale = Vector3.one;
    }

    static Material BuildWaterMaterial()
    {
        System.IO.Directory.CreateDirectory("Assets/Sewer/Materials");

        var existing = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "SewerWater";
        mat.SetColor("_BaseColor", new Color(0.22f, 0.35f, 0.28f, 1f));
        mat.SetFloat("_Smoothness", 0.85f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetTextureScale("_BaseMap", new Vector2(4f, 4f));

        Assign(mat, "_BaseMap", WATER_BASE);

        var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(WATER_NORM);
        if (norm != null)
        {
            mat.SetTexture("_BumpMap", norm);
            mat.EnableKeyword("_NORMALMAP");
        }

        AssetDatabase.CreateAsset(mat, MAT_PATH);
        return mat;
    }

    static void PlaceWater(Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "SewerWater";
        go.transform.position = new Vector3(0f, -0.1f, 0f);
        go.transform.localScale = new Vector3(20f, 1f, 20f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.AddComponent<SewerWater>();
    }

    static void SetupCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        go.transform.position = new Vector3(0f, 5f, -10f);
        go.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
    }

    // ── Sewer Theme on SampleScene ──────────────────────────────────────────

    [MenuItem("Sewer/Create Sewer Scene")]
    static void CreateSewerScene()
    {
        // Материалы — тёмный бетон без normal map
        var wallMat    = CreateSewerMat("SewerWall",    "Assets/Sewer/Textures/Sewer/concrete_dirty.jpg",  new Color(0.35f, 0.38f, 0.38f), 0.04f);
        var floorMat   = CreateSewerMat("SewerFloor",   "Assets/Sewer/Textures/Sewer/brick_pavement.jpg",  new Color(0.30f, 0.32f, 0.32f), 0.06f);
        var ceilingMat = CreateSewerMat("SewerCeiling", "Assets/Sewer/Textures/Sewer/concrete_base_02.jpg",new Color(0.25f, 0.27f, 0.28f), 0.03f);
        AssetDatabase.SaveAssets();

        // Новая пустая сцена
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Освещение — тёмное, холодное
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.09f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.12f, 0.14f);

        // Directional Light слабый
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.15f;
        light.color = new Color(0.4f, 0.5f, 0.6f);
        light.shadows = LightShadows.None;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Копируем нужные объекты из SampleScene через prefab-подход:
        // MazeManager GO
        var mazeManagerGO = new GameObject("MazeManager");
        var mazeGen     = (MazeGenerator)AddComponentByName(mazeManagerGO, "MazeGenerator");
        var mazeMgr     = (MazeManager)  AddComponentByName(mazeManagerGO, "MazeManager");
        AddComponentByName(mazeManagerGO, "AtmosphereSetup");
        AddComponentByName(mazeManagerGO, "GameManager");

        // Назначаем MazeGenerator → MazeManager
        if (mazeGen != null && mazeMgr != null)
        {
            var soMaze = new SerializedObject(mazeMgr);
            soMaze.FindProperty("mazeGenerator").objectReferenceValue = mazeGen;
            soMaze.ApplyModifiedProperties();
        }

        // PlayerSpawner GO
        var spawnerGO = new GameObject("PlayerSpawner");
        var spawner = (PlayerSpawner)AddComponentByName(spawnerGO, "PlayerSpawner");
        AddComponentByName(spawnerGO, "PlayerTorch");
        AddComponentByName(spawnerGO, "MinimapSystem");

        // Назначаем Player prefab → PlayerSpawner
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/StarterAssets/ThirdPersonController/Prefabs/Player_Arissa.prefab");
        if (spawner != null && playerPrefab != null)
        {
            var soSpawner = new SerializedObject(spawner);
            soSpawner.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            soSpawner.ApplyModifiedProperties();
        }

        // SewerTheme — только на этой сцене
        var theme = spawnerGO.AddComponent<SewerTheme>();
        var soTheme = new SerializedObject(theme);
        soTheme.FindProperty("wallMaterial").objectReferenceValue    = wallMat;
        soTheme.FindProperty("floorMaterial").objectReferenceValue   = floorMat;
        soTheme.FindProperty("ceilingMaterial").objectReferenceValue = ceilingMat;
        soTheme.ApplyModifiedProperties();

        // Main Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        AddComponentByName(camGO, "FollowCamera");

        // Global Volume
        new GameObject("Global Volume").AddComponent<UnityEngine.Rendering.Volume>();

        const string scenePath = "Assets/Scenes/SewerScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
        Debug.Log("[SewerSceneBuilder] SewerScene saved. Open it and press Play to test.");
    }

    static Component AddComponentByName(GameObject go, string typeName)
    {
        var type = System.Type.GetType(typeName + ", Assembly-CSharp");
        if (type != null) return go.AddComponent(type);
        Debug.LogWarning("[SewerSceneBuilder] Component not found: " + typeName);
        return null;
    }

    static Material CreateSewerMat(string name, string basePath, Color tint, float smoothness)
    {
        System.IO.Directory.CreateDirectory("Assets/Sewer/Materials");
        var path = "Assets/Sewer/Materials/" + name + ".mat";

        // Пересоздать если уже есть (обновить)
        AssetDatabase.DeleteAsset(path);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        mat.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
        // Без normal map — убирает эффект подушки
        Assign(mat, "_BaseMap", basePath);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void Assign(Material mat, string prop, string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) mat.SetTexture(prop, tex);
    }
}
