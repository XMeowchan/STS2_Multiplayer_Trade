using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace Sts2MultiplayerTrade;

internal sealed class NTradeInvitePopup : Control, IScreenContext
{
    private bool _uiBuilt;

    private string _titleText = string.Empty;

    private string _bodyText = string.Empty;

    private TaskCompletionSource<bool>? _decisionSource;

    private Button? _acceptButton;

    public Control? DefaultFocusedControl => _acceptButton;

    public static NTradeInvitePopup Create(string title, string body)
    {
        NTradeInvitePopup popup = new();
        popup._titleText = title;
        popup._bodyText = body;
        popup.EnsureBuilt();
        return popup;
    }

    public Task<bool> WaitForDecision()
    {
        _decisionSource ??= new TaskCompletionSource<bool>();
        return _decisionSource.Task;
    }

    public override void _Ready()
    {
        EnsureBuilt();
    }

    public override void _ExitTree()
    {
        _decisionSource?.TrySetResult(false);
    }

    private void EnsureBuilt()
    {
        if (_uiBuilt)
        {
            return;
        }

        _uiBuilt = true;
        _decisionSource ??= new TaskCompletionSource<bool>();
        ZIndex = 20;

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        ColorRect backdrop = new()
        {
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = MouseFilterEnum.Stop
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        CenterContainer center = new();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        PanelContainer shell = new();
        shell.CustomMinimumSize = new Vector2(520f, 0f);
        shell.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.92f, 0.93f, 0.98f, 0.96f)));
        center.AddChild(shell);

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        shell.AddChild(margin);

        VBoxContainer stack = new();
        stack.AddThemeConstantOverride("separation", 14);
        margin.AddChild(stack);

        MegaLabel title = new()
        {
            Text = _titleText,
            AutoSizeEnabled = false
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f, 1.0f));
        stack.AddChild(title);

        MegaLabel body = new()
        {
            Text = _bodyText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AutoSizeEnabled = false
        };
        body.AddThemeFontSizeOverride("font_size", 15);
        body.AddThemeColorOverride("font_color", new Color(0.79f, 0.82f, 0.88f, 1.0f));
        stack.AddChild(body);

        HBoxContainer actions = new();
        actions.Alignment = BoxContainer.AlignmentMode.Center;
        actions.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddThemeConstantOverride("separation", 12);
        stack.AddChild(actions);

        Button declineButton = CreateActionButton(TradeUiText.Decline);
        declineButton.Pressed += () => Finish(false);
        actions.AddChild(declineButton);

        _acceptButton = CreateActionButton(TradeUiText.Accept);
        _acceptButton.Pressed += () => Finish(true);
        actions.AddChild(_acceptButton);

        _acceptButton.GrabFocus();
    }

    private static Button CreateActionButton(string text)
    {
        return TradeUiSkin.CreateProceedButton(text, new Vector2(122f, 40f));
    }

    private static StyleBoxTexture CreatePanelStyle(Color tint)
    {
        return TradeUiSkin.CreateHoverTipStyle(tint, 18f, 16f, 18f, 16f);
    }

    private void Finish(bool accepted)
    {
        _decisionSource?.TrySetResult(accepted);
        if (NModalContainer.Instance?.OpenModal == this)
        {
            NModalContainer.Instance.Clear();
            return;
        }

        this.QueueFreeSafely();
    }
}
