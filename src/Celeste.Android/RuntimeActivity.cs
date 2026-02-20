using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Celeste.Android.Platform.Audio;
using Celeste.Android.Platform.Diagnostics;
using Celeste.Android.Platform.Filesystem;
using Celeste.Android.Platform.Fullscreen;
using Celeste.Android.Platform.Input;
using Celeste.Android.Platform.Interop;
using Celeste.Android.Platform.Logging;
using Celeste.Android.Platform.Paths;
using Celeste.Core.Platform.Audio;
using Celeste.Core.Platform.Interop;
using Celeste.Core.Platform.Logging;
using Celeste.Core.Platform.Runtime;
using Celeste.Core.Platform.Services;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Android;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Icon = "@mipmap/ic_launcher",
    LaunchMode = LaunchMode.SingleTask,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
)]
public class RuntimeActivity : AndroidGameActivity
{
    private CelesteRuntimeGame? _game;
    private View? _view;
    private AndroidDualLogger? _logger;
    private ImmersiveFullscreenController? _fullscreen;
    private PlatformServices? _services;
    private AndroidDeviceProfile? _deviceProfile;
    private string _activeAbi = "unknown";
    private AndroidTouchController? _touchController;
    private AndroidExternalLinkLauncher? _externalLinkLauncher;

    protected override void OnCreate(Bundle? bundle)
    {
        base.OnCreate(bundle);

        var paths = new AndroidPathsProvider(this);
        var directoryLayout = paths.EnsureDirectoryLayout();

        _logger = new AndroidDualLogger(paths.LogsPath);
        _fullscreen = new ImmersiveFullscreenController(_logger);
        AndroidCrashReporter.Attach(_logger, nameof(RuntimeActivity));
        _activeAbi = GetActiveAbi();
        _deviceProfile = AndroidDeviceProfile.Capture(this, _activeAbi);
        ApplyRuntimePolicy(_deviceProfile);

        _logger.Log(LogLevel.Info, "APP", "RUNTIME_SESSION_START");
        _logger.Log(LogLevel.Info, "DEVICE", $"ActiveAbi={_activeAbi}");
        _logger.Log(LogLevel.Info, "DEVICE", "PROFILE_CAPTURED", context: _deviceProfile.ToContextString());
        _logger.Log(LogLevel.Info, "POLICY", "ANDROID_RUNTIME_POLICY", context: $"lowMemoryMode={AndroidRuntimePolicy.IsLowMemoryModeEnabled()}; aggressiveGc={AndroidRuntimePolicy.IsAggressiveGarbageCollectionEnabled()}; preferReachProfile={AndroidRuntimePolicy.ShouldPreferReachGraphicsProfile()}; forceLegacyBlend={AndroidRuntimePolicy.ShouldForceLegacyBlendStates()}");
        _logger.Log(LogLevel.Info, "PATHS", $"BaseDataPath={paths.BaseDataPath}");
        _logger.Log(LogLevel.Info, "PATHS", $"ContentPath={paths.ContentPath}");
        _logger.Log(LogLevel.Info, "PATHS", $"LogsPath={paths.LogsPath}");
        _logger.Log(LogLevel.Info, "PATHS", $"SavePath={paths.SavePath}");
        _logger.Log(directoryLayout.Success ? LogLevel.Info : LogLevel.Error, "PATHS", directoryLayout.StatusCode, context: directoryLayout.Message);

        CelestePathBridge.Configure(
            () => paths.ContentPath,
            () => paths.SavePath,
            () => paths.LogsPath,
            (level, tag, message) =>
            {
                var mappedLevel = level switch
                {
                    "ERROR" => LogLevel.Error,
                    "WARN" => LogLevel.Warn,
                    _ => LogLevel.Info
                };

                _logger.Log(mappedLevel, tag, message);
            });

        if (AudioRuntimePolicy.HasAndroidAudioInitCrashMarker(out var audioCrashMarkerPath))
        {
            AudioRuntimePolicy.ConfigureAndroidFmodRuntimeOverride(enabled: false, reason: "AUDIO_INIT_CRASH_MARKER");
            _logger.Log(LogLevel.Warn, "AUDIO", "Detected previous crash during FMOD audio init. Keeping silent backend active until crash marker is removed.", context: $"marker={audioCrashMarkerPath}");
        }

        var fmodEnabled = AudioRuntimePolicy.IsFmodEnabledOnAndroid();
        if (!fmodEnabled && AudioRuntimePolicy.TryGetAndroidFmodRuntimeOverride(out _, out var overrideReason))
        {
            _logger.Log(LogLevel.Warn, "AUDIO", "Android runtime audio override active: FMOD disabled for safety", context: "reason=" + overrideReason);
        }

        _logger.Log(
            fmodEnabled ? LogLevel.Info : LogLevel.Warn,
            "AUDIO",
            fmodEnabled
                ? "Android native FMOD audio enabled"
                : "Android policy active: FMOD disabled; runtime will run in silent mode");

        var fileSystem = new AndroidFileSystem(paths, _logger);
        var input = new AndroidInputProvider(_logger);
        IAudioBackend audio = fmodEnabled
            ? new FmodAudioBackend(this, _logger)
            : new NullAudioBackend(_logger);
        try
        {
            audio.Initialize();
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "AUDIO", "Failed to initialize selected backend at RuntimeActivity startup", exception, $"backend={audio.BackendName}");
        }

