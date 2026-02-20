using System;
using System.IO;
using Celeste.Core.Platform.Interop;

namespace Celeste.Core.Platform.Audio;

public static class AudioRuntimePolicy
{
    public const string EnableFmodOnAndroidSwitch = "Celeste.Android.EnableFmodAudio";
    public const string EnableFmodSamplePreloadOnAndroidSwitch = "Celeste.Android.PreloadFmodSampleData";

    private const string AndroidAudioInitCrashMarkerFileName = "fmod_audio_init_inflight.marker";

    private static readonly object AndroidHintSync = new();
    private static readonly object AndroidRuntimeAudioSync = new();
    private static bool _androidHintsConfigured;
    private static int _androidOutputSampleRate;
    private static int _androidOutputBlockSize;
    private static bool _androidSupportsLowLatency;
    private static bool _androidBluetoothOn;
    private static bool _androidJavaBridgeReady;

    private static bool _androidRuntimeFmodOverrideConfigured;
    private static bool _androidRuntimeFmodEnabled = true;
    private static string _androidRuntimeFmodReason = string.Empty;

    public static bool IsFmodEnabledOnAndroid()
    {
        if (!OperatingSystem.IsAndroid())
        {
            return true;
        }

        lock (AndroidRuntimeAudioSync)
        {
            if (_androidRuntimeFmodOverrideConfigured)
            {
                return _androidRuntimeFmodEnabled;
            }
        }

        if (AppContext.TryGetSwitch(EnableFmodOnAndroidSwitch, out var enabled))
        {
            return enabled;
        }

        return true;
    }

    public static bool ShouldForceSilentAudio()
    {
        return OperatingSystem.IsAndroid() && !IsFmodEnabledOnAndroid();
    }

    public static bool IsFmodSamplePreloadEnabledOnAndroid()
    {
        if (!OperatingSystem.IsAndroid())
        {
            return true;
        }

        if (AppContext.TryGetSwitch(EnableFmodSamplePreloadOnAndroidSwitch, out var enabled))
        {
            return enabled;
        }

        return false;
    }

    public static void ConfigureAndroidFmodRuntimeOverride(bool enabled, string reason)
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        lock (AndroidRuntimeAudioSync)
        {
            _androidRuntimeFmodOverrideConfigured = true;
            _androidRuntimeFmodEnabled = enabled;
            _androidRuntimeFmodReason = reason ?? string.Empty;
        }
    }

    public static bool TryGetAndroidFmodRuntimeOverride(out bool enabled, out string reason)
    {
        lock (AndroidRuntimeAudioSync)
        {
            enabled = _androidRuntimeFmodEnabled;
            reason = _androidRuntimeFmodReason;
            return _androidRuntimeFmodOverrideConfigured;
        }
    }

    public static void MarkAndroidAudioInitStart()
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        string markerPath = ResolveAndroidAudioInitMarkerPath();
        try
        {
            string? directory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Best effort marker.
        }
    }

    public static void MarkAndroidAudioInitComplete()
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        string markerPath = ResolveAndroidAudioInitMarkerPath();
        try
        {
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
        catch
        {
            // Best effort marker cleanup.
        }
    }

    public static bool HasAndroidAudioInitCrashMarker(out string markerPath)
    {
        markerPath = ResolveAndroidAudioInitMarkerPath();
        if (!OperatingSystem.IsAndroid())
        {
            return false;
        }

        try
        {
            return File.Exists(markerPath);
        }
        catch
        {
            return false;
        }
    }

    public static void ConfigureAndroidDeviceAudioHints(int outputSampleRate, int outputBlockSize, bool supportsLowLatency, bool bluetoothOn, bool javaBridgeReady)
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        lock (AndroidHintSync)
        {
            _androidOutputSampleRate = Math.Max(0, outputSampleRate);
            _androidOutputBlockSize = Math.Max(0, outputBlockSize);
            _androidSupportsLowLatency = supportsLowLatency;
            _androidBluetoothOn = bluetoothOn;
            _androidJavaBridgeReady = javaBridgeReady;
            _androidHintsConfigured = true;
        }
    }

    public static bool TryGetAndroidDeviceAudioHints(out int outputSampleRate, out int outputBlockSize, out bool supportsLowLatency, out bool bluetoothOn, out bool javaBridgeReady)
    {
        lock (AndroidHintSync)
        {
            outputSampleRate = _androidOutputSampleRate;
            outputBlockSize = _androidOutputBlockSize;
            supportsLowLatency = _androidSupportsLowLatency;
            bluetoothOn = _androidBluetoothOn;
            javaBridgeReady = _androidJavaBridgeReady;
            return _androidHintsConfigured;
        }
    }

    private static string ResolveAndroidAudioInitMarkerPath()
    {
        return CelestePathBridge.ResolveErrorLogPath(AndroidAudioInitCrashMarkerFileName);
    }
}
