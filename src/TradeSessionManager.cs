using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Sts2MultiplayerTrade;

internal sealed class TradeSessionManager : IDisposable
{
    private readonly RunState _runState;

    private readonly ModConfig _config;

    private readonly Player? _devRemotePlayer;

    private TradeSessionState? _activeSession;

    public TradeSessionManager(RunState runState, ModConfig config, Player? devRemotePlayer = null)
    {
        _runState = runState;
        _config = config;
        _devRemotePlayer = devRemotePlayer;
    }

    public event Action? StateChanged;

    public TradeSessionState? ActiveSession => _activeSession;

    public bool IsLocalTestMode => _devRemotePlayer != null;

    private bool IsHost => RunManager.Instance.NetService.Type == NetGameType.Host;

    private ulong LocalPlayerId
    {
        get
        {
            if (IsLocalTestMode && _runState.Players.Count == 1)
            {
                return _runState.Players[0].NetId;
            }

            return LocalContext.GetMe(_runState)?.NetId
                   ?? LocalContext.NetId
                   ?? 0UL;
        }
    }

    private RunLocationTargetedMessageBuffer Buffer => RunManager.Instance.RunLocationTargetedBuffer;

    public void Initialize()
    {
        Buffer.RegisterMessageHandler<TradeInviteMessage>(HandleInviteMessage);
        Buffer.RegisterMessageHandler<TradeInviteReplyMessage>(HandleInviteReplyMessage);
        Buffer.RegisterMessageHandler<TradeOfferSyncMessage>(HandleOfferSyncMessage);
        Buffer.RegisterMessageHandler<TradeReadyStateMessage>(HandleReadyStateMessage);
        Buffer.RegisterMessageHandler<TradeCommitRequestMessage>(HandleCommitRequestMessage);
        Buffer.RegisterMessageHandler<TradeCommitAppliedMessage>(HandleCommitAppliedMessage);
        Buffer.RegisterMessageHandler<TradeCommitRejectedMessage>(HandleCommitRejectedMessage);
        Buffer.RegisterMessageHandler<TradeCancelMessage>(HandleCancelMessage);

        RunManager.Instance.RoomExited += OnRoomExited;
        if (RunManager.Instance.RunLobby != null)
        {
            RunManager.Instance.RunLobby.RemotePlayerDisconnected += OnRemotePlayerDisconnected;
            RunManager.Instance.RunLobby.LocalPlayerDisconnected += OnLocalPlayerDisconnected;
        }
    }

    public void Dispose()
    {
        Buffer.UnregisterMessageHandler<TradeInviteMessage>(HandleInviteMessage);
        Buffer.UnregisterMessageHandler<TradeInviteReplyMessage>(HandleInviteReplyMessage);
        Buffer.UnregisterMessageHandler<TradeOfferSyncMessage>(HandleOfferSyncMessage);
        Buffer.UnregisterMessageHandler<TradeReadyStateMessage>(HandleReadyStateMessage);
        Buffer.UnregisterMessageHandler<TradeCommitRequestMessage>(HandleCommitRequestMessage);
        Buffer.UnregisterMessageHandler<TradeCommitAppliedMessage>(HandleCommitAppliedMessage);
        Buffer.UnregisterMessageHandler<TradeCommitRejectedMessage>(HandleCommitRejectedMessage);
        Buffer.UnregisterMessageHandler<TradeCancelMessage>(HandleCancelMessage);

        RunManager.Instance.RoomExited -= OnRoomExited;
        if (RunManager.Instance.RunLobby != null)
        {
            RunManager.Instance.RunLobby.RemotePlayerDisconnected -= OnRemotePlayerDisconnected;
            RunManager.Instance.RunLobby.LocalPlayerDisconnected -= OnLocalPlayerDisconnected;
        }

        _activeSession = null;
    }

    public bool CanTrade(Player remotePlayer, out string reason)
    {
        reason = string.Empty;
        if (!_config.Enabled || !_config.TradeEnabled || remotePlayer == null)
        {
            reason = TradeUiText.LocalOnlyReason;
            return false;
        }

        if (LocalContext.IsMe(remotePlayer))
        {
            reason = TradeUiText.LocalOnlyReason;
            return false;
        }

        if (!IsLocalTestMode && (RunManager.Instance.IsSinglePlayerOrFakeMultiplayer || _runState.Players.Count <= 1))
        {
            reason = TradeUiText.LocalOnlyReason;
            return false;
        }

        if (CombatManager.Instance?.IsInProgress == true)
        {
            reason = TradeUiText.CombatReason;
            return false;
        }

        if (_activeSession != null
            && (!_activeSession.Involves(LocalPlayerId) || _activeSession.GetCounterparty(LocalPlayerId) != remotePlayer.NetId))
        {
            reason = TradeUiText.BusyReason;
            return false;
        }

        return true;
    }

    public bool OpenLocalTestTrade()
    {
        if (!IsLocalTestMode || _devRemotePlayer == null)
        {
            return false;
        }

        if (_activeSession == null)
        {
            ClearTradeUi();
        }

        return StartOrOpenTrade(_devRemotePlayer);
    }

