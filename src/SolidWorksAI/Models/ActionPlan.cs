using System.Text.Json.Serialization;

namespace SolidWorksAI.Models;

public class ActionPlan
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("stop_on_error")]
    public bool StopOnError { get; set; } = false;

    [JsonPropertyName("actions")]
    public List<SolidWorksAction> Actions { get; set; } = new();
}
