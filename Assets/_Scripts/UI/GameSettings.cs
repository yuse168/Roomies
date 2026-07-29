using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameAction
{
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Sprint,
    Crouch,
    Jump,
    Interact,
    Carry,
    Rotate
}

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
    const string BindingKeyPrefix = "Settings.Binding.";

    const float DefaultSensitivity = 0.22f;
    const float DefaultVolume = 1f;

    static readonly List<ResolutionOption> resolutionOptions = new();
    static readonly Dictionary<GameAction, Key> keyBindings = new();
    static readonly Dictionary<GameAction, Key> defaultKeyBindings = new()
    {
        { GameAction.MoveForward, Key.W },
        { GameAction.MoveBackward, Key.S },
        { GameAction.MoveLeft, Key.A },
        { GameAction.MoveRight, Key.D },
        { GameAction.Sprint, Key.LeftShift },
        { GameAction.Crouch, Key.LeftCtrl },
        { GameAction.Jump, Key.Space },
        { GameAction.Interact, Key.E },
        { GameAction.Carry, Key.F },
        { GameAction.Rotate, Key.R }
    };
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
        LoadKeyBindings();

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

    static void LoadKeyBindings()
    {
        keyBindings.Clear();
        foreach (var pair in defaultKeyBindings)
        {
            string stored = PlayerPrefs.GetString(
                BindingKeyPrefix + pair.Key,
                pair.Value.ToString());
            keyBindings[pair.Key] = System.Enum.TryParse(stored, out Key key) &&
                                    key != Key.None
                ? key
                : pair.Value;
        }
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

    public static Key GetKeyBinding(GameAction action)
    {
        EnsureInitialized();
        return keyBindings.TryGetValue(action, out Key key)
            ? key
            : defaultKeyBindings[action];
    }

    public static string GetKeyLabel(GameAction action)
    {
        return FormatKey(GetKeyBinding(action));
    }

    public static bool IsPressed(GameAction action)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[GetKeyBinding(action)].isPressed;
    }

    public static bool WasPressedThisFrame(GameAction action)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[GetKeyBinding(action)].wasPressedThisFrame;
    }

    public static void SetKeyBinding(GameAction action, Key key)
    {
        EnsureInitialized();
        if (key == Key.None || key == Key.Escape) return;

        GameAction? conflictingAction = null;
        foreach (var pair in keyBindings)
        {
            if (pair.Key != action && pair.Value == key)
            {
                conflictingAction = pair.Key;
                break;
            }
        }

        Key previous = GetKeyBinding(action);
        keyBindings[action] = key;
        if (conflictingAction.HasValue)
        {
            keyBindings[conflictingAction.Value] = previous;
            PlayerPrefs.SetString(
                BindingKeyPrefix + conflictingAction.Value,
                previous.ToString());
        }

        PlayerPrefs.SetString(BindingKeyPrefix + action, key.ToString());
        PlayerPrefs.Save();
    }

    public static string GetActionLabel(GameAction action)
    {
        return action switch
        {
            GameAction.MoveForward => "前進",
            GameAction.MoveBackward => "後退",
            GameAction.MoveLeft => "左移動",
            GameAction.MoveRight => "右移動",
            GameAction.Sprint => "ダッシュ",
            GameAction.Crouch => "しゃがむ",
            GameAction.Jump => "ジャンプ",
            GameAction.Interact => "使う",
            GameAction.Carry => "持つ・離す",
            GameAction.Rotate => "回転・スタンド",
            _ => action.ToString()
        };
    }

    static string FormatKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl => "L CTRL",
            Key.RightCtrl => "R CTRL",
            Key.LeftShift => "L SHIFT",
            Key.RightShift => "R SHIFT",
            Key.Space => "SPACE",
            Key.Enter => "ENTER",
            Key.Backspace => "BACKSPACE",
            _ => key.ToString().ToUpperInvariant()
        };
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

        foreach (var pair in defaultKeyBindings)
        {
            keyBindings[pair.Key] = pair.Value;
            PlayerPrefs.SetString(BindingKeyPrefix + pair.Key, pair.Value.ToString());
        }
        PlayerPrefs.Save();
    }
}
