using System.Collections.Generic;
using System.IO;
using System.Xml;
using Monocle;

namespace Celeste;

public static class Fonts
{
	private static Dictionary<string, List<string>> paths = new Dictionary<string, List<string>>();

	private static Dictionary<string, PixelFont> loadedFonts = new Dictionary<string, PixelFont>();

	public static PixelFont Load(string face)
	{
		if (!loadedFonts.TryGetValue(face, out var value) && paths.TryGetValue(face, out var value2))
		{
			loadedFonts.Add(face, value = new PixelFont(face));
			foreach (string item in value2)
			{
				value.AddFontSize(item, GFX.Gui);
			}
		}
		return value;
	}

	public static PixelFont Get(string face)
	{
		if (loadedFonts.TryGetValue(face, out var value))
		{
			return value;
		}
		return null;
	}

	public static void Unload(string face)
	{
		if (loadedFonts.TryGetValue(face, out var value))
		{
			value.Dispose();
			loadedFonts.Remove(face);
		}
	}

	public static void Reload()
	{
		List<string> list = new List<string>();
		foreach (string key in loadedFonts.Keys)
		{
			list.Add(key);
		}
		foreach (string item in list)
		{
			loadedFonts[item].Dispose();
			Load(item);
		}
	}

	public static void Prepare()
	{
		XmlReaderSettings xmlReaderSettings = new XmlReaderSettings();
		xmlReaderSettings.CloseInput = true;
		string[] files = ContentFiles.GetFiles(Path.Combine(Engine.ContentDirectory, "Dialog"), "*.fnt", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			string text2 = null;
			using (XmlReader xmlReader = XmlReader.Create(ContentFiles.OpenRead(text), xmlReaderSettings))
			{
				while (xmlReader.Read())
				{
					if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "info")
					{
						text2 = xmlReader.GetAttribute("face");
					}
				}
			}
			if (text2 != null)
			{
				if (!paths.TryGetValue(text2, out var value))
				{
					paths.Add(text2, value = new List<string>());
				}
				value.Add(text);
			}
		}
	}

	public static void Log()
	{
		Engine.Commands.Log("EXISTING FONTS:");
		foreach (KeyValuePair<string, List<string>> path in paths)
		{
			Engine.Commands.Log(" - " + path.Key);
			foreach (string item in path.Value)
			{
				Engine.Commands.Log(" - > " + item);
			}
		}
		Engine.Commands.Log("LOADED:");
		foreach (KeyValuePair<string, PixelFont> loadedFont in loadedFonts)
		{
			Engine.Commands.Log(" - " + loadedFont.Key);
		}
	}
}