    public bool StartOrOpenTrade(Player remotePlayer)
    {
        if (!CanTrade(remotePlayer, out string reason))
        {
            Log.Warn($"{ModEntry.ModId}: cannot start trade: {reason}", 2);
            return false;
        }

        if (_activeSession == null)
        {
            _activeSession = CreateSession(LocalPlayerId, remotePlayer.NetId);
            _activeSession.StatusText = IsLocalTestMode ? "Single-player dev test mode." : TradeUiText.PendingInvite;
            _activeSession.Accepted = IsLocalTestMode;
            if (IsLocalTestMode)
            {
                _activeSession.OffersByPlayerId[remotePlayer.NetId] = BuildDefaultDevOffer(remotePlayer);
                _activeSession.ReadyByPlayerId[remotePlayer.NetId] = true;
            }
            TouchActiveSession();

            if (!IsLocalTestMode)
            {
                TradeInviteMessage invite = new()
                {
                    SessionId = _activeSession.SessionId,
                    InitiatorPlayerId = _activeSession.InitiatorPlayerId,
                    RecipientPlayerId = _activeSession.RecipientPlayerId,
                    Location = Buffer.CurrentLocation
                };

                SendMessage(invite);
            }
        }

        EnsureProposalPopup(remotePlayer.NetId);
        NotifyStateChanged();
        return true;
    }

    public void PollTimeouts()
    {
        if (_activeSession == null)
        {
            return;
        }

        if ((DateTime.UtcNow - _activeSession.LastUpdatedUtc).TotalSeconds < _config.TradeSessionTimeoutSeconds)
        {
            return;
        }

        CancelFromLocal(TradeUiText.SessionTimedOut, broadcast: !IsLocalTestMode);
    }

    public string ComputeResourceSignature(ulong remotePlayerId)
    {
        Player? local = GetPlayer(LocalPlayerId);
        Player? remote = GetPlayer(remotePlayerId);
        if (local == null || remote == null)
        {
            return string.Empty;
        }

        return string.Join(
            "|",
            local.Gold,
            string.Join(",", local.PotionSlots.Select((p, i) => p == null ? $"x{i}" : $"{i}:{p.Id.Entry}")),
            string.Join(",", local.Relics.Select((r, i) => $"{i}:{r.Id.Entry}:{TradeFingerprintHelper.ForRelic(r)}")),
            remote.Gold,
            string.Join(",", remote.PotionSlots.Select((p, i) => p == null ? $"x{i}" : $"{i}:{p.Id.Entry}")),
            string.Join(",", remote.Relics.Select((r, i) => $"{i}:{r.Id.Entry}:{TradeFingerprintHelper.ForRelic(r)}")));
    }

    public TradeValidationResult GetValidationForLocalPlayer()
    {
        if (_activeSession == null)
        {
            return new TradeValidationResult { IsValid = false, Message = string.Empty };
        }

        if (!_activeSession.Accepted)
        {
            return new TradeValidationResult { IsValid = false, Message = TradeUiText.PendingInvite };
        }

        return TryBuildCommitPlan(_activeSession, out _, out string reason)
            ? new TradeValidationResult { IsValid = true, Message = TradeUiText.ValidationOk }
            : new TradeValidationResult { IsValid = false, Message = reason };
    }

    public void SetLocalGoldAmount(int value)
    {
        if (_activeSession == null || !_activeSession.Accepted)
        {
            return;
        }

        TradeOfferDraft offer = GetOffer(LocalPlayerId).Clone();
        int clamped = Math.Max(0, Math.Min(value, GetPlayer(LocalPlayerId)?.Gold ?? 0));
        if (offer.GoldAmount == clamped)
        {
            return;
        }

        offer.GoldAmount = clamped;
        ApplyLocalOfferChange(offer);
    }

    public void ToggleLocalPotion(int slotIndex, bool enabled)
    {
        if (_activeSession == null || !_activeSession.Accepted)
        {
            return;
        }

        Player? local = GetPlayer(LocalPlayerId);
        if (local == null || !local.CanRemovePotions)
        {
            return;
        }

        PotionModel? potion = local.GetPotionAtSlotIndex(slotIndex);
        if (potion == null)
        {
            return;
        }

        TradeOfferDraft offer = GetOffer(LocalPlayerId).Clone();
        offer.Potions.RemoveAll(item => item.SlotIndex == slotIndex);
        if (enabled)
        {
            offer.Potions.Add(new TradePotionSelection
            {
                SlotIndex = slotIndex,
                PotionId = potion.Id,
                Fingerprint = TradeFingerprintHelper.ForPotion(potion, slotIndex)
            });
        }

        offer.Potions = offer.Potions.OrderBy(static item => item.SlotIndex).ToList();
        Log.Info($"{ModEntry.ModId}: local potion toggle player={LocalPlayerId} slot={slotIndex} enabled={enabled} selected={offer.Potions.Count}.", 2);
        ApplyLocalOfferChange(offer);
    }

