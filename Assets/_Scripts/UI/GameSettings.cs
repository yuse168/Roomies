using System.Collections.Generic;
using UnityEngine;

public static class GameSettings
{
    public readonly struct ResolutionOption
    {
        public readonly int Width;
        public readonly int Height;

        public ResolutionOption(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public string Label => $"{Width} × {Height}";
    }

    const string SensitivityKey = "Settings.MouseSensitivity";
    const string VolumeKey = "Settings.MasterVolume";
    const string FullscreenKey = "Settings.Fullscreen";
    const string ResolutionWidthKey = "Settings.ResolutionWidth";
    const string ResolutionHeightKey = "Settings.ResolutionHeight";
    const string QualityKey = "Settings.Quality";
    const string CameraMotionKey = "Settings.CameraMotion";

    const float DefaultSensitivity = 0.5f;
    const float DefaultVolume = 1f;

    static readonly List<ResolutionOption> resolutionOptions = new();
    static bool initialized;

    public static float MouseSensitivity { get; private set; }
    public static float MasterVolume { get; private set; }
    public static bool Fullscreen { get; private set; }
    public static int ResolutionIndex { get; private set; }
    public static int QualityLevel { get; private set; }
    public static bool CameraMotion { get; private set; }
    public static IReadOnlyList<ResolutionOption> ResolutionOptions
    {
        get
        {
            EnsureInitialized();
            return resolutionOptions;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeOnLoad()
    {
        EnsureInitialized();
        ApplySystemSettings();
    }

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        BuildResolutionOptions();

        MouseSensitivity = Mathf.Clamp(
            PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity), 0.05f, 1.5f);
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
        CameraMotion = PlayerPrefs.GetInt(CameraMotionKey, 1) != 0;
        QualityLevel = Mathf.Clamp(
            PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()),
            0,
            Mathf.Max(0, QualitySettings.names.Length - 1));

        int width = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
        int height = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);
        ResolutionIndex = FindResolutionIndex(width, height);
    }

    static void BuildResolutionOptions()
    {
        resolutionOptions.Clear();
        var seen = new HashSet<string>();

        foreach (var resolution in Screen.resolutions)
        {
            string key = $"{resolution.width}x{resolution.height}";
            if (seen.Add(key))
                resolutionOptions.Add(new ResolutionOption(resolution.width, resolution.height));
        }

        resolutionOptions.Sort((a, b) =>
        {
            int pixels = (a.Width * a.Height).CompareTo(b.Width * b.Height);
            return pixels != 0 ? pixels : a.Width.CompareTo(b.Width);
        });

        if (resolutionOptions.Count == 0)
            resolutionOptions.Add(new ResolutionOption(
                Mathf.Max(640, Screen.width), Mathf.Max(360, Screen.height)));
    }

    static int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            if (resolutionOptions[i].Width == width && resolutionOptions[i].Height == height)
                return i;
        }

        int closestIndex = 0;
        long closestDistance = long.MaxValue;
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            long dx = resolutionOptions[i].Width - width;
            long dy = resolutionOptions[i].Height - height;
            long distance = dx * dx + dy * dy;
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            closestIndex = i;
        }

        return closestIndex;
    }

    static void ApplySystemSettings()
    {
        AudioListener.volume = MasterVolume;
        if (QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(QualityLevel, true);
        ApplyDisplay();
    }

    static void ApplyDisplay()
    {
        if (resolutionOptions.Count == 0) return;
        ResolutionIndex = Mathf.Clamp(ResolutionIndex, 0, resolutionOptions.Count - 1);
        var resolution = resolutionOptions[ResolutionIndex];
        Screen.SetResolution(
            resolution.Width,
            resolution.Height,
            Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    public static void ApplyToPlayer(PlayerMovement player)
    {
        EnsureInitialized();
        if (player == null) return;
        player.mouseSensitivity = MouseSensitivity;
        player.enableCameraMotion = CameraMotion;
    }

    static void ApplyToActivePlayers()
    {
        foreach (var player in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include))
        {
            if (player.IsOwner)
                ApplyToPlayer(player);
        }
    }

    public static void SetMouseSensitivity(float value)
    {
        EnsureInitialized();
        MouseSensitivity = Mathf.Clamp(value, 0.05f, 1.5f);
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
        ApplyToActivePlayers();
        PlayerPrefs.Save();
    }

    public static void SetMasterVolume(float value)
    {
        EnsureInitialized();
        MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        PlayerPrefs.Save();
    }

    public static void SetFullscreen(bool value)
    {
        EnsureInitialized();
        Fullscreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        ApplyDisplay();
        PlayerPrefs.Save();
    }

    public static void SetResolutionIndex(int index)
    {
        EnsureInitialized();
        if (resolutionOptions.Count == 0) return;
        ResolutionIndex = (index % resolutionOptions.Count + resolutionOptions.Count)
                          % resolutionOptions.Count;
        var resolution = resolutionOptions[ResolutionIndex];
        PlayerPrefs.SetInt(ResolutionWidthKey, resolution.Width);
        PlayerPrefs.SetInt(ResolutionHeightKey, resolution.Height);
        ApplyDisplay();
        PlayerPrefs.Save();
    }

    public static void SetQualityLevel(int level)
    {
        EnsureInitialized();
        if (QualitySettings.names.Length == 0) return;
        QualityLevel = (level % QualitySettings.names.Length + QualitySettings.names.Length)
                       % QualitySettings.names.Length;
        QualitySettings.SetQualityLevel(QualityLevel, true);
        PlayerPrefs.SetInt(QualityKey, QualityLevel);
        PlayerPrefs.Save();
    }

    public static void SetCameraMotion(bool value)
    {
        EnsureInitialized();
        CameraMotion = value;
        PlayerPrefs.SetInt(CameraMotionKey, value ? 1 : 0);
        ApplyToActivePlayers();
        PlayerPrefs.Save();
    }

    public static void ResetDefaults()
    {
        EnsureInitialized();
        SetMouseSensitivity(DefaultSensitivity);
        SetMasterVolume(DefaultVolume);
        SetFullscreen(true);
        SetResolutionIndex(resolutionOptions.Count - 1);
        SetQualityLevel(Mathf.Max(0, QualitySettings.names.Length - 1));
        SetCameraMotion(true);
    }
}
