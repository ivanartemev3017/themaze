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

    [MenuItem("Sewer/Apply Theme to SampleScene")]
    static void ApplyTheme()
    {
        var wallMat    = CreateSewerMat("SewerWall",    "Assets/Sewer/Textures/Sewer/bricks.jpg",          "Assets/Sewer/Textures/Sewer/brick_modern.jpg",    0.05f);
        var floorMat   = CreateSewerMat("SewerFloor",   "Assets/Sewer/Textures/Sewer/concrete_dirty.jpg",  "Assets/Sewer/Textures/Sewer/concrete_base.png",   0.08f);
        var ceilingMat = CreateSewerMat("SewerCeiling", "Assets/Sewer/Textures/Sewer/concrete_base_02.jpg","Assets/Sewer/Textures/Sewer/concrete_base_03.jpg", 0.03f);
        AssetDatabase.SaveAssets();

        var spawner = Object.FindFirstObjectByType<PlayerSpawner>();
        if (spawner == null) { Debug.LogWarning("[SewerSceneBuilder] PlayerSpawner not found — open SampleScene first."); return; }

        var theme = spawner.gameObject.GetComponent<SewerTheme>() ?? spawner.gameObject.AddComponent<SewerTheme>();

        var so = new SerializedObject(theme);
        so.FindProperty("wallMaterial").objectReferenceValue    = wallMat;
        so.FindProperty("floorMaterial").objectReferenceValue   = floorMat;
        so.FindProperty("ceilingMaterial").objectReferenceValue = ceilingMat;
        so.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[SewerSceneBuilder] SewerTheme applied to PlayerSpawner. Save scene and press Play.");
    }

    static Material CreateSewerMat(string name, string basePath, string normPath, float smoothness)
    {
        System.IO.Directory.CreateDirectory("Assets/Sewer/Materials");
        var path = "Assets/Sewer/Materials/" + name + ".mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
        Assign(mat, "_BaseMap", basePath);

        var norm = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
        if (norm != null) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void Assign(Material mat, string prop, string path)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) mat.SetTexture(prop, tex);
    }
}
