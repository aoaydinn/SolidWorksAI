using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolidWorksAI.Models;

public class SolidWorksAction
{
    [JsonPropertyName("action_type")]
    public string ActionType { get; set; } = "";

    [JsonPropertyName("parameters")]
    public Dictionary<string, JsonElement> Parameters { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, object> Context { get; set; } = new();

    public string GetString(string key, string fallback = "")
    {
        if (!Parameters.TryGetValue(key, out var el)) return fallback;
        string val = el.ValueKind == JsonValueKind.String ? el.GetString() ?? fallback : el.ToString();
        return Resolve(val, Context)?.ToString() ?? fallback;
    }

    public double GetDouble(string key, double fallback = 0.0)
    {
        if (!Parameters.TryGetValue(key, out var el)) return fallback;
        object? val = Resolve(el.ToString(), Context);
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        if (double.TryParse(val?.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var p)) return p;
        return fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        if (!Parameters.TryGetValue(key, out var el)) return fallback;
        object? val = Resolve(el.ToString(), Context);
        if (val is int i) return i;
        if (int.TryParse(val?.ToString(), out var p)) return p;
        return fallback;
    }

    public bool GetBool(string key, bool fallback = false)
    {
        if (!Parameters.TryGetValue(key, out var el)) return fallback;
        object? val = Resolve(el.ToString(), Context);
        if (val is bool b) return b;
        if (bool.TryParse(val?.ToString(), out var res)) return res;
        return fallback;
    }

    private object? Resolve(string value, Dictionary<string, object> context)
    {
        if (context == null) return value;
        
        // Handle {{var}} interpolation
        if (value.Contains("{{"))
        {
            foreach (var kvp in context)
            {
                value = value.Replace("{{" + kvp.Key + "}}", kvp.Value?.ToString() ?? "");
            }
            return value;
        }

        // Handle direct variable reference
        if (context.TryGetValue(value, out var obj)) return obj;

        return value;
    }

    public double[] GetDoubleArray(string key)
    {
        if (!Parameters.TryGetValue(key, out var el)) return Array.Empty<double>();
        
        // If the value is a variable name pointing to a list
        if (el.ValueKind == JsonValueKind.String && Context.TryGetValue(el.GetString() ?? "", out var list))
        {
            if (list is double[] da) return da;
            if (list is List<double> dl) return dl.ToArray();
        }

        if (el.ValueKind != JsonValueKind.Array) return Array.Empty<double>();

        try
        {
            return el.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        }
        catch { return Array.Empty<double>(); }
    }

    public List<SolidWorksAction> GetActions(string key = "actions")
    {
        if (!Parameters.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array)
            return new List<SolidWorksAction>();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<SolidWorksAction>>(el.GetRawText(), options) ?? new List<SolidWorksAction>();
        }
        catch { return new List<SolidWorksAction>(); }
    }
}
