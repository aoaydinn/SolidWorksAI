using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using SolidWorksAI.Models;

namespace SolidWorksAI.Core;

public class PromptBuilder
{
    private readonly string _systemPrompt;

    public PromptBuilder()
    {
        var assembly = Assembly.GetExecutingAssembly();
        _systemPrompt = BuildSystemPrompt(assembly);
    }

    private static string BuildSystemPrompt(Assembly asm)
    {
        var template = ReadEmbedded(asm, "Assets.PromptTemplate.txt");
        var schema = ReadEmbedded(asm, "Schemas.DrawingActionsSchema.json");
        return template.Replace("{{SCHEMA}}", schema);
    }

    private static string ReadEmbedded(Assembly asm, string resourceSuffix)
    {
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (name == null) return "";
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public OllamaRequest BuildRequest(string model, string userCommand)
    {
        return new OllamaRequest
        {
            Model = model,
            Stream = false,
            Options = new OllamaOptions { Temperature = 0.05, NumCtx = 8192 },
            Messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = _systemPrompt },
                new() { Role = "user",   Content = userCommand }
            }
        };
    }

    // LLM cevabından JSON bloğunu ayıkla
    public static string ExtractJson(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse)) return "";

        // 1. ```json ... ``` bloğu
        var fenced = Regex.Match(llmResponse, @"```json\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (fenced.Success) return fenced.Groups[1].Value.Trim();

        // 2. ``` ... ``` bloğu (json etiketsiz)
        var generic = Regex.Match(llmResponse, @"```\s*([\s\S]*?)```");
        if (generic.Success) return generic.Groups[1].Value.Trim();

        // 3. İlk { ile son } arasındaki her şey
        var start = llmResponse.IndexOf('{');
        var end = llmResponse.LastIndexOf('}');
        if (start >= 0 && end > start) return llmResponse[start..(end + 1)];

        return llmResponse.Trim();
    }
}
