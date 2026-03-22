using Avalonia.Controls;
using Avalonia.Interactivity;
using OptiVis.UI.Shared.ViewModels;

namespace OptiVis.UI.Shared.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void OnSetToday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm) vm.SetToday();
    }

    private void OnSetYesterday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm) vm.SetYesterday();
    }

    private void OnSetWeek(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm) vm.SetWeek();
    }

    private void OnSetMonth(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm) vm.SetMonth();
    }

    private async void OnExportXlsx(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
            return;

        var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Dashboard statistikasini eksport qilish",
            DefaultExtension = "xlsx",
            SuggestedFileName = $"dashboard-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
            FileTypeChoices =
			[
				new Avalonia.Platform.Storage.FilePickerFileType("Excel fayllari")
                {
                    Patterns = ["*.xlsx"]
                }
            ]
        });

        if (saveFile is null) return;

        var tempPath = Path.GetTempFileName() + ".xlsx";
        vm.ExportXlsx(tempPath);
        
        await using var sourceStream = File.OpenRead(tempPath);
        await using var targetStream = await saveFile.OpenWriteAsync();
        await sourceStream.CopyToAsync(targetStream);
        
        File.Delete(tempPath);
    }
}
