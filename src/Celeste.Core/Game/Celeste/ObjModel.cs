using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste;

public class ObjModel : IDisposable
{
	public class Mesh
	{
		public string Name = "";

		public ObjModel Model;

		public int VertexStart;

		public int VertexCount;
	}

	public List<Mesh> Meshes = new List<Mesh>();

	public VertexBuffer Vertices;

	private VertexPositionTexture[] verts;

	private bool ResetVertexBuffer()
	{
		if (Vertices == null || Vertices.IsDisposed || Vertices.GraphicsDevice.IsDisposed)
		{
			Vertices = new VertexBuffer(Engine.Graphics.GraphicsDevice, typeof(VertexPositionTexture), verts.Length, BufferUsage.None);
			Vertices.SetData(verts);
			return true;
		}
		return false;
	}

	public void ReassignVertices()
	{
		if (!ResetVertexBuffer())
		{
			Vertices.SetData(verts);
		}
	}

	public void Draw(Effect effect)
	{
		ResetVertexBuffer();
		Engine.Graphics.GraphicsDevice.SetVertexBuffer(Vertices);
		foreach (EffectPass pass in effect.CurrentTechnique.Passes)
		{
			pass.Apply();
			Engine.Graphics.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, Vertices.VertexCount / 3);
		}
	}

	public void Dispose()
	{
		Vertices.Dispose();
		Meshes = null;
	}

	public static ObjModel Create(string filename)
	{
		Path.GetDirectoryName(filename);
		ObjModel objModel = new ObjModel();
		List<VertexPositionTexture> list = new List<VertexPositionTexture>();
		List<Vector3> list2 = new List<Vector3>();
		List<Vector2> list3 = new List<Vector2>();
		Mesh mesh = null;
		if (ContentFiles.FileExists(filename + ".export"))
		{
			using BinaryReader binaryReader = new BinaryReader(ContentFiles.OpenRead(filename + ".export"));
			int num = binaryReader.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				if (mesh != null)
				{
					mesh.VertexCount = list.Count - mesh.VertexStart;
				}
				mesh = new Mesh();
				mesh.Name = binaryReader.ReadString();
				mesh.VertexStart = list.Count;
				objModel.Meshes.Add(mesh);
				int num2 = binaryReader.ReadInt32();
				for (int j = 0; j < num2; j++)
				{
					float x = binaryReader.ReadSingle();
					float y = binaryReader.ReadSingle();
					float z = binaryReader.ReadSingle();
					list2.Add(new Vector3(x, y, z));
				}
				int num3 = binaryReader.ReadInt32();
				for (int k = 0; k < num3; k++)
				{
					float x2 = binaryReader.ReadSingle();
					float y2 = binaryReader.ReadSingle();
					list3.Add(new Vector2(x2, y2));
				}
				int num4 = binaryReader.ReadInt32();
				for (int l = 0; l < num4; l++)
				{
					int index = binaryReader.ReadInt32() - 1;
					int index2 = binaryReader.ReadInt32() - 1;
					list.Add(new VertexPositionTexture
					{
						Position = list2[index],
						TextureCoordinate = list3[index2]
					});
				}
			}
		}
		else
		{
			using StreamReader streamReader = new StreamReader(ContentFiles.OpenRead(filename));
			string text;
			while ((text = streamReader.ReadLine()) != null)
			{
				string[] array = text.Split(' ');
				if (array.Length == 0)
				{
					continue;
				}
				switch (array[0])
				{
				case "o":
					if (mesh != null)
					{
						mesh.VertexCount = list.Count - mesh.VertexStart;
					}
					mesh = new Mesh();
					mesh.Name = array[1];
					mesh.VertexStart = list.Count;
					objModel.Meshes.Add(mesh);
					break;
				case "v":
				{
					Vector3 item3 = new Vector3(Float(array[1]), Float(array[2]), Float(array[3]));
					list2.Add(item3);
					break;
				}
				case "vt":
				{
					Vector2 item2 = new Vector2(Float(array[1]), Float(array[2]));
					list3.Add(item2);
					break;
				}
				case "f":
				{
					for (int m = 1; m < Math.Min(4, array.Length); m++)
					{
						VertexPositionTexture item = default(VertexPositionTexture);
						string[] array2 = array[m].Split('/');
						if (array2[0].Length > 0)
						{
							item.Position = list2[int.Parse(array2[0]) - 1];
						}
						if (array2[1].Length > 0)
						{
							item.TextureCoordinate = list3[int.Parse(array2[1]) - 1];
						}
						list.Add(item);
					}
					break;
				}
				}
			}
		}
		if (mesh != null)
		{
			mesh.VertexCount = list.Count - mesh.VertexStart;
		}
		objModel.verts = list.ToArray();
		objModel.ResetVertexBuffer();
		return objModel;
	}

	private static float Float(string data)
	{
		return float.Parse(data, CultureInfo.InvariantCulture);
	}
}
