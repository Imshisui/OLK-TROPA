using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste;

public static class Input
{
	public enum PrefixMode
	{
		Latest,
		Attached
	}

	private static int gamepad = 0;

	public static readonly int MaxBindings = 8;

	public static VirtualButton ESC;

	public static VirtualButton Pause;

	public static VirtualButton MenuLeft;

	public static VirtualButton MenuRight;

	public static VirtualButton MenuUp;

	public static VirtualButton MenuDown;

	public static VirtualButton MenuConfirm;

	public static VirtualButton MenuCancel;

	public static VirtualButton MenuJournal;

	public static VirtualButton QuickRestart;

	public static VirtualIntegerAxis MoveX;

	public static VirtualIntegerAxis MoveY;

	public static VirtualIntegerAxis GliderMoveY;

	public static VirtualJoystick Aim;

	public static VirtualJoystick Feather;

	public static VirtualJoystick MountainAim;

	public static VirtualButton Jump;

	public static VirtualButton Dash;

	public static VirtualButton Grab;

	public static VirtualButton Talk;

	public static VirtualButton CrouchDash;

	private static bool grabToggle;

	public static Vector2 LastAim;

	public static string OverrideInputPrefix = null;

	private static string preferredControllerPrefix = "xb1";

	private static string preferredControllerPromptStyle = "alt";

	private static Dictionary<Keys, string> keyNameLookup = new Dictionary<Keys, string>();

	private static Dictionary<Buttons, string> buttonNameLookup = new Dictionary<Buttons, string>();

	private static Dictionary<string, Dictionary<string, string>> guiPathLookup = new Dictionary<string, Dictionary<string, string>>();

	private static readonly Dictionary<string, MTexture> externalPromptLookup = new Dictionary<string, MTexture>(StringComparer.Ordinal);

	private static readonly HashSet<string> externalPromptMisses = new HashSet<string>(StringComparer.Ordinal);

	private static readonly Dictionary<string, string[]> xboxAltPromptLookup = new Dictionary<string, string[]>(StringComparer.Ordinal)
	{
		{ "A", new string[4] { "CONTROLES CELESTE/pular.dat", "controls_custom/pular.dat", "CONTROLES CELESTE/pular.png", "controls_custom/pular.png" } },
		{ "B", new string[4] { "CONTROLES CELESTE/dash-back.dat", "controls_custom/dash-back.dat", "CONTROLES CELESTE/dash-back.png", "controls_custom/dash-back.png" } },
		{ "X", new string[4] { "CONTROLES CELESTE/dash.dat", "controls_custom/dash.dat", "CONTROLES CELESTE/dash.png", "controls_custom/dash.png" } },
		{ "LeftTrigger", new string[4] { "CONTROLES CELESTE/agarrar.dat", "controls_custom/agarrar.dat", "CONTROLES CELESTE/agarrar.png", "controls_custom/agarrar.png" } },
		{ "RightTrigger", new string[4] { "CONTROLES CELESTE/agarrar2.dat", "controls_custom/agarrar2.dat", "CONTROLES CELESTE/agarrar2.png", "controls_custom/agarrar2.png" } },
		{ "Start", new string[4] { "CONTROLES CELESTE/pause-start.dat", "controls_custom/pause-start.dat", "CONTROLES CELESTE/pause-start.png", "controls_custom/pause-start.png" } },
		{ "Back", new string[4] { "CONTROLES CELESTE/select.dat", "controls_custom/select.dat", "CONTROLES CELESTE/select.png", "controls_custom/select.png" } }
	};

	private static float[] rumbleStrengths = new float[4] { 0.15f, 0.4f, 1f, 0.05f };

	private static float[] rumbleLengths = new float[5] { 0.1f, 0.25f, 0.5f, 1f, 2f };

	public static int Gamepad
	{
		get
		{
			return gamepad;
		}
		set
		{
			int num = Calc.Clamp(value, 0, MInput.GamePads.Length - 1);
			if (gamepad != num)
			{
				gamepad = num;
				Initialize();
			}
		}
	}

