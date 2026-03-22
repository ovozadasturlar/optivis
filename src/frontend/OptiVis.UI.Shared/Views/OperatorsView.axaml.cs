using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.ViewModels;

namespace OptiVis.UI.Shared.Views;

public partial class OperatorsView : UserControl
{
    public OperatorsView()
    {
        InitializeComponent();
    }

    private void OnSetToday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorsViewModel vm) vm.SetToday();
    }

    private void OnSetYesterday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorsViewModel vm) vm.SetYesterday();
    }

    private void OnSetWeek(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorsViewModel vm) vm.SetWeek();
    }

    private void OnSetMonth(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperatorsViewModel vm) vm.SetMonth();
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border
            && border.DataContext is OperatorStats op
            && DataContext is OperatorsViewModel vm)
        {
            vm.SelectOperator(op);
        }
    }

    private async void OnExportXlsx(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OperatorsViewModel vm || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
            return;

        var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Operatorlar statistikasini eksport qilish",
            DefaultExtension = "xlsx",
            SuggestedFileName = $"operators-{DateTime.Now:yyyyMMdd-HHmm}.xlsx",
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
