using UnityEngine;

/// <summary>Showcase scene: three hero cars parked side by side on a clean
/// Tokyo street at sunset — Ferrarri LaFerrarri (27), Bpcati Chiron (33),
/// MSAIN R34 GT (38). Uses the real PlayerCar prefab + CarVisuals, so the
/// cars are exactly the in-game models (real Kenney models now, painted
/// from the roster colours). No driving, no HUD: each car is frozen right after
/// spawn (CarController disabled before its Start runs, Rigidbody
/// kinematic, WheelColliders off). The scene builds its own camera,
/// sunset light, street and skyline at runtime, so the .unity file stays
/// a one-object stub. Screenshot target: 18-showcase-trio.png, 1280x720.</summary>
public class ShowcaseTrio : MonoBehaviour
{
    public GameObject carPrefab;

    /// <summary>Reference to a URP Lit material (ShowcaseLit.mat). The
    /// scene references it so the URP Lit shader is guaranteed included
    /// in the build — MeshFactory builds all runtime materials with
    /// Shader.Find("Universal Render Pipeline/Lit"), which returns null
    /// if the shader was stripped from the build.</summary>
    public Material litMaterial;

    // Showcase indices in the 55-car roster (re-indexed 2026-11-07:
    // LaFerrarri was 31, Chiron 37, R34 42 before the Chinese cars
    // and the Escalade were removed).
    const int LaFerrarri = 27;
    const int Chiron = 33;
    const int R34 = 38;

    // PlayerCar root sits 0.6 m above the ground in Main.unity.
    const float CarY = 0.6f;

