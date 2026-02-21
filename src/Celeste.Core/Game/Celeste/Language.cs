using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Monocle;

namespace Celeste;

public class Language
{
	public string FilePath;

	public string Id;

	public string Label;

	public string IconPath;

	public MTexture Icon;

	public int Order = 100;

	public string FontFace;

	public float FontFaceSize;

	public string SplitRegex = "(\\s|\\{|\\})";

	public string CommaCharacters = ",";

	public string PeriodCharacters = ".!?";

	public int Lines;

	public int Words;

	public Dictionary<string, string> Dialog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, string> Cleaned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private static readonly Regex command = new Regex("\\{(.*?)\\}", RegexOptions.RightToLeft);

	private static readonly Regex insert = new Regex("\\{\\+\\s*(.*?)\\}");

	private static readonly Regex variable = new Regex("^\\w+\\=.*");

	private static readonly Regex portrait = new Regex("\\[(?<content>[^\\[\\\\]*(?:\\\\.[^\\]\\\\]*)*)\\]", RegexOptions.IgnoreCase);

	public PixelFont Font => Fonts.Get(FontFace);

	public PixelFontSize FontSize => Font.Get(FontFaceSize);

	public string this[string name] => Dialog[name];

	public bool CanDisplay(string text)
	{
		PixelFontSize fontSize = FontSize;
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] != ' ' && fontSize.Get(text[i]) == null)
			{
				return false;
			}
		}
		return true;
	}

	public void Export(string path)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(File.OpenWrite(path));
		binaryWriter.Write(Id);
		binaryWriter.Write(Label);
		binaryWriter.Write(IconPath);
		binaryWriter.Write(Order);
		binaryWriter.Write(FontFace);
		binaryWriter.Write(FontFaceSize);
		binaryWriter.Write(SplitRegex);
		binaryWriter.Write(CommaCharacters);
		binaryWriter.Write(PeriodCharacters);
		binaryWriter.Write(Lines);
		binaryWriter.Write(Words);
		binaryWriter.Write(Dialog.Count);
		foreach (KeyValuePair<string, string> item in Dialog)
		{
			binaryWriter.Write(item.Key);
			binaryWriter.Write(item.Value);
			binaryWriter.Write(Cleaned[item.Key]);
		}
	}

	public static Language FromExport(string path)
	{
		Language language = new Language();
		using BinaryReader binaryReader = new BinaryReader(ContentFiles.OpenRead(path));
		language.Id = binaryReader.ReadString();
		language.Label = binaryReader.ReadString();
		language.IconPath = binaryReader.ReadString();
		language.Icon = new MTexture(VirtualContent.CreateTexture(Path.Combine("Dialog", language.IconPath)));
		language.Order = binaryReader.ReadInt32();
		language.FontFace = binaryReader.ReadString();
		language.FontFaceSize = binaryReader.ReadSingle();
		language.SplitRegex = binaryReader.ReadString();
		language.CommaCharacters = binaryReader.ReadString();
		language.PeriodCharacters = binaryReader.ReadString();
		language.Lines = binaryReader.ReadInt32();
		language.Words = binaryReader.ReadInt32();
		int num = binaryReader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			string key = binaryReader.ReadString();
			language.Dialog[key] = binaryReader.ReadString();
			language.Cleaned[key] = binaryReader.ReadString();
		}
		return language;
	}

	public static Language FromTxt(string path)
	{
		Language language = null;
		string text = "";
		StringBuilder stringBuilder = new StringBuilder();
		string input = "";
		foreach (string item in ContentFiles.ReadAllLines(path, Encoding.UTF8))
		{
			string text2 = item.Trim();
			if (text2.Length <= 0 || text2[0] == '#')
			{
				continue;
			}
			if (text2.IndexOf('[') >= 0)
			{
				text2 = portrait.Replace(text2, "{portrait ${content}}");
			}
			text2 = text2.Replace("\\#", "#");
			if (text2.Length <= 0)
			{
				continue;
			}
			if (variable.IsMatch(text2))
			{
				if (!string.IsNullOrEmpty(text))
				{
					language.Dialog[text] = stringBuilder.ToString();
				}
				string[] array = text2.Split('=');
				string text3 = array[0].Trim();
				string text4 = ((array.Length > 1) ? array[1].Trim() : "");
				if (text3.Equals("language", StringComparison.OrdinalIgnoreCase))
				{
					string[] array2 = text4.Split(',');
					language = new Language();
					language.FontFace = null;
					language.Id = array2[0];
					language.FilePath = Path.GetFileName(path);
					if (array2.Length > 1)
					{
						language.Label = array2[1];
					}
				}
				else if (text3.Equals("icon", StringComparison.OrdinalIgnoreCase))
				{
					VirtualTexture texture = VirtualContent.CreateTexture(Path.Combine("Dialog", text4));
					language.IconPath = text4;
					language.Icon = new MTexture(texture);
				}
				else if (text3.Equals("order", StringComparison.OrdinalIgnoreCase))
				{
					language.Order = int.Parse(text4);
				}
				else if (text3.Equals("font", StringComparison.OrdinalIgnoreCase))
				{
					string[] array3 = text4.Split(',');
					language.FontFace = array3[0];
					language.FontFaceSize = float.Parse(array3[1], CultureInfo.InvariantCulture);
				}
				else if (text3.Equals("SPLIT_REGEX", StringComparison.OrdinalIgnoreCase))
				{
					language.SplitRegex = text4;
				}
				else if (text3.Equals("commas", StringComparison.OrdinalIgnoreCase))
				{
					language.CommaCharacters = text4;
				}
				else if (text3.Equals("periods", StringComparison.OrdinalIgnoreCase))
				{
					language.PeriodCharacters = text4;
				}
				else
				{
					text = text3;
					stringBuilder.Clear();
					stringBuilder.Append(text4);
				}
			}
			else
			{
				if (stringBuilder.Length > 0)
				{
					string text5 = stringBuilder.ToString();
					if (!text5.EndsWith("{break}") && !text5.EndsWith("{n}") && command.Replace(input, "").Length > 0)
					{
						stringBuilder.Append("{break}");
					}
				}
				stringBuilder.Append(text2);
			}
			input = text2;
		}
		if (!string.IsNullOrEmpty(text))
		{
			language.Dialog[text] = stringBuilder.ToString();
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> item2 in language.Dialog)
		{
			list.Add(item2.Key);
		}
		foreach (string item3 in list)
		{
			string text6 = language.Dialog[item3];
			MatchCollection matchCollection = null;
			while (matchCollection == null || matchCollection.Count > 0)
			{
				matchCollection = insert.Matches(text6);
				for (int i = 0; i < matchCollection.Count; i++)
				{
					Match match = matchCollection[i];
					string value = match.Groups[1].Value;
					text6 = ((!language.Dialog.TryGetValue(value, out var value2)) ? text6.Replace(match.Value, "[XXX]") : text6.Replace(match.Value, value2));
				}
			}
			language.Dialog[item3] = text6;
		}
		language.Lines = 0;
		language.Words = 0;
		foreach (string item4 in list)
		{
			string text7 = language.Dialog[item4];
			if (text7.IndexOf('{') >= 0)
			{
				text7 = text7.Replace("{n}", "\n");
				text7 = text7.Replace("{break}", "\n");
				text7 = command.Replace(text7, "");
			}
			language.Cleaned.Add(item4, text7);
		}
		return language;
	}

	public void Dispose()
	{
		if (Icon.Texture != null && !Icon.Texture.IsDisposed)
		{
			Icon.Texture.Dispose();
		}
	}
}
