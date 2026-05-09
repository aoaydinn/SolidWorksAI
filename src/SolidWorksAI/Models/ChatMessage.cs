namespace SolidWorksAI.Models;

public class ChatMessage
{
    public enum MessageKind { User, Assistant, ActionLog, Error, System }

    public MessageKind Kind { get; set; }
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ActionResult
{
    public string ActionType { get; set; } = "";
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
}

public class ExecutionResult
{
    public bool OverallSuccess => Results.All(r => r.Success);
    public List<ActionResult> Results { get; set; } = new();
}
