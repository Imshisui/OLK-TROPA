using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste;

public class MapData
{
	public AreaKey Area;

	public AreaData Data;

	public ModeProperties ModeData;

	public int DetectedStrawberries;

	public bool DetectedRemixNotes;

	public bool DetectedHeartGem;

	public List<LevelData> Levels = new List<LevelData>();

	public List<Rectangle> Filler = new List<Rectangle>();

	public List<EntityData> Strawberries = new List<EntityData>();

	public List<EntityData> Goldenberries = new List<EntityData>();

	public Color BackgroundColor = Color.Black;

	public BinaryPacker.Element Foreground;

	public BinaryPacker.Element Background;

	public Rectangle Bounds;

	public string Filename => Data.Mode[(int)Area.Mode].Path;

	public string Filepath => Path.Combine(Engine.ContentDirectory, "Maps", Filename + ".bin");

	public Rectangle TileBounds => new Rectangle(Bounds.X / 8, Bounds.Y / 8, (int)Math.Ceiling((float)Bounds.Width / 8f), (int)Math.Ceiling((float)Bounds.Height / 8f));

	public int LoadSeed
	{
		get
		{
			int num = 0;
			string name = Data.Name;
			foreach (char c in name)
			{
				num += c;
			}
			return num;
		}
	}

	public int LevelCount
	{
		get
		{
			int num = 0;
			foreach (LevelData level in Levels)
			{
				if (!level.Dummy)
				{
					num++;
				}
			}
			return num;
		}
	}

	public MapData(AreaKey area)
	{
		Area = area;
		Data = AreaData.Areas[Area.ID];
		ModeData = Data.Mode[(int)Area.Mode];
		Load();
	}

	public LevelData GetTransitionTarget(Level level, Vector2 position)
	{
		return GetAt(position);
	}

	public bool CanTransitionTo(Level level, Vector2 position)
	{
		LevelData transitionTarget = GetTransitionTarget(level, position);
		if (transitionTarget != null)
		{
			return !transitionTarget.Dummy;
		}
		return false;
	}

	public void Reload()
	{
		Load();
	}

