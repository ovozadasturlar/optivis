using Avalonia.Controls;
using Avalonia.Interactivity;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.ViewModels;
using System.Diagnostics;

namespace OptiVis.UI.Shared.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnProfileNameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is OperatorProfileItem item
            && DataContext is SettingsViewModel vm)
        {
            await vm.SaveProfileAsync(item.Extension, item.Name ?? "", item.MobileNumber);
        }
    }

    private async void OnProfileMobileLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is OperatorProfileItem item
            && DataContext is SettingsViewModel vm)
        {
            await vm.SaveProfileAsync(item.Extension, item.Name ?? "", item.MobileNumber);
        }
    }

    private async void OnAddOperator(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var extBox = this.FindControl<TextBox>("NewExtensionBox");
        var nameBox = this.FindControl<TextBox>("NewNameBox");

        var ext = extBox?.Text?.Trim() ?? "";
        var name = nameBox?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(ext))
        {
            if (extBox != null)
                extBox.BorderBrush = Avalonia.Media.SolidColorBrush.Parse("#EF4444");
            return;
        }

        await vm.AddManualOperatorAsync(ext, name);

        if (extBox != null) { extBox.Text = ""; extBox.BorderBrush = null; }
        if (nameBox != null) nameBox.Text = "";
    }

    private void OnRemoveOperator(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: OperatorProfileItem item } && DataContext is SettingsViewModel vm)
        {
            vm.RequestRemoveOperator(item.Extension);
        }
    }

    private void OnTelegramClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://t.me/ovozadasturlar",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnBotClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://t.me/ovoza_robot",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
