using SolidWorksAI.Models;
using SolidWorksAI.Services;

namespace SolidWorksAI.Core;

public class ActionExecutor
{
    private readonly SolidWorksConnector _sw;
    private readonly ActionRegistry _registry;
    private readonly Dictionary<string, object> _variables = new();

    public ActionExecutor(SolidWorksConnector sw, ActionRegistry registry)
    {
        _sw = sw;
        _registry = registry;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        ActionPlan plan,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new ExecutionResult();
        _variables.Clear();

        await ExecuteInternalAsync(plan.Actions, plan.StopOnError, result, progress, ct);

        return result;
    }

    private async Task ExecuteInternalAsync(
        List<SolidWorksAction> actions,
        bool stopOnError,
        ExecutionResult overallResult,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        foreach (var originalAction in actions)
        {
            ct.ThrowIfCancellationRequested();

            // Handle Flow Control Actions
            if (originalAction.ActionType == "loop_over_entities")
            {
                await HandleLoopAsync(originalAction, stopOnError, overallResult, progress, ct);
                continue;
            }
            if (originalAction.ActionType == "condition")
            {
                await HandleConditionAsync(originalAction, stopOnError, overallResult, progress, ct);
                continue;
            }
            if (originalAction.ActionType == "filter_entities_by_property")
            {
                HandleFilter(originalAction, overallResult, progress);
                continue;
            }

            // Resolve parameters from variables
            originalAction.Context = _variables;

            if (!_registry.TryGetHandler(originalAction.ActionType, out var handler) || handler == null)
            {
                var unknown = new ActionResult
                {
                    ActionType = originalAction.ActionType,
                    Success = false,
                    Message = $"Bilinmeyen aksiyon: {originalAction.ActionType}"
                };
                overallResult.Results.Add(unknown);
                progress?.Report($"⚠ {unknown.Message}");
                if (stopOnError) break;
                continue;
            }

            var actionResult = await _sw.RunOnStaWithRetryAsync(() =>
            {
                _sw.CheckConnection();
                if (_sw.SwApp == null)
                    return new ActionResult { ActionType = originalAction.ActionType, Success = false, Message = "SolidWorks bağlantısı kesildi." };
                
                var r = handler(originalAction, _sw.SwApp);
                r.ActionType = originalAction.ActionType;
                return r;
            }).ConfigureAwait(false);

            // Store result in variables if output_var is specified
            string outVar = originalAction.GetString("output_var");
            if (!string.IsNullOrEmpty(outVar) && actionResult.Data != null)
            {
                _variables[outVar] = actionResult.Data;
            }

            // Create a serializable version of the result for the history/UI
            var serializableResult = new ActionResult
            {
                ActionType = actionResult.ActionType,
                Success = actionResult.Success,
                Message = actionResult.Message,
                Data = CleanDataForSerialization(actionResult.Data)
            };

            overallResult.Results.Add(serializableResult);
            var icon = actionResult.Success ? "✓" : "✗";
            progress?.Report($"{icon} [{originalAction.ActionType}] {actionResult.Message}");

            if (!actionResult.Success && stopOnError) break;
        }
    }

    private async Task HandleLoopAsync(SolidWorksAction action, bool stopOnError, ExecutionResult res, IProgress<string>? p, CancellationToken ct)
    {
        string varName = action.GetString("list_var");
        if (!_variables.TryGetValue(varName, out var listObj) || listObj is not System.Collections.IEnumerable list)
        {
            res.Results.Add(new ActionResult { ActionType = "loop", Success = false, Message = $"Liste değişkeni bulunamadı: {varName}" });
            return;
        }

        string itemVar = action.GetString("item_var", "item");
        var subActions = action.GetActions();

        foreach (var item in list)
        {
            _variables[itemVar] = item;
            await ExecuteInternalAsync(subActions, stopOnError, res, p, ct);
        }
    }

