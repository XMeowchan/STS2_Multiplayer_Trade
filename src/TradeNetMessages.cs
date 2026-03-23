using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Sts2MultiplayerTrade;

internal sealed class TradeInviteMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradeInviteReplyMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public bool Accepted { get; set; }

    public string Reason { get; set; } = string.Empty;

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.WriteBool(Accepted);
        writer.WriteString(Reason ?? string.Empty);
        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        Accepted = reader.ReadBool();
        Reason = reader.ReadString();
        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradeOfferSyncMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public ulong AuthorPlayerId { get; set; }

    public int Revision { get; set; }

    public int GoldAmount { get; set; }

    public List<TradePotionSelectionMessage> Potions { get; set; } = new();

    public List<TradeRelicSelectionMessage> Relics { get; set; } = new();

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.WriteULong(AuthorPlayerId);
        writer.WriteInt(Revision, 16);
        writer.WriteInt(GoldAmount);
        writer.WriteInt(Potions.Count, 8);
        foreach (TradePotionSelectionMessage potion in Potions)
        {
            potion.Serialize(writer);
        }

        writer.WriteInt(Relics.Count, 8);
        foreach (TradeRelicSelectionMessage relic in Relics)
        {
            relic.Serialize(writer);
        }

        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        AuthorPlayerId = reader.ReadULong();
        Revision = reader.ReadInt(16);
        GoldAmount = reader.ReadInt();

        int potionCount = reader.ReadInt(8);
        Potions = new List<TradePotionSelectionMessage>(potionCount);
        for (int index = 0; index < potionCount; index += 1)
        {
            TradePotionSelectionMessage potion = new();
            potion.Deserialize(reader);
            Potions.Add(potion);
        }

        int relicCount = reader.ReadInt(8);
        Relics = new List<TradeRelicSelectionMessage>(relicCount);
        for (int index = 0; index < relicCount; index += 1)
        {
            TradeRelicSelectionMessage relic = new();
            relic.Deserialize(reader);
            Relics.Add(relic);
        }

        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradeReadyStateMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public ulong AuthorPlayerId { get; set; }

    public int Revision { get; set; }

    public bool IsReady { get; set; }

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.WriteULong(AuthorPlayerId);
        writer.WriteInt(Revision, 16);
        writer.WriteBool(IsReady);
        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        AuthorPlayerId = reader.ReadULong();
        Revision = reader.ReadInt(16);
        IsReady = reader.ReadBool();
        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradeCommitRequestMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public int ExpectedRevision { get; set; }

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => false;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.WriteInt(ExpectedRevision, 16);
        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        ExpectedRevision = reader.ReadInt(16);
        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradeCommitAppliedMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public int Revision { get; set; }

    public List<TradeGoldTransfer> GoldTransfers { get; set; } = new();

    public List<TradePotionTransfer> PotionTransfers { get; set; } = new();

    public List<TradeRelicTransfer> RelicTransfers { get; set; } = new();

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.WriteInt(Revision, 16);
        writer.WriteInt(GoldTransfers.Count, 8);
        foreach (TradeGoldTransfer item in GoldTransfers)
        {
            item.Serialize(writer);
        }

        writer.WriteInt(PotionTransfers.Count, 8);
        foreach (TradePotionTransfer item in PotionTransfers)
        {
            item.Serialize(writer);
        }

        writer.WriteInt(RelicTransfers.Count, 8);
        foreach (TradeRelicTransfer item in RelicTransfers)
        {
            item.Serialize(writer);
        }

        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        Revision = reader.ReadInt(16);

        int goldCount = reader.ReadInt(8);
        GoldTransfers = new List<TradeGoldTransfer>(goldCount);
        for (int index = 0; index < goldCount; index += 1)
        {
            TradeGoldTransfer item = new();
            item.Deserialize(reader);
            GoldTransfers.Add(item);
        }

        int potionCount = reader.ReadInt(8);
        PotionTransfers = new List<TradePotionTransfer>(potionCount);
        for (int index = 0; index < potionCount; index += 1)
        {
            TradePotionTransfer item = new();
            item.Deserialize(reader);
            PotionTransfers.Add(item);
        }

        int relicCount = reader.ReadInt(8);
        RelicTransfers = new List<TradeRelicTransfer>(relicCount);
        for (int index = 0; index < relicCount; index += 1)
        {
            TradeRelicTransfer item = new();
            item.Deserialize(reader);
            RelicTransfers.Add(item);
        }

        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradeCommitRejectedMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.WriteString(Reason ?? string.Empty);
        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        Reason = reader.ReadString();
        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradeCancelMessage : INetMessage, IPacketSerializable, IRunLocationTargetedMessage
{
    public required string SessionId { get; set; }

    public ulong InitiatorPlayerId { get; set; }

    public ulong RecipientPlayerId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public RunLocation Location { get; set; }

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(SessionId);
        writer.WriteULong(InitiatorPlayerId);
        writer.WriteULong(RecipientPlayerId);
        writer.WriteString(Reason ?? string.Empty);
        writer.Write(Location);
    }

    public void Deserialize(PacketReader reader)
    {
        SessionId = reader.ReadString();
        InitiatorPlayerId = reader.ReadULong();
        RecipientPlayerId = reader.ReadULong();
        Reason = reader.ReadString();
        Location = reader.Read<RunLocation>();
    }
}

