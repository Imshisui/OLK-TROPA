using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using Android.Content.Res;
using Celeste.Core.Platform.Logging;

namespace Celeste.Android.Platform.Content;

public sealed class AndroidApkContentSource
{
    private const string ContentAssetRoot = "Content";

    private readonly AssetManager _assetManager;
    private readonly IAppLogger _logger;
    private readonly object _sync = new();
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    private bool _indexed;

    public AndroidApkContentSource(AssetManager assetManager, IAppLogger logger)
    {
        _assetManager = assetManager;
        _logger = logger;
    }

    public Stream OpenApkContentStream(string relativePath)
    {
        var normalizedRelative = NormalizeRelativePath(relativePath);
        var assetPath = BuildAssetPath(normalizedRelative);

        try
        {
            return _assetManager.Open(assetPath, Access.Streaming);
        }
        catch (Exception exception)
        {
            throw new FileNotFoundException(
                $"APK content asset not found: '{assetPath}'. Ensure Content archive is packaged into APK assets under 'Content/'.",
                exception);
        }
    }

    public bool FileExists(string relativePath)
    {
        EnsureAssetIndex();
        return _files.Contains(NormalizeRelativePath(relativePath));
    }

    public bool DirectoryExists(string relativePath)
    {
        EnsureAssetIndex();
        return _directories.Contains(NormalizeDirectoryPath(relativePath));
    }

    public string[] EnumerateFiles(string relativeDirectory, string searchPattern, SearchOption searchOption)
    {
        EnsureAssetIndex();

        var normalizedDirectory = NormalizeDirectoryPath(relativeDirectory);
        var directoryPrefix = string.IsNullOrEmpty(normalizedDirectory) ? string.Empty : normalizedDirectory + "/";
        var recursive = searchOption == SearchOption.AllDirectories;

        var result = new List<string>();
        foreach (var relativeFile in _files)
        {
            if (!string.IsNullOrEmpty(directoryPrefix) && !relativeFile.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var localSegment = string.IsNullOrEmpty(directoryPrefix)
                ? relativeFile
                : relativeFile[directoryPrefix.Length..];

            if (!recursive && localSegment.Contains('/'))
            {
                continue;
            }

            if (!MatchesPattern(Path.GetFileName(relativeFile), searchPattern))
            {
                continue;
            }

            result.Add(relativeFile);
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result.ToArray();
    }

    private void EnsureAssetIndex()
    {
        lock (_sync)
        {
            if (_indexed)
            {
                return;
            }

            _files.Clear();
            _directories.Clear();
            _directories.Add(string.Empty);
            IndexDirectory(ContentAssetRoot, string.Empty);
            _indexed = true;

            _logger.Log(
                LogLevel.Info,
                "CONTENT",
                "APK content index ready",
                context: $"files={_files.Count}; directories={_directories.Count}; root={ContentAssetRoot}");
        }
    }

    private void IndexDirectory(string assetDirectoryPath, string relativeDirectory)
    {
        string[] entries;
        try
        {
            entries = _assetManager.List(assetDirectoryPath) ?? Array.Empty<string>();
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "CONTENT", "Unable to list APK content directory", exception, "path=" + assetDirectoryPath);
            return;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var childAssetPath = assetDirectoryPath + "/" + entry;
            var childRelativePath = string.IsNullOrEmpty(relativeDirectory)
                ? entry
                : relativeDirectory + "/" + entry;

            string[] childEntries;
            try
            {
                childEntries = _assetManager.List(childAssetPath) ?? Array.Empty<string>();
            }
            catch
            {
                childEntries = Array.Empty<string>();
            }

            if (childEntries.Length > 0)
            {
                _directories.Add(NormalizeDirectoryPath(childRelativePath));
                IndexDirectory(childAssetPath, childRelativePath);
                continue;
            }

            _files.Add(NormalizeRelativePath(childRelativePath));
        }
    }

    private static bool MatchesPattern(string fileName, string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(searchPattern) || searchPattern == "*")
        {
            return true;
        }

        return FileSystemName.MatchesSimpleExpression(searchPattern, fileName, ignoreCase: true);
    }

    private static string BuildAssetPath(string normalizedRelativePath)
    {
        return string.IsNullOrEmpty(normalizedRelativePath)
            ? ContentAssetRoot
            : ContentAssetRoot + "/" + normalizedRelativePath;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith(ContentAssetRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(ContentAssetRoot.Length + 1)..];
        }
        else if (string.Equals(normalized, ContentAssetRoot, StringComparison.OrdinalIgnoreCase))
        {
            normalized = string.Empty;
        }

        return normalized;
    }

    private static string NormalizeDirectoryPath(string relativePath)
    {
        return NormalizeRelativePath(relativePath).TrimEnd('/');
    }
}