        if (fmodEnabled && audio is FmodAudioBackend && !audio.IsInitialized)
        {
            AudioRuntimePolicy.ConfigureAndroidFmodRuntimeOverride(enabled: false, reason: "FMOD_BACKEND_INIT_FAILED");
            _logger.Log(LogLevel.Warn, "AUDIO", "FMOD backend preflight failed. Falling back to silent backend to avoid native crash loops.");
            audio = new NullAudioBackend(_logger);
            audio.Initialize();
        }

        _services = new PlatformServices(_logger, paths, fileSystem, input, audio);
        _touchController = new AndroidTouchController(_logger);
        MInput.KeyboardStateOverride = _touchController.ApplyKeyboardState;
        MInput.MouseStateOverride = _touchController.ApplyMouseState;
        if (_logger != null)
        {
            _externalLinkLauncher = new AndroidExternalLinkLauncher(this, _logger);
            CelesteExternalLinkBridge.Configure(_externalLinkLauncher.TryOpenUrl);
        }
        _logger.Log(LogLevel.Info, "INPUT", "Touch controller enabled (Xbox-style virtual controls + map touch navigation)");
        AndroidCrashReporter.LogMemoryPressure(_logger, "RuntimeActivity OnCreate memory snapshot", _deviceProfile.ToContextString());

        global::Celeste.Settings.Initialize();
        if (!global::Celeste.Settings.Existed)
        {
            global::Celeste.Settings.Instance.Language = "english";
        }

        _logger.Log(LogLevel.Info, "RUNTIME", $"Starting Celeste runtime activity ABI={_activeAbi}");

