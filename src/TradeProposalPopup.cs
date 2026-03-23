using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace Sts2MultiplayerTrade;

internal sealed class NTradeProposalPopup : Control, IScreenContext
{
    private const string GoldIconPath = "res://images/packed/sprite_fonts/gold_icon.png";

    private readonly List<Control> _focusChain = new();

    private TradeSessionManager? _manager;

    private ulong _remotePlayerId;

    private bool _uiBuilt;

    private bool _subscribed;

    private bool _refreshing;

    private Label? _headerLabel;

    private Label? _statusLabel;

    private VBoxContainer? _localOfferRoot;

    private VBoxContainer? _remoteOfferRoot;

    private Button? _cancelButton;

    private Button? _resetButton;

    private Button? _readyButton;

    private Button? _confirmButton;

    public ulong RemotePlayerId => _remotePlayerId;

    public Control? DefaultFocusedControl => _focusChain.FirstOrDefault() ?? _readyButton ?? _confirmButton ?? _cancelButton;

    public static NTradeProposalPopup Create(TradeSessionManager manager, ulong remotePlayerId)
    {
        NTradeProposalPopup popup = new()
        {
            Name = "TradeProposalPopup"
        };
        popup._manager = manager;
        popup._remotePlayerId = remotePlayerId;
        popup.BuildUi();
        popup.EnsureSubscription();
        popup.Refresh();
        Log.Info($"{ModEntry.ModId}: trade proposal popup created for remote player {remotePlayerId}.", 2);
        return popup;
    }

    public override void _Ready()
    {
        EnsureSubscription();
        Refresh();
        Log.Info($"{ModEntry.ModId}: trade proposal popup _Ready for remote player {_remotePlayerId}.", 2);
    }

    public override void _ExitTree()
    {
        DetachSubscription();
    }

