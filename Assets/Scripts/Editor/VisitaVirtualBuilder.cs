using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// GINEBRO GAMES - Constructor automatic del projecte.
/// Menu: GinebroGames > Rebuild All Scenes.
/// La primera vegada que s'obre el projecte ho executa sol:
///   1. Executa Blender (Blender/CreateHouse.py) per generar Assets/Models/House.fbx
///   2. Crea materials, render texture i perfil de postprocessat
///   3. Crea les escenes Splash, MainMenu, Credits i VisitaVirtual i les afegeix al Build.
/// </summary>
[InitializeOnLoad]
public static class VisitaVirtualBuilder
{
    private const string ScenesFolder = "Assets/Scenes";
    private const string HouseFbxPath = "Assets/Models/House.fbx";
    private const string HouseFallbackPath = "Assets/Models/Fallback/HouseFallback.obj";
    private const string CreatorName = "Òscar Cortés";

    private static Font uiFont;
    private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

    static VisitaVirtualBuilder()
    {
        EditorApplication.delayCall += AutoBuildIfNeeded;
    }

    private static void AutoBuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(ScenesFolder + "/VisitaVirtual.unity")) return;
        if (SessionState.GetBool("GinebroAutoBuildDone", false)) return;
        SessionState.SetBool("GinebroAutoBuildDone", true);
        BuildAll();
    }

    [MenuItem("GinebroGames/Rebuild All Scenes")]
    public static void BuildAll()
    {
        try
        {
            EditorUtility.DisplayProgressBar("GINEBRO GAMES", "Preparant assets...", 0.05f);
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ConfigureImporters();
            EditorUtility.DisplayProgressBar("GINEBRO GAMES", "Generant la casa amb Blender...", 0.15f);
            EnsureHouseModel(false);
            EditorUtility.DisplayProgressBar("GINEBRO GAMES", "Creant materials...", 0.35f);
            CreateMaterials();
            ConfigurePlayerSettings();
            Directory.CreateDirectory(ScenesFolder);

            EditorUtility.DisplayProgressBar("GINEBRO GAMES", "Escena VisitaVirtual...", 0.75f);
            BuildVisitScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenesFolder + "/VisitaVirtual.unity", true),
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenesFolder + "/VisitaVirtual.unity");
            Debug.Log("<b>GINEBRO GAMES</b>: projecte construit. Prem Play.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("GINEBRO GAMES builder: " + e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("GinebroGames/Regenerate House With Blender")]
    public static void RegenerateHouse()
    {
        EnsureHouseModel(true);
    }

    // =====================================================================  IMPORTERS / SETTINGS
    private static void ConfigureImporters()
    {
        foreach (string path in new[] { "Assets/Sprites/GinebroGamesLogo.png", "Assets/Sprites/MenuBackground.png", "Assets/Sprites/ButtonRounded.png", "Assets/Sprites/Crosshair.png" })
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            if (path.EndsWith("ButtonRounded.png")) ti.spriteBorder = new Vector4(20, 20, 20, 20);
            ti.SaveAndReimport();
        }
        foreach (string path in new[] { "Assets/Audio/Ambient/AmbientRoomTone.wav", "Assets/Audio/Music/MenuMusic.wav" })
        {
            var ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) continue;
            var s = ai.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "GINEBRO GAMES";
        PlayerSettings.productName = "Visita Virtual 3D";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
    }

    // =====================================================================  BLENDER
    private static void EnsureHouseModel(bool force)
    {
        if (!force && File.Exists(HouseFbxPath)) return;
        string blender = FindBlender();
        if (blender == null)
        {
            Debug.LogWarning("GINEBRO GAMES: no s'ha trobat Blender. S'usara el model de reserva " + HouseFallbackPath);
            return;
        }
        string root = Directory.GetParent(Application.dataPath).FullName;
        string script = Path.Combine(root, "Blender", "CreateHouse.py");
        string fbx = Path.Combine(root, "Assets", "Models", "House.fbx");
        string blend = Path.Combine(root, "Blender", "House.blend");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = blender,
            Arguments = $"-b --factory-startup -P \"{script}\" -- \"{fbx}\" \"{blend}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        try
        {
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(180000);
                File.WriteAllText(Path.Combine(root, "Blender", "BlenderLog.txt"), output);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("GINEBRO GAMES: error executant Blender: " + e.Message);
        }
        AssetDatabase.Refresh();
        if (File.Exists(HouseFbxPath))
        {
            var mi = AssetImporter.GetAtPath(HouseFbxPath) as ModelImporter;
            if (mi != null)
            {
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.bakeAxisConversion = true;
                mi.SaveAndReimport();
            }
            Debug.Log("GINEBRO GAMES: casa generada amb Blender -> " + HouseFbxPath);
        }
        else Debug.LogWarning("GINEBRO GAMES: Blender no ha generat l'FBX (mira Blender/BlenderLog.txt). S'usa el model de reserva.");
    }

    private static string FindBlender()
    {
        var candidates = new List<string>();
        foreach (string baseDir in new[] { @"C:\Program Files\Blender Foundation", @"C:\Program Files (x86)\Blender Foundation" })
        {
            if (!Directory.Exists(baseDir)) continue;
            foreach (string dir in Directory.GetDirectories(baseDir))
            {
                string exe = Path.Combine(dir, "blender.exe");
                if (File.Exists(exe)) candidates.Add(exe);
            }
        }
        return candidates.OrderByDescending(c => c).FirstOrDefault();
    }

    // =====================================================================  MATERIALS
    private static void CreateMaterials()
    {
        Mats.Clear();
        Directory.CreateDirectory("Assets/Materials");
        Lit("WallPlaster", "BackroomsWallpaperAlbedo", Color.white, 0.6f, 0.08f);
        Lit("FloorWood", "CarpetAlbedo", Color.white, 0.5f, 0.02f);
        Lit("FloorTile", "CarpetAlbedo", Color.white, 0.5f, 0.02f);
        Lit("FloorHallway", "CarpetAlbedo", Color.white, 0.5f, 0.02f);
        Lit("Ceiling", "CeilingTileAlbedo", Color.white, 0.6f, 0.05f);
        Lit("WoodFurniture", "WoodFurnitureAlbedo", Color.white, 1f, 0.4f);
        Lit("FabricSofa", "FabricSofaAlbedo", Color.white, 2f, 0.05f);
        Lit("FabricRug", "FabricRugAlbedo", Color.white, 2f, 0.0f);
        Lit("FabricBed", "FabricBedAlbedo", Color.white, 2f, 0.05f);
        Lit("PlasticBlack", null, new Color(0.04f, 0.04f, 0.045f), 1f, 0.7f);
        Lit("PlasticWhite", null, new Color(0.92f, 0.92f, 0.9f), 1f, 0.5f);
        Lit("MetalSteel", null, new Color(0.7f, 0.72f, 0.75f), 1f, 0.75f, 0.9f);
        Lit("CounterStone", null, new Color(0.2f, 0.2f, 0.22f), 1f, 0.8f);
        Lit("CabinetWhite", null, new Color(0.85f, 0.86f, 0.84f), 1f, 0.55f);
        Lit("PlantGreen", null, new Color(0.18f, 0.45f, 0.22f), 1f, 0.2f);
        Lit("PotTerracotta", null, new Color(0.62f, 0.32f, 0.2f), 1f, 0.2f);
        Lit("BookRed", null, new Color(0.55f, 0.12f, 0.1f), 1f, 0.3f);
        Lit("BookBlue", null, new Color(0.12f, 0.2f, 0.5f), 1f, 0.3f);
        Lit("BookGreen", null, new Color(0.15f, 0.4f, 0.2f), 1f, 0.3f);
        Lit("DoorWood", "WoodFurnitureAlbedo", new Color(0.95f, 0.9f, 0.85f), 1f, 0.45f);

        Material lamp = Lit("LampEmissive", null, new Color(1f, 0.95f, 0.85f), 1f, 0.5f);
        lamp.EnableKeyword("_EMISSION");
        lamp.SetColor("_EmissionColor", new Color(1f, 1f, 0.92f) * 4f);
        lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        // Pantalla de la TV: encesa (render texture del VideoPlayer) i apagada
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Textures/TvScreenRenderTexture.renderTexture");
        if (rt == null)
        {
            rt = new RenderTexture(1024, 576, 0, RenderTextureFormat.ARGB32) { name = "TvScreenRenderTexture" };
            AssetDatabase.CreateAsset(rt, "Assets/Textures/TvScreenRenderTexture.renderTexture");
        }
        Material on = SaveMat("TvScreenOn", new Material(Shader.Find("Universal Render Pipeline/Unlit")));
        on.SetTexture("_BaseMap", rt);
        on.SetColor("_BaseColor", Color.white);
        Material off = Lit("TvScreenOff", null, new Color(0.01f, 0.012f, 0.015f), 1f, 0.95f);
        EditorUtility.SetDirty(on);
        EditorUtility.SetDirty(off);

        Material logoMat = SaveMat("PaintingLogo", new Material(Shader.Find("Universal Render Pipeline/Lit")));
        logoMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/GinebroGamesLogo.png"));
        logoMat.SetColor("_BaseColor", Color.white);
        Material art = SaveMat("PaintingAbstract", new Material(Shader.Find("Universal Render Pipeline/Lit")));
        art.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/MenuBackground.png"));
        art.SetColor("_BaseColor", new Color(1.6f, 1.3f, 1.1f));
        foreach (var m in Mats.Values) EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
    }

    private static Material Lit(string name, string texture, Color color, float tiling, float smoothness, float metallic = 0f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (texture != null)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/" + texture + ".png");
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        return SaveMat(name, m);
    }

    private static Material SaveMat(string name, Material m)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.shader = m.shader;
            existing.CopyPropertiesFromMaterial(m);
            m = existing;
        }
        else AssetDatabase.CreateAsset(m, path);
        m.name = name;
        Mats[name] = m;
        return m;
    }

    // =====================================================================  HELPERS UI
    private static Scene NewScene(string name)
    {
        return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void Save(Scene s, string name)
    {
        EditorSceneManager.SaveScene(s, ScenesFolder + "/" + name + ".unity");
    }

    private static Camera CreateUiCamera()
    {
        var go = new GameObject("MainCamera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        go.AddComponent<AudioListener>();
        return cam;
    }

    private static Canvas CreateCanvas(string name, int sortOrder = 0)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private static RectTransform Rect(GameObject go, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Rect(go, parent, anchor, pos, size);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = sprite != null && sprite.name != "ButtonRounded";
        return img;
    }

    private static Text CreateText(string name, Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Rect(go, parent, anchor, pos, box);
        var t = go.AddComponent<Text>();
        t.font = uiFont;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.6f);
        sh.effectDistance = new Vector2(2, -2);
        return t;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ButtonRounded.png");
        Image img = CreateImage(name, parent, sprite, Color.white, anchor, pos, size);
        img.type = Image.Type.Sliced;
        var b = img.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.normalColor = new Color(0.16f, 0.36f, 0.26f, 0.92f);
        cb.highlightedColor = new Color(0.24f, 0.55f, 0.38f, 1f);
        cb.pressedColor = new Color(0.1f, 0.25f, 0.18f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        Text t = CreateText("Label", img.transform, label, 34, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(t.rectTransform);
        return b;
    }

    private static AudioSource Create2DAudio(string name, string clipPath, bool loop, float volume, bool playOnAwake)
    {
        var go = new GameObject(name);
        var src = go.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        src.loop = loop;
        src.volume = volume;
        src.playOnAwake = playOnAwake;
        src.spatialBlend = 0f;
        return src;
    }

    // =====================================================================  VISITA VIRTUAL
    private static Transform envRoot, furnitureRoot, lightsRoot, interactRoot, audioRoot;

    private static void BuildVisitScene()
    {
        Scene s = NewScene("VisitaVirtual");
        envRoot = new GameObject("Environment").transform;
        furnitureRoot = new GameObject("Furniture").transform;
        lightsRoot = new GameObject("Lighting").transform;
        interactRoot = new GameObject("Interactables").transform;
        audioRoot = new GameObject("Audio").transform;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.08f);
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        RenderSettings.reflectionIntensity = 0.3f;

        BuildHouse();
        BuildNavMesh();
        BuildPostProcessing();

        // ---------------------------------------------------------- Llums + interruptors
        AudioClip click = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Interaction/LightSwitchClick.wav");
        RoomLight("LivingRoom", "Sala d'estar", new Vector3(-3.5f, 0, -4.2f), new Vector3(-3.5f + 0.95f, 1.3f, -1.31f), Vector3.back, click);
        RoomLight("Kitchen", "Cuina", new Vector3(3.5f, 0, -4.2f), new Vector3(3.5f + 0.95f, 1.3f, -1.31f), Vector3.back, click);
        RoomLight("Bedroom", "Dormitori", new Vector3(-3.5f, 0, 4.2f), new Vector3(-3.5f + 0.95f, 1.3f, 1.31f), Vector3.forward, click);
        RoomLight("Study", "Estudi", new Vector3(3.5f, 0, 4.2f), new Vector3(3.5f + 0.95f, 1.3f, 1.31f), Vector3.forward, click);
        HallwayLights(click);

        // ---------------------------------------------------------- Portes
        AudioClip open = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Interaction/DoorOpen.wav");
        AudioClip close = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Interaction/DoorClose.wav");

        // ---------------------------------------------------------- Mobles + TV
        // Estil "Backrooms" (com a classe): habitacions buides, nomes la sala te mobles (TV i sofa)

        // ---------------------------------------------------------- Audio ambient
        AudioSource amb = Create2DAudio("AmbientRoomTone", "Assets/Audio/Ambient/AmbientRoomTone.wav", true, 0.5f, true);
        amb.transform.SetParent(audioRoot);

        // ---------------------------------------------------------- Jugador + UI
        SpawnPlayer(new Vector3(-5.6f, 1.05f, 0f), 90f);
        BuildHud();
        BuildEnemy(new Vector3(5.6f, 0f, 0f));
        Save(s, "VisitaVirtual");
    }

    private static void BuildHouse()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(HouseFbxPath) ?? AssetDatabase.LoadAssetAtPath<GameObject>(HouseFallbackPath);
        if (model == null) throw new System.Exception("No hi ha model de la casa (" + HouseFbxPath + ")");
        var house = (GameObject)PrefabUtility.InstantiatePrefab(model);
        house.name = "House";
        house.transform.SetParent(envRoot);
        house.transform.position = Vector3.zero;
        house.isStatic = true;

        // Normalitza orientacio / escala / posicio (la planta es simetrica): el sostre ha de quedar a dalt
        NormalizeHouse(house.transform);

        foreach (var r in house.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = r.gameObject.name;
            Material m = Mats["WallPlaster"];
            if (n.StartsWith("Ceiling")) m = Mats["Ceiling"];
            else if (n.StartsWith("FloorHallway")) m = Mats["FloorHallway"];
            else if (n.StartsWith("FloorKitchen") || n.StartsWith("FloorStudy")) m = Mats["FloorTile"];
            else if (n.StartsWith("Floor")) m = Mats["FloorWood"];
            r.sharedMaterials = Enumerable.Repeat(m, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            if (r.GetComponent<MeshCollider>() == null) r.gameObject.AddComponent<MeshCollider>();
            r.gameObject.isStatic = true;
        }
    }

    private static void NormalizeHouse(Transform house)
    {
        // 1) orientacio: la dimensio vertical ha de ser la petita (~3.2 m)
        Quaternion original = house.rotation;
        Quaternion best = original;
        float bestHeight = float.MaxValue;
        foreach (var r in new[] { Quaternion.identity, Quaternion.Euler(-90, 0, 0), Quaternion.Euler(90, 0, 0), Quaternion.Euler(0, 0, 90), Quaternion.Euler(0, 0, -90) })
        {
            house.rotation = r * original;
            float h = GetBounds(house).size.y;
            if (h < bestHeight - 0.001f) { bestHeight = h; best = house.rotation; }
        }
        house.rotation = best;
        // 2) escala: amplada esperada 14.2 m (per si l'FBX ve en cm)
        Bounds b = GetBounds(house);
        float k = 14.2f / Mathf.Max(b.size.x, b.size.z);
        if (Mathf.Abs(k - 1f) > 0.05f) house.localScale *= k;
        // 3) el sostre ha de quedar a dalt
        b = GetBounds(house);
        var ceiling = house.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("Ceiling"));
        if (ceiling != null && ceiling.bounds.center.y < b.center.y)
            house.rotation = Quaternion.Euler(180, 0, 0) * house.rotation;
        // 4) centra a l'origen amb el terra a y = 0
        b = GetBounds(house);
        house.position += new Vector3(-b.center.x, -0.1f - b.min.y, -b.center.z);
    }

    private static Bounds GetBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static void BuildPostProcessing()
    {
        const string path = "Assets/Settings/Volumes/VisitaVirtualVolumeProfile.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = 1.1f;
        bloom.threshold.value = 0.9f;
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.ACES;
        var vig = profile.Add<Vignette>(true);
        vig.intensity.value = 0.3f;
        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.value = 0.25f;
        color.saturation.value = 10f;
        color.colorFilter.value = new Color(0.8f, 1f, 0.62f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var go = new GameObject("GlobalVolume");
        go.transform.SetParent(lightsRoot);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
    }

    // ---------------------------------------------------------------------- Llums
    private static void RoomLight(string id, string label, Vector3 center, Vector3 switchPos, Vector3 facing, AudioClip click)
    {
        var group = new GameObject(id + "Lights").transform;
        group.SetParent(lightsRoot);
        var lamps = new List<Renderer>();
        int n = 0;
        foreach (var o in new[] { new Vector2(-1.6f, -1.4f), new Vector2(1.6f, -1.4f), new Vector2(-1.6f, 1.4f), new Vector2(1.6f, 1.4f) })
            lamps.Add(CeilingLamp(id + "CeilingPanel" + (n++), group, new Vector3(center.x + o.x, 2.99f, center.z + o.y)));
        Light main = PointLight(id + "PointLight", group, new Vector3(center.x, 2.6f, center.z), 9f, 2.8f, new Color(1f, 0.98f, 0.85f), LightShadows.Soft);

    }

    private static void HallwayLights(AudioClip click)
    {
        var group = new GameObject("HallwayLights").transform;
        group.SetParent(lightsRoot);
        var lights = new List<Light>();
        var lamps = new List<Renderer>();
        foreach (float x in new[] { -4.6f, 0f, 4.6f })
        {
            lamps.Add(CeilingLamp("HallwayCeilingLamp" + lights.Count, group, new Vector3(x, 2.92f, 0f)));
            lights.Add(PointLight("HallwayPointLight" + lights.Count, group, new Vector3(x, 2.6f, 0f), 6f, 1.8f, new Color(1f, 0.98f, 0.85f), LightShadows.None));
        }

    }

    private static Renderer CeilingLamp(string name, Transform parent, Vector3 pos)
    {
        // Panell fluorescent quadrat encastat al sostre (estil oficina)
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;
        Object.DestroyImmediate(panel.GetComponent<Collider>());
        panel.transform.SetParent(parent);
        panel.transform.position = new Vector3(pos.x, 2.99f, pos.z);
        panel.transform.localScale = new Vector3(1.2f, 0.02f, 0.6f);
        var r = panel.GetComponent<Renderer>();
        r.sharedMaterial = Mats["LampEmissive"];
        r.shadowCastingMode = ShadowCastingMode.Off;
        return r;
    }

    private static Light PointLight(string name, Transform parent, Vector3 pos, float range, float intensity, Color color, LightShadows shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = color;
        l.shadows = shadows;
        l.shadowStrength = 0.85f;
        return l;
    }

    private static LightSwitch CreateSwitch(string name, Vector3 pos, Vector3 facing)
    {
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = name;
        plate.transform.SetParent(interactRoot);
        plate.transform.position = pos;
        plate.transform.rotation = Quaternion.LookRotation(facing);
        plate.transform.localScale = new Vector3(0.09f, 0.13f, 0.02f);
        plate.GetComponent<Renderer>().sharedMaterial = Mats["PlasticWhite"];
        var toggle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        toggle.name = "Toggle";
        Object.DestroyImmediate(toggle.GetComponent<Collider>());
        toggle.transform.SetParent(plate.transform, false);
        toggle.transform.localPosition = new Vector3(0f, 0f, 0.7f);
        toggle.transform.localScale = new Vector3(0.4f, 0.35f, 1f);
        toggle.GetComponent<Renderer>().sharedMaterial = Mats["CabinetWhite"];
        return plate.AddComponent<LightSwitch>();
    }

    // ---------------------------------------------------------------------- Portes
    private static void Door(string name, string label, float doorX, float wallZ, float angle, AudioClip open, AudioClip close)
    {
        var pivot = new GameObject(name + "Hinge").transform;
        pivot.SetParent(interactRoot);
        pivot.position = new Vector3(doorX - 0.6f, 0f, wallZ);
        var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = name;
        leaf.transform.SetParent(pivot, false);
        leaf.transform.localPosition = new Vector3(0.59f, 1.09f, 0f);
        leaf.transform.localScale = new Vector3(1.16f, 2.16f, 0.05f);
        leaf.GetComponent<Renderer>().sharedMaterial = Mats["DoorWood"];
        foreach (float side in new[] { -1f, 1f })
        {
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            handle.transform.SetParent(pivot, false);
            handle.transform.localPosition = new Vector3(1.02f, 1.0f, 0.05f * side);
            handle.transform.localScale = new Vector3(0.14f, 0.025f, 0.03f);
            handle.GetComponent<Renderer>().sharedMaterial = Mats["MetalSteel"];
        }
        var dc = leaf.AddComponent<DoorController>();
        dc.Configure(pivot, angle, label, open, close);
    }

    // ---------------------------------------------------------------------- Mobles
    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Mats[mat];
        go.isStatic = true;
        return go;
    }

    private static GameObject Cyl(string name, Transform parent, Vector3 center, Vector3 size, string mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Mats[mat];
        go.isStatic = true;
        return go;
    }

    private static Transform Group(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(furnitureRoot);
        return t;
    }

    private static void Plant(Transform parent, Vector3 floorPos, float scale = 1f)
    {
        Cyl("PlantPot", parent, floorPos + Vector3.up * 0.22f * scale, new Vector3(0.4f, 0.22f, 0.4f) * scale, "PotTerracotta");
        var leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = "PlantLeaves";
        Object.DestroyImmediate(leaves.GetComponent<Collider>());
        leaves.transform.SetParent(parent);
        leaves.transform.position = floorPos + Vector3.up * 0.8f * scale;
        leaves.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f) * scale;
        leaves.GetComponent<Renderer>().sharedMaterial = Mats["PlantGreen"];
    }

    private static void Table(Transform parent, string name, Vector3 topCenter, Vector2 size, string mat)
    {
        Box(name + "Top", parent, topCenter, new Vector3(size.x, 0.05f, size.y), mat);
        float h = topCenter.y - 0.025f;
        foreach (var o in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box(name + "Leg", parent, new Vector3(topCenter.x + o.x * (size.x / 2 - 0.06f), h / 2, topCenter.z + o.y * (size.y / 2 - 0.06f)), new Vector3(0.06f, h, 0.06f), mat, false);
    }

    private static void Chair(Transform parent, Vector3 floorPos, float yaw)
    {
        var c = new GameObject("Chair").transform;
        c.SetParent(parent);
        c.position = floorPos;
        c.rotation = Quaternion.Euler(0, yaw, 0);
        Box("ChairSeat", c, Vector3.zero, new Vector3(0.45f, 0.05f, 0.45f), "WoodFurniture").transform.localPosition = new Vector3(0, 0.45f, 0);
        var back = Box("ChairBack", c, Vector3.zero, new Vector3(0.45f, 0.5f, 0.05f), "WoodFurniture");
        back.transform.localPosition = new Vector3(0, 0.72f, -0.2f);
        back.transform.localRotation = Quaternion.identity;
        foreach (var o in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
        {
            var leg = Box("ChairLeg", c, Vector3.zero, new Vector3(0.04f, 0.45f, 0.04f), "WoodFurniture", false);
            leg.transform.localPosition = new Vector3(o.x * 0.19f, 0.225f, o.y * 0.19f);
            leg.transform.localRotation = Quaternion.identity;
        }
        foreach (Transform child in c) { child.localRotation = Quaternion.identity; child.localScale = child.localScale; }
    }

    private static TextMesh Label3D(string name, Transform parent, string text, Vector3 pos, Quaternion rot, float charSize, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.rotation = rot;
        var tm = go.AddComponent<TextMesh>();
        tm.font = uiFont;
        go.GetComponent<MeshRenderer>().sharedMaterial = uiFont.material;
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = charSize;
        tm.anchor = anchor;
        tm.color = color;
        return tm;
    }

    private static void Painting(Transform parent, string name, Vector3 pos, Vector3 facing, Vector2 size, string mat)
    {
        Quaternion rot = Quaternion.LookRotation(-facing);
        var frame = Box(name + "Frame", parent, pos, Vector3.one, "PlasticBlack", false);
        frame.transform.rotation = rot;
        frame.transform.localScale = new Vector3(size.x + 0.08f, size.y + 0.08f, 0.04f);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent);
        quad.transform.position = pos + facing * 0.025f;
        quad.transform.rotation = rot;
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = Mats[mat];
    }

    private static void BuildLivingRoom()
    {
        Transform g = Group("LivingRoomFurniture");
        // Moble TV i televisio (paret oest, mirant +X)
        Box("TvStand", g, new Vector3(-6.55f, 0.25f, -4.2f), new Vector3(0.55f, 0.5f, 2.0f), "WoodFurniture");
        var tv = Box("Television", interactRoot, new Vector3(-6.6f, 1.3f, -4.2f), new Vector3(0.08f, 0.95f, 1.65f), "PlasticBlack");
        tv.isStatic = false;
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "TvScreen";
        Object.DestroyImmediate(screen.GetComponent<Collider>());
        screen.transform.SetParent(interactRoot);
        screen.transform.position = new Vector3(-6.555f, 1.3f, -4.2f);
        screen.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        screen.transform.localScale = new Vector3(1.54f, 0.866f, 1f);
        screen.transform.SetParent(tv.transform, true);
        var screenRenderer = screen.GetComponent<Renderer>();
        screenRenderer.shadowCastingMode = ShadowCastingMode.Off;

        TextMesh label = Label3D("TvChannelLabel", tv.transform, "", new Vector3(-6.545f, 1.66f, -4.9f), Quaternion.Euler(0, -90, 0), 0.012f, new Color(0.5f, 1f, 0.55f), TextAnchor.UpperLeft);
        Light glow = PointLight("TvScreenGlow", tv.transform, new Vector3(-6.0f, 1.3f, -4.2f), 4.5f, 0.9f, new Color(0.65f, 0.75f, 1f), LightShadows.None);

        var vp = tv.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping = true;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Textures/TvScreenRenderTexture.renderTexture");
        var tvAudio = tv.AddComponent<AudioSource>();
        tvAudio.playOnAwake = false;
        tvAudio.spatialBlend = 1f;
        tvAudio.minDistance = 1.5f;
        tvAudio.maxDistance = 14f;
        tvAudio.rolloffMode = AudioRolloffMode.Linear;
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        vp.controlledAudioTrackCount = 1;
        vp.SetTargetAudioSource(0, tvAudio);

        var tvc = tv.AddComponent<TvController>();
        var clips = new[]
        {
            AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Videos/Channel1Documentary.mp4"),
            AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Videos/Channel2Science.mp4"),
            AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Videos/Channel3TestCard.mp4"),
        };
        tvc.Configure(clips, new[] { "Documental", "Ciència", "Carta d'ajust" }, screenRenderer, Mats["TvScreenOn"], Mats["TvScreenOff"], tvAudio, label, glow,
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Interaction/TvPowerOn.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Interaction/TvPowerOff.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Interaction/TvChannelChange.wav"));
        screenRenderer.sharedMaterial = Mats["TvScreenOff"];

        // Panell de botons de la TV (a la paret, a la dreta de la TV)
        var panel = Box("TvButtonPanel", interactRoot, new Vector3(-6.875f, 1.2f, -2.82f), new Vector3(0.03f, 0.5f, 0.44f), "PlasticBlack", false);
        panel.isStatic = false;
        AudioClip press = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Interaction/TvButtonPress.wav");
        var actions = new[] { TvButton.TvButtonAction.Power, TvButton.TvButtonAction.NextChannel, TvButton.TvButtonAction.PreviousChannel };
        var names = new[] { "ON / OFF", "CANAL +", "CANAL -" };
        var colors = new[] { "BookRed", "BookGreen", "BookBlue" };
        for (int i = 0; i < 3; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "TvButton" + actions[i];
            b.transform.SetParent(interactRoot, true);
            b.transform.position = new Vector3(-6.85f, 1.36f - i * 0.16f, -3.0f);
            b.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            b.transform.localScale = new Vector3(0.09f, 0.09f, 0.03f);
            b.GetComponent<Renderer>().sharedMaterial = Mats[colors[i]];
            b.AddComponent<TvButton>().Configure(tvc, actions[i], press);
            Label3D("TvButtonLabel" + i, interactRoot, names[i], new Vector3(-6.84f, 1.36f - i * 0.16f, -2.93f), Quaternion.Euler(0, -90, 0), 0.006f, Color.white, TextAnchor.MiddleLeft);
        }

        // Sofa (mirant la TV)
        Box("SofaBase", g, new Vector3(-2.6f, 0.25f, -4.2f), new Vector3(0.95f, 0.45f, 2.3f), "FabricSofa");
        Box("SofaBack", g, new Vector3(-2.2f, 0.75f, -4.2f), new Vector3(0.22f, 0.6f, 2.3f), "FabricSofa");
        Box("SofaArmLeft", g, new Vector3(-2.6f, 0.42f, -5.3f), new Vector3(0.95f, 0.6f, 0.2f), "FabricSofa");
        Box("SofaArmRight", g, new Vector3(-2.6f, 0.42f, -3.1f), new Vector3(0.95f, 0.6f, 0.2f), "FabricSofa");
        Box("SofaCushionA", g, new Vector3(-2.68f, 0.52f, -4.73f), new Vector3(0.7f, 0.12f, 0.98f), "FabricSofa", false);
        Box("SofaCushionB", g, new Vector3(-2.68f, 0.52f, -3.67f), new Vector3(0.7f, 0.12f, 0.98f), "FabricSofa", false);
        Table(g, "CoffeeTable", new Vector3(-4.3f, 0.42f, -4.2f), new Vector2(0.7f, 1.2f), "WoodFurniture");

        // Rellotge de paret amb tic-tac (so 3D)
        var clock = Cyl("WallClock", g, new Vector3(-3.5f, 2.15f, -7.08f), new Vector3(0.42f, 0.02f, 0.42f), "PlasticWhite", false);
        clock.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var tick = clock.AddComponent<AudioSource>();
        tick.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambient/ClockTick.wav");
        tick.loop = true; tick.playOnAwake = true; tick.spatialBlend = 1f; tick.volume = 0.35f; tick.minDistance = 0.8f; tick.maxDistance = 7f;
        tick.rolloffMode = AudioRolloffMode.Linear;
    }

    private static void BuildKitchen()
    {
        Transform g = Group("KitchenFurniture");
        Box("CounterEast", g, new Vector3(6.6f, 0.45f, -4.9f), new Vector3(0.6f, 0.9f, 4.2f), "CabinetWhite");
        Box("CounterTopEast", g, new Vector3(6.58f, 0.92f, -4.9f), new Vector3(0.66f, 0.04f, 4.22f), "CounterStone");
        Box("UpperCabinets", g, new Vector3(6.7f, 1.95f, -4.9f), new Vector3(0.38f, 0.65f, 4.2f), "CabinetWhite");
        Box("CounterSouth", g, new Vector3(4.6f, 0.45f, -6.8f), new Vector3(3.0f, 0.9f, 0.6f), "CabinetWhite");
        Box("CounterTopSouth", g, new Vector3(4.6f, 0.92f, -6.78f), new Vector3(3.02f, 0.04f, 0.66f), "CounterStone");
        Box("Sink", g, new Vector3(4.2f, 0.93f, -6.78f), new Vector3(0.7f, 0.03f, 0.45f), "MetalSteel", false);
        Box("Hob", g, new Vector3(6.58f, 0.945f, -5.4f), new Vector3(0.5f, 0.01f, 0.6f), "PlasticBlack", false);
        var fridge = Box("Fridge", g, new Vector3(6.55f, 0.95f, -2.0f), new Vector3(0.7f, 1.9f, 0.7f), "MetalSteel");
        var hum = fridge.AddComponent<AudioSource>();
        hum.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambient/FridgeHum.wav");
        hum.loop = true; hum.playOnAwake = true; hum.spatialBlend = 1f; hum.volume = 0.25f; hum.minDistance = 0.8f; hum.maxDistance = 6f;
        hum.rolloffMode = AudioRolloffMode.Linear;
        Table(g, "KitchenTable", new Vector3(2.4f, 0.76f, -4.5f), new Vector2(1.4f, 0.9f), "WoodFurniture");
        Chair(g, new Vector3(2.0f, 0f, -3.75f), 180f);
        Chair(g, new Vector3(2.8f, 0f, -3.75f), 180f);
        Chair(g, new Vector3(2.0f, 0f, -5.25f), 0f);
        Chair(g, new Vector3(2.8f, 0f, -5.25f), 0f);
        Plant(g, new Vector3(0.6f, 0f, -6.6f));
    }

    private static void BuildBedroom()
    {
        Transform g = Group("BedroomFurniture");
        Box("BedFrame", g, new Vector3(-3.5f, 0.22f, 6.0f), new Vector3(1.8f, 0.44f, 2.1f), "WoodFurniture");
        Box("Mattress", g, new Vector3(-3.5f, 0.55f, 6.0f), new Vector3(1.7f, 0.22f, 2.0f), "FabricBed");
        Box("Blanket", g, new Vector3(-3.5f, 0.67f, 5.6f), new Vector3(1.74f, 0.04f, 1.2f), "FabricSofa", false);
        Box("PillowLeft", g, new Vector3(-3.9f, 0.73f, 6.7f), new Vector3(0.6f, 0.14f, 0.38f), "FabricBed", false);
        Box("PillowRight", g, new Vector3(-3.1f, 0.73f, 6.7f), new Vector3(0.6f, 0.14f, 0.38f), "FabricBed", false);
        Box("Headboard", g, new Vector3(-3.5f, 0.85f, 7.03f), new Vector3(1.95f, 1.1f, 0.1f), "WoodFurniture");
        Box("NightstandLeft", g, new Vector3(-4.8f, 0.28f, 6.75f), new Vector3(0.5f, 0.56f, 0.45f), "WoodFurniture");
        Box("NightstandRight", g, new Vector3(-2.2f, 0.28f, 6.75f), new Vector3(0.5f, 0.56f, 0.45f), "WoodFurniture");
        Cyl("BedsideLamp", g, new Vector3(-4.8f, 0.72f, 6.75f), new Vector3(0.22f, 0.16f, 0.22f), "LampEmissive", false);
        Box("Wardrobe", g, new Vector3(-6.6f, 1.05f, 3.4f), new Vector3(0.6f, 2.1f, 1.8f), "WoodFurniture");
        Box("Rug", g, new Vector3(-3.5f, 0.006f, 4.4f), new Vector3(2.6f, 0.012f, 1.6f), "FabricRug", false);
        Painting(g, "BedroomPainting", new Vector3(-0.12f, 1.7f, 4.3f), Vector3.left, new Vector2(1.2f, 0.7f), "PaintingAbstract");
        Plant(g, new Vector3(-0.6f, 0f, 6.6f));
    }

    private static void BuildStudy()
    {
        Transform g = Group("StudyFurniture");
        Table(g, "Desk", new Vector3(4.8f, 0.75f, 6.7f), new Vector2(1.6f, 0.7f), "WoodFurniture");
        Box("Monitor", g, new Vector3(4.8f, 1.05f, 6.85f), new Vector3(0.7f, 0.42f, 0.04f), "PlasticBlack", false);
        Box("MonitorStand", g, new Vector3(4.8f, 0.82f, 6.9f), new Vector3(0.08f, 0.12f, 0.08f), "PlasticBlack", false);
        Box("Keyboard", g, new Vector3(4.8f, 0.785f, 6.55f), new Vector3(0.45f, 0.02f, 0.15f), "PlasticWhite", false);
        Chair(g, new Vector3(4.8f, 0f, 6.0f), 180f);
        Box("Bookcase", g, new Vector3(6.7f, 1.1f, 4.0f), new Vector3(0.35f, 2.2f, 1.6f), "WoodFurniture");
        for (int i = 0; i < 24; i++)
        {
            int shelf = i / 8;
            Box("Book", g, new Vector3(6.6f, 0.5f + shelf * 0.6f, 3.4f + (i % 8) * 0.15f), new Vector3(0.24f, 0.28f + (i % 3) * 0.04f, 0.1f), new[] { "BookRed", "BookBlue", "BookGreen" }[i % 3], false);
        }
        Box("Armchair", g, new Vector3(1.4f, 0.3f, 5.4f), new Vector3(0.9f, 0.6f, 0.9f), "FabricSofa");
        Box("ArmchairBack", g, new Vector3(1.0f, 0.75f, 5.4f), new Vector3(0.18f, 0.7f, 0.9f), "FabricSofa");
        Box("Rug", g, new Vector3(3.5f, 0.006f, 4.2f), new Vector3(2.4f, 0.012f, 1.8f), "FabricRug", false);
        Painting(g, "StudyPainting", new Vector3(3.0f, 1.8f, 7.08f), Vector3.back, new Vector2(1.0f, 1.0f), "PaintingLogo");
        Plant(g, new Vector3(0.6f, 0f, 6.6f));
    }

    private static void BuildHallway()
    {
        Transform g = Group("HallwayFurniture");
        Painting(g, "HallwayLogo", new Vector3(6.88f, 1.65f, 0f), Vector3.left, new Vector2(1.3f, 1.3f), "PaintingLogo");
        Painting(g, "HallwayArtA", new Vector3(0f, 1.7f, 1.08f), Vector3.back, new Vector2(1.6f, 0.8f), "PaintingAbstract");
        Box("HallwayBench", g, new Vector3(0f, 0.22f, -0.85f), new Vector3(1.4f, 0.44f, 0.4f), "WoodFurniture");
        Plant(g, new Vector3(-6.5f, 0f, 0.75f));
        Plant(g, new Vector3(6.5f, 0f, -0.75f));
        Box("HallwayRunner", g, new Vector3(0f, 0.006f, 0f), new Vector3(10f, 0.012f, 1.0f), "FabricRug", false);
    }


    // ---------------------------------------------------------------------- Enemic cutre
    private static void BuildNavMesh()
    {
        var surface = envRoot.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
        surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
        surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        const string path = "Assets/Scenes/VisitaVirtualNavMesh.asset";
        AssetDatabase.DeleteAsset(path);
        if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, path);
    }

    private static void BuildEnemy(Vector3 pos)
    {
        const string texPath = "Assets/Enemy/ScaryFace.png";
        var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null) { ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.4f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", 0f);
        mat = SaveMat("EnemyScaryFace", mat);

        var enemy = new GameObject("Enemy");
        enemy.transform.position = pos;
        var agent = enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.radius = 0.3f;
        agent.height = 2f;
        agent.speed = 2.2f;
        agent.angularSpeed = 720f;
        var pic = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pic.name = "ScaryPicture";
        Object.DestroyImmediate(pic.GetComponent<Collider>());
        pic.transform.SetParent(enemy.transform, false);
        pic.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        pic.transform.localScale = new Vector3(1.4f, 2.1f, 1f);
        pic.GetComponent<Renderer>().sharedMaterial = mat;
        pic.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        enemy.AddComponent<EnemyChase>().Configure(pic.transform, AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Enemy/EnemyScream.wav"));
    }

    // ---------------------------------------------------------------------- Jugador / HUD
    private static void SpawnPlayer(Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerEntity.prefab");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.position = pos;
        player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Other/InputSystem/Inputs.inputactions");
        foreach (var pi in player.GetComponentsInChildren<PlayerInput>(true))
        {
            pi.actions = actions;
            pi.defaultActionMap = "Main";
            EditorUtility.SetDirty(pi);
        }
        foreach (var cam in player.GetComponentsInChildren<Camera>(true))
        {
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(data);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
    }

    private static void BuildHud()
    {
        CreateEventSystem();
        Canvas canvas = CreateCanvas("HudCanvas", 10);
        var hudRoot = new GameObject("Hud", typeof(RectTransform));
        Stretch(Rect(hudRoot, canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero));
        Image cross = CreateImage("Crosshair", hudRoot.transform, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Crosshair.png"), new Color(1, 1, 1, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
        cross.raycastTarget = false;
        Text prompt = CreateText("InteractionPrompt", hudRoot.transform, "", 30, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(1000, 50), TextAnchor.MiddleCenter, FontStyle.Bold);
        prompt.raycastTarget = false;
        hudRoot.AddComponent<InteractionHud>().Configure(prompt, cross);

    }
}
