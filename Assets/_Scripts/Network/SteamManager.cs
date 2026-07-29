// The SteamManager is designed to work with Steamworks.NET
// This file is released into the public domain.
// Where that dedication is not recognized you are granted a perpetual,
// irrevocable license to copy and modify this file as you see fit.
//
// Version: 1.0.13

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using System.Collections;
using Steamworks;
#endif

//
// The SteamManager provides a base implementation of Steamworks.NET on which you can build upon.
// It handles the basics of starting up and shutting down the SteamAPI for use.
//
[DisallowMultipleComponent]
public class SteamManager : MonoBehaviour {
#if !DISABLESTEAMWORKS
	private const uint k_SteamAppId = 480;

	protected static bool s_EverInitialized = false;
	public static string InitializationError { get; private set; }

	[Header("Steam起動設定")]
	[Tooltip("ONの場合だけ、Steam外から起動した時にSteam経由で再起動します。OFFならSteam未起動でもゲームを終了しません。")]
	[SerializeField] private bool restartThroughSteam = false;

	protected static SteamManager s_instance;
	protected static SteamManager Instance {
		get {
			if (s_instance == null) {
				return new GameObject("SteamManager").AddComponent<SteamManager>();
			}
			else {
				return s_instance;
			}
		}
	}

	protected bool m_bInitialized = false;
	public static bool Initialized {
		get {
			return Instance.m_bInitialized;
		}
	}

	public static void ShutdownAndQuit() {
		if (s_instance != null && s_instance.m_bInitialized) {
			try {
				SteamAPI.Shutdown();
			}
			catch (System.Exception e) {
				Debug.LogWarning($"[Steamworks.NET] Shutdown failed: {e.Message}");
			}
			s_instance.m_bInitialized = false;
		}

#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit(0);
#endif
	}

	protected SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

	[AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
	protected static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText) {
		Debug.LogWarning(pchDebugText);
	}

#if UNITY_2019_3_OR_NEWER
	// In case of disabled Domain Reload, reset static members before entering Play Mode.
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void InitOnPlayMode()
	{
		s_EverInitialized = false;
		s_instance = null;
		InitializationError = null;
	}
#endif

	protected virtual void Awake() {
		// Only one instance of SteamManager at a time!
		if (s_instance != null) {
			Destroy(gameObject);
			return;
		}
		s_instance = this;

		if(s_EverInitialized) {
			// Steam未起動時やシーン切り替え中にManagerが再生成されても、
			// ゲーム全体を例外で停止させない。
			Debug.LogWarning("[Steamworks.NET] SteamManagerの二重初期化を無視しました。", this);
			Destroy(gameObject);
			return;
		}

		// We want our SteamManager Instance to persist across scenes.
		DontDestroyOnLoad(gameObject);

		if (!Packsize.Test()) {
			Debug.LogError("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", this);
		}

		if (!DllCheck.Test()) {
			Debug.LogError("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", this);
		}

		try {
			// If Steam is not running or the game wasn't started through Steam, SteamAPI_RestartAppIfNecessary starts the
			// Steam client and also launches this game again if the User owns it. This can act as a rudimentary form of DRM.
			// Note that this will run which ever version you have installed in steam. Which may not be the precise executable
			// we were currently running.

			// Once you get a Steam AppID assigned by Valve, you need to replace AppId_t.Invalid with it and
			// remove steam_appid.txt from the game depot. eg: "(AppId_t)480" or "new AppId_t(480)".
			// See the Valve documentation for more information: https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown
			// RoomiesはSteamなしでもメニューまで起動できる設計にする。
			// Steam経由の強制再起動はInspectorで明示的にONにした場合だけ行う。
			if (restartThroughSteam && SteamAPI.RestartAppIfNecessary(new AppId_t(k_SteamAppId))) {
				Debug.Log("[Steamworks.NET] Shutting down because RestartAppIfNecessary returned true. Steam will restart the application.");

				Application.Quit();
				return;
			}

			// Initializes the Steamworks API.
			// falseはSteam未起動などの通常のオフライン状態として扱う。
			m_bInitialized = SteamAPI.Init();
		}
		catch (System.Exception e) {
			m_bInitialized = false;
			InitializationError = $"Steam初期化を利用できません: {e.GetType().Name}";
			Debug.LogWarning($"[Steamworks.NET] {InitializationError}\nゲームはオフライン状態で続行します。\n{e.Message}", this);
			return;
		}

		if (!m_bInitialized) {
			InitializationError = "Steamが起動していないため、オンライン機能は利用できません";
			Debug.LogWarning($"[Steamworks.NET] {InitializationError}。ゲームはオフライン状態で続行します。 AppID={k_SteamAppId}", this);
			return;
		}

		InitializationError = null;
		s_EverInitialized = true;
	}

	// This should only ever get called on first load and after an Assembly reload, You should never Disable the Steamworks Manager yourself.
	protected virtual void OnEnable() {
		if (s_instance == null) {
			s_instance = this;
		}

		if (!m_bInitialized) {
			return;
		}

		if (m_SteamAPIWarningMessageHook == null) {
			// Set up our callback to receive warning messages from Steam.
			// You must launch with "-debug_steamapi" in the launch args to receive warnings.
			m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t(SteamAPIDebugTextHook);
			SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
		}
	}

	// OnApplicationQuit gets called too early to shutdown the SteamAPI.
	// Because the SteamManager should be persistent and never disabled or destroyed we can shutdown the SteamAPI here.
	// Thus it is not recommended to perform any Steamworks work in other OnDestroy functions as the order of execution can not be garenteed upon Shutdown. Prefer OnDisable().
	protected virtual void OnDestroy() {
		if (s_instance != this) {
			return;
		}

		s_instance = null;

		if (!m_bInitialized) {
			return;
		}

		SteamAPI.Shutdown();
	}

	protected virtual void Update() {
		if (!m_bInitialized) {
			return;
		}

		// Run Steam client callbacks
		try {
			SteamAPI.RunCallbacks();
		}
		catch (System.Exception e) {
			// 起動後にSteamが終了した場合も、以後のSteam呼び出しを止めて
			// ゲーム本体は継続する。
			m_bInitialized = false;
			InitializationError = "Steamとの接続が切れました";
			Debug.LogWarning($"[Steamworks.NET] {InitializationError}。オンライン機能を停止します。\n{e.Message}", this);
		}
	}
#else
	public static bool Initialized {
		get {
			return false;
		}
	}
	public static string InitializationError {
		get {
			return "このプラットフォームではSteamworksを利用できません";
		}
	}
	public static void ShutdownAndQuit() {
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit(0);
#endif
	}
#endif // !DISABLESTEAMWORKS
}