	public static bool GrabCheck => Settings.Instance.GrabMode switch
	{
		GrabModes.Invert => !Grab.Check, 
		GrabModes.Toggle => grabToggle, 
		_ => Grab.Check, 
	};

	public static bool DashPressed
	{
		get
		{
			CrouchDashModes crouchDashMode = Settings.Instance.CrouchDashMode;
			if (crouchDashMode == CrouchDashModes.Press || crouchDashMode != CrouchDashModes.Hold)
			{
				return Dash.Pressed;
			}
			if (Dash.Pressed)
			{
				return !CrouchDash.Check;
			}
			return false;
		}
	}

	public static bool CrouchDashPressed
	{
		get
		{
			CrouchDashModes crouchDashMode = Settings.Instance.CrouchDashMode;
			if (crouchDashMode == CrouchDashModes.Press || crouchDashMode != CrouchDashModes.Hold)
			{
				return CrouchDash.Pressed;
			}
			if (Dash.Pressed)
			{
				return CrouchDash.Check;
			}
			return false;
		}
	}

	public static void Initialize()
	{
		bool flag = false;
		if (MoveX != null)
		{
			flag = MoveX.Inverted;
		}
		Deregister();
		MoveX = new VirtualIntegerAxis(Settings.Instance.Left, Settings.Instance.LeftMoveOnly, Settings.Instance.Right, Settings.Instance.RightMoveOnly, Gamepad, 0.3f);
		MoveX.Inverted = flag;
		MoveY = new VirtualIntegerAxis(Settings.Instance.Up, Settings.Instance.UpMoveOnly, Settings.Instance.Down, Settings.Instance.DownMoveOnly, Gamepad, 0.7f);
		GliderMoveY = new VirtualIntegerAxis(Settings.Instance.Up, Settings.Instance.UpMoveOnly, Settings.Instance.Down, Settings.Instance.DownMoveOnly, Gamepad, 0.3f);
		Aim = new VirtualJoystick(Settings.Instance.Up, Settings.Instance.UpDashOnly, Settings.Instance.Down, Settings.Instance.DownDashOnly, Settings.Instance.Left, Settings.Instance.LeftDashOnly, Settings.Instance.Right, Settings.Instance.RightDashOnly, Gamepad, 0.25f);
		Aim.InvertedX = flag;
		Feather = new VirtualJoystick(Settings.Instance.Up, Settings.Instance.UpMoveOnly, Settings.Instance.Down, Settings.Instance.DownMoveOnly, Settings.Instance.Left, Settings.Instance.LeftMoveOnly, Settings.Instance.Right, Settings.Instance.RightMoveOnly, Gamepad, 0.25f);
		Feather.InvertedX = flag;
		Jump = new VirtualButton(Settings.Instance.Jump, Gamepad, 0.08f, 0.2f);
		Dash = new VirtualButton(Settings.Instance.Dash, Gamepad, 0.08f, 0.2f);
		Talk = new VirtualButton(Settings.Instance.Talk, Gamepad, 0.08f, 0.2f);
		Grab = new VirtualButton(Settings.Instance.Grab, Gamepad, 0f, 0.2f);
		CrouchDash = new VirtualButton(Settings.Instance.DemoDash, Gamepad, 0.08f, 0.2f);
		Binding binding = new Binding();
		binding.Add(Keys.A);
		binding.Add(Buttons.RightThumbstickLeft);
		Binding binding2 = new Binding();
		binding2.Add(Keys.D);
		binding2.Add(Buttons.RightThumbstickRight);
		Binding binding3 = new Binding();
		binding3.Add(Keys.W);
		binding3.Add(Buttons.RightThumbstickUp);
		Binding binding4 = new Binding();
		binding4.Add(Keys.S);
		binding4.Add(Buttons.RightThumbstickDown);
		MountainAim = new VirtualJoystick(binding3, binding4, binding, binding2, Gamepad, 0.1f);
		Binding binding5 = new Binding();
		binding5.Add(Keys.Escape);
		ESC = new VirtualButton(binding5, Gamepad, 0.1f, 0.2f);
		Pause = new VirtualButton(Settings.Instance.Pause, Gamepad, 0.1f, 0.2f);
		QuickRestart = new VirtualButton(Settings.Instance.QuickRestart, Gamepad, 0.1f, 0.2f);
		MenuLeft = new VirtualButton(Settings.Instance.MenuLeft, Gamepad, 0f, 0.4f);
		MenuLeft.SetRepeat(0.4f, 0.1f);
		MenuRight = new VirtualButton(Settings.Instance.MenuRight, Gamepad, 0f, 0.4f);
		MenuRight.SetRepeat(0.4f, 0.1f);
		MenuUp = new VirtualButton(Settings.Instance.MenuUp, Gamepad, 0f, 0.4f);
		MenuUp.SetRepeat(0.4f, 0.1f);
		MenuDown = new VirtualButton(Settings.Instance.MenuDown, Gamepad, 0f, 0.4f);
		MenuDown.SetRepeat(0.4f, 0.1f);
		MenuJournal = new VirtualButton(Settings.Instance.Journal, Gamepad, 0f, 0.2f);
		MenuConfirm = new VirtualButton(Settings.Instance.Confirm, Gamepad, 0f, 0.2f);
		MenuCancel = new VirtualButton(Settings.Instance.Cancel, Gamepad, 0f, 0.2f);
	}