    public void ToggleLocalRelic(int relicIndex, bool enabled)
    {
        if (_activeSession == null || !_activeSession.Accepted)
        {
            return;
        }

        Player? local = GetPlayer(LocalPlayerId);
        if (local == null || relicIndex < 0 || relicIndex >= local.Relics.Count)
        {
            return;
        }

        RelicModel relic = local.Relics[relicIndex];
        if (!relic.IsTradable)
        {
            return;
        }

        TradeOfferDraft offer = GetOffer(LocalPlayerId).Clone();
        offer.Relics.RemoveAll(item => item.RelicIndex == relicIndex);
        if (enabled)
        {
            offer.Relics.Add(new TradeRelicSelection
            {
                RelicIndex = relicIndex,
                RelicId = relic.Id,
                Fingerprint = TradeFingerprintHelper.ForRelic(relic)
            });
        }

        offer.Relics = offer.Relics.OrderBy(static item => item.RelicIndex).ToList();
        Log.Info($"{ModEntry.ModId}: local relic toggle player={LocalPlayerId} relicIndex={relicIndex} enabled={enabled} selected={offer.Relics.Count}.", 2);
        ApplyLocalOfferChange(offer);
    }

    public void ResetLocalOffer()
    {
        if (_activeSession == null || !_activeSession.Accepted)
        {
            return;
        }

        ApplyLocalOfferChange(new TradeOfferDraft());
    }

    public void ToggleLocalReady()
    {
        if (_activeSession == null || !_activeSession.Accepted)
        {
            return;
        }

        bool nextReady = !_activeSession.ReadyByPlayerId.GetValueOrDefault(LocalPlayerId);
        _activeSession.ReadyByPlayerId[LocalPlayerId] = nextReady;
        if (IsLocalTestMode)
        {
            _activeSession.ReadyByPlayerId[_activeSession.GetCounterparty(LocalPlayerId)] = true;
        }
        Log.Info($"{ModEntry.ModId}: local ready toggled player={LocalPlayerId} ready={nextReady} session={_activeSession.SessionId} localOfferPotions={GetOffer(LocalPlayerId).Potions.Count} localOfferRelics={GetOffer(LocalPlayerId).Relics.Count}.", 2);
        TouchActiveSession();

        if (!IsLocalTestMode)
        {
            TradeReadyStateMessage message = new()
            {
                SessionId = _activeSession.SessionId,
                InitiatorPlayerId = _activeSession.InitiatorPlayerId,
                RecipientPlayerId = _activeSession.RecipientPlayerId,
                AuthorPlayerId = LocalPlayerId,
                Revision = _activeSession.Revision,
                IsReady = nextReady,
                Location = Buffer.CurrentLocation
            };
            SendMessage(message);
        }
        NotifyStateChanged();
    }

    public void RequestCommit()
    {
        if (_activeSession == null || !_activeSession.Accepted)
        {
            return;
        }

        _activeSession.StatusText = TradeUiText.ConfirmWaitingHost;
        TouchActiveSession();
        NotifyStateChanged();

        TradeCommitRequestMessage request = new()
        {
            SessionId = _activeSession.SessionId,
            InitiatorPlayerId = _activeSession.InitiatorPlayerId,
            RecipientPlayerId = _activeSession.RecipientPlayerId,
            ExpectedRevision = _activeSession.Revision,
            Location = Buffer.CurrentLocation
        };

        if (IsLocalTestMode || IsHost)
        {
            TaskHelper.RunSafely(ProcessCommitRequestAsync(request, LocalPlayerId));
            return;
        }

        RunManager.Instance.NetService.SendMessage(request);
    }

    public void CancelFromLocal(string reason, bool broadcast)
    {
        if (_activeSession == null)
        {
            return;
        }

        TradeSessionState session = _activeSession;
        CloseSessionLocally(reason);

        if (!broadcast || IsLocalTestMode)
        {
            return;
        }

        TradeCancelMessage message = new()
        {
            SessionId = session.SessionId,
            InitiatorPlayerId = session.InitiatorPlayerId,
            RecipientPlayerId = session.RecipientPlayerId,
            Reason = reason,
            Location = Buffer.CurrentLocation
        };
        SendMessage(message);
    }

    private void ApplyLocalOfferChange(TradeOfferDraft offer)
    {
        if (_activeSession == null)
        {
            return;
        }

        _activeSession.Revision += 1;
        _activeSession.OffersByPlayerId[LocalPlayerId] = offer;
        _activeSession.ReadyByPlayerId[_activeSession.InitiatorPlayerId] = false;
        _activeSession.ReadyByPlayerId[_activeSession.RecipientPlayerId] = IsLocalTestMode;
        _activeSession.StatusText = string.Empty;
        TouchActiveSession();

        if (!IsLocalTestMode)
        {
            TradeOfferSyncMessage message = CreateOfferMessage(_activeSession, LocalPlayerId, offer);
            SendMessage(message);
        }
        NotifyStateChanged();
    }

