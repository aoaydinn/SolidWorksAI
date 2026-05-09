using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolidWorksAI.Core;
using SolidWorksAI.Models;

namespace SolidWorksAI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SolidWorksConnector _sw;
    private readonly OllamaClient _ollama;
    private readonly PromptBuilder _promptBuilder;
    private readonly ActionExecutor _executor;

    private CancellationTokenSource? _cts;

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<string> AvailableModels { get; } = new();

    [ObservableProperty] private string _selectedModel = "";
    [ObservableProperty] private string _userInput = "";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isOllamaConnected;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "SolidWorks'e bağlanmak için 'Bağlan' düğmesine tıklayın.";
    [ObservableProperty] private string _swVersion = "";

    public MainViewModel(
        SolidWorksConnector sw,
        OllamaClient ollama,
        PromptBuilder promptBuilder,
        ActionExecutor executor)
    {
        _sw = sw;
        _ollama = ollama;
        _promptBuilder = promptBuilder;
        _executor = executor;
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        StatusText = "SolidWorks'e bağlanılıyor...";
        IsConnected = await _sw.ConnectAsync();
        if (IsConnected)
        {
            SwVersion = await _sw.GetSwVersionAsync();
            StatusText = $"SolidWorks {SwVersion} — bağlı";
            AddSystemMessage($"SolidWorks {SwVersion} bağlantısı kuruldu.");
        }
        else
        {
            StatusText = "SolidWorks çalışmıyor. Lütfen önce SolidWorks'ü açın.";
            AddSystemMessage("Bağlantı başarısız — SolidWorks çalışmıyor.");
        }
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        var models = await _ollama.GetModelsAsync();
        IsOllamaConnected = models.Count > 0;
        AvailableModels.Clear();

        var localModels = new List<string>();
        var cloudModels = new List<string>();
        foreach (var m in models)
        {
            if (m.Contains(":cloud", StringComparison.OrdinalIgnoreCase))
                cloudModels.Add(m);
            else
                localModels.Add(m);
        }

        // Önce yerel modeller, sonra bulut modeller (ayraçla)
        foreach (var m in localModels) AvailableModels.Add(m);
        if (cloudModels.Count > 0)
        {
            AvailableModels.Add("── bulut (API key gerekir) ──");
            foreach (var m in cloudModels) AvailableModels.Add(m);
        }

        // Varsayılan olarak ilk YEREL modeli seç
        if (string.IsNullOrEmpty(SelectedModel) || SelectedModel.Contains(":cloud"))
            SelectedModel = localModels.FirstOrDefault() ?? "";

        if (models.Count == 0)
        {
            IsOllamaConnected = false;
            AddSystemMessage("Ollama modeli bulunamadı. Ollama'nın çalıştığından emin olun.");
        }
        else
        {
            var cloudNote = cloudModels.Count > 0
                ? $" ({cloudModels.Count} bulut model API key gerektirir)"
                : "";
            AddSystemMessage($"Ollama bağlandı — {localModels.Count} yerel model{cloudNote}.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var input = UserInput.Trim();
        if (string.IsNullOrWhiteSpace(input)) return;

        // Bulut model seçiliyse uyar
        if (SelectedModel.Contains(":cloud", StringComparison.OrdinalIgnoreCase)
            || SelectedModel.StartsWith("──"))
        {
            AddMessage(ChatMessage.MessageKind.Error,
                "Seçili model bir bulut modelidir ve API key gerektirir. Lütfen listeden yerel bir model seçin (örn: qwen2.5-coder:7b).");
            return;
        }

        UserInput = "";
        IsBusy = true;
        _cts = new CancellationTokenSource();

        AddMessage(ChatMessage.MessageKind.User, input);

        try
        {
            // 1. LLM'e gönder
            AddSystemMessage("LLM'e gönderiliyor...");
            var request = _promptBuilder.BuildRequest(SelectedModel, input);
            var llmResponse = await _ollama.ChatAsync(request, _cts.Token);

            AddMessage(ChatMessage.MessageKind.Assistant, llmResponse);

            // 2. JSON ayıkla ve parse et
            var json = PromptBuilder.ExtractJson(llmResponse);
            ActionPlan? plan = null;
            try
            {
                plan = JsonSerializer.Deserialize<ActionPlan>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                AddMessage(ChatMessage.MessageKind.Error, $"JSON ayrıştırma hatası: {ex.Message}");
                return;
            }

            if (plan == null || plan.Actions.Count == 0)
            {
                AddMessage(ChatMessage.MessageKind.Error, "LLM eylem planı döndürmedi.");
                return;
            }

            AddSystemMessage($"Eylem planı: {plan.Description} ({plan.Actions.Count} adım)");

            // 3. SolidWorks'te çalıştır
            if (!IsConnected)
            {
                AddMessage(ChatMessage.MessageKind.Error, "SolidWorks bağlı değil. Önce 'Bağlan' düğmesine tıklayın.");
                return;
            }

            var progress = new Progress<string>(msg => AddMessage(ChatMessage.MessageKind.ActionLog, msg));
            var result = await _executor.ExecuteAsync(plan, progress, _cts.Token);

            var summary = result.OverallSuccess
                ? $"Tüm {result.Results.Count} adım başarıyla tamamlandı."
                : $"{result.Results.Count(r => r.Success)}/{result.Results.Count} adım başarılı.";

            AddMessage(result.OverallSuccess ? ChatMessage.MessageKind.System : ChatMessage.MessageKind.Error, summary);
        }
        catch (OperationCanceledException)
        {
            AddSystemMessage("İşlem iptal edildi.");
        }
        catch (Exception ex)
        {
            AddMessage(ChatMessage.MessageKind.Error, $"Hata: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(SelectedModel);

    partial void OnIsBusyChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnSelectedModelChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void ClearMessages() => Messages.Clear();

    private void AddMessage(ChatMessage.MessageKind kind, string text)
    {
        Messages.Add(new ChatMessage { Kind = kind, Text = text });
    }

    private void AddSystemMessage(string text) =>
        AddMessage(ChatMessage.MessageKind.System, text);
}
