using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Android.App;
using Celeste.Android.Platform.Content;
using Celeste.Android.Platform.Fullscreen;
using Celeste.Android.Platform.Lifecycle;
using Celeste.Android.Platform.Rendering;
using Celeste.Core.Platform.Boot;
using Celeste.Core.Platform.Content;
using Celeste.Core.Platform.Diagnostics;
using Celeste.Core.Platform.Interop;
using Celeste.Core.Platform.Logging;
using Celeste.Core.Platform.Runtime;
using Celeste.Core.Platform.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Celeste.Android;

public class Game1 : Game, IAndroidGameLifecycle
{
    private readonly PlatformServices _services;
    private readonly ImmersiveFullscreenController _fullscreen;
    private readonly Activity _activity;
    private readonly string _activeAbi;
    private readonly BootStateMachine _bootStateMachine;
    private readonly ContentValidator _contentValidator;
    private readonly BitmapFallbackFont _fallbackFont;
    private readonly Action _requestRuntimeLaunch;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private SpriteFont? _internalErrorFont;
    private Texture2D? _pixel;
    private ContentValidationReport? _lastContentReport;
    private BootExecutionResult? _lastBootResult;

    private bool _bootCompleted;
    private bool _showErrorScreen;
    private bool _touchIgnoredLogged;
    private bool _diagnosticMode;
    private bool _diagLatch;
    private bool _runtimeLaunchRequested;
    private bool _internalErrorFontSanitizerLogged;
    private bool _internalErrorFontFallbackLogged;
    private double _diagHoldSeconds;
    private DateTime _lastEmergencyGcUtc;
    private string _bootMessage = "BOOTING";
    private string _gpuInfo = "GPU=Pending";