    private TradeOfferSyncMessage CreateOfferMessage(TradeSessionState session, ulong authorPlayerId, TradeOfferDraft offer)
    {
        return new TradeOfferSyncMessage
        {
            SessionId = session.SessionId,
            InitiatorPlayerId = session.InitiatorPlayerId,
            RecipientPlayerId = session.RecipientPlayerId,
            AuthorPlayerId = authorPlayerId,
            Revision = session.Revision,
            GoldAmount = offer.GoldAmount,
            Potions = offer.Potions.Select(static item => new TradePotionSelectionMessage
            {
                SlotIndex = item.SlotIndex,
                PotionId = item.PotionId,
                Fingerprint = item.Fingerprint
            }).ToList(),
            Relics = offer.Relics.Select(static item => new TradeRelicSelectionMessage
            {
                RelicIndex = item.RelicIndex,
                RelicId = item.RelicId,
                Fingerprint = item.Fingerprint
            }).ToList(),
            Location = Buffer.CurrentLocation
        };
    }

    private void HandleInviteMessage(TradeInviteMessage message, ulong senderId)
    {
        if (senderId == LocalPlayerId)
        {
            return;
        }

        bool localIsParticipant = message.InitiatorPlayerId == LocalPlayerId || message.RecipientPlayerId == LocalPlayerId;
        if (!localIsParticipant && !IsHost)
        {
            return;
        }

        if (_activeSession != null && _activeSession.SessionId != message.SessionId)
        {
            if (IsHost || message.RecipientPlayerId == LocalPlayerId)
            {
                TradeInviteReplyMessage reply = new()
                {
                    SessionId = message.SessionId,
                    InitiatorPlayerId = message.InitiatorPlayerId,
                    RecipientPlayerId = message.RecipientPlayerId,
                    Accepted = false,
                    Reason = TradeUiText.BusyReason,
                    Location = message.Location
                };
                RunManager.Instance.NetService.SendMessage(reply);
            }

            return;
        }

        _activeSession ??= CreateSession(message.InitiatorPlayerId, message.RecipientPlayerId, message.SessionId);
        _activeSession.StatusText = TradeUiText.PendingInvite;
        TouchActiveSession();
        NotifyStateChanged();

        if (message.RecipientPlayerId != LocalPlayerId)
        {
            return;
        }

        if (CombatManager.Instance?.IsInProgress == true
            || NModalContainer.Instance?.OpenModal != null)
        {
            DeclineInvite(message, TradeUiText.BusyReason);
            return;
        }

        TaskHelper.RunSafely(PromptIncomingInviteAsync(message));
    }

    private async Task PromptIncomingInviteAsync(TradeInviteMessage message)
    {
        Player? initiator = GetPlayer(message.InitiatorPlayerId);
        string initiatorName = initiator != null ? GetPlayerName(initiator.NetId) : message.InitiatorPlayerId.ToString();
        NTradeInvitePopup popup = NTradeInvitePopup.Create(
            TradeUiText.InvitePromptTitle(initiatorName),
            TradeUiText.InvitePromptBody);

        if (NModalContainer.Instance != null)
        {
            NModalContainer.Instance.Add(popup, showBackstop: true);
        }
        else
        {
            NGame.Instance?.AddChildSafely(popup);
        }

        bool accepted = await popup.WaitForDecision().ConfigureAwait(false);
        if (accepted)
        {
            AcceptInvite(message);
        }
        else
        {
            DeclineInvite(message, string.Empty);
        }
    }

    private void AcceptInvite(TradeInviteMessage message)
    {
        if (_activeSession == null || _activeSession.SessionId != message.SessionId)
        {
            return;
        }

        _activeSession.Accepted = true;
        _activeSession.StatusText = TradeUiText.AcceptedStatus(GetPlayerName(message.InitiatorPlayerId));
        TouchActiveSession();

        TradeInviteReplyMessage reply = new()
        {
            SessionId = _activeSession.SessionId,
            InitiatorPlayerId = _activeSession.InitiatorPlayerId,
            RecipientPlayerId = _activeSession.RecipientPlayerId,
            Accepted = true,
            Reason = string.Empty,
            Location = Buffer.CurrentLocation
        };

        SendMessage(reply);
        EnsureProposalPopup(message.InitiatorPlayerId);
        NotifyStateChanged();
    }

    private void DeclineInvite(TradeInviteMessage message, string reason)
    {
        TradeInviteReplyMessage reply = new()
        {
            SessionId = message.SessionId,
            InitiatorPlayerId = message.InitiatorPlayerId,
            RecipientPlayerId = message.RecipientPlayerId,
            Accepted = false,
            Reason = reason,
            Location = message.Location
        };

        RunManager.Instance.NetService.SendMessage(reply);
        CloseSessionLocally(reason);
    }

    private void HandleInviteReplyMessage(TradeInviteReplyMessage message, ulong senderId)
    {
        if (_activeSession == null || _activeSession.SessionId != message.SessionId)
        {
            return;
        }

        if (!message.Accepted)
        {
            CloseSessionLocally(string.IsNullOrWhiteSpace(message.Reason) ? TradeUiText.Cancel : message.Reason);
            return;
        }

        _activeSession.Accepted = true;
        if (_activeSession.Involves(LocalPlayerId))
        {
            _activeSession.StatusText = TradeUiText.AcceptedStatus(GetPlayerName(_activeSession.GetCounterparty(LocalPlayerId)));
        }
        TouchActiveSession();
        NotifyStateChanged();

        if (senderId == LocalPlayerId && _activeSession.Involves(LocalPlayerId))
        {
            EnsureProposalPopup(_activeSession.GetCounterparty(LocalPlayerId));
        }
    }

