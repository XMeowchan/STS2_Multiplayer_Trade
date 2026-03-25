using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace Sts2MultiplayerTrade;

[HarmonyPatch(typeof(NTopBar), "_Ready")]
internal static class TopBarDevTradeButtonPatch
{
    private const string ButtonName = "TradeDevTestButton";

    private static void Postfix(NTopBar __instance)
    {
        if (__instance.FindChild(ButtonName, recursive: false, owned: false) is Button existingButton)
        {
            existingButton.QueueFree();
            return;
        }
    }
}
