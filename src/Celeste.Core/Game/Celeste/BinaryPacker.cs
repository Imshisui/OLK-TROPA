using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Monocle;

namespace Celeste;

public static class BinaryPacker
{
	public class Element
	{
		public string Package;

		public string Name;

		public Dictionary<string, object> Attributes;

		public List<Element> Children;

		public bool HasAttr(string name)
		{
			if (Attributes != null)
			{
				return Attributes.ContainsKey(name);
			}
			return false;
		}

		public string Attr(string name, string defaultValue = "")
		{
			if (Attributes == null || !Attributes.TryGetValue(name, out var value))
			{
				value = defaultValue;
			}
			return value.ToString();
		}

		public bool AttrBool(string name, bool defaultValue = false)
		{
			if (Attributes == null || !Attributes.TryGetValue(name, out var value))
			{
				value = defaultValue;
			}
			if (value is bool)
			{
				return (bool)value;
			}
			return bool.Parse(value.ToString());
		}

		public float AttrFloat(string name, float defaultValue = 0f)
		{
			if (Attributes == null || !Attributes.TryGetValue(name, out var value))
			{
				value = defaultValue;
			}
			if (value is float)
			{
				return (float)value;
			}
			return float.Parse(value.ToString(), CultureInfo.InvariantCulture);
		}
	}

	public static readonly HashSet<string> IgnoreAttributes = new HashSet<string> { "_eid" };

	public static string InnerTextAttributeName = "innerText";

	public static string OutputFileExtension = ".bin";

	private static Dictionary<string, short> stringValue = new Dictionary<string, short>();

	private static string[] stringLookup;

	private static short stringCounter;

	public static void ToBinary(string filename, string outdir = null)
	{
		string extension = Path.GetExtension(filename);
		if (outdir != null)
		{
			Path.Combine(outdir + Path.GetFileName(filename));
		}
		filename.Replace(extension, OutputFileExtension);
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(filename);
		XmlElement rootElement = null;
		foreach (object childNode in xmlDocument.ChildNodes)
		{
			if (childNode is XmlElement)
			{
				rootElement = childNode as XmlElement;
				break;
			}
		}
		ToBinary(rootElement, outdir);
	}

	public static void ToBinary(XmlElement rootElement, string outfilename)
	{
		stringValue.Clear();
		stringCounter = 0;
		CreateLookupTable(rootElement);
		AddLookupValue(InnerTextAttributeName);
		using FileStream output = new FileStream(outfilename, FileMode.Create);
		BinaryWriter binaryWriter = new BinaryWriter(output);
		binaryWriter.Write("CELESTE MAP");
		binaryWriter.Write(Path.GetFileNameWithoutExtension(outfilename));
		binaryWriter.Write((short)stringValue.Count);
		foreach (KeyValuePair<string, short> item in stringValue)
		{
			binaryWriter.Write(item.Key);
		}
		WriteElement(binaryWriter, rootElement);
		binaryWriter.Flush();
	}

	private static void CreateLookupTable(XmlElement element)
	{
		AddLookupValue(element.Name);
		foreach (XmlAttribute attribute in element.Attributes)
		{
			if (!IgnoreAttributes.Contains(attribute.Name))
			{
				AddLookupValue(attribute.Name);
				if (ParseValue(attribute.Value, out var type, out var _) && type == 5)
				{
					AddLookupValue(attribute.Value);
				}
			}
		}
		foreach (object childNode in element.ChildNodes)
		{
			if (childNode is XmlElement)
			{
				CreateLookupTable(childNode as XmlElement);
			}
		}
	}

	private static void AddLookupValue(string name)
	{
		if (!stringValue.ContainsKey(name))
		{
			stringValue.Add(name, stringCounter);
			stringCounter++;
		}
	}

