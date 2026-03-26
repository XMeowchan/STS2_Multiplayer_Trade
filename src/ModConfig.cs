using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2MultiplayerTrade;

internal sealed class ModConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("hide_from_multiplayer_mod_list")]
    public bool HideFromMultiplayerModList { get; set; }

    [JsonPropertyName("trade_enabled")]
    public bool TradeEnabled { get; set; } = true;

    [JsonPropertyName("trade_invite_timeout_seconds")]
    public int TradeInviteTimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("trade_session_timeout_seconds")]
    public int TradeSessionTimeoutSeconds { get; set; } = 180;

    [JsonPropertyName("trade_allow_gold")]
    public bool TradeAllowGold { get; set; } = true;

    [JsonPropertyName("trade_allow_potions")]
    public bool TradeAllowPotions { get; set; } = true;

    [JsonPropertyName("trade_allow_relics")]
    public bool TradeAllowRelics { get; set; } = true;

    [JsonPropertyName("dev_test_mode_enabled")]
    public bool DevTestModeEnabled { get; set; }

    [JsonPropertyName("mod_update_enabled")]
    public bool ModUpdateEnabled { get; set; } = true;

    [JsonPropertyName("mod_update_github_repo")]
    public string ModUpdateGithubRepo { get; set; } = "XMeowchan/STS2_Multiplayer_Trade";

    [JsonPropertyName("mod_update_timeout_seconds")]
    public int ModUpdateTimeoutSeconds { get; set; } = 15;

    [JsonPropertyName("telemetry_enabled")]
    public bool TelemetryEnabled { get; set; }

    [JsonPropertyName("telemetry_endpoint")]
    public string TelemetryEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("telemetry_endpoints")]
    public string[] TelemetryEndpoints { get; set; } = Array.Empty<string>();

    [JsonPropertyName("telemetry_timeout_seconds")]
    public int TelemetryTimeoutSeconds { get; set; } = 5;

    public static ModConfig Load(string path)
    {
        ModConfig defaults = new();
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                ModConfig? parsed = JsonSerializer.Deserialize<ModConfig>(json, JsonOptions);
                if (parsed != null)
                {
                    if (parsed.Normalize())
                    {
                        parsed.TryWrite(path);
                    }

                    return parsed;
                }
            }
        }
        catch
        {
        }

        defaults.Normalize();
        defaults.Write(path);
        return defaults;
    }

    public void Write(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(this, JsonOptionsIndented);
        File.WriteAllText(path, json);
    }

    private bool Normalize()
    {
        bool changed = false;

        if (HideFromMultiplayerModList)
        {
            HideFromMultiplayerModList = false;
            changed = true;
        }

        int tradeInviteTimeout = Math.Clamp(TradeInviteTimeoutSeconds, 10, 300);
        if (TradeInviteTimeoutSeconds != tradeInviteTimeout)
        {
            TradeInviteTimeoutSeconds = tradeInviteTimeout;
            changed = true;
        }

        int tradeSessionTimeout = Math.Clamp(TradeSessionTimeoutSeconds, 30, 1800);
        if (TradeSessionTimeoutSeconds != tradeSessionTimeout)
        {
            TradeSessionTimeoutSeconds = tradeSessionTimeout;
            changed = true;
        }

        string updateRepo = (ModUpdateGithubRepo ?? string.Empty).Trim().Trim('/');
        if (!string.Equals(ModUpdateGithubRepo, updateRepo, StringComparison.Ordinal))
        {
            ModUpdateGithubRepo = updateRepo;
            changed = true;
        }

        int updateTimeout = Math.Clamp(ModUpdateTimeoutSeconds, 5, 60);
        if (ModUpdateTimeoutSeconds != updateTimeout)
        {
            ModUpdateTimeoutSeconds = updateTimeout;
            changed = true;
        }

        string endpoint = (TelemetryEndpoint ?? string.Empty).Trim();
        if (!string.Equals(TelemetryEndpoint, endpoint, StringComparison.Ordinal))
        {
            TelemetryEndpoint = endpoint;
            changed = true;
        }

        string[] normalizedEndpoints = (TelemetryEndpoints ?? Array.Empty<string>())
            .Select(static item => (item ?? string.Empty).Trim())
            .Where(static item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (TelemetryEndpoints is null || !TelemetryEndpoints.SequenceEqual(normalizedEndpoints, StringComparer.Ordinal))
        {
            TelemetryEndpoints = normalizedEndpoints;
            changed = true;
        }

        string[] effectiveTelemetryEndpoints = TelemetryEndpoints ?? Array.Empty<string>();
        string effectiveTelemetryEndpoint = TelemetryEndpoint ?? string.Empty;

        if (effectiveTelemetryEndpoints.Length == 0 && effectiveTelemetryEndpoint.Length > 0)
        {
            TelemetryEndpoints = new[] { effectiveTelemetryEndpoint };
            effectiveTelemetryEndpoints = TelemetryEndpoints;
            changed = true;
        }

        if (effectiveTelemetryEndpoints.Length > 0 && !string.Equals(effectiveTelemetryEndpoint, effectiveTelemetryEndpoints[0], StringComparison.Ordinal))
        {
            TelemetryEndpoint = effectiveTelemetryEndpoints[0];
            changed = true;
        }

        int telemetryTimeout = Math.Clamp(TelemetryTimeoutSeconds, 2, 30);
        if (TelemetryTimeoutSeconds != telemetryTimeout)
        {
            TelemetryTimeoutSeconds = telemetryTimeout;
            changed = true;
        }

        return changed;
    }

    private void TryWrite(string path)
    {
        try
        {
            Write(path);
        }
        catch
        {
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions JsonOptionsIndented = new()
    {
        WriteIndented = true
    };
}
