namespace ChatSystem.Core.Models
{
    public enum EventType { MatchStart, KillNotification }
    public enum ChatType { Public, Team }

    public record ChatMessage(ChatType Type, string Sender, string Text);
}