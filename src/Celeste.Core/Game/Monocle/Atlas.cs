using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Xna.Framework;

namespace Monocle;

public class Atlas
{
	public enum AtlasDataFormat
	{
		TexturePacker_Sparrow,
		CrunchXml,
		CrunchBinary,
		CrunchXmlOrBinary,
		CrunchBinaryNoAtlas,
		Packer,
		PackerNoAtlas
	}

	public List<VirtualTexture> Sources;

	private Dictionary<string, MTexture> textures = new Dictionary<string, MTexture>(StringComparer.OrdinalIgnoreCase);

	private Dictionary<string, List<MTexture>> orderedTexturesCache = new Dictionary<string, List<MTexture>>();

	private Dictionary<string, string> links = new Dictionary<string, string>();

	public MTexture this[string id]
	{
		get
		{
			return textures[id];
		}
		set
		{
			textures[id] = value;
		}
	}

	public static Atlas FromAtlas(string path, AtlasDataFormat format)
	{
		Atlas obj = new Atlas
		{
			Sources = new List<VirtualTexture>()
		};
		ReadAtlasData(obj, path, format);
		return obj;
	}

	private static void ReadAtlasData(Atlas atlas, string path, AtlasDataFormat format)
	{
		switch (format)
		{
		case AtlasDataFormat.TexturePacker_Sparrow:
		{
			XmlElement xmlElement = Calc.LoadContentXML(path)["TextureAtlas"];
			VirtualTexture virtualTexture2 = VirtualContent.CreateTexture(Path.Combine(path2: xmlElement.Attr("imagePath", ""), path1: Path.GetDirectoryName(path)));
			MTexture parent2 = new MTexture(virtualTexture2);
			atlas.Sources.Add(virtualTexture2);
			{
				foreach (XmlElement item in xmlElement.GetElementsByTagName("SubTexture"))
				{
					string text2 = item.Attr("name");
					Rectangle clipRect2 = item.Rect();
					if (item.HasAttr("frameX"))
					{
						atlas.textures[text2] = new MTexture(parent2, text2, clipRect2, new Vector2(-item.AttrInt("frameX"), -item.AttrInt("frameY")), item.AttrInt("frameWidth"), item.AttrInt("frameHeight"));
					}
					else
					{
						atlas.textures[text2] = new MTexture(parent2, text2, clipRect2);
					}
				}
				break;
			}
		}
		case AtlasDataFormat.CrunchXml:
		{
			foreach (XmlElement item2 in Calc.LoadContentXML(path)["atlas"])
			{
				VirtualTexture virtualTexture = VirtualContent.CreateTexture(Path.Combine(path2: item2.Attr("n", "") + ".png", path1: Path.GetDirectoryName(path)));
				MTexture parent = new MTexture(virtualTexture);
				atlas.Sources.Add(virtualTexture);
				foreach (XmlElement item3 in item2)
				{
					string text = item3.Attr("n");
					Rectangle clipRect = new Rectangle(item3.AttrInt("x"), item3.AttrInt("y"), item3.AttrInt("w"), item3.AttrInt("h"));
					if (item3.HasAttr("fx"))
					{
						atlas.textures[text] = new MTexture(parent, text, clipRect, new Vector2(-item3.AttrInt("fx"), -item3.AttrInt("fy")), item3.AttrInt("fw"), item3.AttrInt("fh"));
					}
					else
					{
						atlas.textures[text] = new MTexture(parent, text, clipRect);
					}
				}
			}
			break;
		}
		case AtlasDataFormat.CrunchBinary:
		{
			using Stream input2 = ContentFiles.OpenRead(Path.Combine(Engine.ContentDirectory, path));
			BinaryReader binaryReader4 = new BinaryReader(input2);
			short num17 = binaryReader4.ReadInt16();
			for (int num18 = 0; num18 < num17; num18++)
			{
				string text7 = binaryReader4.ReadNullTerminatedString();
				VirtualTexture virtualTexture6 = VirtualContent.CreateTexture(Path.Combine(Path.GetDirectoryName(path), text7 + ".png"));
				atlas.Sources.Add(virtualTexture6);
				MTexture parent4 = new MTexture(virtualTexture6);
				short num19 = binaryReader4.ReadInt16();
				for (int num20 = 0; num20 < num19; num20++)
				{
					string text8 = binaryReader4.ReadNullTerminatedString();
					short x2 = binaryReader4.ReadInt16();
					short y2 = binaryReader4.ReadInt16();
					short width3 = binaryReader4.ReadInt16();
					short height3 = binaryReader4.ReadInt16();
					short num21 = binaryReader4.ReadInt16();
					short num22 = binaryReader4.ReadInt16();
					short width4 = binaryReader4.ReadInt16();
					short height4 = binaryReader4.ReadInt16();
					atlas.textures[text8] = new MTexture(parent4, text8, new Rectangle(x2, y2, width3, height3), new Vector2(-num21, -num22), width4, height4);
				}
			}
			break;
		}
		case AtlasDataFormat.CrunchBinaryNoAtlas:
		{
			using Stream input = ContentFiles.OpenRead(Path.Combine(Engine.ContentDirectory, path + ".bin"));
			BinaryReader binaryReader3 = new BinaryReader(input);
			short num11 = binaryReader3.ReadInt16();
			for (int num12 = 0; num12 < num11; num12++)
			{
				string path4 = binaryReader3.ReadNullTerminatedString();
				string path5 = Path.Combine(Path.GetDirectoryName(path), path4);
				short num13 = binaryReader3.ReadInt16();
				for (int num14 = 0; num14 < num13; num14++)
				{
					string text6 = binaryReader3.ReadNullTerminatedString();
					binaryReader3.ReadInt16();
					binaryReader3.ReadInt16();
					binaryReader3.ReadInt16();
					binaryReader3.ReadInt16();
					short num15 = binaryReader3.ReadInt16();
					short num16 = binaryReader3.ReadInt16();
					short frameWidth2 = binaryReader3.ReadInt16();
					short frameHeight2 = binaryReader3.ReadInt16();
					VirtualTexture virtualTexture5 = VirtualContent.CreateTexture(Path.Combine(path5, text6 + ".png"));
					atlas.Sources.Add(virtualTexture5);
					atlas.textures[text6] = new MTexture(virtualTexture5, new Vector2(-num15, -num16), frameWidth2, frameHeight2);
				}
			}
			break;
		}
		case AtlasDataFormat.Packer:
		{
			using Stream fileStream2 = ContentFiles.OpenRead(Path.Combine(Engine.ContentDirectory, path + ".meta"));
			BinaryReader binaryReader2 = new BinaryReader(fileStream2);
			binaryReader2.ReadInt32();
			binaryReader2.ReadString();
			binaryReader2.ReadInt32();
			short num6 = binaryReader2.ReadInt16();
			for (int l = 0; l < num6; l++)
			{
				string text4 = binaryReader2.ReadString();
				VirtualTexture virtualTexture4 = VirtualContent.CreateTexture(Path.Combine(Path.GetDirectoryName(path), text4 + ".data"));
				atlas.Sources.Add(virtualTexture4);
				MTexture parent3 = new MTexture(virtualTexture4);
				short num7 = binaryReader2.ReadInt16();
				for (int m = 0; m < num7; m++)
				{
					string text5 = binaryReader2.ReadString().Replace('\\', '/');
					short x = binaryReader2.ReadInt16();
					short y = binaryReader2.ReadInt16();
					short width = binaryReader2.ReadInt16();
					short height = binaryReader2.ReadInt16();
					short num8 = binaryReader2.ReadInt16();
					short num9 = binaryReader2.ReadInt16();
					short width2 = binaryReader2.ReadInt16();
					short height2 = binaryReader2.ReadInt16();
					atlas.textures[text5] = new MTexture(parent3, text5, new Rectangle(x, y, width, height), new Vector2(-num8, -num9), width2, height2);
				}
			}
			if (fileStream2.Position < fileStream2.Length && binaryReader2.ReadString() == "LINKS")
			{
				short num10 = binaryReader2.ReadInt16();
				for (int n = 0; n < num10; n++)
				{
					string key2 = binaryReader2.ReadString();
					string value2 = binaryReader2.ReadString();
					atlas.links.Add(key2, value2);
				}
			}
			break;
		}
		case AtlasDataFormat.PackerNoAtlas:
		{
			using Stream fileStream = ContentFiles.OpenRead(Path.Combine(Engine.ContentDirectory, path + ".meta"));
			BinaryReader binaryReader = new BinaryReader(fileStream);
			binaryReader.ReadInt32();
			binaryReader.ReadString();
			binaryReader.ReadInt32();
			short num = binaryReader.ReadInt16();
			for (int i = 0; i < num; i++)
			{
				string path2 = binaryReader.ReadString();
				string path3 = Path.Combine(Path.GetDirectoryName(path), path2);
				short num2 = binaryReader.ReadInt16();
				for (int j = 0; j < num2; j++)
				{
					string text3 = binaryReader.ReadString().Replace('\\', '/');
					binaryReader.ReadInt16();
					binaryReader.ReadInt16();
					binaryReader.ReadInt16();
					binaryReader.ReadInt16();
					short num3 = binaryReader.ReadInt16();
					short num4 = binaryReader.ReadInt16();
					short frameWidth = binaryReader.ReadInt16();
					short frameHeight = binaryReader.ReadInt16();
					VirtualTexture virtualTexture3 = VirtualContent.CreateTexture(Path.Combine(path3, text3 + ".data"));
					atlas.Sources.Add(virtualTexture3);
					atlas.textures[text3] = new MTexture(virtualTexture3, new Vector2(-num3, -num4), frameWidth, frameHeight);
					atlas.textures[text3].AtlasPath = text3;
				}
			}
			if (fileStream.Position < fileStream.Length && binaryReader.ReadString() == "LINKS")
			{
				short num5 = binaryReader.ReadInt16();
				for (int k = 0; k < num5; k++)
				{
					string key = binaryReader.ReadString();
					string value = binaryReader.ReadString();
					atlas.links.Add(key, value);
				}
			}
			break;
		}
		case AtlasDataFormat.CrunchXmlOrBinary:
			if (ContentFiles.FileExists(Path.Combine(Engine.ContentDirectory, path + ".bin")))
			{
				ReadAtlasData(atlas, path + ".bin", AtlasDataFormat.CrunchBinary);
			}
			else
			{
				ReadAtlasData(atlas, path + ".xml", AtlasDataFormat.CrunchXml);
			}
			break;
		default:
			throw new NotImplementedException();
		}
	}

