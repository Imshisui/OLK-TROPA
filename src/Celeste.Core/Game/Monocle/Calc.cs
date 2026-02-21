using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Celeste.Core.Platform.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monocle;

public static class Calc
{
	[CompilerGenerated]
	private sealed class _003CDo_003Ed__246 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public IEnumerator[] numerators;

		private List<Coroutine> _003Croutines_003E5__2;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CDo_003Ed__246(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			bool flag;
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
			{
				_003C_003E1__state = -1;
				if (numerators.Length == 0)
				{
					return false;
				}
				if (numerators.Length == 1)
				{
					_003C_003E2__current = numerators[0];
					_003C_003E1__state = 1;
					return true;
				}
				_003Croutines_003E5__2 = new List<Coroutine>();
				IEnumerator[] array = numerators;
				foreach (IEnumerator functionCall in array)
				{
					_003Croutines_003E5__2.Add(new Coroutine(functionCall));
				}
				goto IL_0091;
			}
			case 1:
				_003C_003E1__state = -1;
				break;
			case 2:
				{
					_003C_003E1__state = -1;
					goto IL_0091;
				}
				IL_0091:
				flag = false;
				foreach (Coroutine item in _003Croutines_003E5__2)
				{
					item.Update();
					if (!item.Finished)
					{
						flag = true;
					}
				}
				if (flag)
				{
					_003C_003E2__current = null;
					_003C_003E1__state = 2;
					return true;
				}
				_003Croutines_003E5__2 = null;
				break;
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	public static Random Random = new Random();

	private static Stack<Random> randomStack = new Stack<Random>();

	private static int[] shakeVectorOffsets = new int[5] { -1, -1, 0, 1, 1 };

	public const float Right = 0f;

	public const float Up = -(float)Math.PI / 2f;

	public const float Left = (float)Math.PI;

	public const float Down = (float)Math.PI / 2f;

	public const float UpRight = -(float)Math.PI / 4f;

	public const float UpLeft = (float)Math.PI * -3f / 4f;

	public const float DownRight = (float)Math.PI / 4f;

	public const float DownLeft = (float)Math.PI * 3f / 4f;

	public const float DegToRad = (float)Math.PI / 180f;

	public const float RadToDeg = 180f / (float)Math.PI;

	public const float DtR = (float)Math.PI / 180f;

	public const float RtD = 180f / (float)Math.PI;

	public const float Circle = (float)Math.PI * 2f;

	public const float HalfCircle = (float)Math.PI;

	public const float QuarterCircle = (float)Math.PI / 2f;

	public const float EighthCircle = (float)Math.PI / 4f;

	private const string Hex = "0123456789ABCDEF";

	private static Stopwatch stopwatch;

	public static int EnumLength(Type e)
	{
		return Enum.GetNames(e).Length;
	}

	public static T StringToEnum<T>(string str) where T : struct
	{
		if (Enum.IsDefined(typeof(T), str))
		{
			return (T)Enum.Parse(typeof(T), str);
		}
		throw new Exception("The string cannot be converted to the enum type.");
	}

	public static T[] StringsToEnums<T>(string[] strs) where T : struct
	{
		T[] array = new T[strs.Length];
		for (int i = 0; i < strs.Length; i++)
		{
			array[i] = StringToEnum<T>(strs[i]);
		}
		return array;
	}

	public static bool EnumHasString<T>(string str) where T : struct
	{
		return Enum.IsDefined(typeof(T), str);
	}

	public static bool StartsWith(this string str, string match)
	{
		return str.IndexOf(match) == 0;
	}

	public static bool EndsWith(this string str, string match)
	{
		return str.LastIndexOf(match) == str.Length - match.Length;
	}

	public static bool IsIgnoreCase(this string str, params string[] matches)
	{
		if (string.IsNullOrEmpty(str))
		{
			return false;
		}
		foreach (string value in matches)
		{
			if (str.Equals(value, StringComparison.InvariantCultureIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	public static string ToString(this int num, int minDigits)
	{
		string text = num.ToString();
		while (text.Length < minDigits)
		{
			text = "0" + text;
		}
		return text;
	}

	public static string[] SplitLines(string text, SpriteFont font, int maxLineWidth, char newLine = '\n')
	{
		List<string> list = new List<string>();
		string[] array = text.Split(newLine);
		foreach (string obj in array)
		{
			string text2 = "";
			string[] array2 = obj.Split(' ');
			foreach (string text3 in array2)
			{
				if (font.MeasureString(text2 + " " + text3).X > (float)maxLineWidth)
				{
					list.Add(text2);
					text2 = text3;
					continue;
				}
				if (text2 != "")
				{
					text2 += " ";
				}
				text2 += text3;
			}
			list.Add(text2);
		}
		return list.ToArray();
	}

	public static int Count<T>(T target, T a, T b)
	{
		int num = 0;
		if (a.Equals(target))
		{
			num++;
		}
		if (b.Equals(target))
		{
			num++;
		}
		return num;
	}

	public static int Count<T>(T target, T a, T b, T c)
	{
		int num = 0;
		if (a.Equals(target))
		{
			num++;
		}
		if (b.Equals(target))
		{
			num++;
		}
		if (c.Equals(target))
		{
			num++;
		}
		return num;
	}

	public static int Count<T>(T target, T a, T b, T c, T d)
	{
		int num = 0;
		if (a.Equals(target))
		{
			num++;
		}
		if (b.Equals(target))
		{
			num++;
		}
		if (c.Equals(target))
		{
			num++;
		}
		if (d.Equals(target))
		{
			num++;
		}
		return num;
	}

	public static int Count<T>(T target, T a, T b, T c, T d, T e)
	{
		int num = 0;
		if (a.Equals(target))
		{
			num++;
		}
		if (b.Equals(target))
		{
			num++;
		}
		if (c.Equals(target))
		{
			num++;
		}
		if (d.Equals(target))
		{
			num++;
		}
		if (e.Equals(target))
		{
			num++;
		}
		return num;
	}

	public static int Count<T>(T target, T a, T b, T c, T d, T e, T f)
	{
		int num = 0;
		if (a.Equals(target))
		{
			num++;
		}
		if (b.Equals(target))
		{
			num++;
		}
		if (c.Equals(target))
		{
			num++;
		}
		if (d.Equals(target))
		{
			num++;
		}
		if (e.Equals(target))
		{
			num++;
		}
		if (f.Equals(target))
		{
			num++;
		}
		return num;
	}

	public static T GiveMe<T>(int index, T a, T b)
	{
		return index switch
		{
			0 => a, 
			1 => b, 
			_ => throw new Exception("Index was out of range!"), 
		};
	}

	public static T GiveMe<T>(int index, T a, T b, T c)
	{
		return index switch
		{
			0 => a, 
			1 => b, 
			2 => c, 
			_ => throw new Exception("Index was out of range!"), 
		};
	}

	public static T GiveMe<T>(int index, T a, T b, T c, T d)
	{
		return index switch
		{
			0 => a, 
			1 => b, 
			2 => c, 
			3 => d, 
			_ => throw new Exception("Index was out of range!"), 
		};
	}

	public static T GiveMe<T>(int index, T a, T b, T c, T d, T e)
	{
		return index switch
		{
			0 => a, 
			1 => b, 
			2 => c, 
			3 => d, 
			4 => e, 
			_ => throw new Exception("Index was out of range!"), 
		};
	}

	public static T GiveMe<T>(int index, T a, T b, T c, T d, T e, T f)
	{
		return index switch
		{
			0 => a, 
			1 => b, 
			2 => c, 
			3 => d, 
			4 => e, 
			5 => f, 
			_ => throw new Exception("Index was out of range!"), 
		};
	}

	public static void PushRandom(int newSeed)
	{
		randomStack.Push(Random);
		Random = new Random(newSeed);
	}

	public static void PushRandom(Random random)
	{
		randomStack.Push(Random);
		Random = random;
	}

	public static void PushRandom()
	{
		randomStack.Push(Random);
		Random = new Random();
	}

	public static void PopRandom()
	{
		Random = randomStack.Pop();
	}

	public static T Choose<T>(this Random random, T a, T b)
	{
		return GiveMe(random.Next(2), a, b);
	}

	public static T Choose<T>(this Random random, T a, T b, T c)
	{
		return GiveMe(random.Next(3), a, b, c);
	}

	public static T Choose<T>(this Random random, T a, T b, T c, T d)
	{
		return GiveMe(random.Next(4), a, b, c, d);
	}

	public static T Choose<T>(this Random random, T a, T b, T c, T d, T e)
	{
		return GiveMe(random.Next(5), a, b, c, d, e);
	}

	public static T Choose<T>(this Random random, T a, T b, T c, T d, T e, T f)
	{
		return GiveMe(random.Next(6), a, b, c, d, e, f);
	}

	public static T Choose<T>(this Random random, params T[] choices)
	{
		return choices[random.Next(choices.Length)];
	}

	public static T Choose<T>(this Random random, List<T> choices)
	{
		return choices[random.Next(choices.Count)];
	}

	public static int Range(this Random random, int min, int max)
	{
		return min + random.Next(max - min);
	}

	public static float Range(this Random random, float min, float max)
	{
		return min + random.NextFloat(max - min);
	}

	public static Vector2 Range(this Random random, Vector2 min, Vector2 max)
	{
		return min + new Vector2(random.NextFloat(max.X - min.X), random.NextFloat(max.Y - min.Y));
	}

	public static int Facing(this Random random)
	{
		if (!(random.NextFloat() < 0.5f))
		{
			return 1;
		}
		return -1;
	}

	public static bool Chance(this Random random, float chance)
	{
		return random.NextFloat() < chance;
	}

	public static float NextFloat(this Random random)
	{
		return (float)random.NextDouble();
	}

	public static float NextFloat(this Random random, float max)
	{
		return random.NextFloat() * max;
	}

	public static float NextAngle(this Random random)
	{
		return random.NextFloat() * ((float)Math.PI * 2f);
	}

	public static Vector2 ShakeVector(this Random random)
	{
		return new Vector2(random.Choose(shakeVectorOffsets), random.Choose(shakeVectorOffsets));
	}

	public static Vector2 ClosestTo(this List<Vector2> list, Vector2 to)
	{
		Vector2 result = list[0];
		float num = Vector2.DistanceSquared(list[0], to);
		for (int i = 1; i < list.Count; i++)
		{
			float num2 = Vector2.DistanceSquared(list[i], to);
			if (num2 < num)
			{
				num = num2;
				result = list[i];
			}
		}
		return result;
	}

	public static Vector2 ClosestTo(this Vector2[] list, Vector2 to)
	{
		Vector2 result = list[0];
		float num = Vector2.DistanceSquared(list[0], to);
		for (int i = 1; i < list.Length; i++)
		{
			float num2 = Vector2.DistanceSquared(list[i], to);
			if (num2 < num)
			{
				num = num2;
				result = list[i];
			}
		}
		return result;
	}

	public static Vector2 ClosestTo(this Vector2[] list, Vector2 to, out int index)
	{
		index = 0;
		Vector2 result = list[0];
		float num = Vector2.DistanceSquared(list[0], to);
		for (int i = 1; i < list.Length; i++)
		{
			float num2 = Vector2.DistanceSquared(list[i], to);
			if (num2 < num)
			{
				index = i;
				num = num2;
				result = list[i];
			}
		}
		return result;
	}

	public static void Shuffle<T>(this List<T> list, Random random)
	{
		int num = list.Count;
		while (--num > 0)
		{
			T value = list[num];
			int index;
			list[num] = list[index = random.Next(num + 1)];
			list[index] = value;
		}
	}

	public static void Shuffle<T>(this List<T> list)
	{
		list.Shuffle(Random);
	}

	public static void ShuffleSetFirst<T>(this List<T> list, Random random, T first)
	{
		int num = 0;
		while (list.Contains(first))
		{
			list.Remove(first);
			num++;
		}
		list.Shuffle(random);
		for (int i = 0; i < num; i++)
		{
			list.Insert(0, first);
		}
	}

	public static void ShuffleSetFirst<T>(this List<T> list, T first)
	{
		list.ShuffleSetFirst(Random, first);
	}

	public static void ShuffleNotFirst<T>(this List<T> list, Random random, T notFirst)
	{
		int num = 0;
		while (list.Contains(notFirst))
		{
			list.Remove(notFirst);
			num++;
		}
		list.Shuffle(random);
		for (int i = 0; i < num; i++)
		{
			list.Insert(random.Next(list.Count - 1) + 1, notFirst);
		}
	}

	public static void ShuffleNotFirst<T>(this List<T> list, T notFirst)
	{
		list.ShuffleNotFirst(Random, notFirst);
	}

	public static Color Invert(this Color color)
	{
		return new Color(255 - color.R, 255 - color.G, 255 - color.B, color.A);
	}

	public static Color HexToColor(string hex)
	{
		int num = 0;
		if (hex.Length >= 1 && hex[0] == '#')
		{
			num = 1;
		}
		if (hex.Length - num >= 6)
		{
			float r = (float)(HexToByte(hex[num]) * 16 + HexToByte(hex[num + 1])) / 255f;
			float g = (float)(HexToByte(hex[num + 2]) * 16 + HexToByte(hex[num + 3])) / 255f;
			float b = (float)(HexToByte(hex[num + 4]) * 16 + HexToByte(hex[num + 5])) / 255f;
			return new Color(r, g, b);
		}
		if (int.TryParse(hex.Substring(num), out var result))
		{
			return HexToColor(result);
		}
		return Color.White;
	}

	public static Color HexToColor(int hex)
	{
		return new Color
		{
			A = byte.MaxValue,
			R = (byte)(hex >> 16),
			G = (byte)(hex >> 8),
			B = (byte)hex
		};
	}

	public static Color HsvToColor(float hue, float s, float v)
	{
		int num = (int)(hue * 360f);
		float num2 = s * v;
		float num3 = num2 * (1f - Math.Abs((float)num / 60f % 2f - 1f));
		float num4 = v - num2;
		if (num < 60)
		{
			return new Color(num4 + num2, num4 + num3, num4);
		}
		if (num < 120)
		{
			return new Color(num4 + num3, num4 + num2, num4);
		}
		if (num < 180)
		{
			return new Color(num4, num4 + num2, num4 + num3);
		}
		if (num < 240)
		{
			return new Color(num4, num4 + num3, num4 + num2);
		}
		if (num < 300)
		{
			return new Color(num4 + num3, num4, num4 + num2);
		}
		return new Color(num4 + num2, num4, num4 + num3);
	}

	public static string ShortGameplayFormat(this TimeSpan time)
	{
		if (time.TotalHours >= 1.0)
		{
			return (int)time.TotalHours + ":" + time.ToString("mm\\:ss\\.fff");
		}
		return time.ToString("m\\:ss\\.fff");
	}

	public static string LongGameplayFormat(this TimeSpan time)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (time.TotalDays >= 2.0)
		{
			stringBuilder.Append((int)time.TotalDays);
			stringBuilder.Append(" days, ");
		}
		else if (time.TotalDays >= 1.0)
		{
			stringBuilder.Append("1 day, ");
		}
		stringBuilder.Append((time.TotalHours - (double)((int)time.TotalDays * 24)).ToString("0.0"));
		stringBuilder.Append(" hours");
		return stringBuilder.ToString();
	}

	public static int Digits(this int num)
	{
		int num2 = 1;
		int num3 = 10;
		while (num >= num3)
		{
			num2++;
			num3 *= 10;
		}
		return num2;
	}

	public static byte HexToByte(char c)
	{
		return (byte)"0123456789ABCDEF".IndexOf(char.ToUpper(c));
	}

	public static float Percent(float num, float zeroAt, float oneAt)
	{
		return MathHelper.Clamp((num - zeroAt) / (oneAt - zeroAt), 0f, 1f);
	}

	public static float SignThreshold(float value, float threshold)
	{
		if (Math.Abs(value) >= threshold)
		{
			return Math.Sign(value);
		}
		return 0f;
	}

	public static float Min(params float[] values)
	{
		float num = values[0];
		for (int i = 1; i < values.Length; i++)
		{
			num = MathHelper.Min(values[i], num);
		}
		return num;
	}

	public static float Max(params float[] values)
	{
		float num = values[0];
		for (int i = 1; i < values.Length; i++)
		{
			num = MathHelper.Max(values[i], num);
		}
		return num;
	}

	public static int Max(int a, int b, int c, int d)
	{
		return Math.Max(Math.Max(Math.Max(a, b), c), d);
	}

	public static float ToRad(this float f)
	{
		return f * ((float)Math.PI / 180f);
	}

	public static float ToDeg(this float f)
	{
		return f * (180f / (float)Math.PI);
	}

	public static int Axis(bool negative, bool positive, int both = 0)
	{
		if (negative)
		{
			if (positive)
			{
				return both;
			}
			return -1;
		}
		if (positive)
		{
			return 1;
		}
		return 0;
	}

	public static int Clamp(int value, int min, int max)
	{
		return Math.Min(Math.Max(value, min), max);
	}

	public static float Clamp(float value, float min, float max)
	{
		return Math.Min(Math.Max(value, min), max);
	}

	public static float YoYo(float value)
	{
		if (value <= 0.5f)
		{
			return value * 2f;
		}
		return 1f - (value - 0.5f) * 2f;
	}

	public static float Map(float val, float min, float max, float newMin = 0f, float newMax = 1f)
	{
		return (val - min) / (max - min) * (newMax - newMin) + newMin;
	}

	public static float SineMap(float counter, float newMin, float newMax)
	{
		return Map((float)Math.Sin(counter), -1f, 1f, newMin, newMax);
	}

	public static float ClampedMap(float val, float min, float max, float newMin = 0f, float newMax = 1f)
	{
		return MathHelper.Clamp((val - min) / (max - min), 0f, 1f) * (newMax - newMin) + newMin;
	}

	public static float LerpSnap(float value1, float value2, float amount, float snapThreshold = 0.1f)
	{
		float num = MathHelper.Lerp(value1, value2, amount);
		if (Math.Abs(num - value2) < snapThreshold)
		{
			return value2;
		}
		return num;
	}

	public static float LerpClamp(float value1, float value2, float lerp)
	{
		return MathHelper.Lerp(value1, value2, MathHelper.Clamp(lerp, 0f, 1f));
	}

	public static Vector2 LerpSnap(Vector2 value1, Vector2 value2, float amount, float snapThresholdSq = 0.1f)
	{
		Vector2 vector = Vector2.Lerp(value1, value2, amount);
		if ((vector - value2).LengthSquared() < snapThresholdSq)
		{
			return value2;
		}
		return vector;
	}

	public static Vector2 Sign(this Vector2 vec)
	{
		return new Vector2(Math.Sign(vec.X), Math.Sign(vec.Y));
	}

	public static Vector2 SafeNormalize(this Vector2 vec)
	{
		return vec.SafeNormalize(Vector2.Zero);
	}

	public static Vector2 SafeNormalize(this Vector2 vec, float length)
	{
		return vec.SafeNormalize(Vector2.Zero, length);
	}

	public static Vector2 SafeNormalize(this Vector2 vec, Vector2 ifZero)
	{
		if (vec == Vector2.Zero)
		{
			return ifZero;
		}
		vec.Normalize();
		return vec;
	}

	public static Vector2 SafeNormalize(this Vector2 vec, Vector2 ifZero, float length)
	{
		if (vec == Vector2.Zero)
		{
			return ifZero * length;
		}
		vec.Normalize();
		return vec * length;
	}

	public static Vector2 TurnRight(this Vector2 vec)
	{
		return new Vector2(0f - vec.Y, vec.X);
	}

	public static float ReflectAngle(float angle, float axis = 0f)
	{
		return 0f - (angle + axis) - axis;
	}

	public static float ReflectAngle(float angleRadians, Vector2 axis)
	{
		return ReflectAngle(angleRadians, axis.Angle());
	}

	public static Vector2 ClosestPointOnLine(Vector2 lineA, Vector2 lineB, Vector2 closestTo)
	{
		Vector2 vector = lineB - lineA;
		float value = Vector2.Dot(closestTo - lineA, vector) / Vector2.Dot(vector, vector);
		value = MathHelper.Clamp(value, 0f, 1f);
		return lineA + vector * value;
	}

	public static Vector2 RoundV2(this Vector2 vec)
	{
		return new Vector2((float)Math.Round(vec.X), (float)Math.Round(vec.Y));
	}

	public static float Snap(float value, float increment)
	{
		return (float)Math.Round(value / increment) * increment;
	}

	public static float Snap(float value, float increment, float offset)
	{
		return (float)Math.Round((value - offset) / increment) * increment + offset;
	}

	public static float WrapAngleDeg(float angleDegrees)
	{
		return ((angleDegrees * (float)Math.Sign(angleDegrees) + 180f) % 360f - 180f) * (float)Math.Sign(angleDegrees);
	}

	public static float WrapAngle(float angleRadians)
	{
		return ((angleRadians * (float)Math.Sign(angleRadians) + (float)Math.PI) % ((float)Math.PI * 2f) - (float)Math.PI) * (float)Math.Sign(angleRadians);
	}

	public static Vector2 AngleToVector(float angleRadians, float length)
	{
		return new Vector2((float)Math.Cos(angleRadians) * length, (float)Math.Sin(angleRadians) * length);
	}

	public static float AngleApproach(float val, float target, float maxMove)
	{
		float value = AngleDiff(val, target);
		if (Math.Abs(value) < maxMove)
		{
			return target;
		}
		return val + MathHelper.Clamp(value, 0f - maxMove, maxMove);
	}

	public static float AngleLerp(float startAngle, float endAngle, float percent)
	{
		return startAngle + AngleDiff(startAngle, endAngle) * percent;
	}

	public static float Approach(float val, float target, float maxMove)
	{
		if (!(val > target))
		{
			return Math.Min(val + maxMove, target);
		}
		return Math.Max(val - maxMove, target);
	}

	public static float AngleDiff(float radiansA, float radiansB)
	{
		float num;
		for (num = radiansB - radiansA; num > (float)Math.PI; num -= (float)Math.PI * 2f)
		{
		}
		for (; num <= -(float)Math.PI; num += (float)Math.PI * 2f)
		{
		}
		return num;
	}

	public static float AbsAngleDiff(float radiansA, float radiansB)
	{
		return Math.Abs(AngleDiff(radiansA, radiansB));
	}

	public static int SignAngleDiff(float radiansA, float radiansB)
	{
		return Math.Sign(AngleDiff(radiansA, radiansB));
	}

	public static float Angle(Vector2 from, Vector2 to)
	{
		return (float)Math.Atan2(to.Y - from.Y, to.X - from.X);
	}

	public static Color ToggleColors(Color current, Color a, Color b)
	{
		if (current == a)
		{
			return b;
		}
		return a;
	}

	public static float ShorterAngleDifference(float currentAngle, float angleA, float angleB)
	{
		if (Math.Abs(AngleDiff(currentAngle, angleA)) < Math.Abs(AngleDiff(currentAngle, angleB)))
		{
			return angleA;
		}
		return angleB;
	}

	public static float ShorterAngleDifference(float currentAngle, float angleA, float angleB, float angleC)
	{
		if (Math.Abs(AngleDiff(currentAngle, angleA)) < Math.Abs(AngleDiff(currentAngle, angleB)))
		{
			return ShorterAngleDifference(currentAngle, angleA, angleC);
		}
		return ShorterAngleDifference(currentAngle, angleB, angleC);
	}

	public static bool IsInRange<T>(this T[] array, int index)
	{
		if (index >= 0)
		{
			return index < array.Length;
		}
		return false;
	}

	public static bool IsInRange<T>(this List<T> list, int index)
	{
		if (index >= 0)
		{
			return index < list.Count;
		}
		return false;
	}

	public static T[] Array<T>(params T[] items)
	{
		return items;
	}

	public static T[] VerifyLength<T>(this T[] array, int length)
	{
		if (array == null)
		{
			return new T[length];
		}
		if (array.Length != length)
		{
			T[] array2 = new T[length];
			for (int i = 0; i < Math.Min(length, array.Length); i++)
			{
				array2[i] = array[i];
			}
			return array2;
		}
		return array;
	}

	public static T[][] VerifyLength<T>(this T[][] array, int length0, int length1)
	{
		array = array.VerifyLength(length0);
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].VerifyLength(length1);
		}
		return array;
	}

	public static bool BetweenInterval(float val, float interval)
	{
		return val % (interval * 2f) > interval;
	}

	public static bool OnInterval(float val, float prevVal, float interval)
	{
		return (int)(prevVal / interval) != (int)(val / interval);
	}

	public static Vector2 Toward(Vector2 from, Vector2 to, float length)
	{
		if (from == to)
		{
			return Vector2.Zero;
		}
		return (to - from).SafeNormalize(length);
	}

	public static Vector2 Toward(Entity from, Entity to, float length)
	{
		return Toward(from.Position, to.Position, length);
	}

	public static Vector2 Perpendicular(this Vector2 vector)
	{
		return new Vector2(0f - vector.Y, vector.X);
	}

	public static float Angle(this Vector2 vector)
	{
		return (float)Math.Atan2(vector.Y, vector.X);
	}

	public static Vector2 Clamp(this Vector2 val, float minX, float minY, float maxX, float maxY)
	{
		return new Vector2(MathHelper.Clamp(val.X, minX, maxX), MathHelper.Clamp(val.Y, minY, maxY));
	}

	public static Vector2 FloorV2(this Vector2 val)
	{
		return new Vector2((int)Math.Floor(val.X), (int)Math.Floor(val.Y));
	}

	public static Vector2 Ceiling(this Vector2 val)
	{
		return new Vector2((int)Math.Ceiling(val.X), (int)Math.Ceiling(val.Y));
	}

	public static Vector2 Abs(this Vector2 val)
	{
		return new Vector2(Math.Abs(val.X), Math.Abs(val.Y));
	}

	public static Vector2 Approach(Vector2 val, Vector2 target, float maxMove)
	{
		if (maxMove == 0f || val == target)
		{
			return val;
		}
		Vector2 vector = target - val;
		if (vector.Length() < maxMove)
		{
			return target;
		}
		vector.Normalize();
		return val + vector * maxMove;
	}

	public static Vector2 FourWayNormal(this Vector2 vec)
	{
		if (vec == Vector2.Zero)
		{
			return Vector2.Zero;
		}
		vec = AngleToVector((float)Math.Floor((vec.Angle() + (float)Math.PI / 4f) / ((float)Math.PI / 2f)) * ((float)Math.PI / 2f), 1f);
		if (Math.Abs(vec.X) < 0.5f)
		{
			vec.X = 0f;
		}
		else
		{
			vec.X = Math.Sign(vec.X);
		}
		if (Math.Abs(vec.Y) < 0.5f)
		{
			vec.Y = 0f;
		}
		else
		{
			vec.Y = Math.Sign(vec.Y);
		}
		return vec;
	}

	public static Vector2 EightWayNormal(this Vector2 vec)
	{
		if (vec == Vector2.Zero)
		{
			return Vector2.Zero;
		}
		vec = AngleToVector((float)Math.Floor((vec.Angle() + (float)Math.PI / 8f) / ((float)Math.PI / 4f)) * ((float)Math.PI / 4f), 1f);
		if (Math.Abs(vec.X) < 0.5f)
		{
			vec.X = 0f;
		}
		else if (Math.Abs(vec.Y) < 0.5f)
		{
			vec.Y = 0f;
		}
		return vec;
	}

	public static Vector2 SnappedNormal(this Vector2 vec, float slices)
	{
		float num = (float)Math.PI * 2f / slices;
		return AngleToVector((float)Math.Floor((vec.Angle() + num / 2f) / num) * num, 1f);
	}

	public static Vector2 Snapped(this Vector2 vec, float slices)
	{
		float num = (float)Math.PI * 2f / slices;
		return AngleToVector((float)Math.Floor((vec.Angle() + num / 2f) / num) * num, vec.Length());
	}

	public static Vector2 XComp(this Vector2 vec)
	{
		return Vector2.UnitX * vec.X;
	}

	public static Vector2 YComp(this Vector2 vec)
	{
		return Vector2.UnitY * vec.Y;
	}

	public static Vector2[] ParseVector2List(string list, char seperator = '|')
	{
		string[] array = list.Split(seperator);
		Vector2[] array2 = new Vector2[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			string[] array3 = array[i].Split(',');
			array2[i] = new Vector2(Convert.ToInt32(array3[0]), Convert.ToInt32(array3[1]));
		}
		return array2;
	}

	public static Vector2 Rotate(this Vector2 vec, float angleRadians)
	{
		return AngleToVector(vec.Angle() + angleRadians, vec.Length());
	}

	public static Vector2 RotateTowards(this Vector2 vec, float targetAngleRadians, float maxMoveRadians)
	{
		return AngleToVector(AngleApproach(vec.Angle(), targetAngleRadians, maxMoveRadians), vec.Length());
	}

	public static Vector3 RotateTowards(this Vector3 from, Vector3 target, float maxRotationRadians)
	{
		Vector3 vector = Vector3.Cross(from, target);
		float num = from.Length();
		float num2 = target.Length();
		float w = (float)Math.Sqrt(num * num * (num2 * num2)) + Vector3.Dot(from, target);
		Quaternion rotation = new Quaternion(vector.X, vector.Y, vector.Z, w);
		if (rotation.Length() <= maxRotationRadians)
		{
			return target;
		}
		rotation.Normalize();
		rotation *= maxRotationRadians;
		return Vector3.Transform(from, rotation);
	}

	public static Vector2 XZ(this Vector3 vector)
	{
		return new Vector2(vector.X, vector.Z);
	}

	public static Vector3 Approach(this Vector3 v, Vector3 target, float amount)
	{
		if (amount > (target - v).Length())
		{
			return target;
		}
		return v + (target - v).SafeNormalize() * amount;
	}

	public static Vector3 SafeNormalize(this Vector3 v)
	{
		float num = v.Length();
		if (num > 0f)
		{
			return v / num;
		}
		return Vector3.Zero;
	}

	public static int[,] ReadCSVIntGrid(string csv, int width, int height)
	{
		int[,] array = new int[width, height];
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				array[i, j] = -1;
			}
		}
		string[] array2 = csv.Split('\n');
		for (int k = 0; k < height && k < array2.Length; k++)
		{
			string[] array3 = array2[k].Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			for (int l = 0; l < width && l < array3.Length; l++)
			{
				array[l, k] = Convert.ToInt32(array3[l]);
			}
		}
		return array;
	}