    public Game1(
        PlatformServices services,
        ImmersiveFullscreenController fullscreen,
        Activity activity,
        string activeAbi,
        Action requestRuntimeLaunch)
    {
        _services = services;
        _fullscreen = fullscreen;
        _activity = activity;
        _activeAbi = activeAbi;
        _requestRuntimeLaunch = requestRuntimeLaunch;

        _bootStateMachine = new BootStateMachine();
        _contentValidator = new ContentValidator(_services.Paths, _services.FileSystem);
        _fallbackFont = new BitmapFallbackFont();

        _graphics = new GraphicsDeviceManager(this)
        {
            IsFullScreen = true,
            SupportedOrientations = DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        TouchPanel.EnabledGestures = GestureType.None;
    }

    protected override void Initialize()
    {
        RunBoot(BootPhase.BootInitLogger, isRetry: false);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        try
        {
            _internalErrorFont = Content.Load<SpriteFont>("ErrorFont");
            _services.Logger.Log(LogLevel.Info, "UI", "Internal error SpriteFont loaded");
        }
        catch (Exception exception)
        {
            _internalErrorFont = null;
            _services.Logger.Log(LogLevel.Warn, "UI", "Internal SpriteFont unavailable, using bitmap fallback", exception);
        }

        _gpuInfo = $"GPU={GraphicsAdapter.DefaultAdapter.Description}";
        _services.Logger.Log(LogLevel.Info, "GPU", _gpuInfo);
    }

    protected override void Update(GameTime gameTime)
    {
        _services.Input.Update();
        UpdateDiagnosticToggle(gameTime);
        IgnoreTouchInput();

        if (_showErrorScreen)
        {
            if (_services.Input.IsRetryPressed())
            {
                _services.Logger.Log(LogLevel.Info, "CONTENT", "Retry requested from error screen");
                RunBoot(BootPhase.BootValidateContent, isRetry: true);
            }

            if (_services.Input.IsBackPressed())
            {
                _services.Logger.Log(LogLevel.Warn, "ERROR_SCREEN", "USER_EXIT_FROM_ERROR_SCREEN");
                Exit();
            }
        }
        else if (_bootCompleted && !_runtimeLaunchRequested)
        {
            _runtimeLaunchRequested = true;
            _services.Logger.Log(LogLevel.Info, "BOOT", "BOOT_COMPLETED_REQUEST_RUNTIME");
            _requestRuntimeLaunch();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_spriteBatch is null || _pixel is null)
        {
            base.Draw(gameTime);
            return;
        }

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);

        if (_showErrorScreen)
        {
            DrawErrorScreen();
        }
        else if (_bootCompleted)
        {
            DrawBootReadyScreen();
        }
        else
        {
            DrawBootingScreen();
        }

        if (_diagnosticMode)
        {
            DrawDiagnosticOverlay();
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    public void HandlePause()
    {
        _services.Logger.Log(LogLevel.Info, "LIFECYCLE", "Game HandlePause");
        _services.Audio.OnPause();
    }

    public void HandleResume()
    {
        _services.Logger.Log(LogLevel.Info, "LIFECYCLE", "Game HandleResume");
        _services.Audio.OnResume();
        _fullscreen.Apply(_activity, "Game-HandleResume");
    }

    public void HandleFocusChanged(bool hasFocus)
    {
        _services.Logger.Log(LogLevel.Info, "LIFECYCLE", $"Game HandleFocusChanged={hasFocus}");
        if (hasFocus)
        {
            _fullscreen.Apply(_activity, "Game-HandleFocusChanged");
        }
    }

    public void HandleLowMemory()
    {
        _services.Logger.Log(LogLevel.Warn, "MEMORY", "Game HandleLowMemory");
        TryEmergencyGc("low_memory", force: true);
    }

    public void HandleTrimMemory(int level, string levelName)
    {
        _services.Logger.Log(LogLevel.Warn, "MEMORY", "Game HandleTrimMemory", context: $"level={level}; levelName={levelName}");
        if (level >= 10 || AndroidRuntimePolicy.IsAggressiveGarbageCollectionEnabled())
        {
            TryEmergencyGc($"trim_memory_{levelName}", force: level >= 15);
        }
    }

    public void HandleDestroy()
    {
        _services.Logger.Log(LogLevel.Info, "LIFECYCLE", "Game HandleDestroy");
        _services.Logger.Flush();
    }

    private void RunBoot(BootPhase startPhase, bool isRetry)
    {
        _lastBootResult = _bootStateMachine.Execute(ExecuteBootPhase, startPhase, _services.Logger);

        if (_lastBootResult.Success)
        {
            _bootCompleted = true;
            _showErrorScreen = false;
            _bootMessage = "BOOT_OK";
            if (isRetry)
            {
                _services.Logger.Log(LogLevel.Info, "CONTENT", "CONTENT_OK_AFTER_RETRY");
            }
            return;
        }

        _bootCompleted = false;
        _showErrorScreen = true;
        _bootMessage = _lastBootResult.Message;

        if (_lastBootResult.FailedPhase == BootPhase.BootValidateContent &&
            _lastBootResult.PhaseResults.TryGetValue(BootPhase.BootValidateContent, out var validatePhaseResult) &&
            validatePhaseResult.Payload is ContentValidationReport report)
        {
            _lastContentReport = report;
        }
    }

    private BootPhaseResult ExecuteBootPhase(BootPhase phase)
    {
        switch (phase)
        {
            case BootPhase.BootInitLogger:
                _services.Logger.Log(LogLevel.Info, "APP", $"Boot session log file: {_services.Logger.CurrentSessionLogFile}");
                _services.Logger.Log(LogLevel.Info, "APP", $"ABI active: {_activeAbi}");
                _services.Logger.Log(LogLevel.Info, "POLICY", "ANDROID_RUNTIME_POLICY", context: $"lowMemoryMode={AndroidRuntimePolicy.IsLowMemoryModeEnabled()}; aggressiveGc={AndroidRuntimePolicy.IsAggressiveGarbageCollectionEnabled()}; preferReachProfile={AndroidRuntimePolicy.ShouldPreferReachGraphicsProfile()}; forceLegacyBlend={AndroidRuntimePolicy.ShouldForceLegacyBlendStates()}");
                return BootPhaseResult.Success("LOGGER_READY");

            case BootPhase.BootInitPaths:
                var layout = _services.Paths.EnsureDirectoryLayout();
                var apkContent = new AndroidApkContentSource(_activity.Assets, _services.Logger);
                CelestePathBridge.Configure(
                    () => _services.Paths.ContentPath,
                    () => _services.Paths.SavePath,
                    () => _services.Paths.LogsPath,
                    (level, tag, message) =>
                    {
                        var mappedLevel = level switch
                        {
                            "ERROR" => LogLevel.Error,
                            "WARN" => LogLevel.Warn,
                            _ => LogLevel.Info
                        };

                        _services.Logger.Log(mappedLevel, tag, message);
                    },
                    apkContent.OpenApkContentStream,
                    apkContent.FileExists,
                    apkContent.DirectoryExists,
                    apkContent.EnumerateFiles);
                _services.Logger.Log(LogLevel.Info, "PATHS", $"BaseDataPath={_services.Paths.BaseDataPath}");
                _services.Logger.Log(LogLevel.Info, "PATHS", "ContentPath=apk://assets/Content");
                _services.Logger.Log(LogLevel.Info, "PATHS", $"LogsPath={_services.Paths.LogsPath}");
                _services.Logger.Log(LogLevel.Info, "PATHS", $"SavePath={_services.Paths.SavePath}");
                _services.Logger.Log(layout.Success ? LogLevel.Info : LogLevel.Error, "PATHS", layout.StatusCode, context: layout.Message);
                return layout.Success
                    ? BootPhaseResult.Success("PATHS_READY")
                    : BootPhaseResult.Failure("PATH_LAYOUT_FAILED");

            case BootPhase.BootApplyFullscreen:
                _fullscreen.Apply(_activity, "BootPhase-BootApplyFullscreen");
                return BootPhaseResult.Success("FULLSCREEN_APPLIED");

            case BootPhase.BootValidateContent:
                var report = _contentValidator.Validate();
                _lastContentReport = report;
                LogContentReport(report);

                return report.IsValid
                    ? BootPhaseResult.Success("CONTENT_OK", report)
                    : BootPhaseResult.Failure("CONTENT_INVALID", payload: report);

            case BootPhase.BootInitInput:
                _services.Input.Update();
                _services.Logger.Log(LogLevel.Info, "INPUT", _services.Input.CurrentInputSummary);
                return BootPhaseResult.Success("INPUT_READY");

            case BootPhase.BootInitAudio:
                try
                {
                    _services.Audio.Initialize();
                }
                catch (Exception exception)
                {
                    _services.Logger.Log(LogLevel.Warn, "AUDIO", "Audio initialization failed. Keeping controlled fallback", exception);
                    return BootPhaseResult.Success("AUDIO_FALLBACK_ACTIVE");
                }

                if (_services.Audio.IsInitialized)
                {
                    _services.Logger.Log(LogLevel.Info, "AUDIO", $"Audio backend initialized: {_services.Audio.BackendName}");
                    return BootPhaseResult.Success("AUDIO_READY");
                }

                _services.Logger.Log(LogLevel.Warn, "AUDIO", $"Audio backend not initialized: {_services.Audio.BackendName}; fallback policy active");
                return BootPhaseResult.Success("AUDIO_FALLBACK_ACTIVE");

            case BootPhase.BootStartGame:
                _services.Logger.Log(LogLevel.Info, "BOOT", "BootStartGame reached");
                return BootPhaseResult.Success("BOOT_START_GAME_OK");

            default:
                return BootPhaseResult.Failure($"Unsupported phase: {phase}");
        }
    }

    private void LogContentReport(ContentValidationReport report)
    {
        _services.Logger.Log(LogLevel.Info, "CONTENT", $"VALIDATION_STATUS={report.Status}; DIRS={report.ScannedDirectoryCount}; FILES={report.ScannedFileCount}");

        foreach (var issue in report.Issues)
        {
            var level = issue.Severity == IssueSeverity.Error ? LogLevel.Error : LogLevel.Warn;
            _services.Logger.Log(level, "CONTENT", $"{issue.Code} | {issue.Message}", context: $"relative={issue.RelativePath}; absolute={issue.AbsolutePath}; suggestion={issue.Suggestion}");
        }
    }

    private void IgnoreTouchInput()
    {
        var touches = TouchPanel.GetState();
        if (touches.Count == 0 || _touchIgnoredLogged)
        {
            return;
        }

        _touchIgnoredLogged = true;
        _services.Logger.Log(LogLevel.Warn, "INPUT", "Touch input detected and ignored by policy");
    }

    private void UpdateDiagnosticToggle(GameTime gameTime)
    {
        if (_services.Input.IsDiagnosticComboActive())
        {
            _diagHoldSeconds += gameTime.ElapsedGameTime.TotalSeconds;
            if (_diagHoldSeconds >= 2.0 && !_diagLatch)
            {
                _diagLatch = true;
                _diagnosticMode = !_diagnosticMode;
                _services.Logger.Log(LogLevel.Info, "DIAG", _diagnosticMode ? "Diagnostic overlay enabled" : "Diagnostic overlay disabled");
            }
        }
        else
        {
            _diagHoldSeconds = 0;
            _diagLatch = false;
        }
    }

    private void DrawBootingScreen()
    {
        var y = 40f;
        y = DrawLine("BOOTING CELESTE ANDROID PORT", 40, y, Color.White);
        y = DrawLine($"Status: {_bootMessage}", 40, y, Color.LightGray);
        y = DrawLine("Sem touch: use gamepad ou teclado.", 40, y, Color.Gray);
        DrawLine($"Log: {_services.Logger.CurrentSessionLogFile}", 40, y, Color.Gray);
    }

    private void DrawBootReadyScreen()
    {
        var y = 40f;
        y = DrawLine("BOOT NORMAL CONCLUIDO", 40, y, Color.LightGreen);
        y = DrawLine("Infra Android pronta: logger, paths, content validation, fullscreen.", 40, y, Color.White);
        y = DrawLine("Sem touch: use gamepad ou teclado.", 40, y, Color.LightGray);
        y = DrawLine("Proximo passo: integrar nucleo completo do jogo Celeste.", 40, y, Color.LightGray);
        DrawLine($"ABI ativa: {_activeAbi}", 40, y, Color.LightGray);
    }

    private void DrawErrorScreen()
    {
        if (_pixel is null || _spriteBatch is null)
        {
            return;
        }

        var viewport = GraphicsDevice.Viewport;
        var panelMargin = 18;
        var panel = new Rectangle(
            panelMargin,
            panelMargin,
            Math.Max(1, viewport.Width - panelMargin * 2),
            Math.Max(1, viewport.Height - panelMargin * 2));

        DrawPanel(panel, new Color(0, 0, 0, 230), new Color(56, 56, 56, 255));

        var titleColor = Color.White;
        var sectionColor = new Color(228, 228, 228);
        var bodyColor = new Color(194, 194, 194);
        var pathColor = new Color(210, 210, 210);
        var compactLayout = viewport.Height < 680 || viewport.Width < 1200;
        var detailScale = compactLayout ? 0.72f : 0.78f;
        var sectionScale = compactLayout ? 0.84f : 0.9f;
        var titleScale = compactLayout ? 0.96f : 1.02f;

        float x = panel.X + 20;
        float right = panel.Right - 20;
        float y = panel.Y + 16;

        y = DrawLine("FALHA NA VALIDACAO DE CONTEUDO", x, y, titleColor, titleScale);
        y = DrawLine("Os arquivos obrigatorios do jogo nao estao completos para iniciar o runtime.", x, y, bodyColor, detailScale);
        y = DrawLine($"Pasta esperada: {_services.Paths.ContentPath}", x, y, pathColor, detailScale);
        DrawHorizontalRule((int)x, (int)right, y + 4f, new Color(70, 70, 70, 255));
        y += 11f;

        if (_lastContentReport is null)
        {
            y = DrawLine("RESUMO", x, y, sectionColor, sectionScale);
            y = DrawLine("- Nenhum relatorio detalhado foi gerado nesta tentativa.", x, y, bodyColor, detailScale);
            y = DrawLine("- Verifique se a pasta Content existe e contem os arquivos originais.", x, y, bodyColor, detailScale);
        }
        else
        {
            var report = _lastContentReport;
            var missingDirectories = BuildIssueEntries(report, includeSuggestion: false,
                "CONTENT_ROOT_MISSING",
                "CONTENT_FOLDER_MISSING",
                "CONTENT_FOLDER_EMPTY");
            var missingFiles = BuildIssueEntries(report, includeSuggestion: false,
                "CONTENT_FILE_MISSING",
                "CONTENT_EXTENSION_MISSING");
            var unreadableFiles = BuildIssueEntries(report, includeSuggestion: false,
                "CONTENT_FILE_UNREADABLE");
            var packageIntegrityIssues = BuildIssueEntries(report, includeSuggestion: false,
                "CONTENT_FILE_COUNT_TOO_LOW");
            var caseEntries = BuildIssueEntries(report, includeSuggestion: true,
                "CONTENT_CASE_MISMATCH",
                "CONTENT_FILE_CASE_MISMATCH");
            var warningEntries = report.WarningIssues
                .Select(issue => $"{issue.Code}: {issue.Message}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var missingTotal = missingDirectories.Count + missingFiles.Count;

            y = DrawLine("RESUMO", x, y, sectionColor, sectionScale);
            y = DrawLine($"- Status: {report.Status}", x, y, bodyColor, detailScale);
            y = DrawLine($"- Erros: {report.ErrorIssues.Count()} | Avisos: {report.WarningIssues.Count()}", x, y, bodyColor, detailScale);
            y = DrawLine($"- Itens ausentes detectados: {missingTotal}", x, y, bodyColor, detailScale);
            y = DrawLine($"- Diretorios analisados: {report.ScannedDirectoryCount} | Arquivos analisados: {report.ScannedFileCount}", x, y, bodyColor, detailScale);
            y += 6f;

            var issueLineBudget = CalculateIssueLineBudget(panel, y, detailScale, footerLines: 6);
            var usedIssueLines = 0;
            var hiddenIssueLines = 0;

            void DrawIssueSection(string title, List<string> entries, int hardCap)
            {
                if (entries.Count == 0)
                {
                    return;
                }

                y += 4f;
                y = DrawLine($"{title} ({entries.Count})", x, y, sectionColor, sectionScale);

                var remainingBudget = Math.Max(0, issueLineBudget - usedIssueLines);
                if (remainingBudget <= 0)
                {
                    hiddenIssueLines += entries.Count;
                    return;
                }

                var toShow = Math.Min(entries.Count, Math.Min(hardCap, remainingBudget));
                for (var i = 0; i < toShow; i++)
                {
                    y = DrawLine($"- {TruncateForErrorUi(entries[i], 122)}", x, y, bodyColor, detailScale);
                }

                usedIssueLines += toShow;
                hiddenIssueLines += entries.Count - toShow;
            }

            DrawIssueSection("PASTAS AUSENTES", missingDirectories, hardCap: 8);
            DrawIssueSection("ARQUIVOS CRITICOS AUSENTES", missingFiles, hardCap: 22);
            DrawIssueSection("ARQUIVOS ILEGIVEIS", unreadableFiles, hardCap: 8);
            DrawIssueSection("INTEGRIDADE DO PACOTE", packageIntegrityIssues, hardCap: 4);
            DrawIssueSection("AJUSTES DE NOME/CASE", caseEntries, hardCap: 6);
            DrawIssueSection("AVISOS ADICIONAIS", warningEntries, hardCap: 4);

            if (missingDirectories.Count == 0 && missingFiles.Count == 0 && unreadableFiles.Count == 0 && packageIntegrityIssues.Count == 0 && caseEntries.Count == 0 && warningEntries.Count == 0)
            {
                y = DrawLine("- Nenhum item especifico foi retornado no relatorio atual.", x, y, bodyColor, detailScale);
            }

            if (hiddenIssueLines > 0)
            {
                y += 4f;
                y = DrawLine($"- ... e mais {hiddenIssueLines} item(ns) listado(s) no log de sessao.", x, y, bodyColor, detailScale);
            }
        }

        y += 8f;
        DrawHorizontalRule((int)x, (int)right, y, new Color(70, 70, 70, 255));
        y += 8f;
        y = DrawLine("COMO CORRIGIR", x, y, sectionColor, sectionScale);
        y = DrawLine("1) Copie o pacote completo de Content para o caminho exibido acima.", x, y, bodyColor, detailScale);
        y = DrawLine("2) Preserve subpastas e nomes exatamente como no jogo original (incluindo maiusculas/minusculas).", x, y, bodyColor, detailScale);
        y = DrawLine("3) Sem touch: use gamepad/teclado. START/ENTER tenta novamente, BACK sai.", x, y, bodyColor, detailScale);
        DrawLine($"Log de sessao: {_services.Logger.CurrentSessionLogFile}", x, y, bodyColor, detailScale);
    }

    private static List<string> BuildIssueEntries(ContentValidationReport report, bool includeSuggestion, params string[] issueCodes)
    {
        if (issueCodes.Length == 0)
        {
            return new List<string>();
        }

        var set = new HashSet<string>(issueCodes, StringComparer.Ordinal);
        return report.Issues
            .Where(issue => set.Contains(issue.Code))
            .Select(issue =>
            {
                var primary = issue.RelativePath;
                if (string.IsNullOrWhiteSpace(primary) || string.Equals(primary, "Content", StringComparison.OrdinalIgnoreCase))
                {
                    primary = issue.Message;
                }

                if (includeSuggestion && !string.IsNullOrWhiteSpace(issue.Suggestion))
                {
                    return $"{primary} -> {issue.Suggestion}";
                }

                return primary;
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int CalculateIssueLineBudget(Rectangle panel, float currentY, float detailScale, int footerLines)
    {
        var lineAdvance = EstimateLineAdvance(detailScale);
        var availableHeight = Math.Max(0f, panel.Bottom - 16f - currentY);
        var reservedFooter = Math.Max(0f, lineAdvance * footerLines + 14f);
        var budget = (int)MathF.Floor((availableHeight - reservedFooter) / Math.Max(1f, lineAdvance));
        return Math.Clamp(budget, 8, 60);
    }

    private float EstimateLineAdvance(float scale)
    {
        if (_internalErrorFont is not null)
        {
            return _internalErrorFont.LineSpacing * scale;
        }

        var fallbackScale = Math.Max(1f, 2f * scale);
        return _fallbackFont.LineHeight(fallbackScale);
    }

    private void DrawPanel(Rectangle rect, Color fill, Color border)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, rect, fill);

        const int thickness = 2;
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), border);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), border);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), border);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), border);
    }

    private void DrawHorizontalRule(int left, int right, float y, Color color)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        var width = Math.Max(1, right - left);
        var rect = new Rectangle(left, (int)MathF.Round(y), width, 1);
        _spriteBatch.Draw(_pixel, rect, color);
    }

    private static string TruncateForErrorUi(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private void DrawDiagnosticOverlay()
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        var snapshot = BuildDiagnosticSnapshot();
        var box = new Rectangle(12, 12, GraphicsDevice.Viewport.Width - 24, 220);
        _spriteBatch.Draw(_pixel, box, new Color(0, 0, 0, 180));

        var y = 20f;
        y = DrawLine("DIAGNOSTICO DE BOOT", 20, y, Color.Cyan);
        y = DrawLine($"BaseDataPath: {snapshot.BaseDataPath}", 20, y, Color.White);
        y = DrawLine($"ContentPath: {snapshot.ContentPath}", 20, y, Color.White);
        y = DrawLine($"SavePath: {snapshot.SavePath}", 20, y, Color.White);
        y = DrawLine($"LogsPath: {snapshot.LogsPath}", 20, y, Color.White);
        y = DrawLine($"ContentStatus: {snapshot.ContentStatus} (errors={snapshot.ContentErrorCount}, warnings={snapshot.ContentWarningCount})", 20, y, Color.White);
        y = DrawLine($"ABI: {snapshot.ActiveAbi}", 20, y, Color.White);
        y = DrawLine($"Audio: {snapshot.AudioBackend} | initialized={snapshot.AudioInitialized}", 20, y, Color.White);
        y = DrawLine($"Policy: lowMemory={snapshot.LowMemoryModeEnabled} | aggressiveGc={snapshot.AggressiveGarbageCollectionEnabled} | preferReach={snapshot.PreferReachGraphicsProfile} | legacyBlend={snapshot.ForceLegacyBlendStates}", 20, y, Color.White);
        y = DrawLine($"Input: {snapshot.InputSummary}", 20, y, Color.White);
        y = DrawLine(_gpuInfo, 20, y, Color.White);
        DrawLine($"Log: {snapshot.SessionLogFile}", 20, y, Color.White);
    }

    private DiagnosticSnapshot BuildDiagnosticSnapshot()
    {
        var report = _lastContentReport;
        return new DiagnosticSnapshot
        {
            BaseDataPath = _services.Paths.BaseDataPath,
            ContentPath = _services.Paths.ContentPath,
            LogsPath = _services.Paths.LogsPath,
            SavePath = _services.Paths.SavePath,
            ActiveAbi = _activeAbi,
            AudioBackend = _services.Audio.BackendName,
            AudioInitialized = _services.Audio.IsInitialized,
            LowMemoryModeEnabled = AndroidRuntimePolicy.IsLowMemoryModeEnabled(),
            AggressiveGarbageCollectionEnabled = AndroidRuntimePolicy.IsAggressiveGarbageCollectionEnabled(),
            PreferReachGraphicsProfile = AndroidRuntimePolicy.ShouldPreferReachGraphicsProfile(),
            ForceLegacyBlendStates = AndroidRuntimePolicy.ShouldForceLegacyBlendStates(),
            InputSummary = _services.Input.CurrentInputSummary,
            ContentStatus = report?.Status ?? ContentValidationStatus.Missing,
            ContentErrorCount = report?.ErrorIssues.Count() ?? 0,
            ContentWarningCount = report?.WarningIssues.Count() ?? 0,
            SessionLogFile = _services.Logger.CurrentSessionLogFile
        };
    }

    private void TryEmergencyGc(string reason, bool force)
    {
        var now = DateTime.UtcNow;
        if (!force && _lastEmergencyGcUtc != DateTime.MinValue && (now - _lastEmergencyGcUtc).TotalSeconds < 2)
        {
            return;
        }

        _lastEmergencyGcUtc = now;
        var managedBefore = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var managedAfter = GC.GetTotalMemory(false);
        _services.Logger.Log(LogLevel.Warn, "MEMORY", "Emergency GC executed", context: $"reason={reason}; managedBefore={managedBefore}; managedAfter={managedAfter}; gc0={GC.CollectionCount(0)}; gc1={GC.CollectionCount(1)}; gc2={GC.CollectionCount(2)}");
    }

    private float DrawLine(string text, float x, float y, Color color, float scale = 1f)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return y;
        }

        if (_internalErrorFont is not null)
        {
            var safeText = SanitizeForSpriteFont(text);
            if (!string.Equals(safeText, text, StringComparison.Ordinal) && !_internalErrorFontSanitizerLogged)
            {
                _internalErrorFontSanitizerLogged = true;
                _services.Logger.Log(LogLevel.Warn, "UI", "Error screen text sanitized for internal SpriteFont compatibility");
            }

            try
            {
                _spriteBatch.DrawString(_internalErrorFont, safeText, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return y + _internalErrorFont.LineSpacing * scale;
            }
            catch (ArgumentException exception)
            {
                if (!_internalErrorFontFallbackLogged)
                {
                    _internalErrorFontFallbackLogged = true;
                    _services.Logger.Log(LogLevel.Warn, "UI", "Internal SpriteFont failed while drawing error screen. Switching to bitmap fallback font.", exception);
                }

                _internalErrorFont = null;
            }
        }

        var fallbackScale = Math.Max(1f, 2f * scale);
        _fallbackFont.DrawString(_spriteBatch, _pixel, text, new Vector2(x, y), color, fallbackScale);
        return y + _fallbackFont.LineHeight(fallbackScale);
    }

    private static string SanitizeForSpriteFont(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character == '\n' || character == '\r' || character == '\t')
            {
                builder.Append(character);
                continue;
            }

            if (character >= ' ' && character <= '~')
            {
                builder.Append(character);
                continue;
            }

            builder.Append('?');
        }

        return builder.ToString();
    }
}