	public static void Deregister()
	{
		if (ESC != null)
		{
			ESC.Deregister();
		}
		if (Pause != null)
		{
			Pause.Deregister();
		}
		if (MenuLeft != null)
		{
			MenuLeft.Deregister();
		}
		if (MenuRight != null)
		{
			MenuRight.Deregister();
		}
		if (MenuUp != null)
		{
			MenuUp.Deregister();
		}
		if (MenuDown != null)
		{
			MenuDown.Deregister();
		}
		if (MenuConfirm != null)
		{
			MenuConfirm.Deregister();
		}
		if (MenuCancel != null)
		{
			MenuCancel.Deregister();
		}
		if (MenuJournal != null)
		{
			MenuJournal.Deregister();
		}
		if (QuickRestart != null)
		{
			QuickRestart.Deregister();
		}
		if (MoveX != null)
		{
			MoveX.Deregister();
		}
		if (MoveY != null)
		{
			MoveY.Deregister();
		}
		if (GliderMoveY != null)
		{
			GliderMoveY.Deregister();
		}
		if (Aim != null)
		{
			Aim.Deregister();
		}
		if (MountainAim != null)
		{
			MountainAim.Deregister();
		}
		if (Jump != null)
		{
			Jump.Deregister();
		}
		if (Dash != null)
		{
			Dash.Deregister();
		}
		if (Grab != null)
		{
			Grab.Deregister();
		}
		if (Talk != null)
		{
			Talk.Deregister();
		}
		if (CrouchDash != null)
		{
			CrouchDash.Deregister();
		}
	}

	public static bool AnyGamepadConfirmPressed(out int gamepadIndex)
	{
		bool result = false;
		gamepadIndex = -1;
		int gamepadIndex2 = MenuConfirm.GamepadIndex;
		for (int i = 0; i < MInput.GamePads.Length; i++)
		{
			MenuConfirm.GamepadIndex = i;
			if (MenuConfirm.Pressed)
			{
				result = true;
				gamepadIndex = i;
				break;
			}
		}
		MenuConfirm.GamepadIndex = gamepadIndex2;
		return result;
	}

	public static void Rumble(RumbleStrength strength, RumbleLength length)
	{
		float num = 1f;
		if (Settings.Instance.Rumble == RumbleAmount.Half)
		{
			num = 0.5f;
		}
		if (Settings.Instance.Rumble != RumbleAmount.Off && MInput.GamePads.Length != 0 && !MInput.Disabled)
		{
			MInput.GamePads[Gamepad].Rumble(rumbleStrengths[(int)strength] * num, rumbleLengths[(int)length]);
		}
	}

