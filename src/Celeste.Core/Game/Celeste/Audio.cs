using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Celeste.Core.Platform.Audio;
using Celeste.Core.Platform.Interop;
using Celeste.Core.Platform.Runtime;
using FMOD;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

public static class Audio
{
	public static class Banks
	{
		public static Bank Master;

		public static Bank Music;

		public static Bank Sfxs;

		public static Bank UI;

		public static Bank DlcMusic;

		public static Bank DlcSfxs;

		public static Bank Load(string name, bool loadStrings)
		{
			string text = ResolveBankPath(name);
			if (!File.Exists(text + ".bank"))
			{
				throw new FileNotFoundException("FMOD bank not found", text + ".bank");
			}

			CheckFmod(system.loadBankFile(text + ".bank", LOAD_BANK_FLAGS.NORMAL, out var bank));
			if (ShouldPreloadFmodSamplesOnCurrentPlatform())
			{
				RESULT result = bank.loadSampleData();
				if (result != RESULT.OK)
				{
					CelestePathBridge.LogWarn("FMOD", $"Bank sample preload failed for '{name}': {result}. Continuing with streaming fallback.");
				}
			}
			if (loadStrings)
			{
				if (!File.Exists(text + ".strings.bank"))
				{
					throw new FileNotFoundException("FMOD strings bank not found", text + ".strings.bank");
				}

				CheckFmod(system.loadBankFile(text + ".strings.bank", LOAD_BANK_FLAGS.NORMAL, out var _));
			}
			return bank;
		}

		private static string ResolveBankPath(string name)
		{
			string[] searchFolders;
			string preferredFolder;
			if (OperatingSystem.IsAndroid())
			{
				searchFolders = new string[4] { "Mobile", "Android", "Desktop", string.Empty };
				preferredFolder = "Mobile";
			}
			else
			{
				searchFolders = new string[4] { "Desktop", "Mobile", "Android", string.Empty };
				preferredFolder = "Desktop";
			}

			string preferredPath = Path.Combine(Engine.ContentDirectory, "FMOD", preferredFolder, name);
			for (int i = 0; i < searchFolders.Length; i++)
			{
				string folder = searchFolders[i];
				string candidatePath = string.IsNullOrEmpty(folder)
					? Path.Combine(Engine.ContentDirectory, "FMOD", name)
					: Path.Combine(Engine.ContentDirectory, "FMOD", folder, name);

				if (!File.Exists(candidatePath + ".bank"))
				{
					continue;
				}

				if (!string.Equals(candidatePath, preferredPath, StringComparison.OrdinalIgnoreCase))
				{
					CelestePathBridge.LogWarn("FMOD", $"Using fallback bank layout '{candidatePath}'. Preferred path '{preferredPath}.bank' was not found.");
				}

				return candidatePath;
			}

			CelestePathBridge.LogError("FMOD", $"Missing bank '{name}' in FMOD folders. Checked: Mobile, Android, Desktop and root.");
			return preferredPath;
		}
	}

	private static FMOD.Studio.System system;

	private static FMOD.Studio._3D_ATTRIBUTES attributes3d = default(FMOD.Studio._3D_ATTRIBUTES);

	public static Dictionary<string, EventDescription> cachedEventDescriptions = new Dictionary<string, EventDescription>();

	private static Camera currentCamera;

	private static bool ready;

	private static bool androidDriverInfoLogged;

	private static bool androidSamplePreloadPolicyLogged;

	public static bool FallbackSilentMode { get; private set; }

	public static string LastInitError { get; private set; } = "";

	private static bool IsSystemReady => system != null && ready && !FallbackSilentMode;

	private static EventInstance currentMusicEvent = null;

	private static EventInstance currentAltMusicEvent = null;

	private static EventInstance currentAmbientEvent = null;

	private static EventInstance mainDownSnapshot = null;

	public static string CurrentMusic = "";

	private static bool musicUnderwater;

	private static EventInstance musicUnderwaterSnapshot;

	public static EventInstance CurrentMusicEventInstance => currentMusicEvent;

	public static EventInstance CurrentAmbienceEventInstance => currentAmbientEvent;

	public static float MusicVolume
	{
		get
		{
			return VCAVolume("vca:/music");
		}
		set
		{
			VCAVolume("vca:/music", value);
		}
	}

	public static float SfxVolume
	{
		get
		{
			return VCAVolume("vca:/gameplay_sfx");
		}
		set
		{
			VCAVolume("vca:/gameplay_sfx", value);
			VCAVolume("vca:/ui_sfx", value);
		}
	}

