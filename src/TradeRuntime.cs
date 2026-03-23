using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2MultiplayerTrade;

internal static class TradeRuntime
{
    private static bool _initialized;

    private static ModConfig _config = new();

    internal static TradeSessionManager? Current { get; private set; }

    public static void Initialize(ModConfig config)
    {
        _config = config;
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        RunManager.Instance.RunStarted += OnRunStarted;
    }

    public static void CleanupCurrent()
    {
        if (Current == null)
        {
            return;
        }

        try
        {
            Current.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warn($"{ModEntry.ModId}: trade runtime cleanup failed: {ex.Message}", 2);
        }
        finally
        {
            Current = null;
        }
    }

    public static bool OpenTrade(Player remotePlayer)
    {
        return Current?.StartOrOpenTrade(remotePlayer) == true;
    }

    public static bool HasLocalTestMode()
    {
        return Current?.IsLocalTestMode == true;
    }

    public static bool OpenLocalTestTrade()
    {
        return Current?.OpenLocalTestTrade() == true;
    }

    public static bool TryGetTradeAvailability(Player remotePlayer, out string reason)
    {
        if (Current == null)
        {
            reason = TradeUiText.LocalOnlyReason;
            return false;
        }

        return Current.CanTrade(remotePlayer, out reason);
    }

    private static void OnRunStarted(RunState runState)
    {
        CleanupCurrent();
        if (!_config.Enabled || !_config.TradeEnabled)
        {
            return;
        }

        if (RunManager.Instance.IsSinglePlayerOrFakeMultiplayer || runState.Players.Count <= 1)
        {
            if (_config.DevTestModeEnabled && runState.Players.Count == 1)
            {
                Player? localPlayer = LocalContext.GetMe(runState);
                if (localPlayer != null)
                {
                    Player devRemotePlayer = CreateDevRemotePlayer(runState, localPlayer);
                    Current = new TradeSessionManager(runState, _config, devRemotePlayer);
                    Current.Initialize();
                    Log.Info($"{ModEntry.ModId}: single-player dev test mode enabled.", 2);
                }
            }
            return;
        }

        Current = new TradeSessionManager(runState, _config);
        Current.Initialize();
    }

    private static Player CreateDevRemotePlayer(RunState runState, Player localPlayer)
    {
        CharacterModel remoteCharacter = runState.UnlockState.Characters.FirstOrDefault(character => character.Id != localPlayer.Character.Id)
            ?? localPlayer.Character;
        Player player = Player.CreateForNewRun(remoteCharacter, runState.UnlockState, 987654321012345678UL);
        player.Gold = Math.Max(120, localPlayer.Gold / 2);

        foreach (PotionModel potion in ModelDb.AllPotions
                     .Where(static item => item.Rarity is PotionRarity.Common or PotionRarity.Uncommon or PotionRarity.Rare)
                     .Take(1))
        {
            player.AddPotionInternal(potion.ToMutable());
        }

        foreach (RelicModel relic in ModelDb.AllRelics
                     .Where(static item => item.IsTradable)
                     .Take(3))
        {
            player.AddRelicInternal(relic.ToMutable());
        }

        return player;
    }
}