	public static void RumbleSpecific(float strength, float time)
	{
		float num = 1f;
		if (Settings.Instance.Rumble == RumbleAmount.Half)
		{
			num = 0.5f;
		}
		if (Settings.Instance.Rumble != RumbleAmount.Off && MInput.GamePads.Length != 0 && !MInput.Disabled)
		{
			MInput.GamePads[Gamepad].Rumble(strength * num, time);
		}
	}

	public static void UpdateGrab()
	{
		if (Settings.Instance.GrabMode == GrabModes.Toggle && Grab.Pressed)
		{
			grabToggle = !grabToggle;
		}
	}

	public static void ResetGrab()
	{
		grabToggle = false;
	}

	public static Vector2 GetAimVector(Facings defaultFacing = Facings.Right)
	{
		Vector2 value = Aim.Value;
		if (value == Vector2.Zero)
		{
			if (SaveData.Instance != null && SaveData.Instance.Assists.DashAssist)
			{
				return LastAim;
			}
			LastAim = Vector2.UnitX * (float)defaultFacing;
		}
		else if (SaveData.Instance != null && SaveData.Instance.Assists.ThreeSixtyDashing)
		{
			LastAim = value.SafeNormalize();
		}
		else
		{
			float num = value.Angle();
			int num2 = ((num < 0f) ? 1 : 0);
			float num3 = (float)Math.PI / 8f - (float)num2 * 0.08726646f;
			if (Calc.AbsAngleDiff(num, 0f) < num3)
			{
				LastAim = new Vector2(1f, 0f);
			}
			else if (Calc.AbsAngleDiff(num, (float)Math.PI) < num3)
			{
				LastAim = new Vector2(-1f, 0f);
			}
			else if (Calc.AbsAngleDiff(num, -(float)Math.PI / 2f) < num3)
			{
				LastAim = new Vector2(0f, -1f);
			}
			else if (Calc.AbsAngleDiff(num, (float)Math.PI / 2f) < num3)
			{
				LastAim = new Vector2(0f, 1f);
			}
			else
			{
				LastAim = new Vector2(Math.Sign(value.X), Math.Sign(value.Y)).SafeNormalize();
			}
		}
		return LastAim;
	}

	public static string GuiInputPrefix(PrefixMode mode = PrefixMode.Latest)
	{
		if (!string.IsNullOrEmpty(OverrideInputPrefix))
		{
			return OverrideInputPrefix;
		}
		if ((mode != PrefixMode.Latest) ? MInput.GamePads[Gamepad].Attached : MInput.ControllerHasFocus)
		{
			return ResolveControllerPrefix();
		}
		return "keyboard";
	}

	public static void SetPreferredControllerPrefix(string prefix)
	{
		string old = ResolveControllerPrefix();
		if (string.IsNullOrWhiteSpace(prefix))
		{
			preferredControllerPrefix = "xb1";
		}
		else
		{
			preferredControllerPrefix = prefix.Trim();
		}

		if (!string.Equals(old, ResolveControllerPrefix(), StringComparison.OrdinalIgnoreCase))
		{
			ResetExternalPromptCache();
		}
	}

	public static void SetPreferredControllerPromptStyle(string style)
	{
		string old = ResolveControllerPromptStyle();
		if (string.IsNullOrWhiteSpace(style))
		{
			preferredControllerPromptStyle = "alt";
		}
		else
		{
			preferredControllerPromptStyle = style.Trim().ToLowerInvariant();
		}

		if (!string.Equals(old, ResolveControllerPromptStyle(), StringComparison.OrdinalIgnoreCase))
		{
			ResetExternalPromptCache();
		}
	}

	public static bool GuiInputController(PrefixMode mode = PrefixMode.Latest)
	{
		return !GuiInputPrefix(mode).Equals("keyboard");
	}

