using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SolidWorksAI.Models;
using SolidWorksAI.ViewModels;

namespace SolidWorksAI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Uygulama açılışında Ollama modellerini yükle
            await vm.LoadModelsCommand.ExecuteAsync(null);

            // Messages koleksiyonu değiştiğinde otomatik aşağı kaydır
            vm.Messages.CollectionChanged += (_, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                    Dispatcher.BeginInvoke(() => ChatScroll.ScrollToEnd());
            };
        }

        InputBox.Focus();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && DataContext is MainViewModel vm)
        {
            if (vm.SendCommand.CanExecute(null))
                vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Message_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ChatMessage msg)
        {
            if (!string.IsNullOrEmpty(msg.Text))
            {
                Clipboard.SetText(msg.Text);
                
                // Opsiyonel: Kopyalandı uyarısı (Tooltip üzerinden veya log)
                if (DataContext is MainViewModel vm)
                {
                    vm.Messages.Add(new ChatMessage 
                    { 
                        Text = "✓ Metin panoya kopyalandı.", 
                        Kind = ChatMessage.MessageKind.System 
                    });
                }
            }
        }
    }
}

/// <summary>
/// ChatMessage.Kind bazlı farklı DataTemplate seçer.
/// </summary>
public class ChatTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserTemplate { get; set; }
    public DataTemplate? AssistantTemplate { get; set; }
    public DataTemplate? ActionLogTemplate { get; set; }
    public DataTemplate? ErrorTemplate { get; set; }
    public DataTemplate? SystemTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ChatMessage msg) return null;
        return msg.Kind switch
        {
            ChatMessage.MessageKind.User      => UserTemplate,
            ChatMessage.MessageKind.Assistant => AssistantTemplate,
            ChatMessage.MessageKind.ActionLog => ActionLogTemplate,
            ChatMessage.MessageKind.Error     => ErrorTemplate,
            _                                  => SystemTemplate
        };
    }
}
