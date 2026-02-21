using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Core.Platform.Audio;
using Celeste.Core.Platform.Filesystem;
using Celeste.Core.Platform.Paths;

namespace Celeste.Core.Platform.Content;

public sealed class ContentValidator
{
    private static readonly string[] RequiredFiles =
    {
        "Graphics/SpritesGui.xml",
        "Graphics/Sprites.xml",
        "Graphics/Portraits.xml",
        "Graphics/BackgroundTiles.xml",
        "Graphics/ForegroundTiles.xml",
        "Graphics/CompleteScreens.xml",
        "Graphics/AnimatedTiles.xml",
        "Graphics/Atlases/Gameplay.meta",
        "Graphics/Atlases/Gui.meta",
        "Dialog/English.txt",
        "Maps/0-Intro.bin"
    };

    private const int MinimumExpectedFileCount = 900;

    private readonly IPathsProvider _paths;
    private readonly IFileSystem _fileSystem;

    public ContentValidator(IPathsProvider paths, IFileSystem fileSystem)
    {
        _paths = paths;
        _fileSystem = fileSystem;
    }

    public ContentValidationReport Validate()
    {
        var report = new ContentValidationReport();
        var contentPath = _paths.ContentPath;

        if (!_fileSystem.DirectoryExists(contentPath))
        {
            report.Status = ContentValidationStatus.Missing;
            report.Issues.Add(new ContentValidationIssue(
                IssueSeverity.Error,
                "CONTENT_ROOT_MISSING",
                "Content",
                contentPath,
                "Pasta Content nao encontrada.",
                "Copie os arquivos do jogo para a pasta Content no caminho exibido."
            ));
            return report;
        }

        var rootDirectories = _fileSystem.EnumerateDirectories(contentPath).ToList();
        var rootEntries = _fileSystem.EnumerateEntries(contentPath).ToList();
        report.ScannedDirectoryCount += rootDirectories.Count;
        report.ScannedFileCount += rootEntries.Count(entry => _fileSystem.FileExists(entry));

        ValidateRequiredDirectory(report, contentPath, "Dialog", requireAnyFile: true);
        ValidateRequiredDirectory(report, contentPath, "Effects", requireAnyFile: false, requiredExtension: ".xnb");
        ValidateRequiredDirectory(report, contentPath, "Graphics", requireAnyFile: true);
        ValidateRequiredFiles(report, contentPath);
        ValidateReadability(report, contentPath);
        ValidateAudioDirectory(report, contentPath);

        report.Status = DetermineStatus(report);
        return report;
    }

    private void ValidateRequiredFiles(ContentValidationReport report, string contentPath)
    {
        foreach (var relativePath in RequiredFiles)
        {
            ValidateRequiredFile(report, contentPath, relativePath);
        }
    }

