using System.Collections.ObjectModel;
using ReactiveUI;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.Services;
using OptiVis.UI.Shared.i18n;
using ClosedXML.Excel;

namespace OptiVis.UI.Shared.ViewModels;

public class OperatorDetailViewModel : LocalizedViewModelBase
{
    private readonly IApiService _apiService;
    private readonly Action _onBack;

    private OperatorStats? _operator;
    public OperatorStats? Operator
    {
        get => _operator;
        set => this.RaiseAndSetIfChanged(ref _operator, value);
    }

    private TimeSpan _todayTalkTime;
    public TimeSpan TodayTalkTime
    {
        get => _todayTalkTime;
        set => this.RaiseAndSetIfChanged(ref _todayTalkTime, value);
    }

    private double _avgResponseTime;
    public double AvgResponseTime
    {
        get => _avgResponseTime;
        set => this.RaiseAndSetIfChanged(ref _avgResponseTime, value);
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

    public ObservableCollection<OperatorCallItem> RecentCalls { get; } = new();

    private ISeries[] _trendSeries = Array.Empty<ISeries>();
    public ISeries[] TrendSeries
    {
        get => _trendSeries;
        set => this.RaiseAndSetIfChanged(ref _trendSeries, value);
    }

    public Axis[] XAxes { get; } = new[]
    {
        new Axis
        {
            Labels = Array.Empty<string>(),
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(SKColors.Gray)
        }
    };

    public Axis[] YAxes { get; } = new[]
    {
        new Axis
        {
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(SKColors.Gray),
            MinLimit = 0
        }
    };

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public override string NavTitle => Translations.Get("OperatorProfile");

    public OperatorDetailViewModel(IApiService apiService, OperatorStats operatorStats, Action onBack)
    {
        _apiService = apiService;
        _onBack = onBack;
        Operator = operatorStats;

        _ = LoadDataAsync();
    }

    public void SetToday() => SetRange(DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));
    public void SetYesterday() => SetRange(DateTime.Today.AddDays(-1), DateTime.Today.AddSeconds(-1));
    public void SetWeek() => SetRange(DateTime.Today.AddDays(-7), DateTime.Today.AddDays(1).AddSeconds(-1));
    public void SetMonth() => SetRange(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1).AddSeconds(-1));

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
        if (Operator == null) return;
        IsLoading = true;

        var from = FromDate?.DateTime ?? DateTime.Today;
        var to = ToDate?.DateTime ?? DateTime.Today.AddDays(1).AddSeconds(-1);

        try
        {
            var calls = await _apiService.GetOperatorCallsAsync(Operator.Extension, from, to);

            RecentCalls.Clear();
            if (calls != null)
            {
                foreach (var call in calls.Take(100))
                {
                    RecentCalls.Add(call);
                }
            }

            CalculateMetrics(calls);
            UpdateTrendChart(calls, from, to);
        }
        catch
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CalculateMetrics(List<OperatorCallItem>? calls)
    {
        if (calls == null || calls.Count == 0)
        {
            TodayTalkTime = TimeSpan.Zero;
            AvgResponseTime = 0;
            return;
        }

        var totalSeconds = calls.Sum(c => c.BillSec);
        TodayTalkTime = TimeSpan.FromSeconds(totalSeconds);
        AvgResponseTime = calls.Count > 0 ? (double)totalSeconds / calls.Count / 60.0 : 0;
    }

    private void UpdateTrendChart(List<OperatorCallItem>? calls, DateTime from, DateTime to)
    {
        if (calls == null || calls.Count == 0)
        {
            TrendSeries = Array.Empty<ISeries>();
            XAxes[0].Labels = Array.Empty<string>();
            return;
        }

        var span = to - from;

        if (span.TotalHours <= 24)
        {
            var grouped = calls
                .GroupBy(c => c.CallDate.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            var allHours = Enumerable.Range(0, 24).ToList();
            var values = allHours.Select(h => grouped.GetValueOrDefault(h, 0)).ToArray();

            TrendSeries = new ISeries[]
            {
                new LineSeries<int>
                {
                    Name = Translations.Get("Calls"),
                    Values = values,
                    Stroke = new SolidColorPaint(SKColor.Parse("#3B82F6")) { StrokeThickness = 3 },
                    Fill = new SolidColorPaint(SKColor.Parse("#3B82F6").WithAlpha(40)),
                    GeometrySize = 0,
                    LineSmoothness = 1
                }
            };

            XAxes[0].Labels = allHours.Select(h => $"{h:D2}:00").ToArray();
        }
        else if (span.TotalDays <= 60)
        {
            var grouped = calls
                .GroupBy(c => c.CallDate.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var allDates = new List<DateTime>();
            for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
            {
                allDates.Add(date);
            }

            var values = allDates.Select(d => grouped.GetValueOrDefault(d, 0)).ToArray();

            TrendSeries = new ISeries[]
            {
                new LineSeries<int>
                {
                    Name = Translations.Get("Calls"),
                    Values = values,
                    Stroke = new SolidColorPaint(SKColor.Parse("#3B82F6")) { StrokeThickness = 3 },
                    Fill = new SolidColorPaint(SKColor.Parse("#3B82F6").WithAlpha(40)),
                    GeometrySize = 0,
                    LineSmoothness = 1
                }
            };

            XAxes[0].Labels = allDates.Select((d, i) =>
            {
                var labelInterval = allDates.Count > 45 ? 7 : (allDates.Count > 21 ? 3 : (allDates.Count > 7 ? 2 : 1));
                return i % labelInterval == 0 ? d.ToString("dd.MM") : string.Empty;
            }).ToArray();
        }
        else
        {
            var grouped = calls
                .GroupBy(c => new DateTime(c.CallDate.Year, c.CallDate.Month, 1))
                .ToDictionary(g => g.Key, g => g.Count());

            var allMonths = new List<DateTime>();
            for (var month = new DateTime(from.Year, from.Month, 1);
                 month <= new DateTime(to.Year, to.Month, 1);
                 month = month.AddMonths(1))
            {
                allMonths.Add(month);
            }

            var values = allMonths.Select(m => grouped.GetValueOrDefault(m, 0)).ToArray();

            TrendSeries = new ISeries[]
            {
                new LineSeries<int>
                {
                    Name = Translations.Get("Calls"),
                    Values = values,
                    Stroke = new SolidColorPaint(SKColor.Parse("#3B82F6")) { StrokeThickness = 3 },
                    Fill = new SolidColorPaint(SKColor.Parse("#3B82F6").WithAlpha(40)),
                    GeometrySize = 0,
                    LineSmoothness = 1
                }
            };

            XAxes[0].Labels = allMonths.Select(m => m.ToString("MMM yy")).ToArray();
        }
    }

    public void ExportToExcel(string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Operator");

        worksheet.Cell(1, 1).Value = $"Operator: {Operator?.Name}";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(2, 1).Value = $"Extension: {Operator?.Extension}";
        worksheet.Cell(3, 1).Value = $"Davr: {FromDate?.DateTime:dd.MM.yyyy} - {ToDate?.DateTime:dd.MM.yyyy}";

        var row = 5;
        worksheet.Cell(row, 1).Value = "Statistika";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        worksheet.Cell(row, 1).Value = "Jami suhbat vaqti";
        worksheet.Cell(row, 2).Value = TodayTalkTime.ToString(@"hh\:mm\:ss");
        row++;

        worksheet.Cell(row, 1).Value = "O'rtacha vaqt (min)";
        worksheet.Cell(row, 2).Value = AvgResponseTime.ToString("F1");
        row += 2;

        worksheet.Cell(row, 1).Value = "Sana";
        worksheet.Cell(row, 2).Value = "Yo'nalish";
        worksheet.Cell(row, 3).Value = "Raqam";
        worksheet.Cell(row, 4).Value = "Holat";
        worksheet.Cell(row, 5).Value = "Davomiylik";
        worksheet.Cell(row, 6).Value = "Suhbat";
        worksheet.Row(row).Style.Font.Bold = true;
        row++;

        foreach (var call in RecentCalls)
        {
            worksheet.Cell(row, 1).Value = call.CallDate.ToString("dd.MM.yyyy HH:mm");
            worksheet.Cell(row, 2).Value = call.IsIncoming ? "Kiruvchi" : "Chiquvchi";
            worksheet.Cell(row, 3).Value = call.PhoneNumber;
            worksheet.Cell(row, 4).Value = call.Disposition;
            worksheet.Cell(row, 5).Value = call.DurationFormatted;
            worksheet.Cell(row, 6).Value = call.BillSecFormatted;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    public void GoBack()
    {
        _onBack?.Invoke();
    }
}