    private void HandleOfferSyncMessage(TradeOfferSyncMessage message, ulong _)
    {
        if (_activeSession == null || _activeSession.SessionId != message.SessionId)
        {
            return;
        }

        TradeOfferDraft offer = new()
        {
            GoldAmount = message.GoldAmount,
            Potions = message.Potions.Select(static item => new TradePotionSelection
            {
                SlotIndex = item.SlotIndex,
                PotionId = item.PotionId,
                Fingerprint = item.Fingerprint
            }).ToList(),
            Relics = message.Relics.Select(static item => new TradeRelicSelection
            {
                RelicIndex = item.RelicIndex,
                RelicId = item.RelicId,
                Fingerprint = item.Fingerprint
            }).ToList()
        };

        _activeSession.OffersByPlayerId[message.AuthorPlayerId] = offer;
        _activeSession.Revision = Math.Max(_activeSession.Revision, message.Revision);
        _activeSession.ReadyByPlayerId[_activeSession.InitiatorPlayerId] = false;
        _activeSession.ReadyByPlayerId[_activeSession.RecipientPlayerId] = false;
        _activeSession.StatusText = string.Empty;
        TouchActiveSession();
        NotifyStateChanged();
    }

    private void HandleReadyStateMessage(TradeReadyStateMessage message, ulong _)
    {
        if (_activeSession == null || _activeSession.SessionId != message.SessionId)
        {
            return;
        }

        _activeSession.ReadyByPlayerId[message.AuthorPlayerId] = message.IsReady;
        TouchActiveSession();
        NotifyStateChanged();
    }

    private void HandleCommitRequestMessage(TradeCommitRequestMessage message, ulong senderId)
    {
        if (!IsHost)
        {
            return;
        }

        TaskHelper.RunSafely(ProcessCommitRequestAsync(message, senderId));
    }

    private async Task ProcessCommitRequestAsync(TradeCommitRequestMessage message, ulong senderId)
    {
        if (_activeSession == null || _activeSession.SessionId != message.SessionId)
        {
            return;
        }

        if (!_activeSession.Involves(senderId))
        {
            return;
        }

        if (message.ExpectedRevision != _activeSession.Revision)
        {
            RejectCommit(_activeSession, "Revision mismatch.");
            return;
        }

        if (!_activeSession.ReadyByPlayerId.GetValueOrDefault(_activeSession.InitiatorPlayerId)
            || !_activeSession.ReadyByPlayerId.GetValueOrDefault(_activeSession.RecipientPlayerId))
        {
            RejectCommit(_activeSession, "Both players must be ready.");
            return;
        }

        if (!TryBuildCommitPlan(_activeSession, out TradeCommitPlan plan, out string reason))
        {
            RejectCommit(_activeSession, reason);
            return;
        }

        TradeCommitAppliedMessage applied = new()
        {
            SessionId = plan.SessionId,
            InitiatorPlayerId = _activeSession.InitiatorPlayerId,
            RecipientPlayerId = _activeSession.RecipientPlayerId,
            Revision = plan.Revision,
            GoldTransfers = plan.GoldTransfers,
            PotionTransfers = plan.PotionTransfers,
            RelicTransfers = plan.RelicTransfers,
            Location = Buffer.CurrentLocation
        };

        await ApplyCommitPlanAsync(applied).ConfigureAwait(false);
        if (!IsLocalTestMode)
        {
            RunManager.Instance.NetService.SendMessage(applied);
        }
        CloseSessionLocally(string.Empty);
    }

    private void RejectCommit(TradeSessionState session, string reason)
    {
        session.StatusText = reason;
        session.ReadyByPlayerId[session.InitiatorPlayerId] = false;
        session.ReadyByPlayerId[session.RecipientPlayerId] = false;
        TouchActiveSession();
        NotifyStateChanged();

        TradeCommitRejectedMessage rejected = new()
        {
            SessionId = session.SessionId,
            InitiatorPlayerId = session.InitiatorPlayerId,
            RecipientPlayerId = session.RecipientPlayerId,
            Reason = reason,
            Location = Buffer.CurrentLocation
        };
        if (!IsLocalTestMode)
        {
            RunManager.Instance.NetService.SendMessage(rejected);
        }
    }

    private async void HandleCommitAppliedMessage(TradeCommitAppliedMessage message, ulong _)
    {
        await ApplyCommitPlanAsync(message).ConfigureAwait(false);
        if (_activeSession?.SessionId == message.SessionId)
        {
            CloseSessionLocally(string.Empty);
        }
    }

    private void HandleCommitRejectedMessage(TradeCommitRejectedMessage message, ulong _)
    {
        if (_activeSession == null || _activeSession.SessionId != message.SessionId)
        {
            return;
        }

        _activeSession.StatusText = message.Reason;
        _activeSession.ReadyByPlayerId[_activeSession.InitiatorPlayerId] = false;
        _activeSession.ReadyByPlayerId[_activeSession.RecipientPlayerId] = false;
        TouchActiveSession();
        NotifyStateChanged();
    }