	private static void WriteElement(BinaryWriter writer, XmlElement element)
	{
		int num = 0;
		foreach (object childNode in element.ChildNodes)
		{
			if (childNode is XmlElement)
			{
				num++;
			}
		}
		int num2 = 0;
		foreach (XmlAttribute attribute in element.Attributes)
		{
			if (!IgnoreAttributes.Contains(attribute.Name))
			{
				num2++;
			}
		}
		if (element.InnerText.Length > 0 && num == 0)
		{
			num2++;
		}
		writer.Write(stringValue[element.Name]);
		writer.Write((byte)num2);
		foreach (XmlAttribute attribute2 in element.Attributes)
		{
			if (!IgnoreAttributes.Contains(attribute2.Name))
			{
				ParseValue(attribute2.Value, out var type, out var result);
				writer.Write(stringValue[attribute2.Name]);
				writer.Write(type);
				switch (type)
				{
				case 0:
					writer.Write((bool)result);
					break;
				case 1:
					writer.Write((byte)result);
					break;
				case 2:
					writer.Write((short)result);
					break;
				case 3:
					writer.Write((int)result);
					break;
				case 4:
					writer.Write((float)result);
					break;
				case 5:
					writer.Write(stringValue[(string)result]);
					break;
				}
			}
		}
		if (element.InnerText.Length > 0 && num == 0)
		{
			writer.Write(stringValue[InnerTextAttributeName]);
			if (element.Name == "solids" || element.Name == "bg")
			{
				byte[] array = RunLengthEncoding.Encode(element.InnerText);
				writer.Write((byte)7);
				writer.Write((short)array.Length);
				writer.Write(array);
			}
			else
			{
				writer.Write((byte)6);
				writer.Write(element.InnerText);
			}
		}
		writer.Write((short)num);
		foreach (object childNode2 in element.ChildNodes)
		{
			if (childNode2 is XmlElement)
			{
				WriteElement(writer, childNode2 as XmlElement);
			}
		}
	}

	private static bool ParseValue(string value, out byte type, out object result)
	{
		byte result3;
		short result4;
		int result5;
		float result6;
		if (bool.TryParse(value, out var result2))
		{
			type = 0;
			result = result2;
		}
		else if (byte.TryParse(value, out result3))
		{
			type = 1;
			result = result3;
		}
		else if (short.TryParse(value, out result4))
		{
			type = 2;
			result = result4;
		}
		else if (int.TryParse(value, out result5))
		{
			type = 3;
			result = result5;
		}
		else if (float.TryParse(value, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result6))
		{
			type = 4;
			result = result6;
		}
		else
		{
			type = 5;
			result = value;
		}
		return true;
	}

	public static Element FromBinary(string filename)
	{
		using Stream input = ContentFiles.OpenRead(filename);
		BinaryReader binaryReader = new BinaryReader(input);
		binaryReader.ReadString();
		string package = binaryReader.ReadString();
		short num = binaryReader.ReadInt16();
		stringLookup = new string[num];
		for (int i = 0; i < num; i++)
		{
			stringLookup[i] = binaryReader.ReadString();
		}
		Element element = ReadElement(binaryReader);
		element.Package = package;
		return element;
	}

	private static Element ReadElement(BinaryReader reader)
	{
		Element element = new Element();
		element.Name = stringLookup[reader.ReadInt16()];
		byte b = reader.ReadByte();
		if (b > 0)
		{
			element.Attributes = new Dictionary<string, object>();
		}
		for (int i = 0; i < b; i++)
		{
			string key = stringLookup[reader.ReadInt16()];
			byte b2 = reader.ReadByte();
			object value = null;
			switch (b2)
			{
			case 0:
				value = reader.ReadBoolean();
				break;
			case 1:
				value = Convert.ToInt32(reader.ReadByte());
				break;
			case 2:
				value = Convert.ToInt32(reader.ReadInt16());
				break;
			case 3:
				value = reader.ReadInt32();
				break;
			case 4:
				value = reader.ReadSingle();
				break;
			case 5:
				value = stringLookup[reader.ReadInt16()];
				break;
			case 6:
				value = reader.ReadString();
				break;
			case 7:
			{
				short count = reader.ReadInt16();
				value = RunLengthEncoding.Decode(reader.ReadBytes(count));
				break;
			}
			}
			element.Attributes.Add(key, value);
		}
		short num = reader.ReadInt16();
		if (num > 0)
		{
			element.Children = new List<Element>();
		}
		for (int j = 0; j < num; j++)
		{
			element.Children.Add(ReadElement(reader));
		}
		return element;
	}
}