    private async Task HandleConditionAsync(SolidWorksAction action, bool stopOnError, ExecutionResult res, IProgress<string>? p, CancellationToken ct)
    {
        string varName = action.GetString("variable");
        string op = action.GetString("operator", "==");
        string val = action.GetString("value");

        _variables.TryGetValue(varName, out var currentVal);
        bool match = false;

        if (op == "==" || op == "equals") match = currentVal?.ToString() == val;
        else if (op == "!=" || op == "not_equals") match = currentVal?.ToString() != val;
        else if (op == ">" || op == "greater_than")  match = Convert.ToDouble(currentVal) > (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0);
        else if (op == "<" || op == "less_than")  match = Convert.ToDouble(currentVal) < (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d2) ? d2 : 0);

        if (match)
        {
            await ExecuteInternalAsync(action.GetActions("then"), stopOnError, res, p, ct);
        }
        else
        {
            await ExecuteInternalAsync(action.GetActions("else"), stopOnError, res, p, ct);
        }
    }

    private void HandleFilter(SolidWorksAction action, ExecutionResult res, IProgress<string>? p)
    {
        string varName = action.GetString("list_var");
        if (!_variables.TryGetValue(varName, out var listObj) || listObj is not System.Collections.IEnumerable list)
        {
            res.Results.Add(new ActionResult { ActionType = "filter", Success = false, Message = $"Liste bulunamadı: {varName}" });
            return;
        }

        string property = action.GetString("property").ToLower();
        string op = action.GetString("operator", "==");
        string val = action.GetString("value");
        string outVar = action.GetString("output_var", varName);

        var filtered = new List<object>();
        foreach (var item in list)
        {
            // If the item is the result of another action stored in variables, we might need a more complex lookup.
            // For now, let's support simple string/double matching.
            bool match = false;
            string itemStr = item?.ToString() ?? "";
            
            if (op == "==" || op == "equals") match = itemStr.Equals(val, StringComparison.OrdinalIgnoreCase);
            else if (op == "!=" || op == "not_equals") match = !itemStr.Equals(val, StringComparison.OrdinalIgnoreCase);
            else if (op == "contains") match = itemStr.Contains(val, StringComparison.CurrentCultureIgnoreCase);
            
            if (match) filtered.Add(item!);
        }

        _variables[outVar] = filtered;
        res.Results.Add(new ActionResult { ActionType = "filter", Success = true, Message = $"Filtreleme: {filtered.Count} öge süzüldü.", Data = filtered });
    }

    private object? CleanDataForSerialization(object? data)
    {
        if (data == null) return null;
        if (data is string || data is int || data is double || data is bool) return data;
        
        if (data is System.Collections.IEnumerable list && data is not string)
        {
            var cleanList = new List<object>();
            int count = 0;
            foreach (var item in list)
            {
                if (count++ > 50) { cleanList.Add("... (truncated)"); break; }
                cleanList.Add(CleanDataForSerialization(item) ?? "null");
            }
            return cleanList;
        }

        if (data is Dictionary<string, object> dict)
        {
            var cleanDict = new Dictionary<string, object>();
            foreach (var kvp in dict)
            {
                cleanDict[kvp.Key] = CleanDataForSerialization(kvp.Value) ?? "null";
            }
            return cleanDict;
        }

        // Handle COM objects
        if (data.GetType().IsCOMObject || data.ToString()?.Contains("System.__ComObject") == true)
        {
            return $"[Entity: {data.GetType().Name}]";
        }

        return data.ToString();
    }

    private SolidWorksAction ResolveAction(SolidWorksAction action)
    {
        var newAction = new SolidWorksAction
        {
            ActionType = action.ActionType,
            Parameters = new Dictionary<string, System.Text.Json.JsonElement>(action.Parameters)
        };

        // For simplicity in this demo, we won't fully reconstruct JsonElement, 
        // but real implementation should handle string interpolation in parameters.
        // We can add a GetParam(string key) to SolidWorksAction that resolves {{vars}}.
        return action; 
    }
}