	public static bool PauseMusic
	{
		get
		{
			return BusPaused("bus:/music");
		}
		set
		{
			BusPaused("bus:/music", value);
		}
	}

	public static bool PauseGameplaySfx
	{
		get
		{
			return BusPaused("bus:/gameplay_sfx");
		}
		set
		{
			BusPaused("bus:/gameplay_sfx", value);
			BusPaused("bus:/music/stings", value);
		}
	}

	public static bool PauseUISfx
	{
		get
		{
			return BusPaused("bus:/ui_sfx");
		}
		set
		{
			BusPaused("bus:/ui_sfx", value);
		}
	}

	public static bool MusicUnderwater
	{
		get
		{
			return musicUnderwater;
		}
		set
		{
			if (musicUnderwater == value)
			{
				return;
			}
			musicUnderwater = value;
			if (musicUnderwater)
			{
				if (musicUnderwaterSnapshot == null)
				{
					musicUnderwaterSnapshot = CreateSnapshot("snapshot:/underwater");
				}
				else
				{
					ResumeSnapshot(musicUnderwaterSnapshot);
				}
			}
			else
			{
				EndSnapshot(musicUnderwaterSnapshot);
			}
		}
	}

	public static void Init()
	{
		FallbackSilentMode = false;
		LastInitError = "";
		androidDriverInfoLogged = false;
		if (AudioRuntimePolicy.ShouldForceSilentAudio())
		{
			string text = string.Empty;
			if (AudioRuntimePolicy.TryGetAndroidFmodRuntimeOverride(out _, out string reason) && !string.IsNullOrWhiteSpace(reason))
			{
				text = "; runtimeReason=" + reason;
			}

			CelestePathBridge.LogWarn("FMOD", $"Android silent-audio policy is active. Skipping FMOD init. Set '{AudioRuntimePolicy.EnableFmodOnAndroidSwitch}' switch to true to test FMOD{text}.");
			ActivateFallback("AUDIO_DISABLED_BY_POLICY_ANDROID");
			return;
		}

		CelestePathBridge.LogInfo("FMOD", "Initializing FMOD audio system");
		FMOD.Studio.INITFLAGS studioFlags = FMOD.Studio.INITFLAGS.NORMAL;
		if (Settings.Instance.LaunchWithFMODLiveUpdate)
		{
			studioFlags = FMOD.Studio.INITFLAGS.LIVEUPDATE;
		}

		InitializeSystemWithOutputFallback(studioFlags);
		attributes3d.forward = new VECTOR
		{
			x = 0f,
			y = 0f,
			z = 1f
		};
		attributes3d.up = new VECTOR
		{
			x = 0f,
			y = 1f,
			z = 0f
		};
		SetListenerPosition(new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, -345f));
		ready = true;
		CelestePathBridge.LogInfo("FMOD", "FMOD initialized successfully");
	}

	private static void InitializeSystemWithOutputFallback(FMOD.Studio.INITFLAGS studioFlags)
	{
		OUTPUTTYPE[] outputCandidates = GetOutputCandidates();
		int[] maxChannelCandidates = GetMaxChannelCandidates();
		AndroidInitProfile[] androidInitProfiles = GetAndroidInitProfiles();
		RESULT lastResult = RESULT.OK;
		string lastAttemptDescription = "none";
		bool versionLogged = false;
		system = null;
		for (int profileIndex = 0; profileIndex < androidInitProfiles.Length; profileIndex++)
		{
			AndroidInitProfile androidInitProfile = androidInitProfiles[profileIndex];
			FMOD.Studio.INITFLAGS studioFlagsForAttempt = androidInitProfile.UseThreadlessUpdate
				? studioFlags | FMOD.Studio.INITFLAGS.SYNCHRONOUS_UPDATE
				: studioFlags;
			FMOD.INITFLAGS lowLevelFlagsForAttempt = androidInitProfile.UseThreadlessUpdate
				? FMOD.INITFLAGS.STREAM_FROM_UPDATE | FMOD.INITFLAGS.MIX_FROM_UPDATE
				: FMOD.INITFLAGS.NORMAL;

			for (int i = 0; i < outputCandidates.Length; i++)
			{
				OUTPUTTYPE outputType = outputCandidates[i];

				for (int j = 0; j < maxChannelCandidates.Length; j++)
				{
					int maxChannels = maxChannelCandidates[j];
					string attemptDescription = $"profile={androidInitProfile.Name}; output={outputType}; channels={maxChannels}; studioFlags={studioFlagsForAttempt}; lowLevelFlags={lowLevelFlagsForAttempt}";
					RESULT createResult = FMOD.Studio.System.create(out var candidateSystem);
					if (createResult != RESULT.OK)
					{
						lastResult = createResult;
						lastAttemptDescription = attemptDescription;
						CelestePathBridge.LogWarn("FMOD", $"Failed to create FMOD studio system ({attemptDescription}): {createResult}");
						continue;
					}

					try
					{
						RESULT getLowLevelResult = candidateSystem.getLowLevelSystem(out var lowLevelSystem);
						if (getLowLevelResult != RESULT.OK)
						{
							lastResult = getLowLevelResult;
							lastAttemptDescription = attemptDescription;
							CelestePathBridge.LogWarn("FMOD", $"Failed to get FMOD low-level system ({attemptDescription}): {getLowLevelResult}");
							continue;
						}

						if (!versionLogged && lowLevelSystem.getVersion(out uint runtimeVersion) == RESULT.OK)
						{
							versionLogged = true;
							CelestePathBridge.LogInfo("FMOD", $"FMOD runtime version detected: 0x{runtimeVersion:X}; wrapper expects 0x{VERSION.number:X}");
							if (runtimeVersion != VERSION.number)
							{
								throw new Exception($"FMOD runtime/wrapper version mismatch: runtime=0x{runtimeVersion:X}; expected=0x{VERSION.number:X}");
							}
						}

						if (OperatingSystem.IsAndroid() && outputType != OUTPUTTYPE.AUTODETECT)
						{
							RESULT setOutputResult = lowLevelSystem.setOutput(outputType);
							if (setOutputResult != RESULT.OK)
							{
								lastResult = setOutputResult;
								lastAttemptDescription = attemptDescription;
								CelestePathBridge.LogWarn("FMOD", $"Failed to set FMOD output mode '{outputType}' on Android ({attemptDescription}): {setOutputResult}");
								continue;
							}
						}

						ApplyAndroidDeviceAudioHints(lowLevelSystem, outputType, androidInitProfile.ApplySoftwareFormat, androidInitProfile.ApplyDspBuffer, androidInitProfile.Name);

						RESULT initializeResult = candidateSystem.initialize(maxChannels, studioFlagsForAttempt, lowLevelFlagsForAttempt, IntPtr.Zero);
						if (initializeResult == RESULT.OK)
						{
							system = candidateSystem;
							candidateSystem = null;
							if (lowLevelSystem.getOutput(out var output) == RESULT.OK)
							{
								CelestePathBridge.LogInfo("FMOD", $"FMOD core output active: {output}; profile={androidInitProfile.Name}; channels={maxChannels}; lowLevelFlags={lowLevelFlagsForAttempt}; studioFlags={studioFlagsForAttempt}");
							}

							return;
						}

						lastResult = initializeResult;
						lastAttemptDescription = attemptDescription;
						CelestePathBridge.LogWarn("FMOD", $"FMOD init attempt failed ({attemptDescription}): {initializeResult}");
						int retryDelayMs = GetAndroidRetryDelayMilliseconds(initializeResult);
						if (retryDelayMs > 0)
						{
							Thread.Sleep(retryDelayMs);
						}
					}
					finally
					{
						if (candidateSystem != null)
						{
							candidateSystem.release();
						}
					}
				}
			}
		}

		throw new Exception($"FMOD initialize failed after trying {androidInitProfiles.Length} init profile(s), {outputCandidates.Length} output mode(s) and {maxChannelCandidates.Length} channel profile(s). Last attempt: {lastAttemptDescription}. Last error: {lastResult}");
	}

	private static int GetAndroidRetryDelayMilliseconds(RESULT result)
	{
		if (!OperatingSystem.IsAndroid())
		{
			return 0;
		}

		return result switch
		{
			RESULT.ERR_INTERNAL => 80,
			RESULT.ERR_OUTPUT_INIT => 60,
			RESULT.ERR_INITIALIZATION => 50,
			RESULT.ERR_NOTREADY => 40,
			_ => 15
		};
	}

	private readonly struct AndroidInitProfile
	{
		public readonly string Name;

		public readonly bool ApplySoftwareFormat;

		public readonly bool ApplyDspBuffer;

		public readonly bool UseThreadlessUpdate;

		public AndroidInitProfile(string name, bool applySoftwareFormat, bool applyDspBuffer, bool useThreadlessUpdate)
		{
			Name = name;
			ApplySoftwareFormat = applySoftwareFormat;
			ApplyDspBuffer = applyDspBuffer;
			UseThreadlessUpdate = useThreadlessUpdate;
		}
	}

	private static AndroidInitProfile[] GetAndroidInitProfiles()
	{
		if (!OperatingSystem.IsAndroid())
		{
			return new AndroidInitProfile[1]
			{
				new AndroidInitProfile("default", applySoftwareFormat: false, applyDspBuffer: false, useThreadlessUpdate: false)
			};
		}

		return new AndroidInitProfile[3]
		{
			new AndroidInitProfile("android-java-hints", applySoftwareFormat: true, applyDspBuffer: true, useThreadlessUpdate: false),
			new AndroidInitProfile("android-threadless-hints", applySoftwareFormat: true, applyDspBuffer: true, useThreadlessUpdate: true),
			new AndroidInitProfile("android-baseline", applySoftwareFormat: false, applyDspBuffer: false, useThreadlessUpdate: false)
		};
	}

	private static OUTPUTTYPE[] GetOutputCandidates()
	{
		if (!OperatingSystem.IsAndroid())
		{
			return new OUTPUTTYPE[1] { OUTPUTTYPE.AUTODETECT };
		}

		if (AudioRuntimePolicy.TryGetAndroidDeviceAudioHints(out _, out _, out _, out bool bluetoothOn, out _) && bluetoothOn)
		{
			return new OUTPUTTYPE[2]
			{
				OUTPUTTYPE.AUDIOTRACK,
				OUTPUTTYPE.AUTODETECT
			};
		}

		return new OUTPUTTYPE[2]
		{
			OUTPUTTYPE.AUTODETECT,
			OUTPUTTYPE.AUDIOTRACK
		};
	}

	private static int[] GetMaxChannelCandidates()
	{
		if (!OperatingSystem.IsAndroid())
		{
			return new int[1] { 1024 };
		}

		if (AndroidRuntimePolicy.IsLowMemoryModeEnabled())
		{
			return new int[5] { 128, 64, 32, 16, 8 };
		}

		return new int[5] { 256, 128, 64, 32, 16 };
	}

	private static void ApplyAndroidDeviceAudioHints(FMOD.System lowLevelSystem, OUTPUTTYPE outputType, bool applySoftwareFormat, bool applyDspBuffer, string profileName)
	{
		if (!OperatingSystem.IsAndroid())
		{
			return;
		}

		if (!applySoftwareFormat && !applyDspBuffer)
		{
			return;
		}

		if (!AudioRuntimePolicy.TryGetAndroidDeviceAudioHints(out int outputSampleRate, out int outputBlockSize, out bool supportsLowLatency, out bool bluetoothOn, out bool javaBridgeReady))
		{
			return;
		}

		int driverSampleRate = 0;
		SPEAKERMODE driverSpeakerMode = SPEAKERMODE.DEFAULT;
		int driverSpeakerModeChannels = 0;
		TryGetAndroidDriverDefaults(lowLevelSystem, out driverSampleRate, out driverSpeakerMode, out driverSpeakerModeChannels);

		int appliedSampleRate = 0;
		SPEAKERMODE appliedSpeakerMode = SPEAKERMODE.DEFAULT;
		uint appliedDspBuffer = 0u;

		if (applySoftwareFormat)
		{
			int targetSampleRate = (outputSampleRate > 0) ? outputSampleRate : driverSampleRate;
			SPEAKERMODE targetSpeakerMode = ResolveTargetSpeakerMode(driverSpeakerMode, outputType);
			if (targetSampleRate > 0 || targetSpeakerMode != SPEAKERMODE.DEFAULT)
			{
				RESULT result = lowLevelSystem.setSoftwareFormat(targetSampleRate, targetSpeakerMode, 0);
				if (result != RESULT.OK)
				{
					CelestePathBridge.LogWarn("FMOD", $"Failed to apply Android software format hint ({targetSampleRate}Hz/{targetSpeakerMode}) for profile '{profileName}': {result}");
				}
				else
				{
					appliedSampleRate = targetSampleRate;
					appliedSpeakerMode = targetSpeakerMode;
				}
			}
		}

		if (applyDspBuffer && TryResolveAndroidDspBufferLength(outputBlockSize, supportsLowLatency, out uint dspBufferLength))
		{
			RESULT result2 = lowLevelSystem.setDSPBufferSize(dspBufferLength, 4);
			if (result2 != RESULT.OK)
			{
				CelestePathBridge.LogWarn("FMOD", $"Failed to apply Android DSP buffer hint ({dspBufferLength}) for profile '{profileName}': {result2}");
			}
			else
			{
				appliedDspBuffer = dspBufferLength;
			}
		}

		CelestePathBridge.LogInfo("FMOD", $"Applying Android audio hints profile={profileName}: javaReady={javaBridgeReady}; sampleRate={outputSampleRate}; blockSize={outputBlockSize}; lowLatency={supportsLowLatency}; bluetooth={bluetoothOn}; driverRate={driverSampleRate}; driverMode={driverSpeakerMode}; driverChannels={driverSpeakerModeChannels}; appliedSampleRate={appliedSampleRate}; appliedSpeakerMode={appliedSpeakerMode}; appliedDspBuffer={appliedDspBuffer}");
	}

	private static SPEAKERMODE ResolveTargetSpeakerMode(SPEAKERMODE driverSpeakerMode, OUTPUTTYPE outputType)
	{
		if (outputType == OUTPUTTYPE.AUDIOTRACK || outputType == OUTPUTTYPE.OPENSL)
		{
			return SPEAKERMODE.STEREO;
		}

		return (driverSpeakerMode != SPEAKERMODE.DEFAULT) ? driverSpeakerMode : SPEAKERMODE.DEFAULT;
	}

	private static bool TryResolveAndroidDspBufferLength(int outputBlockSize, bool supportsLowLatency, out uint dspBufferLength)
	{
		dspBufferLength = 0u;
		if (outputBlockSize <= 0)
		{
			return false;
		}

		if (supportsLowLatency)
		{
			dspBufferLength = (uint)NormalizeDspBlockSize(outputBlockSize);
			return true;
		}

		int clamped = Math.Clamp(outputBlockSize, 64, 4096);
		clamped = RoundUpToMultiple(clamped, 4);
		if (clamped > 4096)
		{
			clamped = 4096;
		}

		dspBufferLength = (uint)clamped;
		return true;
	}

	private static int RoundUpToMultiple(int value, int multiple)
	{
		if (multiple <= 1)
		{
			return value;
		}

		int remainder = value % multiple;
		if (remainder == 0)
		{
			return value;
		}

		return value + multiple - remainder;
	}

	private static void TryGetAndroidDriverDefaults(FMOD.System lowLevelSystem, out int driverSampleRate, out SPEAKERMODE driverSpeakerMode, out int driverSpeakerModeChannels)
	{
		driverSampleRate = 0;
		driverSpeakerMode = SPEAKERMODE.DEFAULT;
		driverSpeakerModeChannels = 0;
		if (lowLevelSystem.getNumDrivers(out int numDrivers) != RESULT.OK || numDrivers <= 0)
		{
			return;
		}

		StringBuilder name = new StringBuilder(256);
		if (lowLevelSystem.getDriverInfo(0, name, name.Capacity, out _, out driverSampleRate, out driverSpeakerMode, out driverSpeakerModeChannels) != RESULT.OK)
		{
			return;
		}

		if (!androidDriverInfoLogged)
		{
			androidDriverInfoLogged = true;
			CelestePathBridge.LogInfo("FMOD", $"Android FMOD driver defaults: name={name}; sampleRate={driverSampleRate}; speakerMode={driverSpeakerMode}; channels={driverSpeakerModeChannels}");
		}
	}

	private static int NormalizeDspBlockSize(int blockSize)
	{
		int clamped = Math.Clamp(blockSize, 64, 4096);
		int normalized = 64;
		while (normalized < clamped && normalized < 4096)
		{
			normalized <<= 1;
		}

		if (normalized > 4096)
		{
			normalized = 4096;
		}

		return normalized;
	}

	private static bool ShouldPreloadFmodSamplesOnCurrentPlatform()
	{
		if (!OperatingSystem.IsAndroid())
		{
			return true;
		}

		bool enabled = AudioRuntimePolicy.IsFmodSamplePreloadEnabledOnAndroid();
		if (!androidSamplePreloadPolicyLogged)
		{
			androidSamplePreloadPolicyLogged = true;
			if (enabled)
			{
				CelestePathBridge.LogInfo("FMOD", $"Android FMOD sample preload enabled by switch '{AudioRuntimePolicy.EnableFmodSamplePreloadOnAndroidSwitch}'.");
			}
			else
			{
				CelestePathBridge.LogWarn("FMOD", "Android FMOD sample preload disabled by default for compatibility. Events will stream sample data on demand.");
			}
		}

		return enabled;
	}

	public static void Update()
	{
		if (IsSystemReady)
		{
			CheckFmod(system.update());
		}
	}

	public static void Unload()
	{
		ready = false;
		cachedEventDescriptions.Clear();
		if (system != null)
		{
			CheckFmod(system.unloadAll());
			CheckFmod(system.release());
			system = null;
		}
	}

	public static void ActivateFallback(string reason, Exception exception = null)
	{
		FallbackSilentMode = true;
		ready = false;
		LastInitError = reason;
		if (string.Equals(reason, "AUDIO_DISABLED_BY_POLICY_ANDROID", StringComparison.Ordinal))
		{
			CelestePathBridge.LogWarn("FMOD", $"AUDIO_FALLBACK_ACTIVE: {reason}");
		}
		else
		{
			CelestePathBridge.LogError("FMOD", $"AUDIO_FALLBACK_ACTIVE: {reason}");
		}

		if (exception != null)
		{
			CelestePathBridge.LogError("FMOD", exception.ToString());
		}

		FMOD.Studio.System system2 = system;
		system = null;
		if (system2 != null)
		{
			if (OperatingSystem.IsAndroid())
			{
				CelestePathBridge.LogWarn("FMOD", "Skipping native FMOD release during fallback on Android to avoid runtime instability after failed bank load.");
			}
			else
			{
				try
				{
					CheckFmod(system2.unloadAll());
					CheckFmod(system2.release());
				}
				catch (Exception ex)
				{
					CelestePathBridge.LogError("FMOD", "Failed while releasing FMOD during fallback: " + ex);
				}
			}
		}

		Banks.Master = default(Bank);
		Banks.Music = default(Bank);
		Banks.Sfxs = default(Bank);
		Banks.UI = default(Bank);
		Banks.DlcMusic = default(Bank);
		Banks.DlcSfxs = default(Bank);
		currentMusicEvent = null;
		currentAltMusicEvent = null;
		currentAmbientEvent = null;
		mainDownSnapshot = null;
		musicUnderwaterSnapshot = null;

		cachedEventDescriptions.Clear();
	}

	public static void SetListenerPosition(Vector3 forward, Vector3 up, Vector3 position)
	{
		if (!IsSystemReady)
		{
			return;
		}

		FMOD.Studio._3D_ATTRIBUTES attributes = default(FMOD.Studio._3D_ATTRIBUTES);
		attributes.forward.x = forward.X;
		attributes.forward.z = forward.Y;
		attributes.forward.z = forward.Z;
		attributes.up.x = up.X;
		attributes.up.y = up.Y;
		attributes.up.z = up.Z;
		attributes.position.x = position.X;
		attributes.position.y = position.Y;
		attributes.position.z = position.Z;
		system.setListenerAttributes(0, attributes);
	}

	public static void SetCamera(Camera camera)
	{
		currentCamera = camera;
	}

	internal static void CheckFmod(RESULT result)
	{
		if (result != RESULT.OK)
		{
			throw new Exception("FMOD Failed: " + result);
		}
	}

	public static EventInstance Play(string path)
	{
		EventInstance eventInstance = CreateInstance(path);
		if (eventInstance != null)
		{
			eventInstance.start();
			eventInstance.release();
		}
		return eventInstance;
	}

	public static EventInstance Play(string path, string param, float value)
	{
		EventInstance eventInstance = CreateInstance(path);
		if (eventInstance != null)
		{
			SetParameter(eventInstance, param, value);
			eventInstance.start();
			eventInstance.release();
		}
		return eventInstance;
	}

	public static EventInstance Play(string path, Vector2 position)
	{
		EventInstance eventInstance = CreateInstance(path, position);
		if (eventInstance != null)
		{
			eventInstance.start();
			eventInstance.release();
		}
		return eventInstance;
	}

	public static EventInstance Play(string path, Vector2 position, string param, float value)
	{
		EventInstance eventInstance = CreateInstance(path, position);
		if (eventInstance != null)
		{
			if (param != null)
			{
				eventInstance.setParameterValue(param, value);
			}
			eventInstance.start();
			eventInstance.release();
		}
		return eventInstance;
	}

	public static EventInstance Play(string path, Vector2 position, string param, float value, string param2, float value2)
	{
		EventInstance eventInstance = CreateInstance(path, position);
		if (eventInstance != null)
		{
			if (param != null)
			{
				eventInstance.setParameterValue(param, value);
			}
			if (param2 != null)
			{
				eventInstance.setParameterValue(param2, value2);
			}
			eventInstance.start();
			eventInstance.release();
		}
		return eventInstance;
	}

	public static EventInstance Loop(string path)
	{
		EventInstance eventInstance = CreateInstance(path);
		if (eventInstance != null)
		{
			eventInstance.start();
		}
		return eventInstance;
	}

	public static EventInstance Loop(string path, string param, float value)
	{
		EventInstance eventInstance = CreateInstance(path);
		if (eventInstance != null)
		{
			eventInstance.setParameterValue(param, value);
			eventInstance.start();
		}
		return eventInstance;
	}

	public static EventInstance Loop(string path, Vector2 position)
	{
		EventInstance eventInstance = CreateInstance(path, position);
		if (eventInstance != null)
		{
			eventInstance.start();
		}
		return eventInstance;
	}

	public static EventInstance Loop(string path, Vector2 position, string param, float value)
	{
		EventInstance eventInstance = CreateInstance(path, position);
		if (eventInstance != null)
		{
			eventInstance.setParameterValue(param, value);
			eventInstance.start();
		}
		return eventInstance;
	}

	public static void Pause(EventInstance instance)
	{
		if (instance != null)
		{
			instance.setPaused(paused: true);
		}
	}

	public static void Resume(EventInstance instance)
	{
		if (instance != null)
		{
			instance.setPaused(paused: false);
		}
	}

	public static void Position(EventInstance instance, Vector2 position)
	{
		if (instance != null)
		{
			Vector2 vector = Vector2.Zero;
			if (currentCamera != null)
			{
				vector = currentCamera.Position + new Vector2(320f, 180f) / 2f;
			}
			float num = position.X - vector.X;
			if (SaveData.Instance != null && SaveData.Instance.Assists.MirrorMode)
			{
				num = 0f - num;
			}
			attributes3d.position.x = num;
			attributes3d.position.y = position.Y - vector.Y;
			attributes3d.position.z = 0f;
			instance.set3DAttributes(attributes3d);
		}
	}

	public static void SetParameter(EventInstance instance, string param, float value)
	{
		if (instance != null)
		{
			instance.setParameterValue(param, value);
		}
	}

	public static void Stop(EventInstance instance, bool allowFadeOut = true)
	{
		if (instance != null)
		{
			instance.stop((!allowFadeOut) ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
			instance.release();
		}
	}

	public static EventInstance CreateInstance(string path, Vector2? position = null)
	{
		EventDescription eventDescription = GetEventDescription(path);
		if (eventDescription != null)
		{
			eventDescription.createInstance(out var instance);
			eventDescription.is3D(out var is3D);
			if (is3D && position.HasValue)
			{
				Position(instance, position.Value);
			}
			return instance;
		}
		return null;
	}

	public static EventDescription GetEventDescription(string path)
	{
		if (!IsSystemReady)
		{
			return null;
		}

		EventDescription value = null;
		if (path != null && !cachedEventDescriptions.TryGetValue(path, out value))
		{
			RESULT rESULT = system.getEvent(path, out value);
			switch (rESULT)
			{
			case RESULT.OK:
				if (ShouldPreloadFmodSamplesOnCurrentPlatform())
				{
					RESULT result = value.loadSampleData();
					if (result != RESULT.OK)
					{
						CelestePathBridge.LogWarn("FMOD", $"Event sample preload failed for '{path}': {result}. Continuing with streaming fallback.");
					}
				}
				cachedEventDescriptions.Add(path, value);
				break;
			default:
				throw new Exception("FMOD getEvent failed: " + rESULT);
			case RESULT.ERR_EVENT_NOTFOUND:
				break;
			}
		}
		return value;
	}

	public static void ReleaseUnusedDescriptions()
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, EventDescription> cachedEventDescription in cachedEventDescriptions)
		{
			cachedEventDescription.Value.getInstanceCount(out var count);
			if (count <= 0)
			{
				cachedEventDescription.Value.unloadSampleData();
				list.Add(cachedEventDescription.Key);
			}
		}
		foreach (string item in list)
		{
			cachedEventDescriptions.Remove(item);
		}
	}

	public static string GetEventName(EventInstance instance)
	{
		if (instance != null)
		{
			instance.getDescription(out var description);
			if (description != null)
			{
				string path = "";
				description.getPath(out path);
				return path;
			}
		}
		return "";
	}

	public static bool IsPlaying(EventInstance instance)
	{
		if (instance != null)
		{
			instance.getPlaybackState(out var state);
			if (state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING)
			{
				return true;
			}
		}
		return false;
	}

	public static bool BusPaused(string path, bool? pause = null)
	{
		bool paused = false;
		if (system != null && system.getBus(path, out var bus) == RESULT.OK)
		{
			if (pause.HasValue)
			{
				bus.setPaused(pause.Value);
			}
			bus.getPaused(out paused);
		}
		return paused;
	}

	public static bool BusMuted(string path, bool? mute)
	{
		bool paused = false;
		if (system != null && system.getBus(path, out var bus) == RESULT.OK)
		{
			if (mute.HasValue)
			{
				bus.setMute(mute.Value);
			}
			bus.getPaused(out paused);
		}
		return paused;
	}

	public static void BusStopAll(string path, bool immediate = false)
	{
		if (system != null && system.getBus(path, out var bus) == RESULT.OK)
		{
			bus.stopAllEvents(immediate ? STOP_MODE.IMMEDIATE : STOP_MODE.ALLOWFADEOUT);
		}
	}

	public static float VCAVolume(string path, float? volume = null)
	{
		if (!IsSystemReady)
		{
			return 0f;
		}

		VCA vca;
		RESULT vCA = system.getVCA(path, out vca);
		float volume2 = 1f;
		float finalvolume = 1f;
		if (vCA == RESULT.OK)
		{
			if (volume.HasValue)
			{
				vca.setVolume(volume.Value);
			}
			vca.getVolume(out volume2, out finalvolume);
		}
		return volume2;
	}

	public static EventInstance CreateSnapshot(string name, bool start = true)
	{
		if (!IsSystemReady)
		{
			return null;
		}

		system.getEvent(name, out var _event);
		if (_event == null)
		{
			throw new Exception("Snapshot " + name + " doesn't exist");
		}
		_event.createInstance(out var instance);
		if (start)
		{
			instance.start();
		}
		return instance;
	}

	public static void ResumeSnapshot(EventInstance snapshot)
	{
		if (snapshot != null)
		{
			snapshot.start();
		}
	}

	public static bool IsSnapshotRunning(EventInstance snapshot)
	{
		if (snapshot != null)
		{
			snapshot.getPlaybackState(out var state);
			if (state != PLAYBACK_STATE.PLAYING && state != PLAYBACK_STATE.STARTING)
			{
				return state == PLAYBACK_STATE.SUSTAINING;
			}
			return true;
		}
		return false;
	}

	public static void EndSnapshot(EventInstance snapshot)
	{
		if (snapshot != null)
		{
			snapshot.stop(STOP_MODE.ALLOWFADEOUT);
		}
	}

	public static void ReleaseSnapshot(EventInstance snapshot)
	{
		if (snapshot != null)
		{
			snapshot.stop(STOP_MODE.ALLOWFADEOUT);
			snapshot.release();
		}
	}

	public static bool SetMusic(string path, bool startPlaying = true, bool allowFadeOut = true)
	{
		if (string.IsNullOrEmpty(path) || path == "null")
		{
			Stop(currentMusicEvent, allowFadeOut);
			currentMusicEvent = null;
			CurrentMusic = "";
		}
		else if (!CurrentMusic.Equals(path, StringComparison.OrdinalIgnoreCase))
		{
			Stop(currentMusicEvent, allowFadeOut);
			EventInstance eventInstance = CreateInstance(path);
			if (eventInstance != null && startPlaying)
			{
				eventInstance.start();
			}
			currentMusicEvent = eventInstance;
			CurrentMusic = GetEventName(eventInstance);
			return true;
		}
		return false;
	}

	public static bool SetAmbience(string path, bool startPlaying = true)
	{
		if (string.IsNullOrEmpty(path) || path == "null")
		{
			Stop(currentAmbientEvent);
			currentAmbientEvent = null;
		}
		else if (!GetEventName(currentAmbientEvent).Equals(path, StringComparison.OrdinalIgnoreCase))
		{
			Stop(currentAmbientEvent);
			EventInstance eventInstance = CreateInstance(path);
			if (eventInstance != null && startPlaying)
			{
				eventInstance.start();
			}
			currentAmbientEvent = eventInstance;
			return true;
		}
		return false;
	}

	public static void SetMusicParam(string path, float value)
	{
		if (currentMusicEvent != null)
		{
			currentMusicEvent.setParameterValue(path, value);
		}
	}

	public static void SetAltMusic(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			EndSnapshot(mainDownSnapshot);
			Stop(currentAltMusicEvent);
			currentAltMusicEvent = null;
		}
		else if (!GetEventName(currentAltMusicEvent).Equals(path, StringComparison.OrdinalIgnoreCase))
		{
			StartMainDownSnapshot();
			Stop(currentAltMusicEvent);
			currentAltMusicEvent = Loop(path);
		}
	}

	private static void StartMainDownSnapshot()
	{
		if (mainDownSnapshot == null)
		{
			mainDownSnapshot = CreateSnapshot("snapshot:/music_mains_mute");
		}
		else
		{
			ResumeSnapshot(mainDownSnapshot);
		}
	}

	private static void EndMainDownSnapshot()
	{
		EndSnapshot(mainDownSnapshot);
	}
}
