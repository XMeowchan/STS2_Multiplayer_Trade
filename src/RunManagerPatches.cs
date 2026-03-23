using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2MultiplayerTrade;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class RunManagerCleanUpPatch
{
    private static void Prefix()
    {
        TradeRuntime.CleanupCurrent();
    }
}