	public static MTexture GuiButton(VirtualButton button, PrefixMode mode = PrefixMode.Latest, string fallback = "controls/keyboard/oemquestion")
	{
		string prefix = GuiInputPrefix(mode);
		bool num = GuiInputController(mode);
		string value = "";
		if (num)
		{
			using List<Buttons>.Enumerator enumerator = button.Binding.Controller.GetEnumerator();
			if (enumerator.MoveNext())
			{
				Buttons current = enumerator.Current;
				if (!buttonNameLookup.TryGetValue(current, out value))
				{
					buttonNameLookup.Add(current, value = current.ToString());
				}
			}
		}
		else
		{
			Keys key = FirstKey(button);
			if (!keyNameLookup.TryGetValue(key, out value))
			{
				keyNameLookup.Add(key, value = key.ToString());
			}
		}
		MTexture mTexture = GuiTexture(prefix, value);
		if (mTexture == null && fallback != null)
		{
			return GFX.Gui[fallback];
		}
		return mTexture;
	}

	public static MTexture GuiSingleButton(Buttons button, PrefixMode mode = PrefixMode.Latest, string fallback = "controls/keyboard/oemquestion")
	{
		string prefix = ((!GuiInputController(mode)) ? ResolveControllerPrefix() : GuiInputPrefix(mode));
		string value = "";
		if (!buttonNameLookup.TryGetValue(button, out value))
		{
			buttonNameLookup.Add(button, value = button.ToString());
		}
		MTexture mTexture = GuiTexture(prefix, value);
		if (mTexture == null && fallback != null)
		{
			return GFX.Gui[fallback];
		}
		return mTexture;
	}

	public static MTexture GuiKey(Keys key, string fallback = "controls/keyboard/oemquestion")
	{
		if (!keyNameLookup.TryGetValue(key, out var value))
		{
			keyNameLookup.Add(key, value = key.ToString());
		}
		MTexture mTexture = GuiTexture("keyboard", value);
		if (mTexture == null && fallback != null)
		{
			return GFX.Gui[fallback];
		}
		return mTexture;
	}