    void Start()
    {
        // Diagnostics: the first build failed with ArgumentNullException
        // ("shader") and a black screen, so log each step — and make sure
        // the camera is created even if car building fails.
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Debug.Log("[ShowcaseTrio] Start begin. URP Lit shader null? " + (lit == null)
            + " litMaterial null? " + (litMaterial == null));
        try
        {
            SetupSkyAndLight();
            Debug.Log("[ShowcaseTrio] sky+light done");
            BuildStreet();
            Debug.Log("[ShowcaseTrio] street done");
            // Cars face +z (front bumper at +z); rotated -20° for a 3/4 view.
            // Tight 3.4 m spacing: the 4.7 m row was too wide for the frame.
            SpawnCar(LaFerrarri, new Vector3(-3.4f, CarY, 0f));
            Debug.Log("[ShowcaseTrio] car 1 (LaFerrarri) done");
            SpawnCar(Chiron, new Vector3(0f, CarY, 0f));
            Debug.Log("[ShowcaseTrio] car 2 (Chiron) done");
            SpawnCar(R34, new Vector3(3.4f, CarY, 0f));
            Debug.Log("[ShowcaseTrio] car 3 (R34) done");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[ShowcaseTrio] FAILED: " + ex.GetType().Name + ": " + ex.Message
                + "\n" + ex.StackTrace);
        }
        SetupCamera();
        Debug.Log("[ShowcaseTrio] scene ready: LaFerrarri(27) Chiron(33) R34(38)");
    }

    void SpawnCar(int index, Vector3 pos)
    {
        CarSelection.SelectedIndex = index;
        // -20°: front 3/4 view — the -35° side view made the cars read as
        // flat wedges. -20° shows nose, grille and long side together.
        GameObject car = Instantiate(carPrefab, pos, Quaternion.Euler(0f, -20f, 0f));
        var body = car.transform.Find("Body");
        Color paint = Color.magenta;
        if (body != null)
        {
            var br = body.GetComponent<Renderer>();
            if (br != null) paint = br.material.color;
        }
        Debug.Log($"[ShowcaseTrio] spawned index {index} at {pos} paint=({paint.r:F2},{paint.g:F2},{paint.b:F2})");
        var cc = car.GetComponent<CarController>();
        if (cc != null) cc.enabled = false; // Start never runs while disabled
        var rb = car.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        foreach (var wc in car.GetComponentsInChildren<WheelCollider>())
            wc.enabled = false;
        // Headlight beams are spotlights added by CarVisuals; keep them off
        // so they do not wash the sunset shot.
        foreach (var l in car.GetComponentsInChildren<Light>())
            l.enabled = false;
    }

    void SetupSkyAndLight()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.95f, 0.55f, 0.32f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.32f, 0.26f);
        RenderSettings.ambientGroundColor = new Color(0.14f, 0.11f, 0.12f);
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.006f;
        RenderSettings.fogColor = new Color(0.88f, 0.52f, 0.32f);

        // Low warm sun. The sun DISC stays on the horizon behind the cars
        // (sunset backdrop), but the key light comes from the front-left
        // so the cars' camera-facing sides are lit, not rim-lit only:
        // backlit paint read as dark blobs in the first captures.
        var sun = new GameObject("SunsetSun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.55f, 0.28f);
        sun.intensity = 1.6f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(25f, 155f, 0f);

        // Cool fill from the camera side so the paint reads (the sun is
        // behind the cars, so their camera-facing sides are rim-lit only).
        var fill = new GameObject("FillLight").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.65f, 0.75f, 1f);
        fill.intensity = 1.1f;
        fill.transform.rotation = Quaternion.Euler(30f, 195f, 0f);

        // Warm bounce from the front so the paint reads in its true colour.
        var bounce = new GameObject("BounceLight").AddComponent<Light>();
        bounce.type = LightType.Directional;
        bounce.color = new Color(1f, 0.85f, 0.7f);
        bounce.intensity = 0.6f;
        bounce.transform.rotation = Quaternion.Euler(22f, 180f, 0f);

        // Sun disc low on the horizon behind the cars. Uses
        // MeshFactory.Emissive (URP Lit + emission): Shader.Find for the
        // URP Unlit shader returns null in a build (stripped — nothing
        // references it), and new Material(null) aborts Start.
        var disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        disc.name = "SunDisc";
        disc.transform.position = new Vector3(-16f, 5f, -32f);
        disc.transform.localScale = Vector3.one * 7f;
        disc.GetComponent<Renderer>().material =
            MeshFactory.Emissive("ShowcaseSun", new Color(1f, 0.62f, 0.25f), 2.5f);
        Destroy(disc.GetComponent<Collider>());
    }

    void SetupCamera()
    {
        var camGO = new GameObject("ShowcaseCamera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.93f, 0.5f, 0.3f);
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 300f;
        // "-rear" on the command line: rear 3/4 view for the second
        // showcase photo (cars face +Z, so the rear is on the -Z side).
        bool rear = false;
        foreach (string a in System.Environment.GetCommandLineArgs())
            if (a == "-rear") rear = true;
        if (rear)
        {
            camGO.transform.position = new Vector3(2.5f, 2.2f, -9.0f);
            camGO.transform.LookAt(new Vector3(0f, 0.7f, 0f));
        }
        else
        {
            camGO.transform.position = new Vector3(-2.5f, 2.2f, 9.0f);
            camGO.transform.LookAt(new Vector3(0f, 0.7f, 0f));
        }
        cam.fieldOfView = 50f;
    }

    void BuildStreet()
    {
        var street = new GameObject("Street");

        // Ground + road (dark asphalt).
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(street.transform, false);
        ground.transform.localScale = new Vector3(8f, 1f, 8f);
        ground.GetComponent<Renderer>().material =
            MeshFactory.Lit("ShowcaseGround", new Color(0.10f, 0.10f, 0.11f), 0f, 0.4f);

        var road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        road.name = "Road";
        road.transform.SetParent(street.transform, false);
        road.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        road.transform.localScale = new Vector3(4f, 1f, 0.9f);
        road.GetComponent<Renderer>().material =
            MeshFactory.Lit("ShowcaseRoad", new Color(0.16f, 0.16f, 0.17f), 0f, 0.5f);

        // Edge lines along the road.
        Material lineMat = MeshFactory.Lit("ShowcaseLine", new Color(0.85f, 0.85f, 0.8f), 0f, 0.5f);
        foreach (float z in new float[] { -4.2f, 4.2f })
            MeshFactory.Part("EdgeLine", "Cube.fbx", street.transform,
                new Vector3(0f, 0.04f, z), new Vector3(40f, 0.01f, 0.15f), lineMat);

        // Sidewalks.
        Material walkMat = MeshFactory.Lit("ShowcaseWalk", new Color(0.45f, 0.45f, 0.47f), 0f, 0.6f);
        foreach (float z in new float[] { -5.6f, 5.6f })
            MeshFactory.Part("Sidewalk", "Cube.fbx", street.transform,
                new Vector3(0f, 0.075f, z), new Vector3(40f, 0.15f, 2.5f), walkMat);

        // Distant skyline: real Kenney skyscraper models (CityCommercial)
        // fitted to the tower footprints, with lit window strips on the
        // front face (z = -12). Falls back to the box silhouette if a
        // model is missing.
        Material towerMat = MeshFactory.Lit("ShowcaseTower", new Color(0.12f, 0.13f, 0.18f), 0.2f, 0.4f);
        Material windowMat = MeshFactory.Emissive("ShowcaseWindow", new Color(1f, 0.75f, 0.35f), 1.8f);
        Material towerAtlas = KenneyModels.AtlasMaterial("CityCommercial", 0.05f, 0.3f);
        float[] towerX = { -18f, -9f, 0f, 9f, 18f };
        float[] towerH = { 12f, 18f, 9f, 15f, 11f };
        float[] towerW = { 7f, 6f, 8f, 6f, 7f };
        string[] towerModels = { "building-skyscraper-a", "building-skyscraper-b",
            "building-skyscraper-c", "building-skyscraper-d", "building-skyscraper-e" };
        for (int i = 0; i < towerX.Length; i++)
        {
            float x = towerX[i], h = towerH[i], w = towerW[i];
            GameObject prefab = KenneyModels.LoadPrefab("Models/Kenney/CityCommercial/" + towerModels[i]);
            if (prefab != null)
            {
                GameObject tower = Instantiate(prefab, street.transform, false);
                tower.name = "Tower_" + towerModels[i];
                Bounds nb = KenneyModels.Measure(tower);
                if (nb.size.x > 0.01f && nb.size.y > 0.01f && nb.size.z > 0.01f)
                {
                    tower.transform.localScale = new Vector3(
                        w / nb.size.x, h / nb.size.y, 6f / nb.size.z);
                    tower.transform.localPosition = new Vector3(x, 0f, -15f);
                    KenneyModels.ApplyMaterial(tower, towerAtlas);
                }
            }
            else
            {
                MeshFactory.Part("Tower", "Cube.fbx", street.transform,
                    new Vector3(x, h / 2f, -15f), new Vector3(w, h, 6f), towerMat);
            }
            // Two lit window strips on the front face (z = -12).
            for (int r = 0; r < 2; r++)
                MeshFactory.Part("TowerWindows", "Cube.fbx", street.transform,
                    new Vector3(x, h * (0.35f + 0.3f * r), -11.95f),
                    new Vector3(w * 0.7f, 0.5f, 0.05f), windowMat);
        }

        // Neon signs on two towers (Tokyo at dusk).
        MeshFactory.Part("NeonPink", "Cube.fbx", street.transform,
            new Vector3(9f, 8f, -11.9f), new Vector3(3.2f, 0.5f, 0.08f),
            MeshFactory.Emissive("ShowcaseNeonPink", new Color(1f, 0.2f, 0.6f), 2.2f));
        MeshFactory.Part("NeonCyan", "Cube.fbx", street.transform,
            new Vector3(-9f, 6f, -11.9f), new Vector3(2.6f, 0.5f, 0.08f),
            MeshFactory.Emissive("ShowcaseNeonCyan", new Color(0.1f, 0.85f, 1f), 2.2f));

        // Streetlights flanking the road: real Kenney light-curved
        // models (CityRoads) scaled to 5 m, with a warm emissive lamp
        // head so they read at dusk. Falls back to pole + head boxes.
        Material poleMat = MeshFactory.Lit("ShowcasePole", new Color(0.08f, 0.08f, 0.09f), 0.6f, 0.5f);
        Material lampMat = MeshFactory.Emissive("ShowcaseLamp", new Color(1f, 0.85f, 0.55f), 2.5f);
        foreach (float x in new float[] { -10f, 10f })
        {
            GameObject lampPrefab = KenneyModels.LoadPrefab("Models/Kenney/CityRoads/light-curved");
            if (lampPrefab != null)
            {
                GameObject lamp = Instantiate(lampPrefab, street.transform, false);
                lamp.name = "Streetlight";
                lamp.transform.localPosition = new Vector3(x, 0f, 4.8f);
                KenneyModels.ScaleToHeight(lamp, 5f);
                KenneyModels.GroundY(lamp, 0f);
                KenneyModels.ConvertColorMaterials(lamp, 0.6f, 0.5f);
                MeshFactory.Part("StreetlightHead", "Cube.fbx", street.transform,
                    new Vector3(x, 5.05f, 4.8f), new Vector3(0.5f, 0.15f, 0.3f), lampMat);
            }
            else
            {
                MeshFactory.Part("StreetlightPole", "Cylinder.fbx", street.transform,
                    new Vector3(x, 2.5f, 4.8f), new Vector3(0.12f, 5f, 0.12f), poleMat);
                MeshFactory.Part("StreetlightHead", "Cube.fbx", street.transform,
                    new Vector3(x, 5.05f, 4.8f), new Vector3(0.5f, 0.15f, 0.3f), lampMat);
            }
        }
    }
}