        _game = new CelesteRuntimeGame(_services, _fullscreen, this, _activeAbi, _touchController);
        _view = _game.Services.GetService(typeof(View)) as View ?? throw new InvalidOperationException("MonoGame did not provide a root view");
        SetContentView(_view);
        _view.Post(() => _fullscreen.Apply(this, "RuntimeActivity-OnCreate-PostSetContentView"));
        _game.Run();
    }

    protected override void OnResume()
    {
        try
        {
            base.OnResume();
        }
        catch (Exception exception)
        {
            _logger?.Log(LogLevel.Error, "LIFECYCLE", "RuntimeActivity base OnResume failed", exception);
        }

        _logger?.Log(LogLevel.Info, "LIFECYCLE", "RuntimeActivity OnResume");
        _fullscreen?.Apply(this, "RuntimeActivity-OnResume");

        try
        {
            _services?.Audio.OnResume();
        }
        catch (Exception exception)
        {
            _logger?.Log(LogLevel.Warn, "FMOD", "Audio backend OnResume failed", exception);
        }

        _game?.HandleResume();
    }

    protected override void OnPause()
    {
        _logger?.Log(LogLevel.Info, "LIFECYCLE", "RuntimeActivity OnPause");

        try
        {
            _services?.Audio.OnPause();
        }
        catch (Exception exception)
        {
            _logger?.Log(LogLevel.Warn, "FMOD", "Audio backend OnPause failed", exception);
        }

        _game?.HandlePause();
        base.OnPause();
    }

    public override void OnBackPressed()
    {
        if (_touchController != null)
        {
            _touchController.QueueMenuCancelPulse();
            _logger?.Log(LogLevel.Info, "INPUT", "Android Back pressed: routed to in-game menu cancel");
            return;
        }

        base.OnBackPressed();
    }

    public override void OnLowMemory()
    {
        var baseForwarded = true;
        try
        {
            base.OnLowMemory();
        }
        catch (Exception exception)
        {
            baseForwarded = false;
            _logger?.Log(LogLevel.Warn, "MEMORY", "RuntimeActivity base OnLowMemory failed", exception);
        }

        if (_logger is not null)
        {
            AndroidCrashReporter.LogMemoryPressure(_logger, "RuntimeActivity OnLowMemory", $"baseForwarded={baseForwarded}");
        }

        _game?.HandleLowMemory();
    }

    public override void OnTrimMemory([GeneratedEnum] TrimMemory level)
    {
        var baseForwarded = true;
        try
        {
            base.OnTrimMemory(level);
        }
        catch (Exception exception)
        {
            baseForwarded = false;
            _logger?.Log(LogLevel.Warn, "MEMORY", "RuntimeActivity base OnTrimMemory failed", exception, $"level={level}");
        }

        if (_logger is not null)
        {
            AndroidCrashReporter.LogMemoryPressure(_logger, "RuntimeActivity OnTrimMemory", $"level={level}; baseForwarded={baseForwarded}");
        }

        _game?.HandleTrimMemory((int)level, level.ToString());
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        _logger?.Log(LogLevel.Info, "LIFECYCLE", $"RuntimeActivity OnWindowFocusChanged={hasFocus}");

        if (hasFocus)
        {
            _fullscreen?.Apply(this, "RuntimeActivity-OnWindowFocusChanged-HasFocus");
        }

        _game?.HandleFocusChanged(hasFocus);
    }

    protected override void OnDestroy()
    {
        _logger?.Log(LogLevel.Info, "LIFECYCLE", "RuntimeActivity OnDestroy");

        if (_services?.Audio is FmodAudioBackend fmodAudioBackend)
        {
            try
            {
                fmodAudioBackend.Shutdown();
            }
            catch (Exception exception)
            {
                _logger?.Log(LogLevel.Warn, "FMOD", "Audio backend shutdown failed", exception);
            }
        }

        _game?.HandleDestroy();
        _game?.Dispose();
        _game = null;
        _view = null;
        _services = null;
        _touchController?.Dispose();
        _touchController = null;
        CelesteExternalLinkBridge.Clear();
        _externalLinkLauncher = null;
        MInput.KeyboardStateOverride = null;
        MInput.MouseStateOverride = null;
        _logger?.Log(LogLevel.Info, "APP", "RUNTIME_SESSION_END");
        _logger?.Flush();
        _logger?.Dispose();
        base.OnDestroy();
    }

    private static string GetActiveAbi()
    {
        var abis = Build.SupportedAbis;
        if (abis is not null && abis.Count > 0)
        {
            return abis[0];
        }

        return "unknown";
    }

    private static void ApplyRuntimePolicy(AndroidDeviceProfile profile)
    {
        AppContext.SetSwitch(AndroidRuntimePolicy.LowMemoryModeSwitch, profile.EnableLowMemoryMode);
        AppContext.SetSwitch(AndroidRuntimePolicy.AggressiveGarbageCollectionSwitch, profile.EnableAggressiveGarbageCollection);
        AppContext.SetSwitch(AndroidRuntimePolicy.PreferReachGraphicsProfileSwitch, profile.PreferReachGraphicsProfile);
        AppContext.SetSwitch(AndroidRuntimePolicy.ForceLegacyBlendStateSwitch, true);
    }
}
