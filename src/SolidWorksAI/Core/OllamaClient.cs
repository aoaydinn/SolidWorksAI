using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SolidWorksAI.Models;

namespace SolidWorksAI.Core;

public class OllamaClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<OllamaTagsResponse>("/api/tags", _opts, ct);
            return response?.Models.Select(m => m.Name).ToList() ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<string> ChatAsync(OllamaRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OllamaResponse>(body, _opts);
        return result?.Message.Content ?? "";
    }
}
