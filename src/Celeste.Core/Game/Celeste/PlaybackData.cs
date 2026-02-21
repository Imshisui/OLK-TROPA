using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

public static class PlaybackData
{
	public static Dictionary<string, List<Player.ChaserState>> Tutorials = new Dictionary<string, List<Player.ChaserState>>();

	public static void Load()
	{
		string[] files = ContentFiles.GetFiles(Path.Combine(Engine.ContentDirectory, "Tutorials"));
		foreach (string path in files)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
			List<Player.ChaserState> value = Import(ContentFiles.ReadAllBytes(path));
			Tutorials[fileNameWithoutExtension] = value;
		}
	}

	public static void Export(List<Player.ChaserState> list, string path)
	{
		float timeStamp = list[0].TimeStamp;
		Vector2 position = list[0].Position;
		using BinaryWriter binaryWriter = new BinaryWriter(File.OpenWrite(path));
		binaryWriter.Write("TIMELINE");
		binaryWriter.Write(2);
		binaryWriter.Write(list.Count);
		foreach (Player.ChaserState item in list)
		{
			binaryWriter.Write(item.Position.X - position.X);
			binaryWriter.Write(item.Position.Y - position.Y);
			binaryWriter.Write(item.TimeStamp - timeStamp);
			binaryWriter.Write(item.Animation);
			binaryWriter.Write((int)item.Facing);
			binaryWriter.Write(item.OnGround);
			Color hairColor = item.HairColor;
			binaryWriter.Write(hairColor.R);
			hairColor = item.HairColor;
			binaryWriter.Write(hairColor.G);
			hairColor = item.HairColor;
			binaryWriter.Write(hairColor.B);
			binaryWriter.Write(item.Depth);
			binaryWriter.Write(item.Scale.X);
			binaryWriter.Write(item.Scale.Y);
			binaryWriter.Write(item.DashDirection.X);
			binaryWriter.Write(item.DashDirection.Y);
		}
	}

	public static List<Player.ChaserState> Import(byte[] buffer)
	{
		List<Player.ChaserState> list = new List<Player.ChaserState>();
		using BinaryReader binaryReader = new BinaryReader(new MemoryStream(buffer));
		int num = 1;
		if (binaryReader.ReadString() == "TIMELINE")
		{
			num = binaryReader.ReadInt32();
		}
		else
		{
			binaryReader.BaseStream.Seek(0L, SeekOrigin.Begin);
		}
		int num2 = binaryReader.ReadInt32();
		for (int i = 0; i < num2; i++)
		{
			Player.ChaserState item = new Player.ChaserState
			{
				Position = 
				{
					X = binaryReader.ReadSingle(),
					Y = binaryReader.ReadSingle()
				},
				TimeStamp = binaryReader.ReadSingle(),
				Animation = binaryReader.ReadString(),
				Facing = (Facings)binaryReader.ReadInt32(),
				OnGround = binaryReader.ReadBoolean(),
				HairColor = new Color(binaryReader.ReadByte(), binaryReader.ReadByte(), binaryReader.ReadByte(), byte.MaxValue),
				Depth = binaryReader.ReadInt32(),
				Sounds = 0
			};
			if (num == 1)
			{
				item.Scale = new Vector2((float)item.Facing, 1f);
				item.DashDirection = Vector2.Zero;
			}
			else
			{
				item.Scale.X = binaryReader.ReadSingle();
				item.Scale.Y = binaryReader.ReadSingle();
				item.DashDirection.X = binaryReader.ReadSingle();
				item.DashDirection.Y = binaryReader.ReadSingle();
			}
			list.Add(item);
		}
		return list;
	}
}
