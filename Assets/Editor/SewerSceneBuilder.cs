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
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Атмосфера — тёмно, туман
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.015f;
        RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.08f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.10f, 0.12f);

        // Directional Light — слабый, холодный
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.15f;
        light.color = new Color(0.4f, 0.5f, 0.6f);
        light.shadows = LightShadows.None;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Сама модель канализации
        var sewerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sewer/Models/Sewers.fbx");
        if (sewerPrefab != null)
        {
            var sewer = (GameObject)PrefabUtility.InstantiatePrefab(sewerPrefab);
            sewer.name = "SewerLevel";
            sewer.transform.position = Vector3.zero;
        }

        // Вода
        var waterMat = BuildWaterMaterial();
        var waterGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterGO.name = "SewerWater";
        waterGO.transform.position = new Vector3(0f, -0.3f, 0f);
        waterGO.transform.localScale = new Vector3(20f, 1f, 20f);
        waterGO.GetComponent<Renderer>().sharedMaterial = waterMat;
        Object.DestroyImmediate(waterGO.GetComponent<Collider>());
        waterGO.AddComponent<SewerWater>();

        // Спавнер игрока — без MazeManager
        var spawnerGO = new GameObject("SewerSpawner");
        var sewerSpawner = spawnerGO.AddComponent<SewerPlayerSpawner>();
        spawnerGO.AddComponent<PlayerTorch>();

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/StarterAssets/ThirdPersonController/Prefabs/Player_Arissa.prefab");
        if (playerPrefab != null)
        {
            var soSpawner = new SerializedObject(sewerSpawner);
            soSpawner.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            soSpawner.ApplyModifiedProperties();
        }

        // Камера
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        AddComponentByName(camGO, "FollowCamera");

        // Post-processing
        var volGO = new GameObject("Global Volume");
        volGO.AddComponent<UnityEngine.Rendering.Volume>();
        AddComponentByName(volGO, "AtmosphereSetup");

        const string scenePath = "Assets/Scenes/SewerScene.unity";
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();
        Debug.Log("[SewerSceneBuilder] SewerScene created. Open Assets/Scenes/SewerScene.unity and press Play.");
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
