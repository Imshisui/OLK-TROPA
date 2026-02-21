using System;
using System.IO;
using Celeste.Core.Platform.Interop;
using Microsoft.Xna.Framework.Content;

namespace Monocle;

public sealed class ExternalContentManager : ContentManager
{
    public ExternalContentManager(IServiceProvider serviceProvider, string rootDirectory)
        : base(serviceProvider, rootDirectory)
    {
    }

    protected override Stream OpenStream(string assetName)
    {
        var relativeAsset = assetName.Replace('\\', '/').TrimStart('/') + ".xnb";
        if (CelestePathBridge.TryOpenContentStream(relativeAsset, out var apkStream))
        {
            CelestePathBridge.LogInfo("CONTENT", $"OpenStream '{assetName}' -> 'apk://Content/{relativeAsset}'");
            return apkStream;
        }

        var rootDirectory = CelestePathBridge.ResolveContentDirectory(RootDirectory);
        var expectedPath = Path.Combine(rootDirectory, relativeAsset.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(expectedPath))
        {
            CelestePathBridge.LogError("CONTENT", $"Missing asset '{assetName}' expected at '{expectedPath}'");
            throw new ContentLoadException($"Asset '{assetName}' not found at '{expectedPath}'");
        }

        CelestePathBridge.LogInfo("CONTENT", $"OpenStream '{assetName}' -> '{expectedPath}'");
        return File.OpenRead(expectedPath);
    }
}
