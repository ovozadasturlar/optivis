using System.Collections.ObjectModel;
using ReactiveUI;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.Services;
using OptiVis.UI.Shared.i18n;
using Avalonia.Threading;
using ClosedXML.Excel;

namespace OptiVis.UI.Shared.ViewModels;

public class OperatorsViewModel : LocalizedViewModelBase
{
    private readonly IApiService _apiService;
    private ISignalRService _signalRService;
    private readonly IOperatorProfileService _profileService;
    private readonly Action<OperatorStats>? _onOperatorSelected;
    private List<OperatorStats> _allOperators = new();
    private Action<List<OperatorStats>>? _operatorStatsHandler;

    public override string NavTitle => Translations.Get("Operators");

    public ObservableCollection<OperatorStats> Operators { get; } = new();

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            FilterOperators();
        }
    }

    private DateTimeOffset? _fromDate = DateTimeOffset.Now.Date;
    public DateTimeOffset? FromDate
    {
        get => _fromDate;
        set
        {
            this.RaiseAndSetIfChanged(ref _fromDate, value);
            _ = LoadDataAsync();
        }
    }

    private DateTimeOffset? _toDate = DateTimeOffset.Now.Date.AddDays(1).AddSeconds(-1);
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

    public int TotalCallsSum => Operators.Sum(o => o.TotalCalls);
    public int TotalIncomingSum => Operators.Sum(o => o.TotalIncoming);
    public int TotalOutgoingSum => Operators.Sum(o => o.TotalOutgoing);
    public int TotalAnsweredSum => Operators.Sum(o => o.TotalAnswered);
    public bool IsEmpty => !Operators.Any();

    public TimeSpan TotalTalkTimeSum =>
        TimeSpan.FromSeconds(Operators.Sum(o => o.TotalTalkTime.TotalSeconds));

    public TimeSpan TotalDurationSum =>
        TimeSpan.FromSeconds(Operators.Sum(o => o.TotalDuration.TotalSeconds));

    public double OverallAnswerRate => TotalCallsSum > 0
        ? Math.Round((double)TotalAnsweredSum / TotalCallsSum * 100, 1)
        : 0;

    public double SuccessRate => TotalCallsSum > 0
        ? Math.Round((double)TotalAnsweredSum / TotalCallsSum * 100, 1)
        : 0;

    public OperatorsViewModel(
        IApiService apiService,
        ISignalRService signalRService,
        IOperatorProfileService profileService,
        Action<OperatorStats>? onOperatorSelected = null)
    {
        _apiService = apiService;
        _signalRService = signalRService;
        _profileService = profileService;
        _onOperatorSelected = onOperatorSelected;

        SubscribeToSignalR();
    }

    private void SubscribeToSignalR()
    {
        _operatorStatsHandler = stats =>
        {
            try
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        var from = FromDate?.DateTime ?? DateTime.Today;
                        var to = ToDate?.DateTime ?? DateTime.Today.AddDays(1).AddSeconds(-1);

                        var updatedStats = await _apiService.GetOperatorStatsAsync(from, to);
                        if (updatedStats != null)
                        {
                            foreach (var op in updatedStats)
                            {
                                op.IsActiveToday = op.TotalCalls > 0;
                            }
                            _allOperators = updatedStats;
                            await RefreshCollectionAsync();
                        }
                    }
                    catch (ObjectDisposedException) { }
                });
            }
            catch (ObjectDisposedException) { }
        };
        _signalRService.OnOperatorStatsUpdate += _operatorStatsHandler;
    }

    public void SetSignalRService(ISignalRService newService)
    {
        if (_signalRService != null)
        {
            _signalRService.OnOperatorStatsUpdate -= _operatorStatsHandler;
        }
        _signalRService = newService;
        SubscribeToSignalR();
    }

    // ─── Tez sana tugmalari ───────────────────────────────────────────────────
    public void SetToday()     => SetRange(DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));
    public void SetYesterday() => SetRange(DateTime.Today.AddDays(-1), DateTime.Today.AddSeconds(-1));
    public void SetWeek()      => SetRange(DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1).AddSeconds(-1));
    public void SetMonth()     => SetRange(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1).AddSeconds(-1));

    private void SetRange(DateTime from, DateTime to)
    {
        _fromDate = from;
        _toDate = to;
        this.RaisePropertyChanged(nameof(FromDate));
        this.RaisePropertyChanged(nameof(ToDate));
        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var from = FromDate?.DateTime ?? DateTime.Today;
            var to = ToDate?.DateTime ?? DateTime.Today.AddDays(1).AddSeconds(-1);

            var stats = await _apiService.GetOperatorStatsAsync(from, to) ?? new List<OperatorStats>();

            foreach (var op in stats)
            {
                op.IsActiveToday = op.TotalCalls > 0;
            }

            _allOperators = stats;
            await RefreshCollectionAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SelectOperator(OperatorStats operatorStats)
        => _onOperatorSelected?.Invoke(operatorStats);

    public void ExportXlsx(string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Operatorlar");

        // Sarlavha
        worksheet.Cell(1, 1).Value = "Operatorlar statistikasi";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        // Jami statistika
        var row = 3;
        worksheet.Cell(row, 1).Value = "JAMI STATISTIKA";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        worksheet.Cell(row, 1).Value = "Jami qo'ng'iroqlar";
        worksheet.Cell(row++, 2).Value = TotalCallsSum;
        
        worksheet.Cell(row, 1).Value = "Jami kiruvchi";
        worksheet.Cell(row++, 2).Value = TotalIncomingSum;
        
        worksheet.Cell(row, 1).Value = "Jami chiquvchi";
        worksheet.Cell(row++, 2).Value = TotalOutgoingSum;
        
        worksheet.Cell(row, 1).Value = "Jami javob berildi";
        worksheet.Cell(row++, 2).Value = TotalAnsweredSum;
        
        worksheet.Cell(row, 1).Value = "Jami suhbat vaqti (BillSec)";
        worksheet.Cell(row++, 2).Value = TotalTalkTimeSum.ToString(@"hh\:mm\:ss");
        
        worksheet.Cell(row, 1).Value = "Jami davomiylik (Duration)";
        worksheet.Cell(row++, 2).Value = TotalDurationSum.ToString(@"hh\:mm\:ss");

        // Operatorlar jadvali
        row += 2;
        worksheet.Cell(row, 1).Value = "OPERATORLAR";
        worksheet.Cell(row++, 1).Style.Font.Bold = true;

        // Jadval sarlavhasi
        var headerRow = row++;
        worksheet.Cell(headerRow, 1).Value = "Extension";
        worksheet.Cell(headerRow, 2).Value = "Ism";
        worksheet.Cell(headerRow, 3).Value = "Jami";
        worksheet.Cell(headerRow, 4).Value = "Kiruvchi";
        worksheet.Cell(headerRow, 5).Value = "Chiquvchi";
        worksheet.Cell(headerRow, 6).Value = "Javob";
        worksheet.Cell(headerRow, 7).Value = "BillSec (Suhbat)";
        worksheet.Cell(headerRow, 8).Value = "Duration";
        worksheet.Cell(headerRow, 9).Value = "Faol bugun";
        worksheet.Row(headerRow).Style.Font.Bold = true;

        // Operatorlar ma'lumotlari
        foreach (var op in _allOperators.OrderByDescending(x => x.TotalCalls))
        {
            worksheet.Cell(row, 1).Value = op.Extension;
            worksheet.Cell(row, 2).Value = op.Name;
            worksheet.Cell(row, 3).Value = op.TotalCalls;
            worksheet.Cell(row, 4).Value = op.TotalIncoming;
            worksheet.Cell(row, 5).Value = op.TotalOutgoing;
            worksheet.Cell(row, 6).Value = op.TotalAnswered;
            worksheet.Cell(row, 7).Value = op.TalkTimeFormatted;
            worksheet.Cell(row, 8).Value = op.DurationFormatted;
            worksheet.Cell(row, 9).Value = op.IsActiveToday ? "Ha" : "Yo'q";
            row++;
        }

        // Ustunlar kengligini avtomatik o'rnatish
        worksheet.Columns().AdjustToContents();
        
        workbook.SaveAs(filePath);
    }

    private async Task RefreshCollectionAsync()
    {
        var snapshot = _allOperators
            .OrderByDescending(o => o.TotalCalls)
            .ThenByDescending(o => o.TotalTalkTime)
            .ToList();

        Operators.Clear();
        foreach (var op in snapshot)
        {
            var profile = await _profileService.GetProfileAsync(op.Extension);

            Operators.Add(new OperatorStats
            {
                Extension        = op.Extension,
                Name             = profile?.Name ?? op.Name,
                MobileNumber     = profile?.MobileNumber ?? op.MobileNumber,
                AvatarColor      = LocalOperatorProfileService.GetAvatarColor(op.Extension),
                TotalIncoming    = op.TotalIncoming,
                AnsweredIncoming = op.AnsweredIncoming,
                MissedIncoming   = op.MissedIncoming,
                TotalOutgoing    = op.TotalOutgoing,
                AnsweredOutgoing = op.AnsweredOutgoing,
                TotalTalkTime    = op.TotalTalkTime,
                AvgTalkTime      = op.AvgTalkTime,
                TotalDuration    = op.TotalDuration,
                IncomingAnswerRate = op.IncomingAnswerRate,
                TotalCalls       = op.TotalCalls,
                TotalAnswered    = op.TotalAnswered,
                IsActiveToday    = op.IsActiveToday
            });
        }

        this.RaisePropertyChanged(nameof(TotalCallsSum));
        this.RaisePropertyChanged(nameof(TotalIncomingSum));
        this.RaisePropertyChanged(nameof(TotalOutgoingSum));
        this.RaisePropertyChanged(nameof(TotalAnsweredSum));
        this.RaisePropertyChanged(nameof(TotalTalkTimeSum));
        this.RaisePropertyChanged(nameof(TotalDurationSum));
        this.RaisePropertyChanged(nameof(OverallAnswerRate));
        this.RaisePropertyChanged(nameof(SuccessRate));
        this.RaisePropertyChanged(nameof(IsEmpty));
    }

    private void FilterOperators()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var filtered = string.IsNullOrWhiteSpace(_searchText)
                ? _allOperators
                : _allOperators.Where(o =>
                    (o.Name?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    o.Extension.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

            Operators.Clear();
            foreach (var op in filtered.OrderByDescending(o => o.TotalCalls))
            {
                Operators.Add(op);
            }
            this.RaisePropertyChanged(nameof(IsEmpty));
        });
    }
}
