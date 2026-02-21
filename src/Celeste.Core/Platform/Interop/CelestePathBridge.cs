using System;
using System.IO;

namespace Celeste.Core.Platform.Interop;

public static class CelestePathBridge
{
    private static Func<string> _contentPathProvider;
    private static Func<string> _savePathProvider;
    private static Func<string> _logsPathProvider;
    private static Action<string, string, string>? _logSink;
    private static Func<string, Stream>? _contentStreamProvider;
    private static Func<string, bool>? _contentFileExistsProvider;
    private static Func<string, bool>? _contentDirectoryExistsProvider;
    private static Func<string, string, SearchOption, string[]>? _contentEnumerateFilesProvider;

    public static void Configure(
        Func<string> contentPathProvider,
        Func<string> savePathProvider,
        Func<string> logsPathProvider,
        Action<string, string, string>? logSink = null,
        Func<string, Stream>? contentStreamProvider = null,
        Func<string, bool>? contentFileExistsProvider = null,
        Func<string, bool>? contentDirectoryExistsProvider = null,
        Func<string, string, SearchOption, string[]>? contentEnumerateFilesProvider = null)
    {
        _contentPathProvider = contentPathProvider;
        _savePathProvider = savePathProvider;
        _logsPathProvider = logsPathProvider;
        _logSink = logSink;
        _contentStreamProvider = contentStreamProvider;
        _contentFileExistsProvider = contentFileExistsProvider;
        _contentDirectoryExistsProvider = contentDirectoryExistsProvider;
        _contentEnumerateFilesProvider = contentEnumerateFilesProvider;
    }

    public static string ResolveContentDirectory(string fallbackDirectory)
    {
        return _contentPathProvider != null ? _contentPathProvider() : fallbackDirectory;
    }

    public static string ResolveSaveDirectory(string fallbackDirectory)
    {
        return _savePathProvider != null ? _savePathProvider() : fallbackDirectory;
    }

    public static string ResolveErrorLogPath(string fallbackFileName)
    {
        if (_logsPathProvider == null)
        {
            return fallbackFileName;
        }

        return Path.Combine(_logsPathProvider(), fallbackFileName);
    }

    public static bool TryOpenContentStream(string relativePath, out Stream stream)
    {
        stream = Stream.Null;
        if (_contentStreamProvider == null)
        {
            return false;
        }

        var normalized = NormalizeRelativeContentPath(relativePath);
        try
        {
            stream = _contentStreamProvider(normalized);
            return stream != null;
        }
        catch
        {
            stream = Stream.Null;
            return false;
        }
    }

    public static bool TryContentFileExists(string relativePath, out bool exists)
    {
        exists = false;
        if (_contentFileExistsProvider == null)
        {
            return false;
        }

        try
        {
            exists = _contentFileExistsProvider(NormalizeRelativeContentPath(relativePath));
            return true;
        }
        catch
        {
            exists = false;
            return false;
        }
    }

    public static bool TryContentDirectoryExists(string relativePath, out bool exists)
    {
        exists = false;
        if (_contentDirectoryExistsProvider == null)
        {
            return false;
        }

        try
        {
            exists = _contentDirectoryExistsProvider(NormalizeRelativeContentPath(relativePath));
            return true;
        }
        catch
        {
            exists = false;
            return false;
        }
    }

    public static bool TryEnumerateContentFiles(string relativeDirectory, string searchPattern, SearchOption searchOption, out string[] files)
    {
        files = Array.Empty<string>();
        if (_contentEnumerateFilesProvider == null)
        {
            return false;
        }

        try
        {
            files = _contentEnumerateFilesProvider(
                NormalizeRelativeContentPath(relativeDirectory),
                searchPattern,
                searchOption) ?? Array.Empty<string>();
            return true;
        }
        catch
        {
            files = Array.Empty<string>();
            return false;
        }
    }

    private static string NormalizeRelativeContentPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Content/".Length..];
        }

        if (string.Equals(normalized, "Content", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return normalized;
    }

    public static void LogInfo(string tag, string message)
    {
        _logSink?.Invoke("INFO", tag, message);
    }

    public static void LogWarn(string tag, string message)
    {
        _logSink?.Invoke("WARN", tag, message);
    }

    public static void LogError(string tag, string message)
    {
        _logSink?.Invoke("ERROR", tag, message);
    }
}
