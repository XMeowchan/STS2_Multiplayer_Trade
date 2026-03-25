using Godot;
using MegaCrit.Sts2.addons.mega_text;

namespace Sts2MultiplayerTrade;

internal static class TradeUiSkin
{
    private const string HoverTipTexturePath = "res://images/ui/hover_tip.png";

    private const string EndTurnButtonTexturePath = "res://images/packed/combat_ui/end_turn_button.png";

    private const string EndTurnButtonGlowTexturePath = "res://images/packed/combat_ui/end_turn_button_glow.png";

    private static readonly Dictionary<Button, EndTurnButtonState> EndTurnButtonStates = new();

    private static Texture2D? _hoverTipTexture;

    private static Texture2D? _endTurnButtonTexture;

    private static Texture2D? _endTurnButtonGlowTexture;

    public static StyleBoxTexture CreateHoverTipStyle(
        Color tint,
        float marginLeft = 18f,
        float marginTop = 16f,
        float marginRight = 18f,
        float marginBottom = 16f)
    {
        StyleBoxTexture style = new()
        {
            Texture = GetHoverTipTexture(),
            RegionRect = new Rect2(0f, 0f, 339f, 107f),
            DrawCenter = true,
            ModulateColor = tint
        };
        style.SetTextureMargin(Side.Left, 55f);
        style.SetTextureMargin(Side.Top, 43f);
        style.SetTextureMargin(Side.Right, 91f);
        style.SetTextureMargin(Side.Bottom, 32f);
        style.ContentMarginLeft = marginLeft;
        style.ContentMarginTop = marginTop;
        style.ContentMarginRight = marginRight;
        style.ContentMarginBottom = marginBottom;
        return style;
    }