	public static Atlas FromMultiAtlas(string rootPath, string[] dataPath, AtlasDataFormat format)
	{
		Atlas atlas = new Atlas();
		atlas.Sources = new List<VirtualTexture>();
		for (int i = 0; i < dataPath.Length; i++)
		{
			ReadAtlasData(atlas, Path.Combine(rootPath, dataPath[i]), format);
		}
		return atlas;
	}

	public static Atlas FromMultiAtlas(string rootPath, string filename, AtlasDataFormat format)
	{
		Atlas atlas = new Atlas();
		atlas.Sources = new List<VirtualTexture>();
		int num = 0;
		while (true)
		{
			string text = Path.Combine(rootPath, filename + num + ".xml");
			if (!ContentFiles.FileExists(Path.Combine(Engine.ContentDirectory, text)))
			{
				break;
			}
			ReadAtlasData(atlas, text, format);
			num++;
		}
		return atlas;
	}

	public static Atlas FromDirectory(string path)
	{
		Atlas atlas = new Atlas();
		atlas.Sources = new List<VirtualTexture>();
		string contentDirectory = Engine.ContentDirectory;
		int length = contentDirectory.Length;
		string text = Path.Combine(contentDirectory, path);
		int length2 = text.Length;
		string[] files = ContentFiles.GetFiles(text, "*", SearchOption.AllDirectories);
		foreach (string text2 in files)
		{
			string extension = Path.GetExtension(text2);
			if (!(extension != ".png") || !(extension != ".xnb"))
			{
				VirtualTexture virtualTexture = VirtualContent.CreateTexture(text2.Substring(length + 1));
				atlas.Sources.Add(virtualTexture);
				string text3 = text2.Substring(length2 + 1);
				text3 = text3.Substring(0, text3.Length - 4);
				text3 = text3.Replace('\\', '/');
				atlas.textures.Add(text3, new MTexture(virtualTexture));
			}
		}
		return atlas;
	}

