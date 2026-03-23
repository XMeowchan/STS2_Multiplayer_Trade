using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Sts2MultiplayerTrade;

internal sealed class TradeOfferDraft
{
    public int GoldAmount { get; set; }

    public List<TradePotionSelection> Potions { get; set; } = new();

    public List<TradeRelicSelection> Relics { get; set; } = new();

    public TradeOfferDraft Clone()
    {
        return new TradeOfferDraft
        {
            GoldAmount = GoldAmount,
            Potions = Potions.Select(static item => item.Clone()).ToList(),
            Relics = Relics.Select(static item => item.Clone()).ToList()
        };
    }
}

internal sealed class TradeSessionState
{
    public required string SessionId { get; init; }

    public required ulong InitiatorPlayerId { get; init; }

    public required ulong RecipientPlayerId { get; init; }

    public int Revision { get; set; }

    public bool Accepted { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public Dictionary<ulong, TradeOfferDraft> OffersByPlayerId { get; } = new();

    public Dictionary<ulong, bool> ReadyByPlayerId { get; } = new();

    public bool Involves(ulong playerId)
    {
        return InitiatorPlayerId == playerId || RecipientPlayerId == playerId;
    }

    public ulong GetCounterparty(ulong playerId)
    {
        return InitiatorPlayerId == playerId ? RecipientPlayerId : InitiatorPlayerId;
    }
}

internal sealed class TradeValidationResult
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;
}

internal sealed class TradeCommitPlan
{
    public required string SessionId { get; init; }

    public required int Revision { get; init; }

    public required List<TradeGoldTransfer> GoldTransfers { get; init; }

    public required List<TradePotionTransfer> PotionTransfers { get; init; }

    public required List<TradeRelicTransfer> RelicTransfers { get; init; }
}

internal sealed class TradePotionSelection
{
    public int SlotIndex { get; init; }

    public required ModelId PotionId { get; init; }

    public required string Fingerprint { get; init; }

    public TradePotionSelection Clone()
    {
        return new TradePotionSelection
        {
            SlotIndex = SlotIndex,
            PotionId = PotionId,
            Fingerprint = Fingerprint
        };
    }
}

internal sealed class TradeRelicSelection
{
    public int RelicIndex { get; init; }

    public required ModelId RelicId { get; init; }

    public required string Fingerprint { get; init; }

    public TradeRelicSelection Clone()
    {
        return new TradeRelicSelection
        {
            RelicIndex = RelicIndex,
            RelicId = RelicId,
            Fingerprint = Fingerprint
        };
    }
}

internal static class TradeFingerprintHelper
{
    public static string ForPotion(PotionModel potion, int slotIndex)
    {
        return FromJson(potion.ToSerializable(slotIndex));
    }

    public static string ForRelic(RelicModel relic)
    {
        return FromJson(relic.ToSerializable());
    }

    private static string FromJson<T>(T value)
    {
        try
        {
            string json = JsonSerializer.Serialize(value);
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes.AsSpan(0, 12)).ToLowerInvariant();
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
