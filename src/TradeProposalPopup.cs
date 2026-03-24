using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;

namespace Sts2MultiplayerTrade;

internal sealed class NTradeProposalPopup : Control, IScreenContext
{
    private const float DragThreshold = 18f;

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

    private PanelContainer? _dropTargetPanel;

    private Label? _dropHintLabel;

    private VBoxContainer? _dropTargetPreviewRoot;

    private Control? _dragCaptureLayer;

    private Button? _cancelButton;

    private Button? _resetButton;

    private Button? _readyButton;

    private Button? _confirmButton;

    private bool _dropTargetEditable;

    private TradeDragPayload? _activeDragPayload;

    private bool _dropHovering;

    private bool _dragPrimed;

    private Vector2 _dragStartPosition;

    private TradeDragPayload? _primedDragPayload;

    private Control? _dragPreview;

    private Control? _dragSourceControl;

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
        EndNativeDrag();
        DetachSubscription();
    }

    public override void _Process(double delta)
    {
        if (_dragPrimed)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                CancelPrimedDrag();
            }
            else if (GetGlobalMousePosition().DistanceTo(_dragStartPosition) >= DragThreshold)
            {
                BeginNativeDrag();
            }
        }

        if (_activeDragPayload == null)
        {
            return;
        }

        _dropHovering = IsPointerOverDropTarget();
        UpdateDragPresentation();
        UpdateDropTargetVisuals();
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
        ProcessMode = ProcessModeEnum.Always;
        ZIndex = 500;
        SetProcessInput(true);
        SetProcess(true);

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

        _dragCaptureLayer = new Control
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            ProcessMode = ProcessModeEnum.Always,
            ZIndex = 850
        };
        _dragCaptureLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dragCaptureLayer.GuiInput += HandleDragCaptureInput;
        AddChild(_dragCaptureLayer);

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
        _statusLabel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        stack.AddChild(_statusLabel);

        stack.AddChild(CreateTitleLabel(TradeUiText.IsChineseLocale() ? "拖到这里加入交易" : "Drop Zone", 16));

        _dropTargetPanel = new TradeDropTargetPanel(this);
        _dropTargetPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _dropTargetPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _dropTargetPanel.CustomMinimumSize = new Vector2(0f, 280f);
        _dropTargetPanel.MouseFilter = MouseFilterEnum.Stop;
        stack.AddChild(_dropTargetPanel);

        MarginContainer dropMargin = CreateMargin(10, 10);
        dropMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        dropMargin.SizeFlagsVertical = SizeFlags.ExpandFill;
        _dropTargetPanel.AddChild(dropMargin);

        VBoxContainer dropStack = new();
        dropStack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        dropStack.SizeFlagsVertical = SizeFlags.ExpandFill;
        dropStack.AddThemeConstantOverride("separation", 10);
        dropMargin.AddChild(dropStack);

        _dropHintLabel = CreateBodyLabel(string.Empty, 14);
        _dropHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _dropHintLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        dropStack.AddChild(_dropHintLabel);

        _dropTargetPreviewRoot = new VBoxContainer();
        _dropTargetPreviewRoot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _dropTargetPreviewRoot.SizeFlagsVertical = SizeFlags.ExpandFill;
        _dropTargetPreviewRoot.AddThemeConstantOverride("separation", 10);
        dropStack.AddChild(_dropTargetPreviewRoot);

        UpdateDropTargetVisuals();
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
            EndNativeDrag();
            this.QueueFreeSafely();
            return;
        }

        Player? localPlayer = GetLocalPlayer();
        Player? remotePlayer = _manager.FindPlayer(_remotePlayerId);
        if (localPlayer == null || remotePlayer == null)
        {
            Log.Warn($"{ModEntry.ModId}: trade popup refresh missing players. local={(localPlayer != null)} remote={(remotePlayer != null)}", 2);
            EndNativeDrag();
            this.QueueFreeSafely();
            return;
        }

        _refreshing = true;
        try
        {
            _focusChain.Clear();
            TradeSessionState session = _manager.ActiveSession;
            TradeOfferDraft localOffer = session.OffersByPlayerId.GetValueOrDefault(localPlayer.NetId) ?? new TradeOfferDraft();
            _headerLabel.Text = TradeUiText.TradeTitle(_manager.GetPlayerName(remotePlayer.NetId));
            _statusLabel.Text = BuildStatusText(localPlayer, remotePlayer);

            RebuildLocalOffer(localPlayer);
            RebuildRemoteOffer(remotePlayer);
            RefreshDropTarget(localPlayer, localOffer);

            bool accepted = session.Accepted;
            bool localReady = session.ReadyByPlayerId.GetValueOrDefault(localPlayer.NetId);
            bool bothReady = session.ReadyByPlayerId.GetValueOrDefault(session.InitiatorPlayerId)
                && session.ReadyByPlayerId.GetValueOrDefault(session.RecipientPlayerId);
            bool canCommit = accepted && bothReady && _manager.GetValidationForLocalPlayer().IsValid;
            _dropTargetEditable = accepted;
            UpdateDropTargetVisuals();

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
        GridContainer grid = CreateCardGrid();
        bool any = false;

        for (int slotIndex = 0; slotIndex < localPlayer.PotionSlots.Count; slotIndex += 1)
        {
            PotionModel? potion = TryGetPotionAtSlot(localPlayer, slotIndex);
            if (potion == null)
            {
                continue;
            }

            any = true;
            Control? holder = TryCreatePotionHolder(potion);
            bool selected = offer.Potions.Any(item => item.SlotIndex == slotIndex);
            Control row = CreateDragSelectionRow(
                $"{slotIndex + 1}. {GetPotionNameSafe(potion, null)}",
                selected,
                !editable || !localPlayer.CanRemovePotions,
                holder,
                new TradeDragPayload(TradeDragKind.Potion, slotIndex, GetPotionNameSafe(potion, null)));
            grid.AddChild(row);
        }

        if (any)
        {
            section.AddChild(grid);
        }
        else
        {
            section.AddChild(CreateEmptyLabel(TradeUiText.NoPotions));
        }

        return section;
    }

    private Control CreateRelicEditor(Player localPlayer, TradeOfferDraft offer, bool editable)
    {
        VBoxContainer section = CreateSection(TradeUiText.Relics);
        GridContainer grid = CreateCardGrid();
        bool any = false;

        for (int relicIndex = 0; relicIndex < localPlayer.Relics.Count; relicIndex += 1)
        {
            RelicModel? relic = TryGetRelicAtIndex(localPlayer, relicIndex);
            if (relic == null || !relic.IsTradable)
            {
                continue;
            }

            any = true;
            NRelicBasicHolder? holder = TryCreateRelicHolder(relic);
            bool selected = offer.Relics.Any(item => item.RelicIndex == relicIndex);
            Control row = CreateDragSelectionRow(
                $"{relicIndex + 1}. {GetRelicName(relic, null)}",
                selected,
                !editable,
                holder,
                new TradeDragPayload(TradeDragKind.Relic, relicIndex, GetRelicName(relic, null)));
            grid.AddChild(row);
        }

        if (any)
        {
            section.AddChild(grid);
        }
        else
        {
            section.AddChild(CreateEmptyLabel(TradeUiText.NoRelics));
        }

        return section;
    }

    private Control CreateOfferPreview(Player player, TradeOfferDraft offer)
    {
        VBoxContainer stack = new();
        stack.AddThemeConstantOverride("separation", 10);

        bool any = false;
        if (offer.GoldAmount > 0)
        {
            stack.AddChild(CreatePreviewGoldSection(offer.GoldAmount));
            any = true;
        }

        if (offer.Potions.Count > 0)
        {
            stack.AddChild(CreatePreviewPotionSection(player, offer));
            any = true;
        }

        if (offer.Relics.Count > 0)
        {
            stack.AddChild(CreatePreviewRelicSection(player, offer));
            any = true;
        }

        if (!any)
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
        GridContainer grid = CreateCardGrid();
        bool any = false;

        foreach (TradePotionSelection selection in offer.Potions.OrderBy(static item => item.SlotIndex))
        {
            any = true;
            PotionModel? potion = TryGetPotionAtSlot(player, selection.SlotIndex);
            Control iconHost = CreatePreviewIconHost();

            string potionName = GetPotionNameSafe(potion, selection);
            grid.AddChild(CreatePreviewSelectionCard($"{selection.SlotIndex + 1}. {potionName}", iconHost));
            QueuePotionPreviewHolder(iconHost, potion);
        }

        if (any)
        {
            section.AddChild(grid);
        }
        else
        {
            section.AddChild(CreateEmptyLabel(TradeUiText.OfferEmpty));
        }

        return section;
    }

    private Control CreatePreviewRelicSection(Player player, TradeOfferDraft offer)
    {
        VBoxContainer section = CreateSection(TradeUiText.Relics);
        GridContainer grid = CreateCardGrid();
        bool any = false;

        foreach (TradeRelicSelection selection in offer.Relics.OrderBy(static item => item.RelicIndex))
        {
            any = true;
            RelicModel? relic = TryGetRelicAtIndex(player, selection.RelicIndex);
            Control iconHost = CreatePreviewIconHost();

            grid.AddChild(CreatePreviewSelectionCard($"{selection.RelicIndex + 1}. {GetRelicName(relic, selection)}", iconHost));
            QueueRelicPreviewHolder(iconHost, relic);
        }

        if (any)
        {
            section.AddChild(grid);
        }
        else
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

    private Control CreateDragSelectionRow(string text, bool selected, bool disabled, Control? icon, TradeDragPayload payload)
    {
        Color normal = selected
            ? new Color(0.20f, 0.29f, 0.20f, 0.98f)
            : new Color(0.18f, 0.21f, 0.27f, 0.98f);
        Color border = selected
            ? new Color(0.66f, 0.88f, 0.66f, 0.95f)
            : new Color(0.48f, 0.56f, 0.68f, 0.85f);

        PanelContainer row = new()
        {
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = disabled ? CursorShape.Arrow : CursorShape.PointingHand,
            MouseFilter = MouseFilterEnum.Stop,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(104f, 104f),
            TooltipText = text
        };
        row.AddThemeStyleboxOverride("panel", CreatePanelStyle(normal, border, 8));

        MarginContainer margin = CreateMargin(8, 8);
        margin.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(margin);

        CenterContainer center = new();
        center.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        center.SizeFlagsVertical = SizeFlags.ExpandFill;
        center.MouseFilter = MouseFilterEnum.Ignore;
        margin.AddChild(center);

        if (icon != null)
        {
            icon.MouseFilter = MouseFilterEnum.Ignore;
            if (disabled)
            {
                icon.Modulate = new Color(1f, 1f, 1f, 0.55f);
            }

            center.AddChild(icon);
        }

        row.GuiInput += @event => HandleDragSourceInput(row, payload, disabled, @event);
        return row;
    }

    private static GridContainer CreateCardGrid(int columns = 3)
    {
        GridContainer grid = new()
        {
            Columns = columns,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        return grid;
    }

    private static Control CreateSelectionIconFrame(Control? icon, bool selected, bool disabled)
    {
        Color background = selected
            ? new Color(0.16f, 0.27f, 0.18f, 0.98f)
            : new Color(0.12f, 0.15f, 0.20f, 0.98f);
        Color border = selected
            ? new Color(0.70f, 0.90f, 0.70f, 0.95f)
            : new Color(0.36f, 0.44f, 0.56f, 0.85f);

        PanelContainer panel = new()
        {
            CustomMinimumSize = new Vector2(84f, 84f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle(background, border, 10));

        CenterContainer center = new();
        center.MouseFilter = MouseFilterEnum.Ignore;
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.AddChild(center);

        if (icon != null)
        {
            icon.MouseFilter = MouseFilterEnum.Ignore;
            if (disabled)
            {
                icon.Modulate = new Color(1f, 1f, 1f, 0.55f);
            }

            center.AddChild(icon);
        }

        return panel;
    }

    private static Control CreatePreviewSelectionCard(string text, Control iconHost)
    {
        PanelContainer card = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(104f, 104f),
            TooltipText = text
        };
        card.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.16f, 0.19f, 0.25f, 0.98f), new Color(0.50f, 0.58f, 0.70f, 0.85f), 8));

        MarginContainer margin = CreateMargin(8, 8);
        card.AddChild(margin);

        CenterContainer center = new();
        center.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        center.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(center);

        iconHost.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(iconHost);
        return card;
    }

    private void HandleDragSourceInput(Control source, TradeDragPayload payload, bool disabled, InputEvent @event)
    {
        if (disabled || _refreshing)
        {
            return;
        }

        if (_activeDragPayload != null || _dragPrimed)
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Log.Info($"{ModEntry.ModId}: drag primed kind={payload.Kind} index={payload.Index}.", 2);
            _dragPrimed = true;
            _dragStartPosition = GetGlobalMousePosition();
            _primedDragPayload = payload;
            _dragSourceControl = source;
            if (_dragCaptureLayer != null && GodotObject.IsInstanceValid(_dragCaptureLayer))
            {
                _dragCaptureLayer.Visible = true;
            }

            source.AcceptEvent();
            return;
        }
    }

    private void ToggleDraggedSelection(TradeDragPayload payload)
    {
        if (_manager?.ActiveSession == null)
        {
            return;
        }

        Player? localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            return;
        }

        TradeOfferDraft offer = _manager.ActiveSession.OffersByPlayerId.GetValueOrDefault(localPlayer.NetId) ?? new TradeOfferDraft();
        switch (payload.Kind)
        {
            case TradeDragKind.Potion:
            {
                bool selected = offer.Potions.Any(item => item.SlotIndex == payload.Index);
                Log.Info($"{ModEntry.ModId}: drag dropped potion slot={payload.Index} selected={selected}.", 2);
                _manager.ToggleLocalPotion(payload.Index, !selected);
                break;
            }
            case TradeDragKind.Relic:
            {
                bool selected = offer.Relics.Any(item => item.RelicIndex == payload.Index);
                Log.Info($"{ModEntry.ModId}: drag dropped relic relicIndex={payload.Index} selected={selected}.", 2);
                _manager.ToggleLocalRelic(payload.Index, !selected);
                break;
            }
        }
    }

    private void BeginNativeDrag()
    {
        if (_activeDragPayload != null || _primedDragPayload == null || _dragSourceControl == null)
        {
            return;
        }

        _activeDragPayload = _primedDragPayload;
        _dragPrimed = false;
        _primedDragPayload = null;
        _dragSourceControl.Modulate = new Color(1f, 1f, 1f, 0.35f);
        _dropHovering = false;
        _dragPreview = CreateManualDragPreview(_activeDragPayload);
        AddChild(_dragPreview);
        UpdateDragPresentation();
        UpdateDropTargetVisuals();
        Log.Info($"{ModEntry.ModId}: drag started kind={_activeDragPayload.Kind} index={_activeDragPayload.Index}.", 2);
    }

    private bool CanAcceptCurrentDrag()
    {
        return _dropTargetEditable && _activeDragPayload != null;
    }

    private void CommitCurrentDrag()
    {
        if (_activeDragPayload == null)
        {
            return;
        }

        ToggleDraggedSelection(_activeDragPayload);
    }

    private void EndNativeDrag()
    {
        if (_activeDragPayload == null && !_dropHovering && !_dragPrimed)
        {
            return;
        }

        if (_dragPreview != null && GodotObject.IsInstanceValid(_dragPreview))
        {
            _dragPreview.QueueFree();
        }

        if (_dragCaptureLayer != null && GodotObject.IsInstanceValid(_dragCaptureLayer))
        {
            _dragCaptureLayer.Visible = false;
        }

        _dragPreview = null;
        if (_dragSourceControl != null && GodotObject.IsInstanceValid(_dragSourceControl))
        {
            _dragSourceControl.Modulate = Colors.White;
        }

        _dragPrimed = false;
        _primedDragPayload = null;
        _dragSourceControl = null;
        _dropHovering = false;
        _activeDragPayload = null;
        UpdateDropTargetVisuals();
    }

    private void CancelPrimedDrag()
    {
        if (!_dragPrimed)
        {
            return;
        }

        EndNativeDrag();
    }

    private void HandleDragCaptureInput(InputEvent @event)
    {
        if (_activeDragPayload == null && !_dragPrimed)
        {
            return;
        }

        if (_dragPrimed && _activeDragPayload == null)
        {
            if (@event is InputEventMouseMotion)
            {
                if (GetGlobalMousePosition().DistanceTo(_dragStartPosition) >= DragThreshold)
                {
                    BeginNativeDrag();
                }

                _dragCaptureLayer?.AcceptEvent();
                return;
            }

            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
            {
                CancelPrimedDrag();
                _dragCaptureLayer?.AcceptEvent();
                return;
            }
        }

        if (@event is InputEventMouseMotion)
        {
            _dropHovering = IsPointerOverDropTarget();
            UpdateDragPresentation();
            _dragCaptureLayer?.AcceptEvent();
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            if (_activeDragPayload == null)
            {
                EndNativeDrag();
                _dragCaptureLayer?.AcceptEvent();
                return;
            }

            TradeDragPayload payload = _activeDragPayload;
            _dropHovering = IsPointerOverDropTarget();
            if (_dropHovering)
            {
                Log.Info($"{ModEntry.ModId}: drag release accepted kind={payload.Kind} index={payload.Index}.", 2);
                CommitCurrentDrag();
            }
            else
            {
                Log.Info($"{ModEntry.ModId}: drag release canceled kind={payload.Kind} index={payload.Index}.", 2);
            }

            EndNativeDrag();
            _dragCaptureLayer?.AcceptEvent();
        }
    }

    private void RefreshDropTarget(Player localPlayer, TradeOfferDraft localOffer)
    {
        if (_dropTargetPreviewRoot == null || !GodotObject.IsInstanceValid(_dropTargetPreviewRoot))
        {
            return;
        }

        ClearChildren(_dropTargetPreviewRoot);
        _dropTargetPreviewRoot.AddChild(CreateOfferPreview(localPlayer, localOffer));
    }

    private void UpdateDragPresentation()
    {
        if (_dragPreview != null && GodotObject.IsInstanceValid(_dragPreview))
        {
            _dragPreview.Position = GetGlobalMousePosition() + new Vector2(20f, 20f);
        }

        UpdateDropTargetVisuals();
    }

    private void UpdateDropTargetVisuals()
    {
        if (_dropTargetPanel == null || _dropHintLabel == null || !GodotObject.IsInstanceValid(_dropTargetPanel) || !GodotObject.IsInstanceValid(_dropHintLabel))
        {
            return;
        }

        bool dragging = _activeDragPayload != null && _dropTargetEditable;
        bool hovering = dragging && _dropHovering;

        Color background = hovering
            ? new Color(0.18f, 0.30f, 0.22f, 0.98f)
            : dragging
                ? new Color(0.16f, 0.21f, 0.29f, 0.98f)
                : new Color(0.14f, 0.17f, 0.22f, 0.92f);
        Color border = hovering
            ? new Color(0.70f, 0.92f, 0.70f, 0.98f)
            : dragging
                ? new Color(0.76f, 0.86f, 0.98f, 0.95f)
                : new Color(0.46f, 0.54f, 0.66f, 0.76f);

        _dropTargetPanel.AddThemeStyleboxOverride("panel", CreatePanelStyle(background, border, 12));
        _dropHintLabel.Text = BuildDropHintText(hovering);
    }

    private string BuildDropHintText(bool hovering)
    {
        if (!_dropTargetEditable)
        {
            return TradeUiText.IsChineseLocale()
                ? "等待交易激活后再拖动物品。"
                : "Wait until the trade is active before dragging items.";
        }

        if (_activeDragPayload == null)
        {
            return TradeUiText.IsChineseLocale()
                ? "把药水或遗物拖到这里，加入或移出交易。"
                : "Drag potions or relics here to add or remove them from the trade.";
        }

        bool selected = IsPayloadSelected(_activeDragPayload);
        if (hovering)
        {
            return selected
                ? (TradeUiText.IsChineseLocale()
                    ? "松开即可移出交易。"
                    : "Release to remove from trade.")
                : (TradeUiText.IsChineseLocale()
                    ? "松开即可加入交易。"
                    : "Release to add to trade.");
        }

        return selected
            ? (TradeUiText.IsChineseLocale()
                ? "拖到中间即可移出交易。"
                : "Drag to center to remove from trade.")
            : (TradeUiText.IsChineseLocale()
                ? "拖到中间即可加入交易。"
                : "Drag to center to add to trade.");
    }

    private bool IsPayloadSelected(TradeDragPayload payload)
    {
        if (_manager?.ActiveSession == null)
        {
            return false;
        }

        Player? localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            return false;
        }

        TradeOfferDraft offer = _manager.ActiveSession.OffersByPlayerId.GetValueOrDefault(localPlayer.NetId) ?? new TradeOfferDraft();
        return payload.Kind switch
        {
            TradeDragKind.Potion => offer.Potions.Any(item => item.SlotIndex == payload.Index),
            TradeDragKind.Relic => offer.Relics.Any(item => item.RelicIndex == payload.Index),
            _ => false
        };
    }

    private bool IsPointerOverDropTarget()
    {
        return _dropTargetEditable
            && _dropTargetPanel != null
            && GodotObject.IsInstanceValid(_dropTargetPanel)
            && _dropTargetPanel.GetGlobalRect().HasPoint(GetGlobalMousePosition());
    }

    private Control CreateNativeDragPreview(TradeDragPayload payload)
    {
        PanelContainer preview = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        preview.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.18f, 0.21f, 0.27f, 0.98f), new Color(0.78f, 0.86f, 0.96f, 0.95f), 10));

        MarginContainer margin = CreateMargin(10, 6);
        margin.MouseFilter = MouseFilterEnum.Ignore;
        preview.AddChild(margin);

        CenterContainer center = new();
        center.MouseFilter = MouseFilterEnum.Ignore;
        margin.AddChild(center);

        Control? icon = CreateDragPreviewIcon(payload);
        if (icon != null)
        {
            icon.MouseFilter = MouseFilterEnum.Ignore;
            center.AddChild(icon);
        }
        return preview;
    }

    private Control CreateManualDragPreview(TradeDragPayload payload)
    {
        Control preview = CreateNativeDragPreview(payload);
        preview.TopLevel = true;
        preview.ZIndex = 900;
        preview.MouseFilter = MouseFilterEnum.Ignore;
        return preview;
    }

    private Control? CreateDragPreviewIcon(TradeDragPayload payload)
    {
        Player? localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            return null;
        }

        return payload.Kind switch
        {
            TradeDragKind.Potion => TryCreatePotionHolder(TryGetPotionAtSlot(localPlayer, payload.Index)),
            TradeDragKind.Relic => TryCreateRelicHolder(TryGetRelicAtIndex(localPlayer, payload.Index)),
            _ => null
        };
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
        host.CustomMinimumSize = new Vector2(72f, 72f);
        host.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
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

            Control? holder = TryCreatePotionHolder(potion);
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

    private static Control? TryCreatePotionHolder(PotionModel? potion)
    {
        if (potion == null)
        {
            return null;
        }

        try
        {
            NLabPotionHolder holder = NLabPotionHolder.Create(potion, ModelVisibility.Visible);
            holder.FocusMode = FocusModeEnum.None;
            holder.MouseFilter = MouseFilterEnum.Ignore;
            return holder;
        }
        catch (Exception ex)
        {
            Log.Warn($"{ModEntry.ModId}: failed to create potion holder for '{SafePotionId(potion)}': {ex.Message}", 2);
            try
            {
                return new TradePotionIconHost(potion);
            }
            catch
            {
                return null;
            }
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

    private enum TradeDragKind
    {
        Potion,
        Relic
    }

    private sealed class TradeDragPayload
    {
        public TradeDragPayload(TradeDragKind kind, int index, string label)
        {
            Kind = kind;
            Index = index;
            Label = label;
        }

        public TradeDragKind Kind { get; }

        public int Index { get; }

        public string Label { get; }
    }

    private sealed class TradeDropTargetPanel : PanelContainer
    {
        private readonly NTradeProposalPopup _popup;

        public TradeDropTargetPanel(NTradeProposalPopup popup)
        {
            _popup = popup;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return _popup.CanAcceptCurrentDrag();
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            _popup.CommitCurrentDrag();
        }
    }

    private sealed class TradePotionIconHost : CenterContainer
    {
        private readonly PotionModel _potion;

        private bool _built;

        public TradePotionIconHost(PotionModel potion)
        {
            _potion = potion;
            CustomMinimumSize = new Vector2(72f, 72f);
            Size = new Vector2(72f, 72f);
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Ready()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            try
            {
                Control icon = CreateFallbackPotionIcon(_potion);
                icon.MouseFilter = MouseFilterEnum.Ignore;
                AddChild(icon);
            }
            catch (Exception ex)
            {
                Log.Warn($"{ModEntry.ModId}: failed to build potion icon for '{SafePotionId(_potion)}': {ex.Message}", 2);
            }
        }

        private static Control CreateFallbackPotionIcon(PotionModel potion)
        {
            Texture2D? imageTexture = null;
            Texture2D? outlineTexture = null;
            try
            {
                imageTexture = PreloadManager.Cache.GetTexture2D(potion.ImagePath);
            }
            catch
            {
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(potion.OutlinePath))
                {
                    outlineTexture = PreloadManager.Cache.GetTexture2D(potion.OutlinePath);
                }
            }
            catch
            {
            }

            imageTexture ??= potion.Image;
            outlineTexture ??= potion.Outline;
            if (imageTexture == null)
            {
                Log.Warn($"{ModEntry.ModId}: potion image missing for '{SafePotionId(potion)}' imagePath='{potion.ImagePath}' outlinePath='{potion.OutlinePath ?? "<null>"}'.", 2);
            }

            Control root = new()
            {
                CustomMinimumSize = new Vector2(64f, 64f),
                Size = new Vector2(64f, 64f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Ignore
            };

            TextureRect outline = new()
            {
                Texture = outlineTexture,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            outline.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            outline.Visible = outlineTexture != null;
            root.AddChild(outline);

            TextureRect image = new()
            {
                Texture = imageTexture,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore
            };
            image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            root.AddChild(image);
            return root;
        }
    }
}