    public static Button CreateProceedButton(
        string text,
        Vector2 minimumSize,
        Color? accentColor = null,
        Color? fontColor = null)
    {
        Button button = new()
        {
            Text = string.Empty,
            Flat = true,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            CustomMinimumSize = minimumSize,
            ClipContents = true
        };

        StyleBoxEmpty emptyStyle = new();
        button.AddThemeStyleboxOverride("normal", emptyStyle);
        button.AddThemeStyleboxOverride("hover", emptyStyle);
        button.AddThemeStyleboxOverride("pressed", emptyStyle);
        button.AddThemeStyleboxOverride("disabled", emptyStyle);
        button.AddThemeStyleboxOverride("focus", emptyStyle);

        TextureRect background = new()
        {
            Texture = GetEndTurnButtonTexture(),
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(background);

        TextureRect glow = new()
        {
            Texture = GetEndTurnButtonGlowTexture(),
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(0.70f, 0.92f, 1f, 0.08f)
        };
        glow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(glow);

        MegaLabel label = new()
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 10,
            MaxFontSize = 16,
            ClipText = true
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(label);

        EndTurnButtonStates[button] = new EndTurnButtonState(
            background,
            glow,
            label,
            text,
            fontColor ?? new Color(0.96f, 0.94f, 0.90f, 1f));

        button.Resized += () => LayoutEndTurnButton(button);
        button.MouseEntered += () =>
        {
            if (TryGetEndTurnButtonState(button, out EndTurnButtonState state))
            {
                state.Hovered = true;
                RefreshProceedButton(button);
            }
        };
        button.MouseExited += () =>
        {
            if (TryGetEndTurnButtonState(button, out EndTurnButtonState state))
            {
                state.Hovered = false;
                state.Pressed = false;
                RefreshProceedButton(button);
            }
        };
        button.FocusEntered += () =>
        {
            if (TryGetEndTurnButtonState(button, out EndTurnButtonState state))
            {
                state.Focused = true;
                RefreshProceedButton(button);
            }
        };
        button.FocusExited += () =>
        {
            if (TryGetEndTurnButtonState(button, out EndTurnButtonState state))
            {
                state.Focused = false;
                state.Pressed = false;
                RefreshProceedButton(button);
            }
        };
        button.ButtonDown += () =>
        {
            if (TryGetEndTurnButtonState(button, out EndTurnButtonState state))
            {
                state.Pressed = true;
                RefreshProceedButton(button);
            }
        };
        button.ButtonUp += () =>
        {
            if (TryGetEndTurnButtonState(button, out EndTurnButtonState state))
            {
                state.Pressed = false;
                RefreshProceedButton(button);
            }
        };
        button.TreeEntered += () =>
        {
            LayoutEndTurnButton(button);
            RefreshProceedButton(button);
        };
        button.TreeExited += () => EndTurnButtonStates.Remove(button);

        LayoutEndTurnButton(button);
        RefreshProceedButton(button);
        return button;
    }

    public static void SetProceedButtonText(Button button, string text)
    {
        if (!TryGetEndTurnButtonState(button, out EndTurnButtonState state))
        {
            button.Text = text;
            return;
        }

        state.Text = text;
        state.Label.SetTextAutoSize(text);
        LayoutEndTurnButton(button);
    }

    public static void RefreshProceedButton(Button button)
    {
        if (!TryGetEndTurnButtonState(button, out EndTurnButtonState state))
        {
            return;
        }

        bool highlighted = (state.Hovered || state.Focused) && !button.Disabled;

        state.Background.Modulate = button.Disabled
            ? new Color(0.60f, 0.60f, 0.60f, 0.48f)
            : state.Pressed
                ? new Color(0.88f, 0.88f, 0.88f, 1f)
                : Colors.White;
        state.Glow.Modulate = button.Disabled
            ? new Color(0.70f, 0.92f, 1f, 0f)
            : highlighted
                ? new Color(0.70f, 0.92f, 1f, state.Pressed ? 0.35f : 0.62f)
                : new Color(0.70f, 0.92f, 1f, 0.10f);
        state.Label.AddThemeColorOverride("font_color", state.FontColor);
        state.Label.Modulate = button.Disabled
            ? new Color(1f, 1f, 1f, 0.42f)
            : new Color(1f, 1f, 1f, 0.98f);

        LayoutEndTurnButton(button);
    }

    private static bool TryGetEndTurnButtonState(Button button, out EndTurnButtonState state)
    {
        if (EndTurnButtonStates.TryGetValue(button, out EndTurnButtonState? found)
            && GodotObject.IsInstanceValid(button)
            && GodotObject.IsInstanceValid(found.Background)
            && GodotObject.IsInstanceValid(found.Glow)
            && GodotObject.IsInstanceValid(found.Label))
        {
            state = found;
            return true;
        }

        EndTurnButtonStates.Remove(button);
        state = null!;
        return false;
    }

    private static void LayoutEndTurnButton(Button button)
    {
        if (!TryGetEndTurnButtonState(button, out EndTurnButtonState state))
        {
            return;
        }

        Vector2 availableSize = button.Size;
        if (availableSize.X <= 0f || availableSize.Y <= 0f)
        {
            availableSize = button.CustomMinimumSize;
        }

        float verticalShift = state.Pressed ? 1f : 0f;
        state.Label.OffsetLeft = 18f;
        state.Label.OffsetTop = verticalShift;
        state.Label.OffsetRight = -18f;
        state.Label.OffsetBottom = verticalShift;
        state.Label.MaxFontSize = (int)Mathf.Clamp(Mathf.Round(availableSize.Y * 0.42f), 14f, 18f);
        state.Label.MinFontSize = Math.Max(12, state.Label.MaxFontSize - 4);
        state.Label.SetTextAutoSize(state.Text);
    }

    private static Texture2D GetHoverTipTexture()
    {
        _hoverTipTexture ??= ResourceLoader.Load<Texture2D>(HoverTipTexturePath);
        return _hoverTipTexture;
    }

    private static Texture2D GetEndTurnButtonTexture()
    {
        _endTurnButtonTexture ??= ResourceLoader.Load<Texture2D>(EndTurnButtonTexturePath);
        return _endTurnButtonTexture;
    }

    private static Texture2D GetEndTurnButtonGlowTexture()
    {
        _endTurnButtonGlowTexture ??= ResourceLoader.Load<Texture2D>(EndTurnButtonGlowTexturePath);
        return _endTurnButtonGlowTexture;
    }

    private sealed class EndTurnButtonState
    {
        public EndTurnButtonState(TextureRect background, TextureRect glow, MegaLabel label, string text, Color fontColor)
        {
            Background = background;
            Glow = glow;
            Label = label;
            Text = text;
            FontColor = fontColor;
        }

        public TextureRect Background { get; }

        public TextureRect Glow { get; }

        public MegaLabel Label { get; }

        public string Text { get; set; }

        public Color FontColor { get; }

        public bool Hovered { get; set; }

        public bool Focused { get; set; }

        public bool Pressed { get; set; }
    }
}