internal sealed class TradePotionSelectionMessage : IPacketSerializable
{
    public int SlotIndex { get; set; }

    public ModelId PotionId { get; set; } = ModelId.none;

    public string Fingerprint { get; set; } = string.Empty;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(SlotIndex, 8);
        writer.WriteModelEntry(PotionId);
        writer.WriteString(Fingerprint ?? string.Empty);
    }

    public void Deserialize(PacketReader reader)
    {
        SlotIndex = reader.ReadInt(8);
        PotionId = reader.ReadModelIdAssumingType<PotionModel>();
        Fingerprint = reader.ReadString();
    }
}

internal sealed class TradeRelicSelectionMessage : IPacketSerializable
{
    public int RelicIndex { get; set; }

    public ModelId RelicId { get; set; } = ModelId.none;

    public string Fingerprint { get; set; } = string.Empty;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteInt(RelicIndex, 8);
        writer.WriteModelEntry(RelicId);
        writer.WriteString(Fingerprint ?? string.Empty);
    }

    public void Deserialize(PacketReader reader)
    {
        RelicIndex = reader.ReadInt(8);
        RelicId = reader.ReadModelIdAssumingType<RelicModel>();
        Fingerprint = reader.ReadString();
    }
}

internal sealed class TradeGoldTransfer : IPacketSerializable
{
    public ulong SourcePlayerId { get; set; }

    public ulong TargetPlayerId { get; set; }

    public int GoldAmount { get; set; }

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(SourcePlayerId);
        writer.WriteULong(TargetPlayerId);
        writer.WriteInt(GoldAmount);
    }

    public void Deserialize(PacketReader reader)
    {
        SourcePlayerId = reader.ReadULong();
        TargetPlayerId = reader.ReadULong();
        GoldAmount = reader.ReadInt();
    }
}

internal sealed class TradePotionTransfer : IPacketSerializable
{
    public ulong SourcePlayerId { get; set; }

    public ulong TargetPlayerId { get; set; }

    public int SourceSlotIndex { get; set; }

    public int TargetSlotIndex { get; set; }

    public SerializablePotion Potion { get; set; } = new();

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(SourcePlayerId);
        writer.WriteULong(TargetPlayerId);
        writer.WriteInt(SourceSlotIndex, 8);
        writer.WriteInt(TargetSlotIndex, 8);
        Potion.Serialize(writer);
    }

    public void Deserialize(PacketReader reader)
    {
        SourcePlayerId = reader.ReadULong();
        TargetPlayerId = reader.ReadULong();
        SourceSlotIndex = reader.ReadInt(8);
        TargetSlotIndex = reader.ReadInt(8);
        Potion = new SerializablePotion();
        Potion.Deserialize(reader);
    }
}

internal sealed class TradeRelicTransfer : IPacketSerializable
{
    public ulong SourcePlayerId { get; set; }

    public ulong TargetPlayerId { get; set; }

    public int SourceRelicIndex { get; set; }

    public int TargetRelicIndex { get; set; }

    public SerializableRelic Relic { get; set; } = new();

    public void Serialize(PacketWriter writer)
    {
        writer.WriteULong(SourcePlayerId);
        writer.WriteULong(TargetPlayerId);
        writer.WriteInt(SourceRelicIndex, 8);
        writer.WriteInt(TargetRelicIndex, 8);
        Relic.Serialize(writer);
    }

    public void Deserialize(PacketReader reader)
    {
        SourcePlayerId = reader.ReadULong();
        TargetPlayerId = reader.ReadULong();
        SourceRelicIndex = reader.ReadInt(8);
        TargetRelicIndex = reader.ReadInt(8);
        Relic = new SerializableRelic();
        Relic.Deserialize(reader);
    }
}
