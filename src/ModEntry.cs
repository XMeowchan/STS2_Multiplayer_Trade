using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Sts2MultiplayerTrade;

[ModInitializer("Initialize")]
public static class ModEntry
{
    public const string ModId = "Sts2MultiplayerTrade";

    private static readonly object InitLock = new();

    private static bool _initialized;

    private static Harmony? _harmony;

    internal static string ModDirectory { get; private set; } = string.Empty;

    internal static ModConfig Config { get; private set; } = new();

    internal static ModAutoUpdater AutoUpdater { get; private set; } = new(string.Empty, new ModConfig());

    internal static TelemetryClient Telemetry { get; private set; } = new(string.Empty, new ModConfig());

    public static void Initialize()
    {
        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            ModDirectory = ResolveModDirectory();
            MigrateLegacyFiles(ModDirectory);
            Config = ModConfig.Load(ModLayout.GetConfigPath(ModDirectory));
            AutoUpdater = new ModAutoUpdater(ModDirectory, Config);
            Telemetry = new TelemetryClient(ModDirectory, Config);
            TradeRuntime.Initialize(Config);

            _harmony = new Harmony("cn.codex.sts2.multiplayer.trade");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            AutoUpdater.QueueCheck();
            Telemetry.QueueDailyHeartbeat();
            _initialized = true;

            Log.Info($"{ModId} loaded from '{ModDirectory}'.", 2);
        }
    }

    private static string ResolveModDirectory()
    {
        string? assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            string? directory = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static void MigrateLegacyFiles(string modDirectory)
    {
        TryMigrateFile(
            Path.Combine(modDirectory, ModLayout.LegacyConfigFileName),
            ModLayout.GetConfigPath(modDirectory),
            "config",
            overwriteExistingTarget: true);

        if (File.Exists(ModLayout.GetManifestPath(modDirectory)))
        {
            TryDeleteLegacyFile(Path.Combine(modDirectory, ModLayout.LegacyManifestFileName), "legacy manifest");
        }
    }

    private static void TryMigrateFile(string sourcePath, string targetPath, string label, bool overwriteExistingTarget = false)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            if (File.Exists(targetPath))
            {
                if (overwriteExistingTarget)
                {
                    File.Copy(sourcePath, targetPath, overwrite: true);
                }

                File.Delete(sourcePath);
                if (overwriteExistingTarget)
                {
                    Log.Info($"{ModId}: migrated legacy {label} to '{targetPath}'.", 2);
                }

                return;
            }

            string? directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Move(sourcePath, targetPath);
            Log.Info($"{ModId}: migrated legacy {label} to '{targetPath}'.", 2);
        }
        catch (Exception ex)
        {
            Log.Warn($"{ModId}: failed to migrate legacy {label} '{sourcePath}': {ex.Message}", 2);
        }
    }

    private static void TryDeleteLegacyFile(string path, string label)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Log.Info($"{ModId}: removed {label} at '{path}'.", 2);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"{ModId}: failed to remove {label} at '{path}': {ex.Message}", 2);
        }
    }
}