	public static Buttons FirstButton(VirtualButton button)
	{
		using (List<Buttons>.Enumerator enumerator = button.Binding.Controller.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				return enumerator.Current;
			}
		}
		return Buttons.A;
	}

	public static Keys FirstKey(VirtualButton button)
	{
		foreach (Keys item in button.Binding.Keyboard)
		{
			if (item != Keys.None)
			{
				return item;
			}
		}
		return Keys.None;
	}

	public static MTexture GuiDirection(Vector2 direction)
	{
		int num = Math.Sign(direction.X);
		string input = string.Concat(arg2: Math.Sign(direction.Y), arg0: num, arg1: "x");
		return GuiTexture("directions", input);
	}

	private static MTexture GuiTexture(string prefix, string input)
	{
		if (TryGetExternalPromptTexture(prefix, input, out var externalTexture))
		{
			return externalTexture;
		}

		if (!guiPathLookup.TryGetValue(prefix, out var value))
		{
			guiPathLookup.Add(prefix, value = new Dictionary<string, string>());
		}
		if (!value.TryGetValue(input, out var value2))
		{
			value.Add(input, value2 = "controls/" + prefix + "/" + input);
		}
		if (!GFX.Gui.Has(value2))
		{
			if (prefix != "fallback")
			{
				return GuiTexture("fallback", input);
			}
			return null;
		}
		return GFX.Gui[value2];
	}

	private static string ResolveControllerPrefix()
	{
		if (string.IsNullOrWhiteSpace(preferredControllerPrefix))
		{
			return "xb1";
		}
		return preferredControllerPrefix;
	}

	private static string ResolveControllerPromptStyle()
	{
		if (string.IsNullOrWhiteSpace(preferredControllerPromptStyle))
		{
			return "alt";
		}
		return preferredControllerPromptStyle;
	}

	private static bool TryGetExternalPromptTexture(string prefix, string input, out MTexture texture)
	{
		texture = null;
		if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(input))
		{
			return false;
		}

		string normalizedPrefix = prefix.Trim();
		if (!normalizedPrefix.Equals("xb1", StringComparison.OrdinalIgnoreCase) && !normalizedPrefix.Equals("ps4", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string style = normalizedPrefix.Equals("xb1", StringComparison.OrdinalIgnoreCase) ? ResolveControllerPromptStyle() : "alt";
		string cacheKey = normalizedPrefix.ToLowerInvariant() + "|" + style + "|" + input;

		if (externalPromptLookup.TryGetValue(cacheKey, out texture))
		{
			return texture != null;
		}

		if (externalPromptMisses.Contains(cacheKey))
		{
			return false;
		}

		if (!TryGetPromptCandidates(normalizedPrefix, style, input, out string[] candidates) || candidates.Length <= 0)
		{
			externalPromptMisses.Add(cacheKey);
			return false;
		}

		foreach (string root in EnumeratePromptSourceRoots())
		{
			if (!ContentFiles.DirectoryExists(root))
			{
				continue;
			}

			for (int i = 0; i < candidates.Length; i++)
			{
				string relative = candidates[i].Replace('/', Path.DirectorySeparatorChar);
				string fullPath = Path.Combine(root, relative);
				if (!ContentFiles.FileExists(fullPath))
				{
					continue;
				}

				try
				{
					if (TryLoadExternalPromptTexture(fullPath, out texture))
					{
						externalPromptLookup[cacheKey] = texture;
						return true;
					}
				}
				catch
				{
				}
			}
		}

		externalPromptMisses.Add(cacheKey);
		return false;
	}

	private static bool TryLoadExternalPromptTexture(string fullPath, out MTexture texture)
	{
		texture = null;
		if (string.IsNullOrWhiteSpace(fullPath))
		{
			return false;
		}

		if (!Path.GetExtension(fullPath).Equals(".dat", StringComparison.OrdinalIgnoreCase))
		{
			texture = new MTexture(VirtualContent.CreateTexture(fullPath));
			return true;
		}

		GraphicsDevice graphicsDevice = Engine.Graphics?.GraphicsDevice;
		if (graphicsDevice == null)
		{
			return false;
		}

		byte[] encoded = ContentFiles.ReadAllBytes(fullPath);
		byte[] decoded = CustomIconData.DecodeIfNeeded(encoded);
		using var stream = new MemoryStream(decoded, writable: false);
		Texture2D texture2D = Texture2D.FromStream(graphicsDevice, stream);
		VirtualTexture virtualTexture = VirtualContent.CreateTexture("external_prompt_" + Path.GetFileName(fullPath), texture2D.Width, texture2D.Height, Color.Transparent);
		virtualTexture.Texture?.Dispose();
		virtualTexture.Texture = texture2D;
		texture = new MTexture(virtualTexture);
		return true;
	}

	private static bool TryGetPromptCandidates(string _prefix, string _style, string input, out string[] candidates)
	{
		candidates = null;
		return xboxAltPromptLookup.TryGetValue(input, out candidates);
	}

	private static IEnumerable<string> EnumeratePromptSourceRoots()
	{
		var roots = new List<string>();
		if (!string.IsNullOrWhiteSpace(Environment.CurrentDirectory))
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(Environment.CurrentDirectory);
			for (int i = 0; i < 6 && directoryInfo != null; i++)
			{
				roots.Add(directoryInfo.FullName);
				roots.Add(Path.Combine(directoryInfo.FullName, "CELESTEPORT"));
				directoryInfo = directoryInfo.Parent;
			}
		}

		if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
		{
			roots.Add(AppContext.BaseDirectory);
		}

		if (!string.IsNullOrWhiteSpace(Engine.ContentDirectory))
		{
			roots.Add(Engine.ContentDirectory);
		}

		roots.Add("/data/celeste.app/Files");
		roots.Add("/data/celeste.app/Files/Config");

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < roots.Count; i++)
		{
			string root = roots[i];
			if (string.IsNullOrWhiteSpace(root) || !seen.Add(root))
			{
				continue;
			}

			yield return root;
		}
	}

	private static void ResetExternalPromptCache()
	{
		foreach (MTexture value in externalPromptLookup.Values)
		{
			value?.Texture?.Dispose();
		}

		externalPromptLookup.Clear();
		externalPromptMisses.Clear();
	}

	public static void SetLightbarColor(Color color)
	{
	}
}
