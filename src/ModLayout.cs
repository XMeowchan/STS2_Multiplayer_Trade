namespace Sts2MultiplayerTrade;

internal static class ModLayout
{
    public const string ManifestFileName = ModEntry.ModId + ".json";

    public const string LegacyManifestFileName = "mod_manifest.json";

    public const string ConfigFileName = "config.cfg";

    public const string LegacyConfigFileName = "config.json";

    public static string GetManifestPath(string modDirectory)
    {
        return Path.Combine(modDirectory, ManifestFileName);
    }

    public static string GetConfigPath(string modDirectory)
    {
        return Path.Combine(modDirectory, ConfigFileName);
    }

    public static IEnumerable<string> GetManifestCandidatePaths(string modDirectory)
    {
        yield return GetManifestPath(modDirectory);
        yield return Path.Combine(modDirectory, LegacyManifestFileName);
    }

    public static string? FindManifestPath(string modDirectory)
    {
        foreach (string path in GetManifestCandidatePaths(modDirectory))
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
