using Godot;

namespace Sts2MultiplayerTrade;

internal static class TradeUiText
{
    public static bool IsChineseLocale()
    {
        string locale = TranslationServer.GetLocale();
        return !string.IsNullOrWhiteSpace(locale)
            && locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    public static string TradeButton => IsChineseLocale() ? "交易" : "Trade";

    public static string TradeTestButton => IsChineseLocale() ? "交易测试" : "Trade Test";

    public static string TradeTitle(string remotePlayerName)
        => IsChineseLocale() ? $"与 {remotePlayerName} 交易" : $"Trade with {remotePlayerName}";

    public static string PendingInvite => IsChineseLocale() ? "等待对方接受邀请。" : "Waiting for the other player to accept.";

    public static string Ready => IsChineseLocale() ? "准备" : "Ready";

    public static string Unready => IsChineseLocale() ? "取消准备" : "Unready";

    public static string Confirm => IsChineseLocale() ? "确认交易" : "Confirm Trade";

    public static string Reset => IsChineseLocale() ? "重置报价" : "Reset Offer";

    public static string Cancel => IsChineseLocale() ? "取消" : "Cancel";

    public static string Accept => IsChineseLocale() ? "接受" : "Accept";

    public static string Decline => IsChineseLocale() ? "拒绝" : "Decline";

    public static string Gold => IsChineseLocale() ? "金币" : "Gold";

    public static string Potions => IsChineseLocale() ? "药水" : "Potions";

    public static string Relics => IsChineseLocale() ? "遗物" : "Relics";

    public static string YourOffer => IsChineseLocale() ? "你的报价" : "Your Offer";

    public static string TheirOffer => IsChineseLocale() ? "对方报价" : "Their Offer";

    public static string SessionTimedOut => IsChineseLocale() ? "交易会话已超时。" : "The trade session timed out.";

    public static string BusyReason => IsChineseLocale() ? "已有其他交易正在进行。" : "Another trade is already in progress.";

    public static string CombatReason => IsChineseLocale() ? "战斗中无法发起交易。" : "Trading is unavailable during combat.";

    public static string LocalOnlyReason => IsChineseLocale() ? "单人或伪联机场景不支持交易。" : "Trading is unavailable in single-player or fake multiplayer.";

    public static string AcceptedStatus(string remotePlayerName)
        => IsChineseLocale() ? $"已与 {remotePlayerName} 建立交易。" : $"Trade session active with {remotePlayerName}.";

    public static string InvitePromptTitle(string remotePlayerName)
        => IsChineseLocale() ? $"{remotePlayerName} 想与你交易" : $"{remotePlayerName} wants to trade";

    public static string InvitePromptBody => IsChineseLocale()
        ? "接受后会打开交易窗口。"
        : "Accepting will open the trade window.";

    public static string ValidationOk => IsChineseLocale() ? "报价可提交。" : "Offer is valid.";

    public static string ConfirmWaitingHost => IsChineseLocale() ? "等待主机确认。" : "Waiting for host confirmation.";

    public static string Status => IsChineseLocale() ? "状态" : "Status";

    public static string OfferEmpty => IsChineseLocale() ? "未选择任何交易内容" : "Nothing selected";

    public static string NoPotions => IsChineseLocale() ? "没有可交易药水" : "No tradable potions";

    public static string NoRelics => IsChineseLocale() ? "没有可交易遗物" : "No tradable relics";

    public static string TestTraderName => IsChineseLocale() ? "测试交易员" : "Test Trader";

    public static string ReadyState(bool ready)
        => IsChineseLocale() ? (ready ? "已准备" : "未准备") : (ready ? "READY" : "WAITING");
}
