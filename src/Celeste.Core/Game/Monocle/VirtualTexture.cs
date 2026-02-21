using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monocle;

public class VirtualTexture : VirtualAsset
{
	private const int ByteArraySize = 524288;

	private const int ByteArrayCheckSize = 524256;

	internal static readonly byte[] buffer = new byte[67108864];

	internal static readonly byte[] bytes = new byte[524288];

	public Texture2D Texture;

	private Color color;

	public string Path { get; private set; }

	public bool IsDisposed
	{
		get
		{
			if (Texture != null && !Texture.IsDisposed)
			{
				return Texture.GraphicsDevice.IsDisposed;
			}
			return true;
		}
	}

	internal VirtualTexture(string path)
	{
		base.Name = (Path = path);
		Reload();
	}

	internal VirtualTexture(string name, int width, int height, Color color)
	{
		base.Name = name;
		base.Width = width;
		base.Height = height;
		this.color = color;
		Reload();
	}

	internal override void Unload()
	{
		if (Texture != null && !Texture.IsDisposed)
		{
			Texture.Dispose();
		}
		Texture = null;
	}

	internal unsafe override void Reload()
	{
		Unload();
		if (string.IsNullOrEmpty(Path))
		{
			Texture = new Texture2D(Engine.Instance.GraphicsDevice, base.Width, base.Height);
			Color[] array = new Color[base.Width * base.Height];
			fixed (Color* ptr = array)
			{
				for (int i = 0; i < array.Length; i++)
				{
					ptr[i] = color;
				}
			}
			Texture.SetData(array);
			return;
		}
		switch (System.IO.Path.GetExtension(Path))
		{
		case ".data":
		{
			using (Stream fileStream = ContentFiles.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
			{
				fileStream.Read(bytes, 0, 524288);
				int num2 = 0;
				int num3 = BitConverter.ToInt32(bytes, num2);
				int num4 = BitConverter.ToInt32(bytes, num2 + 4);
				bool flag = bytes[num2 + 8] == 1;
				num2 += 9;
				int num5 = num3 * num4 * 4;
				int num6 = 0;
				fixed (byte* ptr3 = bytes)
				{
					fixed (byte* ptr4 = buffer)
					{
						while (num6 < num5)
						{
							int num7 = ptr3[num2] * 4;
							if (flag)
							{
								byte b = ptr3[num2 + 1];
								if (b > 0)
								{
									ptr4[num6] = ptr3[num2 + 4];
									ptr4[num6 + 1] = ptr3[num2 + 3];
									ptr4[num6 + 2] = ptr3[num2 + 2];
									ptr4[num6 + 3] = b;
									num2 += 5;
								}
								else
								{
									ptr4[num6] = 0;
									ptr4[num6 + 1] = 0;
									ptr4[num6 + 2] = 0;
									ptr4[num6 + 3] = 0;
									num2 += 2;
								}
							}
							else
							{
								ptr4[num6] = ptr3[num2 + 3];
								ptr4[num6 + 1] = ptr3[num2 + 2];
								ptr4[num6 + 2] = ptr3[num2 + 1];
								ptr4[num6 + 3] = byte.MaxValue;
								num2 += 4;
							}
							if (num7 > 4)
							{
								int k = num6 + 4;
								for (int num8 = num6 + num7; k < num8; k += 4)
								{
									ptr4[k] = ptr4[num6];
									ptr4[k + 1] = ptr4[num6 + 1];
									ptr4[k + 2] = ptr4[num6 + 2];
									ptr4[k + 3] = ptr4[num6 + 3];
								}
							}
							num6 += num7;
							if (num2 > 524256)
							{
								int num9 = 524288 - num2;
								for (int l = 0; l < num9; l++)
								{
									ptr3[l] = ptr3[num2 + l];
								}
								fileStream.Read(bytes, num9, 524288 - num9);
								num2 = 0;
							}
						}
					}
				}
				Texture = new Texture2D(Engine.Graphics.GraphicsDevice, num3, num4);
				Texture.SetData(buffer, 0, num5);
			}
			break;
		}
		case ".png":
		{
			using (Stream stream2 = ContentFiles.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
			{
				Texture = Texture2D.FromStream(Engine.Graphics.GraphicsDevice, stream2);
			}
			int num = Texture.Width * Texture.Height;
			Color[] array2 = new Color[num];
			Texture.GetData(array2, 0, num);
			fixed (Color* ptr2 = array2)
			{
				for (int j = 0; j < num; j++)
				{
					ptr2[j].R = (byte)((float)(int)ptr2[j].R * ((float)(int)ptr2[j].A / 255f));
					ptr2[j].G = (byte)((float)(int)ptr2[j].G * ((float)(int)ptr2[j].A / 255f));
					ptr2[j].B = (byte)((float)(int)ptr2[j].B * ((float)(int)ptr2[j].A / 255f));
				}
			}
			Texture.SetData(array2, 0, num);
			break;
		}
		case ".xnb":
		{
			string assetName = Path.Replace(".xnb", "");
			Texture = Engine.Instance.Content.Load<Texture2D>(assetName);
			break;
		}
		default:
		{
			using (Stream stream = ContentFiles.OpenRead(System.IO.Path.Combine(Engine.ContentDirectory, Path)))
			{
				Texture = Texture2D.FromStream(Engine.Graphics.GraphicsDevice, stream);
			}
			break;
		}
		}
		base.Width = Texture.Width;
		base.Height = Texture.Height;
	}

	public override void Dispose()
	{
		Unload();
		Texture = null;
		VirtualContent.Remove(this);
	}
}