	public static int[] ReadCSVInt(string csv)
	{
		if (csv == "")
		{
			return new int[0];
		}
		string[] array = csv.Split(',');
		int[] array2 = new int[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array2[i] = Convert.ToInt32(array[i].Trim());
		}
		return array2;
	}

	public static int[] ReadCSVIntWithTricks(string csv)
	{
		if (csv == "")
		{
			return new int[0];
		}
		string[] array = csv.Split(',');
		List<int> list = new List<int>();
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text.IndexOf('-') != -1)
			{
				string[] array3 = text.Split('-');
				int num = Convert.ToInt32(array3[0]);
				int num2 = Convert.ToInt32(array3[1]);
				for (int j = num; j != num2; j += Math.Sign(num2 - num))
				{
					list.Add(j);
				}
				list.Add(num2);
			}
			else if (text.IndexOf('*') != -1)
			{
				string[] array4 = text.Split('*');
				int item = Convert.ToInt32(array4[0]);
				int num3 = Convert.ToInt32(array4[1]);
				for (int k = 0; k < num3; k++)
				{
					list.Add(item);
				}
			}
			else
			{
				list.Add(Convert.ToInt32(text));
			}
		}
		return list.ToArray();
	}

	public static string[] ReadCSV(string csv)
	{
		if (csv == "")
		{
			return new string[0];
		}
		string[] array = csv.Split(',');
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = array[i].Trim();
		}
		return array;
	}

	public static string IntGridToCSV(int[,] data)
	{
		StringBuilder stringBuilder = new StringBuilder();
		List<int> list = new List<int>();
		int num = 0;
		for (int i = 0; i < data.GetLength(1); i++)
		{
			int num2 = 0;
			for (int j = 0; j < data.GetLength(0); j++)
			{
				if (data[j, i] == -1)
				{
					num2++;
					continue;
				}
				for (int k = 0; k < num; k++)
				{
					stringBuilder.Append('\n');
				}
				for (int l = 0; l < num2; l++)
				{
					list.Add(-1);
				}
				num2 = (num = 0);
				list.Add(data[j, i]);
			}
			if (list.Count > 0)
			{
				stringBuilder.Append(string.Join(",", list));
				list.Clear();
			}
			num++;
		}
		return stringBuilder.ToString();
	}

	public static bool[,] GetBitData(string data, char rowSep = '\n')
	{
		int num = 0;
		for (int i = 0; i < data.Length; i++)
		{
			if (data[i] == '1' || data[i] == '0')
			{
				num++;
			}
			else if (data[i] == rowSep)
			{
				break;
			}
		}
		int num2 = data.Count((char c) => c == '\n') + 1;
		bool[,] array = new bool[num, num2];
		int num3 = 0;
		int num4 = 0;
		for (int num5 = 0; num5 < data.Length; num5++)
		{
			switch (data[num5])
			{
			case '1':
				array[num3, num4] = true;
				num3++;
				break;
			case '0':
				array[num3, num4] = false;
				num3++;
				break;
			case '\n':
				num3 = 0;
				num4++;
				break;
			}
		}
		return array;
	}

	public static void CombineBitData(bool[,] combineInto, string data, char rowSep = '\n')
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < data.Length; i++)
		{
			switch (data[i])
			{
			case '1':
				combineInto[num, num2] = true;
				num++;
				break;
			case '0':
				num++;
				break;
			case '\n':
				num = 0;
				num2++;
				break;
			}
		}
	}

	public static void CombineBitData(bool[,] combineInto, bool[,] data)
	{
		for (int i = 0; i < combineInto.GetLength(0); i++)
		{
			for (int j = 0; j < combineInto.GetLength(1); j++)
			{
				if (data[i, j])
				{
					combineInto[i, j] = true;
				}
			}
		}
	}

	public static int[] ConvertStringArrayToIntArray(string[] strings)
	{
		int[] array = new int[strings.Length];
		for (int i = 0; i < strings.Length; i++)
		{
			array[i] = Convert.ToInt32(strings[i]);
		}
		return array;
	}

	public static float[] ConvertStringArrayToFloatArray(string[] strings)
	{
		float[] array = new float[strings.Length];
		for (int i = 0; i < strings.Length; i++)
		{
			array[i] = Convert.ToSingle(strings[i], CultureInfo.InvariantCulture);
		}
		return array;
	}

	public static bool FileExists(string filename)
	{
		return File.Exists(filename);
	}

	public static bool SaveFile<T>(T obj, string filename) where T : new()
	{
		Stream stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
		try
		{
			new XmlSerializer(typeof(T)).Serialize(stream, obj);
			stream.Close();
			return true;
		}
		catch
		{
			stream.Close();
			return false;
		}
	}

	public static bool LoadFile<T>(string filename, ref T data) where T : new()
	{
		Stream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
		try
		{
			T val = (T)new XmlSerializer(typeof(T)).Deserialize(stream);
			stream.Close();
			data = val;
			return true;
		}
		catch
		{
			stream.Close();
			return false;
		}
	}

	public static XmlDocument LoadContentXML(string filename)
	{
		XmlDocument xmlDocument = new XmlDocument();
		string text = Path.Combine(Engine.ContentDirectory, filename);
		if (ContentFiles.FileExists(text))
		{
			using Stream inStream = ContentFiles.OpenRead(text);
			xmlDocument.Load(inStream);
			return xmlDocument;
		}

		string text2 = Path.Combine(Engine.Instance.Content.RootDirectory, filename);
		CelestePathBridge.LogWarn("CONTENT", "XML not found in external content path, trying TitleContainer: " + text2);
		using Stream inStream2 = TitleContainer.OpenStream(text2);
		xmlDocument.Load(inStream2);
		return xmlDocument;
	}

	public static XmlDocument LoadXML(string filename)
	{
		XmlDocument xmlDocument = new XmlDocument();
		using Stream inStream = ContentFiles.OpenRead(filename);
		xmlDocument.Load(inStream);
		return xmlDocument;
	}

	public static bool ContentXMLExists(string filename)
	{
		return ContentFiles.FileExists(Path.Combine(Engine.ContentDirectory, filename));
	}

	public static bool XMLExists(string filename)
	{
		return File.Exists(filename);
	}

	public static bool HasAttr(this XmlElement xml, string attributeName)
	{
		return xml.Attributes[attributeName] != null;
	}

	public static string Attr(this XmlElement xml, string attributeName)
	{
		return xml.Attributes[attributeName].InnerText;
	}

	public static string Attr(this XmlElement xml, string attributeName, string defaultValue)
	{
		if (!xml.HasAttr(attributeName))
		{
			return defaultValue;
		}
		return xml.Attributes[attributeName].InnerText;
	}

	public static int AttrInt(this XmlElement xml, string attributeName)
	{
		return Convert.ToInt32(xml.Attributes[attributeName].InnerText);
	}

	public static int AttrInt(this XmlElement xml, string attributeName, int defaultValue)
	{
		if (!xml.HasAttr(attributeName))
		{
			return defaultValue;
		}
		return Convert.ToInt32(xml.Attributes[attributeName].InnerText);
	}

	public static float AttrFloat(this XmlElement xml, string attributeName)
	{
		return Convert.ToSingle(xml.Attributes[attributeName].InnerText, CultureInfo.InvariantCulture);
	}

	public static float AttrFloat(this XmlElement xml, string attributeName, float defaultValue)
	{
		if (!xml.HasAttr(attributeName))
		{
			return defaultValue;
		}
		return Convert.ToSingle(xml.Attributes[attributeName].InnerText, CultureInfo.InvariantCulture);
	}

	public static Vector3 AttrVector3(this XmlElement xml, string attributeName)
	{
		string[] array = xml.Attr(attributeName).Split(',');
		float x = float.Parse(array[0].Trim(), CultureInfo.InvariantCulture);
		float y = float.Parse(array[1].Trim(), CultureInfo.InvariantCulture);
		float z = float.Parse(array[2].Trim(), CultureInfo.InvariantCulture);
		return new Vector3(x, y, z);
	}

	public static Vector2 AttrVector2(this XmlElement xml, string xAttributeName, string yAttributeName)
	{
		return new Vector2(xml.AttrFloat(xAttributeName), xml.AttrFloat(yAttributeName));
	}

	public static Vector2 AttrVector2(this XmlElement xml, string xAttributeName, string yAttributeName, Vector2 defaultValue)
	{
		return new Vector2(xml.AttrFloat(xAttributeName, defaultValue.X), xml.AttrFloat(yAttributeName, defaultValue.Y));
	}

	public static bool AttrBool(this XmlElement xml, string attributeName)
	{
		return Convert.ToBoolean(xml.Attributes[attributeName].InnerText);
	}

	public static bool AttrBool(this XmlElement xml, string attributeName, bool defaultValue)
	{
		if (!xml.HasAttr(attributeName))
		{
			return defaultValue;
		}
		return xml.AttrBool(attributeName);
	}

	public static char AttrChar(this XmlElement xml, string attributeName)
	{
		return Convert.ToChar(xml.Attributes[attributeName].InnerText);
	}

	public static char AttrChar(this XmlElement xml, string attributeName, char defaultValue)
	{
		if (!xml.HasAttr(attributeName))
		{
			return defaultValue;
		}
		return xml.AttrChar(attributeName);
	}

	public static T AttrEnum<T>(this XmlElement xml, string attributeName) where T : struct
	{
		if (Enum.IsDefined(typeof(T), xml.Attributes[attributeName].InnerText))
		{
			return (T)Enum.Parse(typeof(T), xml.Attributes[attributeName].InnerText);
		}
		throw new Exception("The attribute value cannot be converted to the enum type.");
	}

	public static T AttrEnum<T>(this XmlElement xml, string attributeName, T defaultValue) where T : struct
	{
		if (!xml.HasAttr(attributeName))
		{
			return defaultValue;
		}
		return xml.AttrEnum<T>(attributeName);
	}

	public static Color AttrHexColor(this XmlElement xml, string attributeName)
	{
		return HexToColor(xml.Attr(attributeName));
	}

	public static Color AttrHexColor(this XmlElement xml, string attributeName, Color defaultValue)
	{
		if (!xml.HasAttr(attributeName))
		{
			return defaultValue;
		}
		return xml.AttrHexColor(attributeName);
	}

	public static Color AttrHexColor(this XmlElement xml, string attributeName, string defaultValue)
	{
		if (!xml.HasAttr(attributeName))
		{
			return HexToColor(defaultValue);
		}
		return xml.AttrHexColor(attributeName);
	}

	public static Vector2 Position(this XmlElement xml)
	{
		return new Vector2(xml.AttrFloat("x"), xml.AttrFloat("y"));
	}

	public static Vector2 Position(this XmlElement xml, Vector2 defaultPosition)
	{
		return new Vector2(xml.AttrFloat("x", defaultPosition.X), xml.AttrFloat("y", defaultPosition.Y));
	}

	public static int X(this XmlElement xml)
	{
		return xml.AttrInt("x");
	}

	public static int X(this XmlElement xml, int defaultX)
	{
		return xml.AttrInt("x", defaultX);
	}

	public static int Y(this XmlElement xml)
	{
		return xml.AttrInt("y");
	}

	public static int Y(this XmlElement xml, int defaultY)
	{
		return xml.AttrInt("y", defaultY);
	}

	public static int Width(this XmlElement xml)
	{
		return xml.AttrInt("width");
	}

	public static int Width(this XmlElement xml, int defaultWidth)
	{
		return xml.AttrInt("width", defaultWidth);
	}

	public static int Height(this XmlElement xml)
	{
		return xml.AttrInt("height");
	}

	public static int Height(this XmlElement xml, int defaultHeight)
	{
		return xml.AttrInt("height", defaultHeight);
	}

	public static Rectangle Rect(this XmlElement xml)
	{
		return new Rectangle(xml.X(), xml.Y(), xml.Width(), xml.Height());
	}

	public static int ID(this XmlElement xml)
	{
		return xml.AttrInt("id");
	}

	public static int InnerInt(this XmlElement xml)
	{
		return Convert.ToInt32(xml.InnerText);
	}

	public static float InnerFloat(this XmlElement xml)
	{
		return Convert.ToSingle(xml.InnerText, CultureInfo.InvariantCulture);
	}

	public static bool InnerBool(this XmlElement xml)
	{
		return Convert.ToBoolean(xml.InnerText);
	}

	public static T InnerEnum<T>(this XmlElement xml) where T : struct
	{
		if (Enum.IsDefined(typeof(T), xml.InnerText))
		{
			return (T)Enum.Parse(typeof(T), xml.InnerText);
		}
		throw new Exception("The attribute value cannot be converted to the enum type.");
	}

	public static Color InnerHexColor(this XmlElement xml)
	{
		return HexToColor(xml.InnerText);
	}

	public static bool HasChild(this XmlElement xml, string childName)
	{
		return xml[childName] != null;
	}

	public static string ChildText(this XmlElement xml, string childName)
	{
		return xml[childName].InnerText;
	}

	public static string ChildText(this XmlElement xml, string childName, string defaultValue)
	{
		if (xml.HasChild(childName))
		{
			return xml[childName].InnerText;
		}
		return defaultValue;
	}

	public static int ChildInt(this XmlElement xml, string childName)
	{
		return xml[childName].InnerInt();
	}

	public static int ChildInt(this XmlElement xml, string childName, int defaultValue)
	{
		if (xml.HasChild(childName))
		{
			return xml[childName].InnerInt();
		}
		return defaultValue;
	}

	public static float ChildFloat(this XmlElement xml, string childName)
	{
		return xml[childName].InnerFloat();
	}

	public static float ChildFloat(this XmlElement xml, string childName, float defaultValue)
	{
		if (xml.HasChild(childName))
		{
			return xml[childName].InnerFloat();
		}
		return defaultValue;
	}

	public static bool ChildBool(this XmlElement xml, string childName)
	{
		return xml[childName].InnerBool();
	}

	public static bool ChildBool(this XmlElement xml, string childName, bool defaultValue)
	{
		if (xml.HasChild(childName))
		{
			return xml[childName].InnerBool();
		}
		return defaultValue;
	}

	public static T ChildEnum<T>(this XmlElement xml, string childName) where T : struct
	{
		if (Enum.IsDefined(typeof(T), xml[childName].InnerText))
		{
			return (T)Enum.Parse(typeof(T), xml[childName].InnerText);
		}
		throw new Exception("The attribute value cannot be converted to the enum type.");
	}

	public static T ChildEnum<T>(this XmlElement xml, string childName, T defaultValue) where T : struct
	{
		if (xml.HasChild(childName))
		{
			if (Enum.IsDefined(typeof(T), xml[childName].InnerText))
			{
				return (T)Enum.Parse(typeof(T), xml[childName].InnerText);
			}
			throw new Exception("The attribute value cannot be converted to the enum type.");
		}
		return defaultValue;
	}

	public static Color ChildHexColor(this XmlElement xml, string childName)
	{
		return HexToColor(xml[childName].InnerText);
	}

	public static Color ChildHexColor(this XmlElement xml, string childName, Color defaultValue)
	{
		if (xml.HasChild(childName))
		{
			return HexToColor(xml[childName].InnerText);
		}
		return defaultValue;
	}

	public static Color ChildHexColor(this XmlElement xml, string childName, string defaultValue)
	{
		if (xml.HasChild(childName))
		{
			return HexToColor(xml[childName].InnerText);
		}
		return HexToColor(defaultValue);
	}

	public static Vector2 ChildPosition(this XmlElement xml, string childName)
	{
		return xml[childName].Position();
	}

	public static Vector2 ChildPosition(this XmlElement xml, string childName, Vector2 defaultValue)
	{
		if (xml.HasChild(childName))
		{
			return xml[childName].Position(defaultValue);
		}
		return defaultValue;
	}

	public static Vector2 FirstNode(this XmlElement xml)
	{
		if (xml["node"] == null)
		{
			return Vector2.Zero;
		}
		return new Vector2((int)xml["node"].AttrFloat("x"), (int)xml["node"].AttrFloat("y"));
	}

	public static Vector2? FirstNodeNullable(this XmlElement xml)
	{
		if (xml["node"] == null)
		{
			return null;
		}
		return new Vector2((int)xml["node"].AttrFloat("x"), (int)xml["node"].AttrFloat("y"));
	}

	public static Vector2? FirstNodeNullable(this XmlElement xml, Vector2 offset)
	{
		if (xml["node"] == null)
		{
			return null;
		}
		return new Vector2((int)xml["node"].AttrFloat("x"), (int)xml["node"].AttrFloat("y")) + offset;
	}

	public static Vector2[] Nodes(this XmlElement xml, bool includePosition = false)
	{
		XmlNodeList elementsByTagName = xml.GetElementsByTagName("node");
		if (elementsByTagName == null)
		{
			if (!includePosition)
			{
				return new Vector2[0];
			}
			return new Vector2[1] { xml.Position() };
		}
		Vector2[] array;
		if (includePosition)
		{
			array = new Vector2[elementsByTagName.Count + 1];
			array[0] = xml.Position();
			for (int i = 0; i < elementsByTagName.Count; i++)
			{
				array[i + 1] = new Vector2(Convert.ToInt32(elementsByTagName[i].Attributes["x"].InnerText), Convert.ToInt32(elementsByTagName[i].Attributes["y"].InnerText));
			}
		}
		else
		{
			array = new Vector2[elementsByTagName.Count];
			for (int j = 0; j < elementsByTagName.Count; j++)
			{
				array[j] = new Vector2(Convert.ToInt32(elementsByTagName[j].Attributes["x"].InnerText), Convert.ToInt32(elementsByTagName[j].Attributes["y"].InnerText));
			}
		}
		return array;
	}

	public static Vector2[] Nodes(this XmlElement xml, Vector2 offset, bool includePosition = false)
	{
		Vector2[] array = xml.Nodes(includePosition);
		for (int i = 0; i < array.Length; i++)
		{
			array[i] += offset;
		}
		return array;
	}

	public static Vector2 GetNode(this XmlElement xml, int nodeNum)
	{
		return xml.Nodes()[nodeNum];
	}

	public static Vector2? GetNodeNullable(this XmlElement xml, int nodeNum)
	{
		if (xml.Nodes().Length > nodeNum)
		{
			return xml.Nodes()[nodeNum];
		}
		return null;
	}

	public static void SetAttr(this XmlElement xml, string attributeName, object setTo)
	{
		XmlAttribute xmlAttribute;
		if (xml.HasAttr(attributeName))
		{
			xmlAttribute = xml.Attributes[attributeName];
		}
		else
		{
			xmlAttribute = xml.OwnerDocument.CreateAttribute(attributeName);
			xml.Attributes.Append(xmlAttribute);
		}
		xmlAttribute.Value = setTo.ToString();
	}

	public static void SetChild(this XmlElement xml, string childName, object setTo)
	{
		XmlElement xmlElement;
		if (xml.HasChild(childName))
		{
			xmlElement = xml[childName];
		}
		else
		{
			xmlElement = xml.OwnerDocument.CreateElement(null, childName, xml.NamespaceURI);
			xml.AppendChild(xmlElement);
		}
		xmlElement.InnerText = setTo.ToString();
	}

	public static XmlElement CreateChild(this XmlDocument doc, string childName)
	{
		XmlElement xmlElement = doc.CreateElement(null, childName, doc.NamespaceURI);
		doc.AppendChild(xmlElement);
		return xmlElement;
	}

	public static XmlElement CreateChild(this XmlElement xml, string childName)
	{
		XmlElement xmlElement = xml.OwnerDocument.CreateElement(null, childName, xml.NamespaceURI);
		xml.AppendChild(xmlElement);
		return xmlElement;
	}

	public static int SortLeftToRight(Entity a, Entity b)
	{
		return (int)((a.X - b.X) * 100f);
	}

	public static int SortRightToLeft(Entity a, Entity b)
	{
		return (int)((b.X - a.X) * 100f);
	}

	public static int SortTopToBottom(Entity a, Entity b)
	{
		return (int)((a.Y - b.Y) * 100f);
	}

	public static int SortBottomToTop(Entity a, Entity b)
	{
		return (int)((b.Y - a.Y) * 100f);
	}

	public static int SortByDepth(Entity a, Entity b)
	{
		return a.Depth - b.Depth;
	}

	public static int SortByDepthReversed(Entity a, Entity b)
	{
		return b.Depth - a.Depth;
	}

	public static void Log()
	{
	}

	public static void TimeLog()
	{
	}

	public static void Log(params object[] obj)
	{
		for (int i = 0; i < obj.Length; i++)
		{
			_ = obj[i];
		}
	}

	public static void TimeLog(object obj)
	{
	}

	public static void LogEach<T>(IEnumerable<T> collection)
	{
		foreach (T item in collection)
		{
			_ = item;
		}
	}

	public static void Dissect(object obj)
	{
		FieldInfo[] fields = obj.GetType().GetFields();
		for (int i = 0; i < fields.Length; i++)
		{
			_ = fields[i];
		}
	}

	public static void StartTimer()
	{
		stopwatch = new Stopwatch();
		stopwatch.Start();
	}

	public static void EndTimer()
	{
		if (stopwatch != null)
		{
			stopwatch.Stop();
			_ = "Timer: " + stopwatch.ElapsedTicks + " ticks, or " + TimeSpan.FromTicks(stopwatch.ElapsedTicks).TotalSeconds.ToString("00.0000000") + " seconds";
			stopwatch = null;
		}
	}

	public static Delegate GetMethod<T>(object obj, string method) where T : class
	{
		if (obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null)
		{
			return null;
		}
		return Delegate.CreateDelegate(typeof(T), obj, method);
	}

	public static T At<T>(this T[,] arr, Pnt at)
	{
		return arr[at.X, at.Y];
	}

	public static string ConvertPath(string path)
	{
		return path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
	}

	public static string ReadNullTerminatedString(this BinaryReader stream)
	{
		string text = "";
		char c;
		while ((c = stream.ReadChar()) != 0)
		{
			text += c;
		}
		return text;
	}

	[IteratorStateMachine(typeof(_003CDo_003Ed__246))]
	public static IEnumerator Do(params IEnumerator[] numerators)
	{
		if (numerators.Length == 0)
		{
			yield break;
		}
		if (numerators.Length == 1)
		{
			yield return numerators[0];
			yield break;
		}
		List<Coroutine> routines = new List<Coroutine>();
		foreach (IEnumerator functionCall in numerators)
		{
			routines.Add(new Coroutine(functionCall));
		}
		while (true)
		{
			bool flag = false;
			foreach (Coroutine item in routines)
			{
				item.Update();
				if (!item.Finished)
				{
					flag = true;
				}
			}
			if (flag)
			{
				yield return null;
				continue;
			}
			break;
		}
	}

	public static Rectangle ClampTo(this Rectangle rect, Rectangle clamp)
	{
		if (rect.X < clamp.X)
		{
			rect.Width -= clamp.X - rect.X;
			rect.X = clamp.X;
		}
		if (rect.Y < clamp.Y)
		{
			rect.Height -= clamp.Y - rect.Y;
			rect.Y = clamp.Y;
		}
		if (rect.Right > clamp.Right)
		{
			rect.Width = clamp.Right - rect.X;
		}
		if (rect.Bottom > clamp.Bottom)
		{
			rect.Height = clamp.Bottom - rect.Y;
		}
		return rect;
	}
}
