using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Core.Platform.Filesystem;
using Celeste.Core.Platform.Interop;
using Celeste.Core.Platform.Logging;
using Celeste.Core.Platform.Paths;

namespace Celeste.Android.Platform.Filesystem;

public sealed class AndroidFileSystem : IFileSystem
{
    private readonly IPathsProvider _paths;
    private readonly IAppLogger _logger;

    public AndroidFileSystem(IPathsProvider paths, IAppLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public string ResolvePath(string path)
    {
        return PathResolver.ResolveRootedPath(_paths, path);
    }

    public bool FileExists(string path)
    {
        var resolved = ResolvePath(path);
        if (TryGetContentRelativePath(path, resolved, out var relativePath) &&
            CelestePathBridge.TryContentFileExists(relativePath, out var exists))
        {
            return exists;
        }

        return File.Exists(resolved);
    }

    public bool DirectoryExists(string path)
    {
        var resolved = ResolvePath(path);
        if (TryGetContentRelativePath(path, resolved, out var relativePath) &&
            CelestePathBridge.TryContentDirectoryExists(relativePath, out var exists))
        {
            return exists;
        }

        return Directory.Exists(resolved);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        var resolved = ResolvePath(path);
        if (TryGetContentRelativePath(path, resolved, out var relativePath) &&
            CelestePathBridge.TryEnumerateContentFiles(relativePath, searchPattern, searchOption, out var contentFiles))
        {
            return contentFiles
                .Select(relative => Path.Combine(_paths.ContentPath, relative.Replace('/', Path.DirectorySeparatorChar)))
                .ToList();
        }

        if (!Directory.Exists(resolved))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            return Directory.EnumerateFiles(resolved, searchPattern, searchOption).ToList();
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FS", $"EnumerateFiles failed for '{resolved}'", exception);
            return Enumerable.Empty<string>();
        }
    }

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        var resolved = ResolvePath(path);
        if (TryGetContentRelativePath(path, resolved, out var relativePath) &&
            CelestePathBridge.TryEnumerateContentFiles(relativePath, "*", SearchOption.AllDirectories, out var contentFiles))
        {
            var normalizedBase = NormalizeRelativePath(relativePath);
            var immediateDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in contentFiles)
            {
                var normalizedFile = NormalizeRelativePath(file);
                var localSegment = string.IsNullOrEmpty(normalizedBase)
                    ? normalizedFile
                    : normalizedFile.StartsWith(normalizedBase + "/", StringComparison.OrdinalIgnoreCase)
                        ? normalizedFile[(normalizedBase.Length + 1)..]
                        : string.Empty;

                if (string.IsNullOrEmpty(localSegment))
                {
                    continue;
                }

                var slashIndex = localSegment.IndexOf('/');
                if (slashIndex <= 0)
                {
                    continue;
                }

                var childDir = localSegment[..slashIndex];
                var relativeDir = string.IsNullOrEmpty(normalizedBase)
                    ? childDir
                    : normalizedBase + "/" + childDir;
                immediateDirectories.Add(Path.Combine(_paths.ContentPath, relativeDir.Replace('/', Path.DirectorySeparatorChar)));
            }

            return immediateDirectories.ToList();
        }

        if (!Directory.Exists(resolved))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            return Directory.EnumerateDirectories(resolved).ToList();
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FS", $"EnumerateDirectories failed for '{resolved}'", exception);
            return Enumerable.Empty<string>();
        }
    }

    public IEnumerable<string> EnumerateEntries(string path)
    {
        var resolved = ResolvePath(path);
        if (TryGetContentRelativePath(path, resolved, out var relativePath))
        {
            var entries = new List<string>();

            if (CelestePathBridge.TryEnumerateContentFiles(relativePath, "*", SearchOption.TopDirectoryOnly, out var topFiles))
            {
                entries.AddRange(topFiles.Select(relative => Path.Combine(_paths.ContentPath, relative.Replace('/', Path.DirectorySeparatorChar))));
            }

            if (CelestePathBridge.TryEnumerateContentFiles(relativePath, "*", SearchOption.AllDirectories, out var allFiles))
            {
                var normalizedBase = NormalizeRelativePath(relativePath);
                var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var file in allFiles)
                {
                    var normalizedFile = NormalizeRelativePath(file);
                    var localSegment = string.IsNullOrEmpty(normalizedBase)
                        ? normalizedFile
                        : normalizedFile.StartsWith(normalizedBase + "/", StringComparison.OrdinalIgnoreCase)
                            ? normalizedFile[(normalizedBase.Length + 1)..]
                            : string.Empty;

                    if (string.IsNullOrEmpty(localSegment))
                    {
                        continue;
                    }

                    var slashIndex = localSegment.IndexOf('/');
                    if (slashIndex <= 0)
                    {
                        continue;
                    }

                    var childDir = localSegment[..slashIndex];
                    var relativeDir = string.IsNullOrEmpty(normalizedBase)
                        ? childDir
                        : normalizedBase + "/" + childDir;
                    directories.Add(Path.Combine(_paths.ContentPath, relativeDir.Replace('/', Path.DirectorySeparatorChar)));
                }

                entries.AddRange(directories);
            }

            if (entries.Count > 0)
            {
                return entries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        if (!Directory.Exists(resolved))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            return Directory.EnumerateFileSystemEntries(resolved).ToList();
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Warn, "FS", $"EnumerateEntries failed for '{resolved}'", exception);
            return Enumerable.Empty<string>();
        }
    }

    public Stream OpenRead(string path)
    {
        var resolved = ResolvePath(path);
        if (TryGetContentRelativePath(path, resolved, out var relativePath) &&
            CelestePathBridge.TryOpenContentStream(relativePath, out var contentStream))
        {
            return contentStream;
        }

        try
        {
            return File.OpenRead(resolved);
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Error, "FS", $"OpenRead failed: source='{path}' resolved='{resolved}'", exception);
            throw;
        }
    }

    public Stream OpenWrite(string path, bool overwrite = true)
    {
        var resolved = ResolvePath(path);
        var directory = Path.GetDirectoryName(resolved);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            return new FileStream(
                resolved,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
        }
        catch (Exception exception)
        {
            _logger.Log(LogLevel.Error, "FS", $"OpenWrite failed: source='{path}' resolved='{resolved}'", exception);
            throw;
        }
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(ResolvePath(path));
    }

    public void DeleteFile(string path)
    {
        var resolved = ResolvePath(path);
        if (File.Exists(resolved))
        {
            File.Delete(resolved);
        }
    }

    private static bool TryGetContentRelativePath(string sourcePath, string resolvedPath, out string relativePath)
    {
        relativePath = string.Empty;

        if (TryExtractContentRelativePath(NormalizeRelativePath(sourcePath), out relativePath))
        {
            return true;
        }

        return TryExtractContentRelativePath(NormalizeRelativePath(resolvedPath), out relativePath);
    }

    private static bool TryExtractContentRelativePath(string normalizedPath, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        if (string.Equals(normalizedPath, "Content", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedPath.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
        {
            relativePath = normalizedPath["Content/".Length..];
            return true;
        }

        var markerIndex = normalizedPath.IndexOf("/Content/", StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            relativePath = normalizedPath[(markerIndex + "/Content/".Length)..];
            return true;
        }

        return false;
    }

    private static string NormalizeRelativePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').TrimStart('/');
    }
}
