using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace Sts2MultiplayerTrade;

[HarmonyPatch(typeof(NMultiplayerPlayerExpandedState), "_Ready")]
internal static class MultiplayerPlayerExpandedStateReadyPatch
{
    private const string TradeButtonName = "TradeActionButton";

    private static void Postfix(NMultiplayerPlayerExpandedState __instance)
    {
        Button? tradeButton = FindTradeButton(__instance);
        tradeButton?.QueueFree();

        Player? remotePlayer = GetPlayer(__instance);
        if (remotePlayer == null || LocalContext.IsMe(remotePlayer))
        {
            return;
        }

        if (!TradeRuntime.TryGetTradeAvailability(remotePlayer, out string reason) && string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        Control? backButton = __instance.GetNodeOrNull<Control>("%BackButton");
        Node? parent = backButton?.GetParent();
        if (backButton == null || parent is not Control controlParent)
        {
            return;
        }

        Button button = CreateTradeButton(remotePlayer, reason);
        button.Name = TradeButtonName;
        controlParent.AddChild(button);
        controlParent.MoveChild(button, backButton.GetIndex() + 1);
        button.Position = backButton.Position + new Vector2(backButton.Size.X + 18f, 0f);
        button.Size = new Vector2(140f, Math.Max(36f, backButton.Size.Y));
    }

    private static Button CreateTradeButton(Player remotePlayer, string reason)
    {
        bool canTrade = TradeRuntime.TryGetTradeAvailability(remotePlayer, out string availabilityReason);
        Button button = new()
        {
            Text = TradeUiText.TradeButton,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            Disabled = !canTrade,
            TooltipText = canTrade ? string.Empty : availabilityReason
        };
        button.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f, 1.0f));
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.17f, 0.20f, 0.26f, 0.98f), new Color(0.44f, 0.53f, 0.70f, 0.88f)));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.22f, 0.26f, 0.34f, 1.0f), new Color(0.70f, 0.80f, 0.94f, 1.0f)));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.11f, 0.15f, 0.20f, 1.0f), new Color(0.78f, 0.86f, 0.96f, 1.0f)));
        button.Pressed += () => TradeRuntime.OpenTrade(remotePlayer);
        return button;
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
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        style.ContentMarginTop = 7;
        style.ContentMarginBottom = 7;
        return style;
    }

    private static Button? FindTradeButton(NMultiplayerPlayerExpandedState screen)
    {
        return screen.FindChild(TradeButtonName, recursive: true, owned: false) as Button;
    }

    private static Player? GetPlayer(NMultiplayerPlayerExpandedState screen)
    {
        return typeof(NMultiplayerPlayerExpandedState)
            .GetField("_player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(screen) as Player;
    }
}

[HarmonyPatch(typeof(NMultiplayerPlayerExpandedState), "UpdateNavigation")]
internal static class MultiplayerPlayerExpandedStateNavigationPatch
{
    private const string TradeButtonName = "TradeActionButton";

    private static void Postfix(NMultiplayerPlayerExpandedState __instance)
    {
        Button? tradeButton = __instance.FindChild(TradeButtonName, recursive: true, owned: false) as Button;
        Control? backButton = __instance.GetNodeOrNull<Control>("%BackButton");
        Control? relicContainer = __instance.GetNodeOrNull<Control>("%RelicContainer");
        Control? potionContainer = __instance.GetNodeOrNull<Control>("%PotionContainer");
        Control? cardContainer = __instance.GetNodeOrNull<Control>("%CardContainer");
        if (tradeButton == null || backButton == null)
        {
            return;
        }

        Control? firstEntry =
            relicContainer?.GetChildCount() > 0 ? relicContainer.GetChild<Control>(0) :
            potionContainer?.GetChildCount() > 0 ? potionContainer.GetChild<Control>(0) :
            cardContainer?.GetChildCount() > 0 ? cardContainer.GetChild<Control>(0) :
            null;

        tradeButton.FocusNeighborLeft = backButton.GetPath();
        tradeButton.FocusNeighborRight = tradeButton.GetPath();
        tradeButton.FocusNeighborTop = tradeButton.GetPath();
        tradeButton.FocusNeighborBottom = firstEntry?.GetPath() ?? backButton.GetPath();
        backButton.FocusNeighborRight = tradeButton.GetPath();
        if (firstEntry != null)
        {
            firstEntry.FocusNeighborTop = tradeButton.GetPath();
        }
    }
}
