using System;
using System.Threading;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Celeste.Core.Platform.Audio;
using Celeste.Core.Platform.Logging;
using JavaSystem = Java.Lang.JavaSystem;
using JavaUnsatisfiedLinkError = Java.Lang.UnsatisfiedLinkError;

namespace Celeste.Android.Platform.Audio;

public sealed class FmodAudioBackend : IAudioBackend
{
    private const string FmodClassPath = "org/fmod/FMOD";
    private const int JavaBridgeInitRetryCount = 3;
    private const int JavaBridgeRetryDelayMs = 40;

    private readonly IAppLogger _logger;
    private readonly Context _context;
    private bool _javaBridgeReady;

    public FmodAudioBackend(Context context, IAppLogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public string BackendName => "FmodAudioBackend";

    public bool IsInitialized { get; private set; }

    public void Initialize()
    {
        if (IsInitialized)
        {
            return;
        }

        try
        {
            _logger.Log(LogLevel.Info, "FMOD", $"Preparing FMOD backend for ABI={Build.SupportedAbis?[0] ?? "unknown"}");

            var nativeLibrariesReady = EnsureNativeLibrariesLoaded();
            if (!nativeLibrariesReady)
            {
                _logger.Log(LogLevel.Warn, "FMOD", "Native FMOD libraries were not preloaded from Java; continuing with bridge init fallback");
            }

            _javaBridgeReady = EnsureJavaBridgeReady("startup");
            if (_javaBridgeReady)
            {
                _logger.Log(LogLevel.Info, "FMOD", "FMOD Java bridge initialized (org.fmod.FMOD.init)");
                CaptureJavaAudioHints();
            }
            else
            {
                _logger.Log(LogLevel.Warn, "FMOD", "FMOD Java bridge could not be initialized; native FMOD init may fail on some devices");
            }

            IsInitialized = nativeLibrariesReady || _javaBridgeReady;
            if (IsInitialized)
            {
                _logger.Log(LogLevel.Info, "FMOD", "FMOD backend ready");
            }
            else
            {
                _logger.Log(LogLevel.Warn, "FMOD", "FMOD backend preflight failed: native libraries and Java bridge are unavailable");
            }
        }
        catch (Exception exception)
        {
            IsInitialized = false;
            _logger.Log(LogLevel.Error, "FMOD", "Failed to prepare FMOD backend", exception);
        }
    }

    public void OnPause()
    {
        // FMOD Java bridge does not expose a pause callback.
    }

    public void OnResume()
    {
        _javaBridgeReady = EnsureJavaBridgeReady("resume");
        if (!_javaBridgeReady)
        {
            _logger.Log(LogLevel.Warn, "FMOD", "FMOD Java bridge recovery failed on resume");
            return;
        }

        CaptureJavaAudioHints();
    }

    public void Shutdown()
    {
        if (!_javaBridgeReady)
        {
            return;
        }

        TryInvokeRequiredNoArg("close", "FMOD Java bridge close");
        _javaBridgeReady = false;
        IsInitialized = false;
    }

    private bool TryInitJavaBridge()
    {
        IntPtr classRef = IntPtr.Zero;
        try
        {
            classRef = JNIEnv.FindClass(FmodClassPath);
            if (classRef == IntPtr.Zero)
            {
                if (TryClearPendingJavaException(out var detail))
                {
                    _logger.Log(LogLevel.Warn, "FMOD", "Failed to locate org.fmod.FMOD class", context: detail);
                }

                return false;
            }

            JValue[] initArgs = { new(_context) };
            if (TryCallStaticVoid(classRef, "init", "(Landroid/content/Context;)V", initArgs))
            {
                return true;
            }

            if (TryCallStaticBoolean(classRef, "init", "(Landroid/content/Context;)Z", initArgs, out var initResult))
            {
                return initResult;
            }

            if (TryCallStaticInt(classRef, "init", "(Landroid/content/Context;)I", initArgs, out var initCode))
            {
                return initCode == 0;
            }

            return false;
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FMOD", "Exception while initializing FMOD Java bridge", exception);
            return false;
        }
        finally
        {
            if (classRef != IntPtr.Zero)
            {
                JNIEnv.DeleteLocalRef(classRef);
            }
        }
    }

    private bool EnsureNativeLibrariesLoaded()
    {
        var lowLevelLoaded = TryLoadNativeLibrary("fmod");
        var studioLoaded = TryLoadNativeLibrary("fmodstudio");
        return lowLevelLoaded && studioLoaded;
    }

    private bool EnsureJavaBridgeReady(string reason)
    {
        if (TryCheckJavaInit(out var alreadyReady) && alreadyReady)
        {
            return true;
        }

        for (var attempt = 1; attempt <= JavaBridgeInitRetryCount; attempt++)
        {
            var initCallSucceeded = TryInitJavaBridge();
            if (initCallSucceeded)
            {
                if (!TryCheckJavaInit(out var readyAfterInit) || readyAfterInit)
                {
                    return true;
                }

                _logger.Log(LogLevel.Warn, "FMOD", "FMOD Java bridge init returned but checkInit is false", context: $"reason={reason}; attempt={attempt}");
            }
            else
            {
                _logger.Log(LogLevel.Warn, "FMOD", "FMOD Java bridge init call failed", context: $"reason={reason}; attempt={attempt}");
            }

            if (attempt < JavaBridgeInitRetryCount)
            {
                Thread.Sleep(JavaBridgeRetryDelayMs);
            }
        }

        return false;
    }

    private bool TryLoadNativeLibrary(string libraryName)
    {
        try
        {
            JavaSystem.LoadLibrary(libraryName);
            _logger.Log(LogLevel.Info, "FMOD", $"Native FMOD library loaded: {libraryName}");
            return true;
        }
        catch (JavaUnsatisfiedLinkError exception)
        {
            if (exception.Message?.IndexOf("already loaded", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.Log(LogLevel.Info, "FMOD", $"Native FMOD library already loaded: {libraryName}");
                return true;
            }

            _logger.Log(LogLevel.Warn, "FMOD", $"Native FMOD library '{libraryName}' could not be loaded", context: exception.ToString());
            return false;
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FMOD", $"Unexpected error while loading native FMOD library '{libraryName}'", exception);
            return false;
        }
    }

    private bool TryCheckJavaInit(out bool ready)
    {
        ready = false;
        IntPtr classRef = IntPtr.Zero;
        try
        {
            classRef = JNIEnv.FindClass(FmodClassPath);
            if (classRef == IntPtr.Zero)
            {
                TryClearPendingJavaException(out _);
                return false;
            }

            return TryCallStaticBoolean(classRef, "checkInit", "()Z", out ready);
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FMOD", "Failed to query FMOD Java checkInit", exception);
            return false;
        }
        finally
        {
            if (classRef != IntPtr.Zero)
            {
                JNIEnv.DeleteLocalRef(classRef);
            }
        }
    }

    private void CaptureJavaAudioHints()
    {
        IntPtr classRef = IntPtr.Zero;
        try
        {
            classRef = JNIEnv.FindClass(FmodClassPath);
            if (classRef == IntPtr.Zero)
            {
                TryClearPendingJavaException(out _);
                return;
            }

            bool javaReady = TryCallStaticBoolean(classRef, "checkInit", "()Z", out var checkInitResult) && checkInitResult;
            bool supportsLowLatency = TryCallStaticBoolean(classRef, "supportsLowLatency", "()Z", out var lowLatencyResult) && lowLatencyResult;
            int sampleRate = TryCallStaticInt(classRef, "getOutputSampleRate", "()I", out var sampleRateResult)
                ? sampleRateResult
                : 0;
            int blockSize = TryCallStaticInt(classRef, "getOutputBlockSize", "()I", out var blockSizeResult)
                ? blockSizeResult
                : 0;
            bool bluetoothOn = TryCallStaticBoolean(classRef, "isBluetoothOn", "()Z", out var bluetoothResult) && bluetoothResult;

            AudioRuntimePolicy.ConfigureAndroidDeviceAudioHints(sampleRate, blockSize, supportsLowLatency, bluetoothOn, javaReady);
            _logger.Log(
                LogLevel.Info,
                "FMOD",
                "FMOD Java audio hints captured",
                context: $"javaReady={javaReady}; sampleRate={sampleRate}; blockSize={blockSize}; lowLatency={supportsLowLatency}; bluetooth={bluetoothOn}");
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FMOD", "Failed to capture FMOD Java audio hints", exception);
        }
        finally
        {
            if (classRef != IntPtr.Zero)
            {
                JNIEnv.DeleteLocalRef(classRef);
            }
        }
    }

    private void TryInvokeRequiredNoArg(string methodName, string operation)
    {
        IntPtr classRef = IntPtr.Zero;
        try
        {
            classRef = JNIEnv.FindClass(FmodClassPath);
            if (classRef == IntPtr.Zero)
            {
                TryClearPendingJavaException(out _);
                return;
            }

            if (TryCallStaticVoid(classRef, methodName, "()V"))
            {
                _logger.Log(LogLevel.Info, "FMOD", operation);
            }
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FMOD", operation + " failed", exception);
        }
        finally
        {
            if (classRef != IntPtr.Zero)
            {
                JNIEnv.DeleteLocalRef(classRef);
            }
        }
    }

    private bool TryCallStaticVoid(IntPtr classRef, string methodName, string signature, JValue[]? args = null)
    {
        IntPtr method = JNIEnv.GetStaticMethodID(classRef, methodName, signature);
        if (method == IntPtr.Zero)
        {
            TryClearPendingJavaException(out _);
            return false;
        }

        if (args is null)
        {
            JNIEnv.CallStaticVoidMethod(classRef, method);
        }
        else
        {
            JNIEnv.CallStaticVoidMethod(classRef, method, args);
        }

        if (TryClearPendingJavaException(out var detail))
        {
            _logger.Log(LogLevel.Warn, "FMOD", $"Java call {methodName}{signature} failed", context: detail);
            return false;
        }

        return true;
    }

    private bool TryCallStaticBoolean(IntPtr classRef, string methodName, string signature, JValue[] args, out bool value)
    {
        value = false;
        IntPtr method = JNIEnv.GetStaticMethodID(classRef, methodName, signature);
        if (method == IntPtr.Zero)
        {
            TryClearPendingJavaException(out _);
            return false;
        }

        value = JNIEnv.CallStaticBooleanMethod(classRef, method, args);
        if (TryClearPendingJavaException(out var detail))
        {
            _logger.Log(LogLevel.Warn, "FMOD", $"Java call {methodName}{signature} failed", context: detail);
            value = false;
            return false;
        }

        return true;
    }

    private bool TryCallStaticBoolean(IntPtr classRef, string methodName, string signature, out bool value)
    {
        return TryCallStaticBoolean(classRef, methodName, signature, Array.Empty<JValue>(), out value);
    }

    private bool TryCallStaticInt(IntPtr classRef, string methodName, string signature, JValue[] args, out int value)
    {
        value = -1;
        IntPtr method = JNIEnv.GetStaticMethodID(classRef, methodName, signature);
        if (method == IntPtr.Zero)
        {
            TryClearPendingJavaException(out _);
            return false;
        }

        value = JNIEnv.CallStaticIntMethod(classRef, method, args);
        if (TryClearPendingJavaException(out var detail))
        {
            _logger.Log(LogLevel.Warn, "FMOD", $"Java call {methodName}{signature} failed", context: detail);
            value = -1;
            return false;
        }

        return true;
    }

    private bool TryCallStaticInt(IntPtr classRef, string methodName, string signature, out int value)
    {
        return TryCallStaticInt(classRef, methodName, signature, Array.Empty<JValue>(), out value);
    }

    private static bool TryClearPendingJavaException(out string detail)
    {
        detail = string.Empty;
        IntPtr exceptionRef = IntPtr.Zero;
        try
        {
            exceptionRef = JNIEnv.ExceptionOccurred();
            if (exceptionRef == IntPtr.Zero)
            {
                return false;
            }

            JNIEnv.ExceptionClear();
            detail = "Java exception";

            using var throwable = Java.Lang.Object.GetObject<Java.Lang.Throwable>(exceptionRef, JniHandleOwnership.TransferLocalRef);
            detail = throwable?.ToString() ?? detail;
            exceptionRef = IntPtr.Zero;
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                JNIEnv.ExceptionClear();
            }
            catch
            {
            }

            if (exceptionRef != IntPtr.Zero)
            {
                try
                {
                    JNIEnv.DeleteLocalRef(exceptionRef);
                }
                catch
                {
                }
            }

            detail = "Java exception (failed to decode): " + exception.Message;
            return true;
        }
    }
}
