using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Celeste.Core.Platform.Interop;

namespace Monocle;

public static class ContentFiles
{
	public static bool FileExists(string path)
	{
		if (TryGetContentRelativePath(path, out string relativePath) && CelestePathBridge.TryContentFileExists(relativePath, out bool exists))
		{
			return exists;
		}

		return File.Exists(path);
	}

	public static bool DirectoryExists(string path)
	{
		if (TryGetContentRelativePath(path, out string relativePath) && CelestePathBridge.TryContentDirectoryExists(relativePath, out bool exists))
		{
			return exists;
		}

		return Directory.Exists(path);
	}

	public static Stream OpenRead(string path)
	{
		if (TryGetContentRelativePath(path, out string relativePath) && CelestePathBridge.TryOpenContentStream(relativePath, out Stream stream))
		{
			return stream;
		}

		return File.OpenRead(path);
	}

	public static byte[] ReadAllBytes(string path)
	{
		using Stream stream = OpenRead(path);
		using MemoryStream memoryStream = new MemoryStream();
		stream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	public static string[] ReadAllLines(string path, Encoding encoding)
	{
		using Stream stream = OpenRead(path);
		using StreamReader streamReader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
		List<string> list = new List<string>();
		while (!streamReader.EndOfStream)
		{
			string item = streamReader.ReadLine() ?? string.Empty;
			list.Add(item);
		}

		return list.ToArray();
	}

	public static string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
	{
		if (TryGetContentRelativePath(path, out string relativeDirectory) && CelestePathBridge.TryEnumerateContentFiles(relativeDirectory, searchPattern, searchOption, out string[] files))
		{
			string contentRoot = GetContentRootPath();
			return files
				.Select(static relative => relative.Replace('/', Path.DirectorySeparatorChar))
				.Select(relative => Path.Combine(contentRoot, relative))
				.ToArray();
		}

		return Directory.GetFiles(path, searchPattern, searchOption);
	}

	public static bool TryGetContentRelativePath(string path, out string relativePath)
	{
		relativePath = string.Empty;
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		string normalizedPath = Normalize(path).TrimEnd('/');
		foreach (string root in EnumerateContentRoots())
		{
			if (TrySplitPathByRoot(normalizedPath, root, out relativePath))
			{
				return true;
			}
		}

		if (TrySplitPathByRoot(normalizedPath, "Content", out relativePath))
		{
			return true;
		}

		int embeddedIndex = normalizedPath.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
		if (embeddedIndex >= 0)
		{
			relativePath = normalizedPath[(embeddedIndex + "/Content/".Length)..];
			return true;
		}

		if (normalizedPath.EndsWith("/Content", StringComparison.OrdinalIgnoreCase))
		{
			relativePath = string.Empty;
			return true;
		}

		return false;
	}

	private static bool TrySplitPathByRoot(string normalizedPath, string rootPath, out string relativePath)
	{
		relativePath = string.Empty;
		if (string.IsNullOrWhiteSpace(rootPath))
		{
			return false;
		}

		string normalizedRoot = Normalize(rootPath).TrimEnd('/');
		if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
		{
			relativePath = string.Empty;
			return true;
		}

		string rootPrefix = normalizedRoot + "/";
		if (normalizedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
		{
			relativePath = normalizedPath[rootPrefix.Length..];
			return true;
		}

		string rootedPrefix = "/" + rootPrefix;
		if (normalizedPath.StartsWith(rootedPrefix, StringComparison.OrdinalIgnoreCase))
		{
			relativePath = normalizedPath[rootedPrefix.Length..];
			return true;
		}

		return false;
	}

	private static IEnumerable<string> EnumerateContentRoots()
	{
		if (!string.IsNullOrWhiteSpace(Engine.ContentDirectory))
		{
			yield return Engine.ContentDirectory;
		}

		if (Engine.Instance?.Content != null && !string.IsNullOrWhiteSpace(Engine.Instance.Content.RootDirectory))
		{
			yield return Engine.Instance.Content.RootDirectory;
		}
	}

	private static string GetContentRootPath()
	{
		if (!string.IsNullOrWhiteSpace(Engine.ContentDirectory))
		{
			return Engine.ContentDirectory;
		}

		if (Engine.Instance?.Content != null && !string.IsNullOrWhiteSpace(Engine.Instance.Content.RootDirectory))
		{
			return Engine.Instance.Content.RootDirectory;
		}

		return "Content";
	}

	private static string Normalize(string path)
	{
		return path.Replace('\\', '/');
	}
}
