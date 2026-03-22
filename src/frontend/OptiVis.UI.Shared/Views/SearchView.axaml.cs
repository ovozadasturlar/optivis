using Avalonia.Controls;
using Avalonia.Interactivity;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.ViewModels;

namespace OptiVis.UI.Shared.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    private void OnSetToday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            vm.SetToday();
    }

    private void OnSetWeek(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            vm.SetWeek();
    }

    private void OnSetMonth(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            vm.SetMonth();
    }

    private void OnSetYesterday(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            vm.SetYesterday();
    }

    private async void OnExportExcel(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SearchViewModel vm || TopLevel.GetTopLevel(this) is not TopLevel topLevel)
        {
            return;
        }

        var saveFile = await topLevel.StorageProvider.SaveFilePickerAsync(new()
        {
            Title = "Excel ga yuklash",
            DefaultExtension = "xlsx",
            SuggestedFileName = $"raqamlar-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"
        });

        if (saveFile is null)
        {
            return;
        }

        await using var stream = await saveFile.OpenWriteAsync();
        using var workbook = vm.ExportExcel();
        workbook.SaveAs(stream);
    }

    private void OnViewDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PhoneNumberStats stats && DataContext is SearchViewModel vm)
        {
            vm.SelectedPhoneNumber = stats;
        }
    }

    private void OnClosePopupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
        {
            vm.CloseDetailPopup();
        }
    }
}