    private void ValidateRequiredFile(ContentValidationReport report, string contentPath, string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(contentPath, normalizedRelativePath);
        if (_fileSystem.FileExists(absolutePath))
        {
            return;
        }

        var parentPath = Path.GetDirectoryName(absolutePath);
        var expectedFileName = Path.GetFileName(absolutePath);
        if (!string.IsNullOrWhiteSpace(parentPath) && _fileSystem.DirectoryExists(parentPath))
        {
            foreach (var entry in _fileSystem.EnumerateEntries(parentPath))
            {
                if (!_fileSystem.FileExists(entry))
                {
                    continue;
                }

                var actualFileName = Path.GetFileName(entry);
                if (!string.Equals(actualFileName, expectedFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(actualFileName, expectedFileName, StringComparison.Ordinal))
                {
                    report.Issues.Add(new ContentValidationIssue(
                        IssueSeverity.Error,
                        "CONTENT_FILE_CASE_MISMATCH",
                        $"Content/{relativePath}",
                        entry,
                        $"Arquivo com case incorreto. Esperado '{expectedFileName}', encontrado '{actualFileName}'.",
                        $"Renomeie '{actualFileName}' para '{expectedFileName}'."
                    ));
                    return;
                }
            }
        }

        report.Issues.Add(new ContentValidationIssue(
            IssueSeverity.Error,
            "CONTENT_FILE_MISSING",
            $"Content/{relativePath}",
            absolutePath,
            $"Arquivo crítico ausente: {relativePath}.",
            $"Copie o arquivo '{relativePath}' para dentro da pasta Content."
        ));
    }

    private void ValidateReadability(ContentValidationReport report, string contentPath)
    {
        var allFiles = _fileSystem.EnumerateFiles(contentPath, "*", SearchOption.AllDirectories).ToList();
        report.ScannedFileCount = Math.Max(report.ScannedFileCount, allFiles.Count);

        if (allFiles.Count < MinimumExpectedFileCount)
        {
            report.Issues.Add(new ContentValidationIssue(
                IssueSeverity.Error,
                "CONTENT_FILE_COUNT_TOO_LOW",
                "Content",
                contentPath,
                $"Quantidade de arquivos muito baixa: {allFiles.Count} (mínimo esperado {MinimumExpectedFileCount}).",
                "Copie novamente o pacote completo de Content sem perder subpastas/arquivos."
            ));
        }

        foreach (var file in allFiles)
        {
            try
            {
                using var stream = _fileSystem.OpenRead(file);
                if (stream.CanRead && stream.Length > 0)
                {
                    _ = stream.ReadByte();
                }
            }
            catch (Exception exception)
            {
                var relativePath = Path.GetRelativePath(contentPath, file).Replace('\\', '/');
                report.Issues.Add(new ContentValidationIssue(
                    IssueSeverity.Error,
                    "CONTENT_FILE_UNREADABLE",
                    $"Content/{relativePath}",
                    file,
                    $"Arquivo não pôde ser lido: {relativePath}.",
                    $"Verifique permissões e recopie o arquivo. Detalhe: {exception.Message}"
                ));
            }
        }
    }

    private void ValidateRequiredDirectory(ContentValidationReport report, string contentPath, string expectedName, bool requireAnyFile, string? requiredExtension = null)
    {
        var absoluteExpectedPath = Path.Combine(contentPath, expectedName);
        if (!_fileSystem.DirectoryExists(absoluteExpectedPath))
        {
            if (TryFindCaseMismatch(contentPath, expectedName, out var actualName))
            {
                var actualPath = Path.Combine(contentPath, actualName);
                report.Issues.Add(new ContentValidationIssue(
                    IssueSeverity.Error,
                    "CONTENT_CASE_MISMATCH",
                    $"Content/{expectedName}",
                    actualPath,
                    $"Nome de pasta com case incorreto. Esperado '{expectedName}', encontrado '{actualName}'.",
                    $"Renomeie '{actualName}' para '{expectedName}'."
                ));
            }
            else
            {
                report.Issues.Add(new ContentValidationIssue(
                    IssueSeverity.Error,
                    "CONTENT_FOLDER_MISSING",
                    $"Content/{expectedName}",
                    absoluteExpectedPath,
                    $"Pasta critica ausente: {expectedName}.",
                    $"Copie a pasta '{expectedName}' para dentro de Content/."
                ));
            }

            return;
        }

        var files = _fileSystem.EnumerateFiles(absoluteExpectedPath, "*", SearchOption.AllDirectories).ToList();
        report.ScannedFileCount += files.Count;

        if (requireAnyFile && files.Count == 0)
        {
            report.Issues.Add(new ContentValidationIssue(
                IssueSeverity.Error,
                "CONTENT_FOLDER_EMPTY",
                $"Content/{expectedName}",
                absoluteExpectedPath,
                $"Pasta critica vazia: {expectedName}.",
                "Copie novamente os assets completos do jogo."
            ));
            return;
        }

        if (!string.IsNullOrEmpty(requiredExtension))
        {
            var hasExpectedExtension = files.Any(path => string.Equals(Path.GetExtension(path), requiredExtension, StringComparison.OrdinalIgnoreCase));
            if (!hasExpectedExtension)
            {
                report.Issues.Add(new ContentValidationIssue(
                    IssueSeverity.Error,
                    "CONTENT_EXTENSION_MISSING",
                    $"Content/{expectedName}",
                    absoluteExpectedPath,
                    $"Nenhum arquivo {requiredExtension} encontrado em {expectedName}.",
                    $"Verifique se os arquivos {requiredExtension} foram copiados corretamente."
                ));
            }
        }
    }

    private void ValidateAudioDirectory(ContentValidationReport report, string contentPath)
    {
        var fmodPath = Path.Combine(contentPath, "FMOD");
        if (AudioRuntimePolicy.ShouldForceSilentAudio())
        {
            report.Issues.Add(new ContentValidationIssue(
                IssueSeverity.Warning,
                "AUDIO_DISABLED_BY_POLICY_ANDROID",
                "Content/FMOD",
                fmodPath,
                "Audio FMOD desativado por politica Android. O jogo executara em modo silencioso.",
                $"Para testar audio FMOD novamente, ative o switch '{AudioRuntimePolicy.EnableFmodOnAndroidSwitch}' em true."
            ));
            return;
        }

        if (!_fileSystem.DirectoryExists(fmodPath))
        {
            report.Issues.Add(new ContentValidationIssue(
                IssueSeverity.Warning,
                "AUDIO_FMOD_MISSING",
                "Content/FMOD",
                fmodPath,
                "Pasta FMOD nao encontrada. Audio pode entrar em fallback silencioso.",
                "Copie os bancos de audio para Content/FMOD."
            ));
            return;
        }

        var bankFiles = _fileSystem.EnumerateFiles(fmodPath, "*.bank", SearchOption.AllDirectories).ToList();
        report.ScannedFileCount += bankFiles.Count;
        if (bankFiles.Count == 0)
        {
            report.Issues.Add(new ContentValidationIssue(
                IssueSeverity.Warning,
                "AUDIO_BANKS_MISSING",
                "Content/FMOD",
                fmodPath,
                "Nenhum banco .bank encontrado. Audio pode entrar em fallback silencioso.",
                "Copie os arquivos .bank para Content/FMOD/Mobile (ou Android/Desktop)."
            ));

            return;
        }

        var requiredBanks = new[]
        {
            "Master Bank.bank",
            "Master Bank.strings.bank",
            "music.bank",
            "sfx.bank",
            "ui.bank",
            "dlc_music.bank",
            "dlc_sfx.bank"
        };
        var existingNames = bankFiles
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var requiredBank in requiredBanks)
        {
            if (existingNames.Contains(requiredBank))
            {
                continue;
            }

            report.Issues.Add(new ContentValidationIssue(
                IssueSeverity.Warning,
                "AUDIO_REQUIRED_BANK_MISSING",
                "Content/FMOD",
                fmodPath,
                $"Banco obrigatorio ausente: {requiredBank}. Audio pode cair em fallback silencioso.",
                "Gere e copie os bancos FMOD 2.03.12 para Content/FMOD/Mobile."
            ));
        }
    }

    private bool TryFindCaseMismatch(string parentPath, string expectedName, out string actualName)
    {
        var directories = _fileSystem.EnumerateDirectories(parentPath);
        foreach (var directory in directories)
        {
            var folderName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.Equals(folderName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(folderName, expectedName, StringComparison.Ordinal))
            {
                actualName = folderName;
                return true;
            }
        }

        actualName = string.Empty;
        return false;
    }

    private static ContentValidationStatus DetermineStatus(ContentValidationReport report)
    {
        var hasErrors = report.Issues.Any(issue => issue.Severity == IssueSeverity.Error);
        if (!hasErrors)
        {
            return ContentValidationStatus.Ok;
        }

        if (report.Issues.Any(issue => issue.Code == "CONTENT_ROOT_MISSING"))
        {
            return ContentValidationStatus.Missing;
        }

        return ContentValidationStatus.Incomplete;
    }
}
