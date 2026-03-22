using System.Collections.ObjectModel;
using ReactiveUI;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.Services;
using OptiVis.UI.Shared.i18n;
using DynamicData;

namespace OptiVis.UI.Shared.ViewModels;

public class SearchViewModel : LocalizedViewModelBase
{
    private readonly IApiService _apiService;
    private readonly IOperatorProfileService _profileService;

    public override string NavTitle => Translations.Get("Numbers");

    public ObservableCollection<PhoneNumberStats> PhoneNumbers { get; } = new();
    public ObservableCollection<PhoneNumberStats> FilteredRecords { get; } = new();
    public ObservableCollection<PhoneNumberCallDetail> SelectedNumberDetails { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            ApplyFilter();
        }
    }

    private PhoneNumberStats? _selectedPhoneNumber;
    public PhoneNumberStats? SelectedPhoneNumber
    {
        get => _selectedPhoneNumber;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPhoneNumber, value);
            if (value != null)
            {
                _ = LoadPhoneNumberDetailsAsync(value.PhoneNumber);
            }
        }
    }

    private bool _isDetailPopupOpen;
    public bool IsDetailPopupOpen
    {
        get => _isDetailPopupOpen;
        set => this.RaiseAndSetIfChanged(ref _isDetailPopupOpen, value);
    }

    private DateTimeOffset? _fromDate = new DateTimeOffset(DateTime.Today);
    public DateTimeOffset? FromDate
    {
        get => _fromDate;
        set
        {
            this.RaiseAndSetIfChanged(ref _fromDate, value);
            _ = LoadDataAsync();
        }
    }

    private DateTimeOffset? _toDate = new DateTimeOffset(DateTime.Today.AddDays(1).AddSeconds(-1));
    public DateTimeOffset? ToDate
    {
        get => _toDate;
        set
        {
            this.RaiseAndSetIfChanged(ref _toDate, value);
            _ = LoadDataAsync();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public SearchViewModel(IApiService apiService, IOperatorProfileService profileService)
    {
        _apiService = apiService;
        _profileService = profileService;

        _ = LoadDataAsync();
    }

    public void SetToday() => SetTodayRange();
    public void SetYesterday() => SetYesterdayRange();
    public void SetWeek() => SetDateRange(7);
    public void SetMonth() => SetDateRange(30);

    private void SetTodayRange()
    {
        var today = DateTime.Today;
        _fromDate = new DateTimeOffset(today);
        _toDate = new DateTimeOffset(today.AddDays(1).AddSeconds(-1));
        this.RaisePropertyChanged(nameof(FromDate));
        this.RaisePropertyChanged(nameof(ToDate));
        _ = LoadDataAsync();
    }

    private void SetYesterdayRange()
    {
        var yesterday = DateTimeOffset.Now.AddDays(-1).Date;
        _fromDate = new DateTimeOffset(yesterday);
        _toDate = new DateTimeOffset(yesterday.AddDays(1).AddTicks(-1));
        this.RaisePropertyChanged(nameof(FromDate));
        this.RaisePropertyChanged(nameof(ToDate));
        _ = LoadDataAsync();
    }

    private void SetDateRange(int days)
    {
        _fromDate = new DateTimeOffset(DateTime.Today.AddDays(-days));
        _toDate = new DateTimeOffset(DateTime.Today.AddDays(1).AddSeconds(-1));
        this.RaisePropertyChanged(nameof(FromDate));
        this.RaisePropertyChanged(nameof(ToDate));
        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        PhoneNumbers.Clear();
        try
        {
            var from = FromDate?.DateTime ?? DateTime.Now.AddMonths(-2);
            var to = ToDate?.DateTime ?? DateTime.Today.AddDays(1).AddSeconds(-1);

            var results = await _apiService.GetPhoneNumberStatsAsync(from, to);
            if (results != null)
            {
                foreach (var result in results.OrderByDescending(x => x.TotalCalls))
                {
                    PhoneNumbers.Add(result);
                }
            }
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredRecords.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? PhoneNumbers
            : PhoneNumbers.Where(p => p.PhoneNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        
        foreach (var item in filtered)
        {
            FilteredRecords.Add(item);
        }
    }

    public async Task LoadPhoneNumberDetailsAsync(string phoneNumber)
    {
        SelectedNumberDetails.Clear();
        var from = FromDate?.DateTime ?? DateTime.Now.AddMonths(-2);
        var to = ToDate?.DateTime ?? DateTime.Now.AddDays(1);
        var details = await _apiService.GetPhoneNumberDetailsAsync(phoneNumber, from, to);
        if (details != null)
        {
            foreach (var detail in details)
            {
                detail.OperatorName = await _profileService.GetOperatorNameAsync(detail.OperatorExtension);
                SelectedNumberDetails.Add(detail);
            }
        }
        IsDetailPopupOpen = true;
    }

    public void CloseDetailPopup()
    {
        IsDetailPopupOpen = false;
    }

    public ClosedXML.Excel.XLWorkbook ExportExcel()
    {
        var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Raqamlar");
        
        // Header
        worksheet.Cell(1, 1).Value = "Telefon raqam";
        worksheet.Cell(1, 2).Value = "Jami qo'ng'iroqlar";
        worksheet.Cell(1, 3).Value = "Muvaffaqiyatli";
        worksheet.Cell(1, 4).Value = "Bekor qilingan";
        worksheet.Cell(1, 5).Value = "Javobsiz";
        worksheet.Cell(1, 6).Value = "Band";
        worksheet.Cell(1, 7).Value = "Oxirgi qo'ng'iroq";
        
        // Header style
        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(59, 130, 246);
        headerRow.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
        
        // Data
        int row = 2;
        foreach (var item in FilteredRecords)
        {
            worksheet.Cell(row, 1).Value = item.PhoneNumber;
            worksheet.Cell(row, 2).Value = item.TotalCalls;
            worksheet.Cell(row, 3).Value = item.SuccessfulCalls;
            worksheet.Cell(row, 4).Value = item.CancelledCalls;
            worksheet.Cell(row, 5).Value = item.NoAnswerCalls;
            worksheet.Cell(row, 6).Value = item.BusyCalls;
            worksheet.Cell(row, 7).Value = item.LastCallDate;
            row++;
        }
        
        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
        
        return workbook;
    }
}
