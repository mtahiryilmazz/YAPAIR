namespace Yapair.Desktop.WinUI;

public sealed class ChatMessage
{
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsUser { get; set; }
    public string TimeDisplay { get; set; } = "";
    public string AuthorColor => IsUser ? "#3fb950" : "#58a6ff";
    public string BubbleBg => IsUser ? "#14324b" : "#161b22";
}

public sealed class FindingItem
{
    public string Severity { get; set; } = "info";
    public string Summary { get; set; } = "";
    public string PlaybookText { get; set; } = "";
    public string RiskLabel { get; set; } = "R0";
    public string RiskBg { get; set; } = "#21262d";
    public string RiskFg { get; set; } = "#8b949e";
}