    private void BuildUi()
    {
        if (_uiBuilt)
        {
            return;
        }

        _uiBuilt = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        ZIndex = 500;

        ColorRect backdrop = new()
        {
            Color = new Color(0f, 0f, 0f, 0.74f),
            MouseFilter = MouseFilterEnum.Stop
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        CenterContainer center = new();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        PanelContainer shell = new();
        shell.CustomMinimumSize = new Vector2(1220f, 760f);
        shell.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        shell.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        shell.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.10f, 0.12f, 0.16f, 0.98f), new Color(0.56f, 0.64f, 0.76f, 0.95f), 14));
        center.AddChild(shell);

        MarginContainer margin = CreateMargin(20, 18);
        shell.AddChild(margin);

        VBoxContainer layout = new();
        layout.CustomMinimumSize = new Vector2(1160f, 720f);
        layout.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        layout.SizeFlagsVertical = SizeFlags.ExpandFill;
        layout.AddThemeConstantOverride("separation", 16);
        margin.AddChild(layout);

        _headerLabel = CreateTitleLabel(string.Empty, 26);
        layout.AddChild(_headerLabel);

        HBoxContainer columns = new();
        columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        columns.AddThemeConstantOverride("separation", 14);
        layout.AddChild(columns);

        _localOfferRoot = CreateOfferColumn(columns, TradeUiText.YourOffer, 420f);
        CreateStatusColumn(columns);
        _remoteOfferRoot = CreateOfferColumn(columns, TradeUiText.TheirOffer, 420f);

        HBoxContainer actions = new();
        actions.Alignment = BoxContainer.AlignmentMode.End;
        actions.AddThemeConstantOverride("separation", 10);
        layout.AddChild(actions);

        _cancelButton = CreateActionButton(TradeUiText.Cancel, destructive: true);
        _cancelButton.Pressed += () => _manager?.CancelFromLocal("Canceled by player.", broadcast: true);
        actions.AddChild(_cancelButton);

        _resetButton = CreateActionButton(TradeUiText.Reset, destructive: false);
        _resetButton.Pressed += () => _manager?.ResetLocalOffer();
        actions.AddChild(_resetButton);

        _readyButton = CreateActionButton(TradeUiText.Ready, destructive: false);
        _readyButton.Pressed += () => _manager?.ToggleLocalReady();
        actions.AddChild(_readyButton);

        _confirmButton = CreateActionButton(TradeUiText.Confirm, destructive: false, emphasized: true);
        _confirmButton.Pressed += () =>
        {
            if (_confirmButton.Disabled)
            {
                return;
            }

            _manager?.RequestCommit();
        };
        actions.AddChild(_confirmButton);
    }

    private void EnsureSubscription()
    {
        if (_manager == null || _subscribed)
        {
            return;
        }

        _manager.StateChanged += Refresh;
        _subscribed = true;
    }

    internal void DetachSubscription()
    {
        if (_manager == null || !_subscribed)
        {
            return;
        }

        _manager.StateChanged -= Refresh;
        _subscribed = false;
    }

    private VBoxContainer CreateOfferColumn(HBoxContainer parent, string title, float minWidth)
    {
        PanelContainer shell = new();
        shell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        shell.SizeFlagsVertical = SizeFlags.ExpandFill;
        shell.CustomMinimumSize = new Vector2(minWidth, 0f);
        shell.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.13f, 0.15f, 0.20f, 0.92f), new Color(0.36f, 0.42f, 0.52f, 0.72f), 12));
        parent.AddChild(shell);

        MarginContainer margin = CreateMargin(12, 12);
        shell.AddChild(margin);

        VBoxContainer stack = new();
        stack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stack.SizeFlagsVertical = SizeFlags.ExpandFill;
        stack.AddThemeConstantOverride("separation", 10);
        margin.AddChild(stack);

        stack.AddChild(CreateTitleLabel(title, 18));

        VBoxContainer content = new();
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 10);
        stack.AddChild(content);
        return content;
    }

    private void CreateStatusColumn(HBoxContainer parent)
    {
        PanelContainer shell = new();
        shell.CustomMinimumSize = new Vector2(260f, 0f);
        shell.SizeFlagsHorizontal = SizeFlags.Fill;
        shell.SizeFlagsVertical = SizeFlags.ExpandFill;
        shell.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.13f, 0.15f, 0.20f, 0.92f), new Color(0.36f, 0.42f, 0.52f, 0.72f), 12));
        parent.AddChild(shell);

        MarginContainer margin = CreateMargin(12, 12);
        shell.AddChild(margin);

        VBoxContainer stack = new();
        stack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stack.SizeFlagsVertical = SizeFlags.ExpandFill;
        stack.AddThemeConstantOverride("separation", 10);
        margin.AddChild(stack);

        stack.AddChild(CreateTitleLabel(TradeUiText.Status, 18));

        _statusLabel = CreateBodyLabel(string.Empty, 15);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _statusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _statusLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        stack.AddChild(_statusLabel);
    }

    private void Refresh()
    {
        if (!GodotObject.IsInstanceValid(this) || IsQueuedForDeletion())
        {
            DetachSubscription();
            return;
        }

        if (_manager == null || !_uiBuilt || _localOfferRoot == null || _remoteOfferRoot == null || _statusLabel == null || _headerLabel == null)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_localOfferRoot)
            || !GodotObject.IsInstanceValid(_remoteOfferRoot)
            || !GodotObject.IsInstanceValid(_statusLabel)
            || !GodotObject.IsInstanceValid(_headerLabel)
            || !GodotObject.IsInstanceValid(_cancelButton)
            || !GodotObject.IsInstanceValid(_resetButton)
            || !GodotObject.IsInstanceValid(_readyButton)
            || !GodotObject.IsInstanceValid(_confirmButton))
        {
            DetachSubscription();
            return;
        }

        if (_manager.ActiveSession == null)
        {
            Log.Info($"{ModEntry.ModId}: trade popup closing because active session is null.", 2);
            this.QueueFreeSafely();
            return;
        }

        Player? localPlayer = GetLocalPlayer();
        Player? remotePlayer = _manager.FindPlayer(_remotePlayerId);
        if (localPlayer == null || remotePlayer == null)
        {
            Log.Warn($"{ModEntry.ModId}: trade popup refresh missing players. local={(localPlayer != null)} remote={(remotePlayer != null)}", 2);
            this.QueueFreeSafely();
            return;
        }

        _refreshing = true;
        try
        {
            _focusChain.Clear();
            _headerLabel.Text = TradeUiText.TradeTitle(_manager.GetPlayerName(remotePlayer.NetId));
            _statusLabel.Text = BuildStatusText(localPlayer, remotePlayer);

            RebuildLocalOffer(localPlayer);
            RebuildRemoteOffer(remotePlayer);

            TradeSessionState session = _manager.ActiveSession;
            bool accepted = session.Accepted;
            bool localReady = session.ReadyByPlayerId.GetValueOrDefault(localPlayer.NetId);
            bool bothReady = session.ReadyByPlayerId.GetValueOrDefault(session.InitiatorPlayerId)
                && session.ReadyByPlayerId.GetValueOrDefault(session.RecipientPlayerId);
            bool canCommit = accepted && bothReady && _manager.GetValidationForLocalPlayer().IsValid;

            _readyButton!.Text = localReady ? TradeUiText.Unready : TradeUiText.Ready;
            _readyButton.Disabled = !accepted;
            _resetButton!.Disabled = !accepted;
            _confirmButton!.Disabled = !canCommit;
            _cancelButton!.Disabled = false;

            _focusChain.Add(_cancelButton);
            _focusChain.Add(_resetButton);
            _focusChain.Add(_readyButton);
            _focusChain.Add(_confirmButton);
            WireFocusChain();

            int tradableRelics = localPlayer.Relics.Count(static relic => relic.IsTradable);
            Log.Info($"{ModEntry.ModId}: trade popup content rebuilt. localPlayer={localPlayer.NetId} potions={localPlayer.Potions.Count()} tradableRelics={tradableRelics} leftChildren={_localOfferRoot.GetChildCount()} rightChildren={_remoteOfferRoot.GetChildCount()} focusables={_focusChain.Count}", 2);
        }
        catch (ObjectDisposedException)
        {
            DetachSubscription();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RebuildLocalOffer(Player localPlayer)
    {
        ClearChildren(_localOfferRoot!);

        TradeOfferDraft offer = _manager!.ActiveSession!.OffersByPlayerId.GetValueOrDefault(localPlayer.NetId) ?? new TradeOfferDraft();
        bool editable = _manager.ActiveSession.Accepted;

        _localOfferRoot!.AddChild(CreateGoldEditor(localPlayer, offer, editable));
        _localOfferRoot.AddChild(CreatePotionEditor(localPlayer, offer, editable));
        _localOfferRoot.AddChild(CreateRelicEditor(localPlayer, offer, editable));
    }

    private void RebuildRemoteOffer(Player remotePlayer)
    {
        ClearChildren(_remoteOfferRoot!);

        TradeOfferDraft offer = _manager!.ActiveSession!.OffersByPlayerId.GetValueOrDefault(remotePlayer.NetId) ?? new TradeOfferDraft();
        _remoteOfferRoot!.AddChild(CreateOfferPreview(remotePlayer, offer));
    }

    private Control CreateGoldEditor(Player localPlayer, TradeOfferDraft offer, bool editable)
    {
        VBoxContainer section = CreateSection(TradeUiText.Gold);
        HBoxContainer row = CreateAssetRow();
        section.AddChild(row);

        row.AddChild(CreateGoldIcon());

        SpinBox spinBox = new()
        {
            MinValue = 0,
            MaxValue = Math.Max(0, localPlayer.Gold),
            Step = 1,
            Value = offer.GoldAmount,
            Editable = editable,
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(140f, 36f)
        };
        ApplyControlFont(spinBox, 16);
        spinBox.ValueChanged += value =>
        {
            if (!_refreshing)
            {
                _manager?.SetLocalGoldAmount((int)value);
            }
        };
        row.AddChild(spinBox);
        _focusChain.Add(spinBox);

        row.AddChild(CreateBodyLabel($"/ {localPlayer.Gold}", 15));
        return section;
    }

    private Control CreatePotionEditor(Player localPlayer, TradeOfferDraft offer, bool editable)
    {
        VBoxContainer section = CreateSection(TradeUiText.Potions);
        bool any = false;

        for (int slotIndex = 0; slotIndex < localPlayer.PotionSlots.Count; slotIndex += 1)
        {
            PotionModel? potion = TryGetPotionAtSlot(localPlayer, slotIndex);
            if (potion == null)
            {
                continue;
            }

            any = true;
            NPotionHolder? holder = TryCreatePotionHolder(potion);
            bool selected = offer.Potions.Any(item => item.SlotIndex == slotIndex);
            Control row = CreateSelectionRow(
                $"{slotIndex + 1}. {GetPotionName(potion, null)}",
                selected,
                !editable || !localPlayer.CanRemovePotions,
                holder,
                () =>
                {
                    if (!_refreshing)
                    {
                        Log.Info($"{ModEntry.ModId}: potion row clicked slot={slotIndex} selected={selected}.", 2);
                        _manager?.ToggleLocalPotion(slotIndex, !selected);
                    }
                });
            section.AddChild(row);
            _focusChain.Add(row);
        }

        if (!any)
        {
            section.AddChild(CreateEmptyLabel(TradeUiText.NoPotions));
        }

        return section;
    }

    private Control CreateRelicEditor(Player localPlayer, TradeOfferDraft offer, bool editable)
    {
        VBoxContainer section = CreateSection(TradeUiText.Relics);
        bool any = false;

        for (int relicIndex = 0; relicIndex < localPlayer.Relics.Count; relicIndex += 1)
        {
            RelicModel? relic = TryGetRelicAtIndex(localPlayer, relicIndex);
            if (relic == null || !relic.IsTradable)
            {
                continue;
            }

            any = true;
            NRelicBasicHolder? holder = NRelicBasicHolder.Create(relic);
            if (holder != null)
            {
                holder.FocusMode = FocusModeEnum.None;
                holder.MouseFilter = MouseFilterEnum.Ignore;
            }
            bool selected = offer.Relics.Any(item => item.RelicIndex == relicIndex);
            Control row = CreateSelectionRow(
                $"{relicIndex + 1}. {GetRelicName(relic, null)}",
                selected,
                !editable,
                holder,
                () =>
                {
                    if (!_refreshing)
                    {
                        Log.Info($"{ModEntry.ModId}: relic row clicked relicIndex={relicIndex} selected={selected}.", 2);
                        _manager?.ToggleLocalRelic(relicIndex, !selected);
                    }
                });
            section.AddChild(row);
            _focusChain.Add(row);
        }

        if (!any)
        {
            section.AddChild(CreateEmptyLabel(TradeUiText.NoRelics));
        }

        return section;
    }

    private Control CreateOfferPreview(Player player, TradeOfferDraft offer)
    {
        VBoxContainer stack = new();
        stack.AddThemeConstantOverride("separation", 10);

        stack.AddChild(CreatePreviewGoldSection(offer.GoldAmount));
        stack.AddChild(CreatePreviewPotionSection(player, offer));
        stack.AddChild(CreatePreviewRelicSection(player, offer));

        if (offer.GoldAmount == 0 && offer.Potions.Count == 0 && offer.Relics.Count == 0)
        {
            stack.AddChild(CreateEmptyLabel(TradeUiText.OfferEmpty));
        }

        return stack;
    }

    private Control CreatePreviewGoldSection(int goldAmount)
    {
        VBoxContainer section = CreateSection(TradeUiText.Gold);
        HBoxContainer row = CreateAssetRow();
        row.AddChild(CreateGoldIcon());
        row.AddChild(CreateBodyLabel(goldAmount.ToString(), 15));
        section.AddChild(row);
        return section;
    }

    private Control CreatePreviewPotionSection(Player player, TradeOfferDraft offer)
    {
        VBoxContainer section = CreateSection(TradeUiText.Potions);
        bool any = false;

        foreach (TradePotionSelection selection in offer.Potions.OrderBy(static item => item.SlotIndex))
        {
            any = true;
            PotionModel? potion = TryGetPotionAtSlot(player, selection.SlotIndex);
            HBoxContainer row = CreateAssetRow();
            Control iconHost = CreatePreviewIconHost();
            row.AddChild(iconHost);

            string potionName = GetPotionNameSafe(potion, selection);
            row.AddChild(CreateBodyLabel($"{selection.SlotIndex + 1}. {potionName}", 15));
            section.AddChild(row);
            QueuePotionPreviewHolder(iconHost, potion);
        }

        if (!any)
        {
            section.AddChild(CreateEmptyLabel(TradeUiText.OfferEmpty));
        }

        return section;
    }

    private Control CreatePreviewRelicSection(Player player, TradeOfferDraft offer)
    {
        VBoxContainer section = CreateSection(TradeUiText.Relics);
        bool any = false;

        foreach (TradeRelicSelection selection in offer.Relics.OrderBy(static item => item.RelicIndex))
        {
            any = true;
            RelicModel? relic = TryGetRelicAtIndex(player, selection.RelicIndex);
            HBoxContainer row = CreateAssetRow();
            Control iconHost = CreatePreviewIconHost();
            row.AddChild(iconHost);

            row.AddChild(CreateBodyLabel($"{selection.RelicIndex + 1}. {GetRelicName(relic, selection)}", 15));
            section.AddChild(row);
            QueueRelicPreviewHolder(iconHost, relic);
        }

        if (!any)
        {
            section.AddChild(CreateEmptyLabel(TradeUiText.OfferEmpty));
        }

        return section;
    }

    private string BuildStatusText(Player localPlayer, Player remotePlayer)
    {
        if (_manager?.ActiveSession == null)
        {
            return string.Empty;
        }

        TradeSessionState session = _manager.ActiveSession;
        TradeValidationResult validation = _manager.GetValidationForLocalPlayer();
        List<string> lines = new();

        if (!string.IsNullOrWhiteSpace(session.StatusText))
        {
            lines.Add(session.StatusText);
        }

        lines.Add($"{_manager.GetPlayerName(localPlayer.NetId)}: {TradeUiText.ReadyState(session.ReadyByPlayerId.GetValueOrDefault(localPlayer.NetId))}");
        lines.Add($"{_manager.GetPlayerName(remotePlayer.NetId)}: {TradeUiText.ReadyState(session.ReadyByPlayerId.GetValueOrDefault(remotePlayer.NetId))}");

        if (!string.IsNullOrWhiteSpace(validation.Message))
        {
            lines.Add(validation.Message);
        }

        return string.Join("\n", lines);
    }

    private Player? GetLocalPlayer()
    {
        if (_manager?.ActiveSession == null)
        {
            return null;
        }

        ulong localPlayerId = _manager.ActiveSession.InitiatorPlayerId == _remotePlayerId
            ? _manager.ActiveSession.RecipientPlayerId
            : _manager.ActiveSession.InitiatorPlayerId;
        return _manager.FindPlayer(localPlayerId);
    }

    private void WireFocusChain()
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        List<Control> alive = _focusChain
            .Where(static control => control != null)
            .Where(GodotObject.IsInstanceValid)
            .Where(static control => control.IsInsideTree())
            .ToList();

        for (int index = 0; index < alive.Count; index += 1)
        {
            Control current = alive[index];
            NodePath self = current.GetPath();
            current.FocusNeighborTop = index == 0 ? self : alive[index - 1].GetPath();
            current.FocusNeighborBottom = index == alive.Count - 1 ? self : alive[index + 1].GetPath();
        }
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static MarginContainer CreateMargin(int horizontal, int vertical)
    {
        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", horizontal);
        margin.AddThemeConstantOverride("margin_right", horizontal);
        margin.AddThemeConstantOverride("margin_top", vertical);
        margin.AddThemeConstantOverride("margin_bottom", vertical);
        return margin;
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color border, int radius)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(radius);
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        style.ContentMarginTop = 8;
        style.ContentMarginBottom = 8;
        return style;
    }

    private static Button CreateActionButton(string text, bool destructive, bool emphasized = false)
    {
        Color normal = destructive
            ? new Color(0.26f, 0.14f, 0.16f, 0.98f)
            : emphasized
                ? new Color(0.21f, 0.25f, 0.33f, 0.98f)
                : new Color(0.18f, 0.21f, 0.27f, 0.98f);
        Color border = destructive
            ? new Color(0.78f, 0.40f, 0.44f, 0.90f)
            : emphasized
                ? new Color(0.82f, 0.90f, 0.98f, 0.95f)
                : new Color(0.48f, 0.56f, 0.68f, 0.85f);

        Button button = new()
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(150f, 42f)
        };
        ApplyControlFont(button, 16);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(normal, border, 10));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(normal.Lightened(0.15f), border.Lightened(0.10f), 10));
        button.AddThemeStyleboxOverride("pressed", CreatePanelStyle(normal.Darkened(0.12f), border, 10));
        button.AddThemeColorOverride("font_color", new Color(0.97f, 0.95f, 0.91f, 1f));
        return button;
    }

    private static Button CreateSelectionButton(string text, bool selected, bool disabled)
    {
        Color normal = selected
            ? new Color(0.20f, 0.29f, 0.20f, 0.98f)
            : new Color(0.18f, 0.21f, 0.27f, 0.98f);
        Color border = selected
            ? new Color(0.66f, 0.88f, 0.66f, 0.95f)
            : new Color(0.48f, 0.56f, 0.68f, 0.85f);
        Button button = new()
        {
            Text = (selected ? "[x] " : "[ ] ") + text,
            Disabled = disabled,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0f, 42f)
        };
        ApplyControlFont(button, 16);
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(normal, border, 8));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(normal.Lightened(0.10f), border, 8));
        button.AddThemeStyleboxOverride("pressed", CreatePanelStyle(normal.Darkened(0.08f), border, 8));
        button.AddThemeColorOverride("font_color", new Color(0.97f, 0.95f, 0.91f, 1f));
        return button;
    }

    private static Control CreateSelectionRow(string text, bool selected, bool disabled, Control? icon, Action onClick)
    {
        PanelContainer row = new()
        {
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 42f)
        };
        Color normal = selected
            ? new Color(0.20f, 0.29f, 0.20f, 0.98f)
            : new Color(0.18f, 0.21f, 0.27f, 0.98f);
        Color border = selected
            ? new Color(0.66f, 0.88f, 0.66f, 0.95f)
            : new Color(0.48f, 0.56f, 0.68f, 0.85f);
        row.AddThemeStyleboxOverride("panel", CreatePanelStyle(normal, border, 8));

        MarginContainer margin = CreateMargin(10, 6);
        row.AddChild(margin);

        HBoxContainer content = new();
        content.AddThemeConstantOverride("separation", 10);
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.AddChild(content);

        if (icon != null)
        {
            icon.MouseFilter = MouseFilterEnum.Ignore;
            content.AddChild(icon);
        }

        Label label = CreateBodyLabel((selected ? "[x] " : "[ ] ") + text, 16);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.MouseFilter = MouseFilterEnum.Ignore;
        if (disabled)
        {
            label.Modulate = new Color(1f, 1f, 1f, 0.55f);
        }

        content.AddChild(label);
        row.GuiInput += inputEvent =>
        {
            if (disabled)
            {
                return;
            }

            if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                row.AcceptEvent();
                onClick();
            }
        };
        return row;
    }

    private static VBoxContainer CreateSection(string titleText)
    {
        VBoxContainer section = new();
        section.AddThemeConstantOverride("separation", 8);
        section.AddChild(CreateTitleLabel(titleText, 16));
        return section;
    }

    private static HBoxContainer CreateAssetRow()
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 10);
        return row;
    }

    private static Label CreateTitleLabel(string text, int fontSize)
    {
        Label label = new()
        {
            Text = text
        };
        ApplyControlFont(label, fontSize);
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.94f, 0.90f, 1f));
        return label;
    }

    private static Label CreateBodyLabel(string text, int fontSize)
    {
        Label label = new()
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        ApplyControlFont(label, fontSize);
        label.AddThemeColorOverride("font_color", new Color(0.90f, 0.90f, 0.94f, 1f));
        return label;
    }

    private static Label CreateEmptyLabel(string text)
    {
        Label label = CreateBodyLabel(text, 14);
        label.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.82f, 1f));
        return label;
    }

    private static TextureRect CreateGoldIcon()
    {
        return new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(GoldIconPath),
            CustomMinimumSize = new Vector2(28f, 28f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
    }

    private static void ApplyControlFont(Control control, int fontSize)
    {
        control.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private static Control CreatePreviewIconHost()
    {
        MarginContainer host = new();
        host.CustomMinimumSize = new Vector2(56f, 56f);
        host.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return host;
    }

    private static void QueuePotionPreviewHolder(Control host, PotionModel? potion)
    {
        if (potion == null || !GodotObject.IsInstanceValid(host))
        {
            return;
        }

        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(host))
            {
                return;
            }

            NPotionHolder? holder = TryCreatePotionHolder(potion);
            if (holder == null)
            {
                return;
            }

            ClearChildren(host);
            host.AddChild(holder);
        }).CallDeferred();
    }

    private static void QueueRelicPreviewHolder(Control host, RelicModel? relic)
    {
        if (relic == null || !GodotObject.IsInstanceValid(host))
        {
            return;
        }

        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(host))
            {
                return;
            }

            NRelicBasicHolder? holder = TryCreateRelicHolder(relic);
            if (holder == null)
            {
                return;
            }

            ClearChildren(host);
            host.AddChild(holder);
        }).CallDeferred();
    }

    private static NPotionHolder? TryCreatePotionHolder(PotionModel? potion)
    {
        if (potion == null)
        {
            return null;
        }

        try
        {
            NPotionHolder? holder = NPotionHolder.Create(isUsable: false);
            NPotion? potionNode = NPotion.Create(potion);
            if (holder == null || potionNode == null)
            {
                return null;
            }

            holder.AddPotion(potionNode);
            holder.FocusMode = FocusModeEnum.None;
            holder.MouseFilter = MouseFilterEnum.Ignore;
            return holder;
        }
        catch (Exception ex)
        {
            Log.Warn($"{ModEntry.ModId}: failed to create potion holder for '{SafePotionId(potion)}': {ex.Message}", 2);
            return null;
        }
    }

    private static NRelicBasicHolder? TryCreateRelicHolder(RelicModel? relic)
    {
        if (relic == null)
        {
            return null;
        }

        try
        {
            NRelicBasicHolder? holder = NRelicBasicHolder.Create(relic);
            if (holder == null)
            {
                return null;
            }

            holder.FocusMode = FocusModeEnum.None;
            holder.MouseFilter = MouseFilterEnum.Ignore;
            return holder;
        }
        catch (Exception ex)
        {
            Log.Warn($"{ModEntry.ModId}: failed to create relic holder for '{SafeRelicId(relic)}': {ex.Message}", 2);
            return null;
        }
    }

    private static PotionModel? TryGetPotionAtSlot(Player player, int slotIndex)
    {
        try
        {
            return slotIndex < 0 ? null : player.GetPotionAtSlotIndex(slotIndex);
        }
        catch
        {
            return null;
        }
    }

    private static RelicModel? TryGetRelicAtIndex(Player player, int relicIndex)
    {
        if (relicIndex < 0 || relicIndex >= player.Relics.Count)
        {
            return null;
        }

        try
        {
            return player.Relics[relicIndex];
        }
        catch
        {
            return null;
        }
    }

    private static string GetPotionName(PotionModel? potion, TradePotionSelection? selection)
    {
        if (potion != null)
        {
            try
            {
                string title = potion.Title.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(potion.Id.Entry))
            {
                return potion.Id.Entry;
            }
        }

        if (selection != null && !string.IsNullOrWhiteSpace(selection.PotionId.Entry))
        {
            return selection.PotionId.Entry;
        }

        return TradeUiText.Potions;
    }

    private static string GetPotionNameSafe(PotionModel? potion, TradePotionSelection? selection)
    {
        try
        {
            return GetPotionName(potion, selection);
        }
        catch
        {
            return selection?.PotionId?.Entry ?? SafePotionId(potion) ?? TradeUiText.Potions;
        }
    }

    private static string GetRelicName(RelicModel? relic, TradeRelicSelection? selection)
    {
        if (relic != null)
        {
            try
            {
                string title = relic.Title.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(relic.Id.Entry))
            {
                return relic.Id.Entry;
            }
        }

        if (selection != null && !string.IsNullOrWhiteSpace(selection.RelicId.Entry))
        {
            return selection.RelicId.Entry;
        }

        return TradeUiText.Relics;
    }

    private static string? SafePotionId(PotionModel? potion)
    {
        try
        {
            return potion?.Id.Entry;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeRelicId(RelicModel? relic)
    {
        try
        {
            return relic?.Id.Entry;
        }
        catch
        {
            return null;
        }
    }
}