    private void HandleCancelMessage(TradeCancelMessage message, ulong _)
    {
        if (_activeSession == null || _activeSession.SessionId != message.SessionId)
        {
            return;
        }

        CloseSessionLocally(message.Reason);
    }

    private async Task ApplyCommitPlanAsync(TradeCommitAppliedMessage message)
    {
        foreach (TradeGoldTransfer transfer in message.GoldTransfers)
        {
            Player? source = GetPlayer(transfer.SourcePlayerId);
            Player? target = GetPlayer(transfer.TargetPlayerId);
            if (source == null || target == null || transfer.GoldAmount <= 0)
            {
                continue;
            }

            if (IsLocalTestMode)
            {
                source.Gold -= transfer.GoldAmount;
                target.Gold += transfer.GoldAmount;
            }
            else
            {
                await PlayerCmd.LoseGold(transfer.GoldAmount, source).ConfigureAwait(false);
                await PlayerCmd.GainGold(transfer.GoldAmount, target).ConfigureAwait(false);
            }
        }

        foreach (TradePotionTransfer transfer in message.PotionTransfers.OrderByDescending(static item => item.SourceSlotIndex))
        {
            Player? source = GetPlayer(transfer.SourcePlayerId);
            if (source == null)
            {
                continue;
            }

            PotionModel? livePotion = null;
            try
            {
                livePotion = source.GetPotionAtSlotIndex(transfer.SourceSlotIndex);
            }
            catch
            {
            }

            if (livePotion != null)
            {
                await PotionCmd.Discard(livePotion).ConfigureAwait(false);
            }
        }

        foreach (TradePotionTransfer transfer in message.PotionTransfers.OrderBy(static item => item.TargetSlotIndex))
        {
            Player? target = GetPlayer(transfer.TargetPlayerId);
            if (target == null)
            {
                continue;
            }

            PotionModel clonedPotion = PotionModel.FromSerializable(transfer.Potion);
            var result = await PotionCmd.TryToProcure(clonedPotion, target, transfer.TargetSlotIndex).ConfigureAwait(false);
            if (!result.success)
            {
                Log.Warn($"{ModEntry.ModId}: failed to grant traded potion '{clonedPotion.Id.Entry}' to player {target.NetId}: {result.failureReason}", 2);
            }
        }

        foreach (IGrouping<ulong, TradeRelicTransfer> group in message.RelicTransfers.GroupBy(static item => item.SourcePlayerId))
        {
            Player? source = GetPlayer(group.Key);
            if (source == null)
            {
                continue;
            }

            foreach (TradeRelicTransfer transfer in group.OrderByDescending(static item => item.SourceRelicIndex))
            {
                if (transfer.SourceRelicIndex < 0 || transfer.SourceRelicIndex >= source.Relics.Count)
                {
                    continue;
                }

                RelicModel liveRelic = source.Relics[transfer.SourceRelicIndex];
                await RelicCmd.Remove(liveRelic).ConfigureAwait(false);
            }
        }

        foreach (IGrouping<ulong, TradeRelicTransfer> group in message.RelicTransfers.GroupBy(static item => item.TargetPlayerId))
        {
            Player? target = GetPlayer(group.Key);
            if (target == null)
            {
                continue;
            }

            foreach (TradeRelicTransfer transfer in group.OrderBy(static item => item.TargetRelicIndex))
            {
                RelicModel clonedRelic = RelicModel.FromSerializable(transfer.Relic);
                int targetIndex = transfer.TargetRelicIndex;
                if (targetIndex < 0 || targetIndex > target.Relics.Count)
                {
                    targetIndex = -1;
                }

                await RelicCmd.Obtain(clonedRelic, target, targetIndex).ConfigureAwait(false);
            }
        }
    }

