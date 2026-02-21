using System;
using System.IO;
using Android.Content;
using Celeste.Core.Platform.Paths;

namespace Celeste.Android.Platform.Paths;

public sealed class AndroidPathsProvider : IPathsProvider
{
    public AndroidPathsProvider(Context context)
    {
        var external = context.GetExternalFilesDir(null)?.AbsolutePath;
        BaseDataPath = string.IsNullOrWhiteSpace(external)
            ? context.FilesDir?.AbsolutePath ?? throw new InvalidOperationException("Unable to resolve BaseDataPath")
            : external;

        ContentPath = "Content";
        LogsPath = Path.Combine(BaseDataPath, "Logs");
        SavePath = Path.Combine(BaseDataPath, "Save");
    }

    public string BaseDataPath { get; }

    public string ContentPath { get; }

    public string LogsPath { get; }

    public string SavePath { get; }

    public DirectoryLayoutResult EnsureDirectoryLayout()
    {
        try
        {
            Directory.CreateDirectory(BaseDataPath);
            Directory.CreateDirectory(LogsPath);
            Directory.CreateDirectory(SavePath);

            var probePath = Path.Combine(LogsPath, ".layout_probe");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);

            return new DirectoryLayoutResult(
                true,
                "DIRS_OK",
                $"BaseDataPath={BaseDataPath}; ContentPath=apk://assets/Content; LogsPath={LogsPath}; SavePath={SavePath}");
        }
        catch (Exception exception)
        {
            return new DirectoryLayoutResult(false, "DIRS_FAIL", exception.ToString());
        }
    }
}
