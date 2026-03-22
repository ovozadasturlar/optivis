using System.Collections.ObjectModel;
using ReactiveUI;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using OptiVis.UI.Shared.Models;
using OptiVis.UI.Shared.Services;
using OptiVis.UI.Shared.i18n;
using Avalonia.Threading;
using ClosedXML.Excel;

namespace OptiVis.UI.Shared.ViewModels;

public class DashboardViewModel : LocalizedViewModelBase
{
	private readonly IApiService _apiService;
	private ISignalRService _signalRService;

	// Handlerlarni saqlash (obunani bekor qilish uchun kerak)
	private Action<CallRecord>? _newCallHandler;
	private Action<DashboardSummary>? _dashboardUpdateHandler;

	public override string NavTitle => Translations.Get("Dashboard");

	#region Properties

	private DashboardSummary _summary = new();
	public DashboardSummary Summary
	{
		get => _summary;
		set
		{
			this.RaiseAndSetIfChanged(ref _summary, value);
			this.RaisePropertyChanged(nameof(TotalAnswered));
			this.RaisePropertyChanged(nameof(TotalNotAnswered));
		}
	}

	private bool _isLoading;
	public bool IsLoading
	{
		get => _isLoading;
		set => this.RaiseAndSetIfChanged(ref _isLoading, value);
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

	public ObservableCollection<CallRecord> RecentCalls { get; } = new();

	// Chart Series
	private ISeries[] _trendSeries = [];
	public ISeries[] TrendSeries { get => _trendSeries; set => this.RaiseAndSetIfChanged(ref _trendSeries, value); }

	private ISeries[] _overallSeries = [];
	public ISeries[] OverallSeries { get => _overallSeries; set => this.RaiseAndSetIfChanged(ref _overallSeries, value); }

	private ISeries[] _outgoingSeries = [];
	public ISeries[] OutgoingSeries { get => _outgoingSeries; set => this.RaiseAndSetIfChanged(ref _outgoingSeries, value); }

	private ISeries[] _incomingSeries = [];
	public ISeries[] IncomingSeries { get => _incomingSeries; set => this.RaiseAndSetIfChanged(ref _incomingSeries, value); }

	public int TotalAnswered => Summary.AnsweredIncoming + Summary.AnsweredOutgoing;
	public int TotalNotAnswered => Summary.MissedIncoming + Summary.MissedOutgoing + Summary.Abandoned;

	// Chart Axes
	public Axis[] XAxes { get; } = {
		new Axis { Labels = Array.Empty<string>(), TextSize = 12, LabelsPaint = new SolidColorPaint(SKColors.Gray) }
	};

	public Axis[] YAxes { get; } = {
		new Axis { TextSize = 12, LabelsPaint = new SolidColorPaint(SKColors.Gray), MinLimit = 0 }
	};

	#endregion

	public DashboardViewModel(IApiService apiService, ISignalRService signalRService)
	{
		_apiService = apiService;
		_signalRService = signalRService;

		// 1. Birinchi navbatda SignalR ga ulanish (jonli ma'lumotlar uchun)
		SubscribeToSignalR();

		// 2. Eskiroq ma'lumotlarni API orqali yuklab olish
		_ = LoadDataAsync();
	}

	private void SubscribeToSignalR()
	{
		UnsubscribeFromSignalR();

		_newCallHandler = call =>
		{
			var timestamp = DateTime.Now.ToString("HH:mm:ss");
			Console.WriteLine($"[{timestamp}] [Dashboard] ✅ New call received: {call.Src} -> {call.Dst}");
			System.Diagnostics.Debug.WriteLine($"[{timestamp}] [Dashboard] New call received: {call.Src} -> {call.Dst}");
			
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					var from = FromDate?.DateTime ?? DateTime.Today;
					var to = ToDate?.DateTime ?? DateTime.Today.AddDays(1).AddSeconds(-1);

					Console.WriteLine($"[{timestamp}] [Dashboard] Date filter: {from:yyyy-MM-dd HH:mm} to {to:yyyy-MM-dd HH:mm}");
					Console.WriteLine($"[{timestamp}] [Dashboard] Call date: {call.CallDate:yyyy-MM-dd HH:mm}");

					if (call.CallDate >= from && call.CallDate <= to)
					{
						Console.WriteLine($"[{timestamp}] [Dashboard] Call is within range");
						
						if (!RecentCalls.Any(c => c.Sequence == call.Sequence))
						{
							RecentCalls.Insert(0, call);
							if (RecentCalls.Count > 8)
								RecentCalls.RemoveAt(RecentCalls.Count - 1);
							
							Console.WriteLine($"[{timestamp}] [Dashboard] ✅ Added to recent calls. Total: {RecentCalls.Count}");
							System.Diagnostics.Debug.WriteLine($"[{timestamp}] [Dashboard] Added to recent calls");
						}
						else
						{
							Console.WriteLine($"[{timestamp}] [Dashboard] Call already exists in recent calls");
						}

						_ = Task.Run(async () =>
						{
							try
							{
								Console.WriteLine($"[{timestamp}] [Dashboard] Fetching updated data from API...");
								
								var updatedSummary = await _apiService.GetDashboardSummaryAsync(from, to);
								var updatedTrend = await _apiService.GetCallTrendAsync(from, to);

								Dispatcher.UIThread.Post(() =>
								{
									if (updatedSummary != null)
									{
										Summary = updatedSummary;
										UpdateStatusChart();
										Console.WriteLine($"[{timestamp}] [Dashboard] ✅ Summary updated: {updatedSummary.TotalCalls} calls");
									}

									if (updatedTrend is { Count: > 0 })
									{
										TrendSeries =
										[
											new LineSeries<int>
											{
												Name = Translations.Get("Inbound"),
												Values = updatedTrend.Select(p => p.Inbound).ToArray(),
												Stroke = new SolidColorPaint(SKColor.Parse("#3B82F6")) { StrokeThickness = 3 },
												Fill = new SolidColorPaint(SKColor.Parse("#3B82F6").WithAlpha(40)),
												GeometrySize = 0,
												LineSmoothness = 1
											},
											new LineSeries<int>
											{
												Name = Translations.Get("Outbound"),
												Values = updatedTrend.Select(p => p.Outbound).ToArray(),
												Stroke = new SolidColorPaint(SKColor.Parse("#10B981")) { StrokeThickness = 3 },
												Fill = new SolidColorPaint(SKColor.Parse("#10B981").WithAlpha(40)),
												GeometrySize = 0,
												LineSmoothness = 1
											}
										];
										XAxes[0].Labels = updatedTrend.Select(p => p.Label).ToArray();
										Console.WriteLine($"[{timestamp}] [Dashboard] ✅ Trend chart updated");
									}
								});
							}
							catch (Exception ex)
							{
								Console.WriteLine($"[{timestamp}] [Dashboard] ❌ Error fetching updated data: {ex.Message}");
							}
						});
					}
					else
					{
						Console.WriteLine($"[{timestamp}] [Dashboard] ⚠️ Call is outside date range, skipping");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[{timestamp}] [Dashboard] ❌ Error in handler: {ex.Message}");
				}
			});
		};