    private bool TryBuildCommitPlan(TradeSessionState session, out TradeCommitPlan plan, out string reason)
    {
        reason = string.Empty;
        plan = null!;

        Player? initiator = GetPlayer(session.InitiatorPlayerId);
        Player? recipient = GetPlayer(session.RecipientPlayerId);
        if (initiator == null || recipient == null)
        {
            reason = "A player is no longer available.";
            return false;
        }

        Dictionary<ulong, TradeOfferDraft> offers = new()
        {
            [session.InitiatorPlayerId] = GetOffer(session.InitiatorPlayerId).Clone(),
            [session.RecipientPlayerId] = GetOffer(session.RecipientPlayerId).Clone()
        };

        Dictionary<ulong, List<int>> projectedPotionSlots = new()
        {
            [initiator.NetId] = GetAvailablePotionSlotsAfterOutgoing(initiator, offers[initiator.NetId]),
            [recipient.NetId] = GetAvailablePotionSlotsAfterOutgoing(recipient, offers[recipient.NetId])
        };

        Dictionary<ulong, int> projectedRelicStartIndices = new()
        {
            [initiator.NetId] = initiator.Relics.Count - offers[initiator.NetId].Relics.Select(static item => item.RelicIndex).Distinct().Count(),
            [recipient.NetId] = recipient.Relics.Count - offers[recipient.NetId].Relics.Select(static item => item.RelicIndex).Distinct().Count()
        };

        Dictionary<ulong, int> projectedRelicOffsets = new()
        {
            [initiator.NetId] = 0,
            [recipient.NetId] = 0
        };

        List<TradeGoldTransfer> goldTransfers = new();
        List<TradePotionTransfer> potionTransfers = new();
        List<TradeRelicTransfer> relicTransfers = new();

        foreach ((ulong giverId, ulong receiverId) in new[]
                 {
                     (initiator.NetId, recipient.NetId),
                     (recipient.NetId, initiator.NetId)
                 })
        {
            Player giver = giverId == initiator.NetId ? initiator : recipient;
            TradeOfferDraft offer = offers[giverId];

            if (offer.GoldAmount > 0)
            {
                if (!_config.TradeAllowGold || giver.Gold < offer.GoldAmount)
                {
                    reason = "Gold offer is no longer valid.";
                    return false;
                }

                goldTransfers.Add(new TradeGoldTransfer
                {
                    SourcePlayerId = giverId,
                    TargetPlayerId = receiverId,
                    GoldAmount = offer.GoldAmount
                });
            }

            foreach (int slotIndex in offer.Potions.Select(static item => item.SlotIndex).Distinct().OrderBy(static item => item))
            {
                PotionModel? livePotion = giver.GetPotionAtSlotIndex(slotIndex);
                TradePotionSelection? selection = offer.Potions.FirstOrDefault(item => item.SlotIndex == slotIndex);
                if (!_config.TradeAllowPotions
                    || livePotion == null
                    || selection == null
                    || livePotion.Id != selection.PotionId
                    || TradeFingerprintHelper.ForPotion(livePotion, slotIndex) != selection.Fingerprint
                    || !giver.CanRemovePotions)
                {
                    Log.Warn($"{ModEntry.ModId}: potion validation failed giver={giverId} receiver={receiverId} slot={slotIndex} live={(livePotion != null)} selection={(selection != null)} canRemove={giver.CanRemovePotions}.", 2);
                    reason = "Potion offer is no longer valid.";
                    return false;
                }

                if (projectedPotionSlots[receiverId].Count == 0)
                {
                    Log.Warn($"{ModEntry.ModId}: potion validation failed because receiver={receiverId} has no open slot.", 2);
                    reason = "The recipient no longer has an open potion slot.";
                    return false;
                }

                int targetSlotIndex = projectedPotionSlots[receiverId][0];
                projectedPotionSlots[receiverId].RemoveAt(0);
                potionTransfers.Add(new TradePotionTransfer
                {
                    SourcePlayerId = giverId,
                    TargetPlayerId = receiverId,
                    SourceSlotIndex = slotIndex,
                    TargetSlotIndex = targetSlotIndex,
                    Potion = livePotion.ToSerializable(slotIndex)
                });
            }

            foreach (int relicIndex in offer.Relics.Select(static item => item.RelicIndex).Distinct().OrderBy(static item => item))
            {
                if (!_config.TradeAllowRelics || relicIndex < 0 || relicIndex >= giver.Relics.Count)
                {
                    reason = "Relic offer is no longer valid.";
                    return false;
                }

                RelicModel liveRelic = giver.Relics[relicIndex];
                TradeRelicSelection? selection = offer.Relics.FirstOrDefault(item => item.RelicIndex == relicIndex);
                if (selection == null
                    || liveRelic.Id != selection.RelicId
                    || TradeFingerprintHelper.ForRelic(liveRelic) != selection.Fingerprint
                    || !liveRelic.IsTradable)
                {
                    Log.Warn($"{ModEntry.ModId}: relic validation failed giver={giverId} receiver={receiverId} relicIndex={relicIndex} selection={(selection != null)} tradable={liveRelic.IsTradable}.", 2);
                    reason = "Relic offer is no longer valid.";
                    return false;
                }

                int targetRelicIndex = projectedRelicStartIndices[receiverId] + projectedRelicOffsets[receiverId];
                projectedRelicOffsets[receiverId] += 1;
                relicTransfers.Add(new TradeRelicTransfer
                {
                    SourcePlayerId = giverId,
                    TargetPlayerId = receiverId,
                    SourceRelicIndex = relicIndex,
                    TargetRelicIndex = targetRelicIndex,
                    Relic = liveRelic.ToSerializable()
                });
            }
        }

        plan = new TradeCommitPlan
        {
            SessionId = session.SessionId,
            Revision = session.Revision,
            GoldTransfers = goldTransfers,
            PotionTransfers = potionTransfers,
            RelicTransfers = relicTransfers
        };
        return true;
    }

    private static List<int> GetAvailablePotionSlotsAfterOutgoing(Player player, TradeOfferDraft outgoingOffer)
    {
        HashSet<int> freedSlots = outgoingOffer.Potions.Select(static item => item.SlotIndex).ToHashSet();
        return player.PotionSlots
            .Select((item, index) => new { item, index })
            .Where(x => x.item == null || freedSlots.Contains(x.index))
            .Select(static x => x.index)
            .OrderBy(static index => index)
            .ToList();
    }

