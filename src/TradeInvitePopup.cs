using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

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
        shell.AddThemeStyleboxOverride("panel", CreatePanelStyle(new Color(0.11f, 0.13f, 0.17f, 0.98f), new Color(0.52f, 0.60f, 0.74f, 0.85f)));
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

        Label title = new() { Text = _titleText };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f, 1.0f));
        stack.AddChild(title);

        Label body = new() { Text = _bodyText, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddThemeFontSizeOverride("font_size", 15);
        body.AddThemeColorOverride("font_color", new Color(0.79f, 0.82f, 0.88f, 1.0f));
        stack.AddChild(body);

        HBoxContainer actions = new();
        actions.Alignment = BoxContainer.AlignmentMode.End;
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
        Button button = new()
        {
            Text = text,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(120f, 38f)
        };
        button.AddThemeStyleboxOverride("normal", CreatePanelStyle(new Color(0.18f, 0.21f, 0.27f, 0.98f), new Color(0.45f, 0.52f, 0.64f, 0.85f)));
        button.AddThemeStyleboxOverride("hover", CreatePanelStyle(new Color(0.24f, 0.28f, 0.35f, 1.0f), new Color(0.66f, 0.76f, 0.90f, 1.0f)));
        button.AddThemeStyleboxOverride("pressed", CreatePanelStyle(new Color(0.13f, 0.16f, 0.21f, 1.0f), new Color(0.76f, 0.84f, 0.94f, 1.0f)));
        button.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.90f, 1.0f));
        return button;
    }

    private static StyleBoxFlat CreatePanelStyle(Color background, Color border)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(12);
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        style.ContentMarginTop = 8;
        style.ContentMarginBottom = 8;
        return style;
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