	public bool Has(string id)
	{
		return textures.ContainsKey(id);
	}

	public MTexture GetOrDefault(string id, MTexture defaultTexture)
	{
		if (string.IsNullOrEmpty(id) || !Has(id))
		{
			return defaultTexture;
		}
		return textures[id];
	}

	public List<MTexture> GetAtlasSubtextures(string key)
	{
		if (!orderedTexturesCache.TryGetValue(key, out var value))
		{
			value = new List<MTexture>();
			int num = 0;
			while (true)
			{
				MTexture atlasSubtextureFromAtlasAt = GetAtlasSubtextureFromAtlasAt(key, num);
				if (atlasSubtextureFromAtlasAt == null)
				{
					break;
				}
				value.Add(atlasSubtextureFromAtlasAt);
				num++;
			}
			orderedTexturesCache.Add(key, value);
		}
		return value;
	}

	private MTexture GetAtlasSubtextureFromCacheAt(string key, int index)
	{
		return orderedTexturesCache[key][index];
	}

	private MTexture GetAtlasSubtextureFromAtlasAt(string key, int index)
	{
		if (index == 0 && textures.ContainsKey(key))
		{
			return textures[key];
		}
		string text = index.ToString();
		int length = text.Length;
		while (text.Length < length + 6)
		{
			if (textures.TryGetValue(key + text, out var value))
			{
				return value;
			}
			text = "0" + text;
		}
		return null;
	}

	public MTexture GetAtlasSubtexturesAt(string key, int index)
	{
		if (orderedTexturesCache.TryGetValue(key, out var value))
		{
			return value[index];
		}
		return GetAtlasSubtextureFromAtlasAt(key, index);
	}

	public MTexture GetLinkedTexture(string key)
	{
		if (key != null && links.TryGetValue(key, out var value) && textures.TryGetValue(value, out var value2))
		{
			return value2;
		}
		return null;
	}

	public void Dispose()
	{
		foreach (VirtualTexture source in Sources)
		{
			source.Dispose();
		}
		Sources.Clear();
		textures.Clear();
	}
}