		_dashboardUpdateHandler = summary =>
		{
			var timestamp = DateTime.Now.ToString("HH:mm:ss");
			Console.WriteLine($"[{timestamp}] [Dashboard] ReceiveDashboardUpdate event (ignored, using API refresh)");
			System.Diagnostics.Debug.WriteLine($"[{timestamp}] [Dashboard] Summary update ignored - using API refresh instead");
		};

		_signalRService.OnNewCall += _newCallHandler;
		_signalRService.OnDashboardUpdate += _dashboardUpdateHandler;
		
		var timestamp2 = DateTime.Now.ToString("HH:mm:ss");
		Console.WriteLine($"[{timestamp2}] [Dashboard] ✅ SignalR handlers subscribed");
		System.Diagnostics.Debug.WriteLine($"[{timestamp2}] [Dashboard] SignalR handlers subscribed");
	}

	private void UnsubscribeFromSignalR()
	{
		if (_newCallHandler != null) _signalRService.OnNewCall -= _newCallHandler;
		if (_dashboardUpdateHandler != null) _signalRService.OnDashboardUpdate -= _dashboardUpdateHandler;
	}

	public void SetSignalRService(ISignalRService newService)
	{
		UnsubscribeFromSignalR();
		_signalRService = newService;
		SubscribeToSignalR();
	}

	protected override void OnLanguageChanged()
	{
		base.OnLanguageChanged();
		UpdateStatusChart();
		UpdateTrendNames();
	}

	private void UpdateTrendNames()
	{
		if (TrendSeries is { Length: >= 2 })
		{
			TrendSeries[0].Name = Translations.Get("Inbound");
			TrendSeries[1].Name = Translations.Get("Outbound");
		}
	}

	public void SetToday() => SetRange(DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));
	public void SetYesterday() => SetRange(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1).AddHours(23).AddMinutes(59));
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
		if (IsLoading) return;
		IsLoading = true;

		var from = FromDate?.DateTime ?? DateTime.Today;
		var to = ToDate?.DateTime ?? DateTime.Today.AddDays(1).AddSeconds(-1);

		try
		{
			var summaryTask = _apiService.GetDashboardSummaryAsync(from, to);
			var trendTask = _apiService.GetCallTrendAsync(from, to);
			var recentTask = _apiService.GetRecentCallsAsync(8);

			await Task.WhenAll(summaryTask, trendTask, recentTask);

			Summary = await summaryTask ?? new DashboardSummary();

			var trend = await trendTask;
			if (trend is { Count: > 0 })
			{
				TrendSeries =
				[
					new LineSeries<int>
					{
						Name = Translations.Get("Inbound"),
						Values = trend.Select(p => p.Inbound).ToArray(),
						Stroke = new SolidColorPaint(SKColor.Parse("#3B82F6")) { StrokeThickness = 3 },
						Fill = new SolidColorPaint(SKColor.Parse("#3B82F6").WithAlpha(40)),
						GeometrySize = 0,
						LineSmoothness = 1
					},
					new LineSeries<int>
					{
						Name = Translations.Get("Outbound"),
						Values = trend.Select(p => p.Outbound).ToArray(),
						Stroke = new SolidColorPaint(SKColor.Parse("#10B981")) { StrokeThickness = 3 },
						Fill = new SolidColorPaint(SKColor.Parse("#10B981").WithAlpha(40)),
						GeometrySize = 0,
						LineSmoothness = 1
					}
				];
				XAxes[0].Labels = trend.Select(p => p.Label).ToArray();
			}

			var recent = await recentTask;
			RecentCalls.Clear();
			if (recent != null)
			{
				foreach (var call in recent) RecentCalls.Add(call);
			}

			UpdateStatusChart();
		}
		catch (Exception ex)
		{
			// Log xatolikni bu yerda ko'rishingiz mumkin
			System.Diagnostics.Debug.WriteLine($"Dashboard yuklashda xato: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	public void ExportXlsx(string filePath)
	{
		using var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Dashboard");

		worksheet.Cell(1, 1).Value = "Umumiy statistika";
		worksheet.Cell(1, 1).Style.Font.Bold = true;
		worksheet.Cell(1, 1).Style.Font.FontSize = 14;

		var row = 3;
		worksheet.Cell(row, 1).Value = "Ko'rsatkich";
		worksheet.Cell(row, 2).Value = "Qiymat";
		worksheet.Row(row).Style.Font.Bold = true;
		row++;

		worksheet.Cell(row++, 1).Value = "Jami qo'ng'iroqlar";
		worksheet.Cell(row - 1, 2).Value = Summary.TotalCalls;
		worksheet.Cell(row++, 1).Value = "Jami bog'lanildi";
		worksheet.Cell(row - 1, 2).Value = Summary.TotalAnswered;
		worksheet.Cell(row++, 1).Value = "Jami bog'lanilmadi";
		worksheet.Cell(row - 1, 2).Value = Summary.TotalNotAnswered;

		row += 2;
		worksheet.Cell(row++, 1).Value = "KIRUVCHI QO'NG'IROQLAR";
		worksheet.Cell(row - 1, 1).Style.Font.Bold = true;

		worksheet.Cell(row++, 1).Value = "Jami kiruvchi";
		worksheet.Cell(row - 1, 2).Value = Summary.TotalIncoming;
		worksheet.Cell(row++, 1).Value = "Javob berildi";
		worksheet.Cell(row - 1, 2).Value = Summary.AnsweredIncoming;
		worksheet.Cell(row++, 1).Value = "O'tkazib yuborildi";
		worksheet.Cell(row - 1, 2).Value = Summary.MissedIncoming;

		worksheet.Columns().AdjustToContents();
		workbook.SaveAs(filePath);
	}

	private void UpdateStatusChart()
	{
		OverallSeries = new ISeries[]
		{
			new PieSeries<int> { Name = "Chiquvchi", Values = new[] { Summary.TotalOutgoing }, Fill = new SolidColorPaint(SKColor.Parse("#8B5CF6")) },
			new PieSeries<int> { Name = "Kiruvchi", Values = new[] { Summary.TotalIncoming }, Fill = new SolidColorPaint(SKColor.Parse("#3B82F6")) }
		};

		OutgoingSeries = new ISeries[]
		{
			new PieSeries<int> { Name = "Javob olindi", Values = new[] { Summary.AnsweredOutgoing }, Fill = new SolidColorPaint(SKColor.Parse("#10B981")) },
			new PieSeries<int> { Name = "Javob berilmadi", Values = new[] { Summary.MissedOutgoing }, Fill = new SolidColorPaint(SKColor.Parse("#F97316")) }
		};

		IncomingSeries = new ISeries[]
		{
			new PieSeries<int> { Name = "Javob berildi", Values = new[] { Summary.AnsweredIncoming }, Fill = new SolidColorPaint(SKColor.Parse("#10B981")) },
			new PieSeries<int> { Name = "O'tkazib yuborildi", Values = new[] { Summary.MissedIncoming }, Fill = new SolidColorPaint(SKColor.Parse("#F59E0B")) },
			new PieSeries<int> { Name = "Bekor qilindi", Values = new[] { Summary.Abandoned }, Fill = new SolidColorPaint(SKColor.Parse("#EC4899")) }
		};
	}
}