	private void Load()
	{
		if (!ContentFiles.FileExists(Filepath))
		{
			return;
		}
		Strawberries = new List<EntityData>();
		BinaryPacker.Element element = BinaryPacker.FromBinary(Filepath);
		if (!element.Package.Equals(ModeData.Path))
		{
			throw new Exception("Corrupted Level Data");
		}
		foreach (BinaryPacker.Element child in element.Children)
		{
			if (child.Name == "levels")
			{
				Levels = new List<LevelData>();
				foreach (BinaryPacker.Element child2 in child.Children)
				{
					LevelData levelData = new LevelData(child2);
					DetectedStrawberries += levelData.Strawberries;
					if (levelData.HasGem)
					{
						DetectedRemixNotes = true;
					}
					if (levelData.HasHeartGem)
					{
						DetectedHeartGem = true;
					}
					Levels.Add(levelData);
				}
			}
			else if (child.Name == "Filler")
			{
				Filler = new List<Rectangle>();
				if (child.Children == null)
				{
					continue;
				}
				foreach (BinaryPacker.Element child3 in child.Children)
				{
					Filler.Add(new Rectangle((int)child3.Attributes["x"], (int)child3.Attributes["y"], (int)child3.Attributes["w"], (int)child3.Attributes["h"]));
				}
			}
			else
			{
				if (!(child.Name == "Style"))
				{
					continue;
				}
				if (child.HasAttr("color"))
				{
					BackgroundColor = Calc.HexToColor(child.Attr("color"));
				}
				if (child.Children == null)
				{
					continue;
				}
				foreach (BinaryPacker.Element child4 in child.Children)
				{
					if (child4.Name == "Backgrounds")
					{
						Background = child4;
					}
					else if (child4.Name == "Foregrounds")
					{
						Foreground = child4;
					}
				}
			}
		}
		foreach (LevelData level in Levels)
		{
			foreach (EntityData entity in level.Entities)
			{
				if (entity.Name == "strawberry")
				{
					Strawberries.Add(entity);
				}
				else if (entity.Name == "goldenBerry")
				{
					Goldenberries.Add(entity);
				}
			}
		}
		int num = int.MaxValue;
		int num2 = int.MaxValue;
		int num3 = int.MinValue;
		int num4 = int.MinValue;
		foreach (LevelData level2 in Levels)
		{
			if (level2.Bounds.Left < num)
			{
				num = level2.Bounds.Left;
			}
			if (level2.Bounds.Top < num2)
			{
				num2 = level2.Bounds.Top;
			}
			if (level2.Bounds.Right > num3)
			{
				num3 = level2.Bounds.Right;
			}
			if (level2.Bounds.Bottom > num4)
			{
				num4 = level2.Bounds.Bottom;
			}
		}
		foreach (Rectangle item in Filler)
		{
			if (item.Left < num)
			{
				num = item.Left;
			}
			if (item.Top < num2)
			{
				num2 = item.Top;
			}
			if (item.Right > num3)
			{
				num3 = item.Right;
			}
			if (item.Bottom > num4)
			{
				num4 = item.Bottom;
			}
		}
		int num5 = 64;
		Bounds = new Rectangle(num - num5, num2 - num5, num3 - num + num5 * 2, num4 - num2 + num5 * 2);
		ModeData.TotalStrawberries = 0;
		ModeData.StartStrawberries = 0;
		ModeData.StrawberriesByCheckpoint = new EntityData[10, 25];
		int num6 = 0;
		while (ModeData.Checkpoints != null && num6 < ModeData.Checkpoints.Length)
		{
			if (ModeData.Checkpoints[num6] != null)
			{
				ModeData.Checkpoints[num6].Strawberries = 0;
			}
			num6++;
		}
		foreach (EntityData strawberry in Strawberries)
		{
			if (!strawberry.Bool("moon"))
			{
				int num7 = strawberry.Int("checkpointID");
				int num8 = strawberry.Int("order");
				if (ModeData.StrawberriesByCheckpoint[num7, num8] == null)
				{
					ModeData.StrawberriesByCheckpoint[num7, num8] = strawberry;
				}
				if (num7 == 0)
				{
					ModeData.StartStrawberries++;
				}
				else if (ModeData.Checkpoints != null)
				{
					ModeData.Checkpoints[num7 - 1].Strawberries++;
				}
				ModeData.TotalStrawberries++;
			}
		}
	}

	public int[] GetStrawberries(out int total)
	{
		total = 0;
		int[] array = new int[10];
		foreach (LevelData level in Levels)
		{
			foreach (EntityData entity in level.Entities)
			{
				if (entity.Name == "strawberry")
				{
					total++;
					array[entity.Int("checkpointID")]++;
				}
			}
		}
		return array;
	}

	public LevelData StartLevel()
	{
		return GetAt(Vector2.Zero);
	}

	public LevelData GetAt(Vector2 at)
	{
		foreach (LevelData level in Levels)
		{
			if (level.Check(at))
			{
				return level;
			}
		}
		return null;
	}

	public LevelData Get(string levelName)
	{
		foreach (LevelData level in Levels)
		{
			if (level.Name.Equals(levelName))
			{
				return level;
			}
		}
		return null;
	}

	public List<Backdrop> CreateBackdrops(BinaryPacker.Element data)
	{
		List<Backdrop> list = new List<Backdrop>();
		if (data != null && data.Children != null)
		{
			foreach (BinaryPacker.Element child in data.Children)
			{
				if (child.Name.Equals("apply", StringComparison.OrdinalIgnoreCase))
				{
					if (child.Children == null)
					{
						continue;
					}
					foreach (BinaryPacker.Element child2 in child.Children)
					{
						list.Add(ParseBackdrop(child2, child));
					}
				}
				else
				{
					list.Add(ParseBackdrop(child, null));
				}
			}
		}
		return list;
	}

