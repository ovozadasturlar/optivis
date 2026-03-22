using Avalonia.Controls;
using Avalonia.Interactivity;
using OptiVis.UI.Shared.ViewModels;

namespace OptiVis.UI.Shared.Views;

public partial class OperatorDetailView : UserControl
{
    public OperatorDetailView()
    {
        InitializeComponent();
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorDetailViewModel vm)
        {
            vm.GoBack();
        }
    }

    private void OnSetToday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorDetailViewModel vm) vm.SetToday();
    }

    private void OnSetYesterday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorDetailViewModel vm) vm.SetYesterday();
    }

    private void OnSetWeek(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorDetailViewModel vm) vm.SetWeek();
    }

    private void OnSetMonth(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorDetailViewModel vm) vm.SetMonth();
    }

    private async void OnExportExcel(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OperatorDetailViewModel vm || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
            return;

        var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Operator ma'lumotlarini eksport qilish",
            DefaultExtension = "xlsx",
            SuggestedFileName = $"operator-{vm.Operator?.Extension}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
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
        vm.ExportToExcel(tempPath);

        await using var sourceStream = File.OpenRead(tempPath);
        await using var targetStream = await saveFile.OpenWriteAsync();
        await sourceStream.CopyToAsync(targetStream);

        File.Delete(tempPath);
    }
}
