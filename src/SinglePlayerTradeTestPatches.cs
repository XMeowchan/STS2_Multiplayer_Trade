using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2MultiplayerTrade;

[HarmonyPatch(typeof(NTopBar), "_Ready")]
internal static class TopBarDevTradeButtonPatch
{
    private const string ButtonName = "TradeDevTestButton";

    private static void Postfix(NTopBar __instance)
    {
        if (!TradeRuntime.HasLocalTestMode())
        {
            return;
        }

        if (__instance.FindChild(ButtonName, recursive: false, owned: false) is Button)
        {
            return;
        }

        Button button = new()
        {
            Name = ButtonName,
            Text = TradeUiText.TradeTestButton,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        button.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        button.OffsetLeft = -156;
        button.OffsetTop = 8;
        button.OffsetRight = -16;
        button.OffsetBottom = 42;
        button.ZIndex = 100;
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.16f, 0.20f, 0.26f, 0.98f), new Color(0.45f, 0.54f, 0.70f, 0.92f)));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.23f, 0.27f, 0.34f, 1.0f), new Color(0.72f, 0.82f, 0.96f, 1.0f)));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.11f, 0.15f, 0.20f, 1.0f), new Color(0.80f, 0.88f, 0.98f, 1.0f)));
        button.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.90f, 1.0f));
        button.Connect(Button.SignalName.Pressed, Callable.From(() =>
        {
            Log.Info($"{ModEntry.ModId}: top-bar single-player trade test button pressed.", 2);
            TradeRuntime.OpenLocalTestTrade();
        }));
        __instance.AddChild(button);
    }

    private static StyleBoxFlat CreateButtonStyle(Color background, Color border)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = border
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(10);
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        return style;
    }
}