	private Backdrop ParseBackdrop(BinaryPacker.Element child, BinaryPacker.Element above)
	{
		Backdrop backdrop = null;
		if (child.Name.Equals("parallax", StringComparison.OrdinalIgnoreCase))
		{
			string id = child.Attr("texture");
			string text = child.Attr("atlas", "game");
			MTexture mTexture = null;
			mTexture = ((text == "game" && GFX.Game.Has(id)) ? GFX.Game[id] : ((!(text == "gui") || !GFX.Gui.Has(id)) ? GFX.Misc[id] : GFX.Gui[id]));
			Parallax parallax = new Parallax(mTexture);
			backdrop = parallax;
			string text2 = "";
			if (child.HasAttr("blendmode"))
			{
				text2 = child.Attr("blendmode", "alphablend").ToLower();
			}
			else if (above != null && above.HasAttr("blendmode"))
			{
				text2 = above.Attr("blendmode", "alphablend").ToLower();
			}
			if (text2.Equals("additive"))
			{
				parallax.BlendState = BlendState.Additive;
			}
			parallax.DoFadeIn = bool.Parse(child.Attr("fadeIn", "false"));
		}
		else if (child.Name.Equals("snowfg", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new Snow(foreground: true);
		}
		else if (child.Name.Equals("snowbg", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new Snow(foreground: false);
		}
		else if (child.Name.Equals("windsnow", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new WindSnowFG();
		}
		else if (child.Name.Equals("dreamstars", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new DreamStars();
		}
		else if (child.Name.Equals("stars", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new StarsBG();
		}
		else if (child.Name.Equals("mirrorfg", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new MirrorFG();
		}
		else if (child.Name.Equals("reflectionfg", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new ReflectionFG();
		}
		else if (child.Name.Equals("godrays", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new Godrays();
		}
		else if (child.Name.Equals("tentacles", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new Tentacles((Tentacles.Side)Enum.Parse(typeof(Tentacles.Side), child.Attr("side", "Right")), Calc.HexToColor(child.Attr("color")), child.AttrFloat("offset"));
		}
		else if (child.Name.Equals("northernlights", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new NorthernLights();
		}
		else if (child.Name.Equals("bossStarField", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new FinalBossStarfield();
		}
		else if (child.Name.Equals("petals", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new Petals();
		}
		else if (child.Name.Equals("heatwave", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new HeatWave();
		}
		else if (child.Name.Equals("corestarsfg", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new CoreStarsFG();
		}
		else if (child.Name.Equals("starfield", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new Starfield(Calc.HexToColor(child.Attr("color")), child.AttrFloat("speed", 1f));
		}
		else if (child.Name.Equals("planets", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new Planets((int)child.AttrFloat("count", 32f), child.Attr("size", "small"));
		}
		else if (child.Name.Equals("rain", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new RainFG();
		}
		else if (child.Name.Equals("stardust", StringComparison.OrdinalIgnoreCase))
		{
			backdrop = new StardustFG();
		}
		else
		{
			if (!child.Name.Equals("blackhole", StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception("Background type " + child.Name + " does not exist");
			}
			backdrop = new BlackholeBG();
		}
		if (child.HasAttr("tag"))
		{
			backdrop.Tags.Add(child.Attr("tag"));
		}
		if (above != null && above.HasAttr("tag"))
		{
			backdrop.Tags.Add(above.Attr("tag"));
		}
		if (child.HasAttr("x"))
		{
			backdrop.Position.X = child.AttrFloat("x");
		}
		else if (above != null && above.HasAttr("x"))
		{
			backdrop.Position.X = above.AttrFloat("x");
		}
		if (child.HasAttr("y"))
		{
			backdrop.Position.Y = child.AttrFloat("y");
		}
		else if (above != null && above.HasAttr("y"))
		{
			backdrop.Position.Y = above.AttrFloat("y");
		}
		if (child.HasAttr("scrollx"))
		{
			backdrop.Scroll.X = child.AttrFloat("scrollx");
		}
		else if (above != null && above.HasAttr("scrollx"))
		{
			backdrop.Scroll.X = above.AttrFloat("scrollx");
		}
		if (child.HasAttr("scrolly"))
		{
			backdrop.Scroll.Y = child.AttrFloat("scrolly");
		}
		else if (above != null && above.HasAttr("scrolly"))
		{
			backdrop.Scroll.Y = above.AttrFloat("scrolly");
		}
		if (child.HasAttr("speedx"))
		{
			backdrop.Speed.X = child.AttrFloat("speedx");
		}
		else if (above != null && above.HasAttr("speedx"))
		{
			backdrop.Speed.X = above.AttrFloat("speedx");
		}
		if (child.HasAttr("speedy"))
		{
			backdrop.Speed.Y = child.AttrFloat("speedy");
		}
		else if (above != null && above.HasAttr("speedy"))
		{
			backdrop.Speed.Y = above.AttrFloat("speedy");
		}
		backdrop.Color = Color.White;
		if (child.HasAttr("color"))
		{
			backdrop.Color = Calc.HexToColor(child.Attr("color"));
		}
		else if (above != null && above.HasAttr("color"))
		{
			backdrop.Color = Calc.HexToColor(above.Attr("color"));
		}
		if (child.HasAttr("alpha"))
		{
			backdrop.Color *= child.AttrFloat("alpha");
		}
		else if (above != null && above.HasAttr("alpha"))
		{
			backdrop.Color *= above.AttrFloat("alpha");
		}
		if (child.HasAttr("flipx"))
		{
			backdrop.FlipX = child.AttrBool("flipx");
		}
		else if (above != null && above.HasAttr("flipx"))
		{
			backdrop.FlipX = above.AttrBool("flipx");
		}
		if (child.HasAttr("flipy"))
		{
			backdrop.FlipY = child.AttrBool("flipy");
		}
		else if (above != null && above.HasAttr("flipy"))
		{
			backdrop.FlipY = above.AttrBool("flipy");
		}
		if (child.HasAttr("loopx"))
		{
			backdrop.LoopX = child.AttrBool("loopx");
		}
		else if (above != null && above.HasAttr("loopx"))
		{
			backdrop.LoopX = above.AttrBool("loopx");
		}
		if (child.HasAttr("loopy"))
		{
			backdrop.LoopY = child.AttrBool("loopy");
		}
		else if (above != null && above.HasAttr("loopy"))
		{
			backdrop.LoopY = above.AttrBool("loopy");
		}
		if (child.HasAttr("wind"))
		{
			backdrop.WindMultiplier = child.AttrFloat("wind");
		}
		else if (above != null && above.HasAttr("wind"))
		{
			backdrop.WindMultiplier = above.AttrFloat("wind");
		}
		string text3 = null;
		if (child.HasAttr("exclude"))
		{
			text3 = child.Attr("exclude");
		}
		else if (above != null && above.HasAttr("exclude"))
		{
			text3 = above.Attr("exclude");
		}
		if (text3 != null)
		{
			backdrop.ExcludeFrom = ParseLevelsList(text3);
		}
		string text4 = null;
		if (child.HasAttr("only"))
		{
			text4 = child.Attr("only");
		}
		else if (above != null && above.HasAttr("only"))
		{
			text4 = above.Attr("only");
		}
		if (text4 != null)
		{
			backdrop.OnlyIn = ParseLevelsList(text4);
		}
		string text5 = null;
		if (child.HasAttr("flag"))
		{
			text5 = child.Attr("flag");
		}
		else if (above != null && above.HasAttr("flag"))
		{
			text5 = above.Attr("flag");
		}
		if (text5 != null)
		{
			backdrop.OnlyIfFlag = text5;
		}
		string text6 = null;
		if (child.HasAttr("notflag"))
		{
			text6 = child.Attr("notflag");
		}
		else if (above != null && above.HasAttr("notflag"))
		{
			text6 = above.Attr("notflag");
		}
		if (text6 != null)
		{
			backdrop.OnlyIfNotFlag = text6;
		}
		string text7 = null;
		if (child.HasAttr("always"))
		{
			text7 = child.Attr("always");
		}
		else if (above != null && above.HasAttr("always"))
		{
			text7 = above.Attr("always");
		}
		if (text7 != null)
		{
			backdrop.AlsoIfFlag = text7;
		}
		bool? dreaming = null;
		if (child.HasAttr("dreaming"))
		{
			dreaming = child.AttrBool("dreaming");
		}
		else if (above != null && above.HasAttr("dreaming"))
		{
			dreaming = above.AttrBool("dreaming");
		}
		if (dreaming.HasValue)
		{
			backdrop.Dreaming = dreaming;
		}
		if (child.HasAttr("instantIn"))
		{
			backdrop.InstantIn = child.AttrBool("instantIn");
		}
		else if (above != null && above.HasAttr("instantIn"))
		{
			backdrop.InstantIn = above.AttrBool("instantIn");
		}
		if (child.HasAttr("instantOut"))
		{
			backdrop.InstantOut = child.AttrBool("instantOut");
		}
		else if (above != null && above.HasAttr("instantOut"))
		{
			backdrop.InstantOut = above.AttrBool("instantOut");
		}
		string text8 = null;
		if (child.HasAttr("fadex"))
		{
			text8 = child.Attr("fadex");
		}
		else if (above != null && above.HasAttr("fadex"))
		{
			text8 = above.Attr("fadex");
		}
		if (text8 != null)
		{
			backdrop.FadeX = new Backdrop.Fader();
			string[] array = text8.Split(':');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length == 2)
				{
					string[] array3 = array2[0].Split('-');
					string[] array4 = array2[1].Split('-');
					float fadeFrom = float.Parse(array4[0], CultureInfo.InvariantCulture);
					float fadeTo = float.Parse(array4[1], CultureInfo.InvariantCulture);
					int num = 1;
					int num2 = 1;
					if (array3[0][0] == 'n')
					{
						num = -1;
						array3[0] = array3[0].Substring(1);
					}
					if (array3[1][0] == 'n')
					{
						num2 = -1;
						array3[1] = array3[1].Substring(1);
					}
					backdrop.FadeX.Add(num * int.Parse(array3[0]), num2 * int.Parse(array3[1]), fadeFrom, fadeTo);
				}
			}
		}
		string text9 = null;
		if (child.HasAttr("fadey"))
		{
			text9 = child.Attr("fadey");
		}
		else if (above != null && above.HasAttr("fadey"))
		{
			text9 = above.Attr("fadey");
		}
		if (text9 != null)
		{
			backdrop.FadeY = new Backdrop.Fader();
			string[] array = text9.Split(':');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array5 = array[i].Split(',');
				if (array5.Length == 2)
				{
					string[] array6 = array5[0].Split('-');
					string[] array7 = array5[1].Split('-');
					float fadeFrom2 = float.Parse(array7[0], CultureInfo.InvariantCulture);
					float fadeTo2 = float.Parse(array7[1], CultureInfo.InvariantCulture);
					int num3 = 1;
					int num4 = 1;
					if (array6[0][0] == 'n')
					{
						num3 = -1;
						array6[0] = array6[0].Substring(1);
					}
					if (array6[1][0] == 'n')
					{
						num4 = -1;
						array6[1] = array6[1].Substring(1);
					}
					backdrop.FadeY.Add(num3 * int.Parse(array6[0]), num4 * int.Parse(array6[1]), fadeFrom2, fadeTo2);
				}
			}
		}
		return backdrop;
	}

	private HashSet<string> ParseLevelsList(string list)
	{
		HashSet<string> hashSet = new HashSet<string>();
		string[] array = list.Split(',');
		foreach (string text in array)
		{
			if (text.Contains('*'))
			{
				string pattern = "^" + Regex.Escape(text).Replace("\\*", ".*") + "$";
				foreach (LevelData level in Levels)
				{
					if (Regex.IsMatch(level.Name, pattern))
					{
						hashSet.Add(level.Name);
					}
				}
			}
			else
			{
				hashSet.Add(text);
			}
		}
		return hashSet;
	}
}
