using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// MainMenuSteamだけで動く、実際のRoomiesルームを使った背景演出。
/// ゲーム本編のManagerやNetworkObjectは持ち込まず、見た目だけを表示する。
///
/// 構図の方針：
///  ・Roomiesたちを主役にする（引きの部屋の絵ではなく、キャラが立っている絵）
///  ・被写体を画面の右寄りに置く。画面左半分はUI（タイトルとボタン）の場所
///  ・奥の部屋はボケさせて、手前のキャラだけが締まって見えるようにする
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuRoomShowcase : MonoBehaviour
{
    public const int CurrentBuildVersion = 3;

    // 構図（1920x1080想定）
    private static readonly Vector3 CameraPosition = new Vector3(21.4f, 7.15f, 19.7f);
    private static readonly Vector3 FallbackSubject = new Vector3(17.3f, 5.72f, 16.73f);
    private const float SubjectEyeHeight = 0.45f;   // 足元基準からの見上げ補正
    private const float FrameBias        = 1.15f;   // 被写体を画面右へ寄せる量
    private const float FieldOfView      = 40f;

    [SerializeField] private Transform roomRoot;
    [SerializeField] private Transform[] characters;
    [SerializeField] private Material skyboxMaterial;
    [SerializeField, HideInInspector] private int buildVersion;

    private Camera showcaseCamera;
    private Vector3 cameraPosition;
    private Vector3 cameraFocus;
    private Vector3[] characterBasePositions;
    private Quaternion[] characterBaseRotations;
    private VolumeProfile runtimeProfile;

    private static readonly Color[] CharacterColors =
    {
        new Color(0.98f, 0.31f, 0.58f),
        new Color(0.20f, 0.70f, 0.96f),
        new Color(0.35f, 0.82f, 0.42f),
    };

    public bool IsCurrentBuild => buildVersion == CurrentBuildVersion;

    public void Configure(Transform room, Transform[] roomies, Material skybox)
    {
        roomRoot = room;
        characters = roomies;
        skyboxMaterial = skybox;
        buildVersion = CurrentBuildVersion;
    }

    private void Awake()
    {
        PrepareCharacters();
        FrameSubject();
        ApplyCamera();
        ConfigurePostProcessing();
        ConfigureLighting();

        if (roomRoot != null)
        {
            foreach (Collider collider in roomRoot.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }
    }

    private void PrepareCharacters()
    {
        if (characters == null) characters = System.Array.Empty<Transform>();
        characterBasePositions = new Vector3[characters.Length];
        characterBaseRotations = new Quaternion[characters.Length];

        for (int i = 0; i < characters.Length; i++)
        {
            Transform character = characters[i];
            if (character == null) continue;

            characterBasePositions[i] = character.position;
            characterBaseRotations[i] = character.rotation;

            foreach (Collider collider in character.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            Color color = CharacterColors[i % CharacterColors.Length];
            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.name.Contains("BobBody")) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                renderer.SetPropertyBlock(block);
            }
        }
    }

    /// <summary>
    /// キャラの実際の位置から注視点を決める。
    /// 注視点を被写体の少し左に置くことで、キャラが画面の右寄りに写る。
    /// </summary>
    private void FrameSubject()
    {
        cameraPosition = CameraPosition;

        Vector3 subject = FallbackSubject;
        int counted = 0;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] == null) continue;
            sum += characterBasePositions[i];
            counted++;
        }
        if (counted > 0) subject = sum / counted;

        Vector3 forward = (subject - cameraPosition).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        cameraFocus = subject + Vector3.up * SubjectEyeHeight - right * FrameBias;
    }

    private void ApplyCamera()
    {
        showcaseCamera = FindAnyObjectByType<Camera>();
        if (showcaseCamera == null) return;

        showcaseCamera.transform.position = cameraPosition;
        showcaseCamera.transform.rotation =
            Quaternion.LookRotation(cameraFocus - cameraPosition, Vector3.up);
        showcaseCamera.fieldOfView = FieldOfView;
        showcaseCamera.clearFlags = CameraClearFlags.Skybox;

        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.85f;
            RenderSettings.reflectionIntensity = 0.65f;
            DynamicGI.UpdateEnvironment();
        }
    }

    /// <summary>
    /// ポストプロセスは専用のローカルVolumeで持つ。
    /// 共有のVolumeProfileアセットへ直接Overrideを足すと、
    /// エディタで再生するたびにアセットが書き換わってしまうため。
    /// </summary>
    private void ConfigurePostProcessing()
    {
        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "MenuShowcaseProfile";

        var depthOfField = runtimeProfile.Add<DepthOfField>(true);
        depthOfField.active = true;
        depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        depthOfField.gaussianStart.Override(7.5f);
        depthOfField.gaussianEnd.Override(22f);
        depthOfField.gaussianMaxRadius.Override(0.9f);
        depthOfField.highQualitySampling.Override(false);

        // 中央〜右のキャラに視線を集めるための、ごく浅い周辺減光
        var vignette = runtimeProfile.Add<Vignette>(true);
        vignette.active = true;
        vignette.color.Override(new Color(0.08f, 0.04f, 0.16f));
        vignette.intensity.Override(0.26f);
        vignette.smoothness.Override(0.55f);

        var volumeObject = new GameObject("Menu Showcase Volume");
        volumeObject.transform.SetParent(transform, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;
        volume.weight = 1f;
        volume.sharedProfile = runtimeProfile;
    }

    private void OnDestroy()
    {
        if (runtimeProfile != null) Destroy(runtimeProfile);
    }

    private void ConfigureLighting()
    {
        Light directional = FindAnyObjectByType<Light>();
        if (directional != null)
        {
            directional.color = new Color(1f, 0.91f, 0.80f);
            directional.intensity = 1.45f;
            directional.shadows = LightShadows.Soft;
        }

        // パーティーゲームらしい明るさ。影の中も沈まないよう環境光を上げる。
        RenderSettings.ambientLight = new Color(0.48f, 0.50f, 0.64f);

        CreateFillLight(
            "Menu Warm Fill",
            new Vector3(17.4f, 8.4f, 14.6f),
            new Color(1f, 0.52f, 0.42f),
            5.0f,
            13f);
        CreateFillLight(
            "Menu Cool Fill",
            new Vector3(13.8f, 7.2f, 19.2f),
            new Color(0.28f, 0.66f, 1f),
            3.8f,
            11f);
        // キャラの輪郭を背景から浮かせるリムライト
        CreateFillLight(
            "Menu Rim",
            new Vector3(15.2f, 7.9f, 13.6f),
            new Color(1f, 0.86f, 0.72f),
            4.4f,
            10f);
    }

    private void CreateFillLight(
        string lightName,
        Vector3 position,
        Color color,
        float intensity,
        float range)
    {
        var lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.position = position;
        Light fill = lightObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.color = color;
        fill.intensity = intensity;
        fill.range = range;
        fill.shadows = LightShadows.None;
    }

    private void Update()
    {
        if (showcaseCamera != null)
        {
            float drift = Mathf.Sin(Time.unscaledTime * 0.2f) * 0.2f;
            Vector3 position = cameraPosition + new Vector3(drift, 0f, -drift * 0.25f);
            showcaseCamera.transform.position = position;
            showcaseCamera.transform.rotation =
                Quaternion.LookRotation(cameraFocus - position, Vector3.up);
        }

        for (int i = 0; i < characters.Length; i++)
        {
            Transform character = characters[i];
            if (character == null) continue;

            float phase = Time.unscaledTime * 1.35f + i * 1.7f;
            character.position = characterBasePositions[i] +
                Vector3.up * (Mathf.Sin(phase) * 0.05f);
            character.rotation = characterBaseRotations[i] *
                Quaternion.Euler(0f, Mathf.Sin(phase * 0.62f) * 5f, 0f);
        }
    }
}