    private TradeOfferDraft GetOffer(ulong playerId)
    {
        if (_activeSession == null)
        {
            return new TradeOfferDraft();
        }

        if (!_activeSession.OffersByPlayerId.TryGetValue(playerId, out TradeOfferDraft? offer))
        {
            offer = new TradeOfferDraft();
            _activeSession.OffersByPlayerId[playerId] = offer;
        }

        return offer;
    }

    private TradeSessionState CreateSession(ulong initiatorPlayerId, ulong recipientPlayerId, string? sessionId = null)
    {
        TradeSessionState session = new()
        {
            SessionId = sessionId ?? Guid.NewGuid().ToString("N"),
            InitiatorPlayerId = initiatorPlayerId,
            RecipientPlayerId = recipientPlayerId,
            Revision = 0,
            Accepted = false,
            StatusText = string.Empty,
            LastUpdatedUtc = DateTime.UtcNow
        };
        session.OffersByPlayerId[initiatorPlayerId] = new TradeOfferDraft();
        session.OffersByPlayerId[recipientPlayerId] = new TradeOfferDraft();
        session.ReadyByPlayerId[initiatorPlayerId] = false;
        session.ReadyByPlayerId[recipientPlayerId] = false;
        return session;
    }

    private void TouchActiveSession()
    {
        if (_activeSession != null)
        {
            _activeSession.LastUpdatedUtc = DateTime.UtcNow;
        }
    }

    private void NotifyStateChanged()
    {
        Delegate[] handlers = StateChanged?.GetInvocationList() ?? Array.Empty<Delegate>();
        foreach (Action handler in handlers.OfType<Action>())
        {
            try
            {
                handler();
            }
            catch (ObjectDisposedException)
            {
                StateChanged -= handler;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase))
            {
                StateChanged -= handler;
            }
        }
    }

    private void SendMessage<T>(T message) where T : INetMessage
    {
        if (IsLocalTestMode)
        {
            return;
        }

        RunManager.Instance.NetService.SendMessage(message);
    }

    private void CloseSessionLocally(string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Log.Info($"{ModEntry.ModId}: trade session closed: {reason}", 2);
        }

        _activeSession = null;
        ClearTradeUi();
        NotifyStateChanged();
    }

    private void EnsureProposalPopup(ulong remotePlayerId)
    {
        if (NGame.Instance?.GetChildren().OfType<NTradeProposalPopup>().FirstOrDefault() is NTradeProposalPopup existing
            && existing.RemotePlayerId == remotePlayerId)
        {
            Log.Info($"{ModEntry.ModId}: trade popup already open for player {remotePlayerId}.", 2);
            return;
        }

        if (NGame.Instance == null)
        {
            Log.Warn($"{ModEntry.ModId}: NGame.Instance is null; cannot open trade popup.", 2);
            return;
        }

        ClearTradeUi();
        NTradeProposalPopup popup = NTradeProposalPopup.Create(this, remotePlayerId);
        Log.Info($"{ModEntry.ModId}: opening trade proposal popup for player {remotePlayerId}.", 2);
        NGame.Instance.AddChildSafely(popup);
    }

    private static void ClearTradeUi()
    {
        if (NModalContainer.Instance?.OpenModal is NTradeInvitePopup)
        {
            NModalContainer.Instance.Clear();
        }

        foreach (Node node in NGame.Instance?.GetChildren() ?? Enumerable.Empty<Node>())
        {
            if (node is NTradeProposalPopup or NTradeInvitePopup)
            {
                if (GodotObject.IsInstanceValid(node))
                {
                    if (node is NTradeProposalPopup popup)
                    {
                        popup.DetachSubscription();
                    }

                    node.QueueFree();
                }
            }
        }
    }

    private void OnRoomExited()
    {
        if (_activeSession == null)
        {
            return;
        }

        if (IsHost || _activeSession.Involves(LocalPlayerId))
        {
            CancelFromLocal("Room changed.", broadcast: true);
            return;
        }

        CloseSessionLocally("Room changed.");
    }

    private void OnRemotePlayerDisconnected(ulong playerId)
    {
        if (_activeSession == null || !_activeSession.Involves(playerId))
        {
            return;
        }

        if (IsHost)
        {
            CancelFromLocal("Player disconnected.", broadcast: true);
            return;
        }

        CloseSessionLocally("Player disconnected.");
    }

    private void OnLocalPlayerDisconnected()
    {
        CloseSessionLocally("Disconnected.");
    }

    private Player? GetPlayer(ulong playerId)
    {
        if (_devRemotePlayer != null && _devRemotePlayer.NetId == playerId)
        {
            return _devRemotePlayer;
        }

        return _runState.Players.FirstOrDefault(player => player.NetId == playerId);
    }

    internal string GetPlayerName(ulong playerId)
    {
        if (_devRemotePlayer != null && _devRemotePlayer.NetId == playerId)
        {
            return TradeUiText.TestTraderName;
        }

        try
        {
            return PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, playerId);
        }
        catch
        {
            return $"Player {playerId}";
        }
    }

    private static TradeOfferDraft BuildDefaultDevOffer(Player remotePlayer)
    {
        return new TradeOfferDraft();
    }

    internal Player? FindPlayer(ulong playerId)
    {
        return GetPlayer(playerId);
    }
}
