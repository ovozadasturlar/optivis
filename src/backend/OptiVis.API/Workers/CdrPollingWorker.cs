using MediatR;
using Microsoft.AspNetCore.SignalR;
using OptiVis.Application.DTOs;
using OptiVis.Application.Features.Dashboard.Queries;
using OptiVis.Application.Features.Operators.Queries;
using OptiVis.Application.Interfaces;
using OptiVis.API.Hubs;
using OptiVis.Domain.Enums;
using OptiVis.Domain.Interfaces;

namespace OptiVis.API.Workers;

public class CdrPollingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<DashboardHub, IDashboardHubClient> _hubContext;
    private readonly ILogger<CdrPollingWorker> _logger;
    private readonly int _intervalSeconds;
    private int _lastSequence;
    private bool _isConnected;

    public CdrPollingWorker(
        IServiceProvider serviceProvider,
        IHubContext<DashboardHub, IDashboardHubClient> hubContext,
        ILogger<CdrPollingWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
        _intervalSeconds = configuration.GetValue<int>("CdrPolling:IntervalSeconds", 10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CDR Polling Worker started. Interval: {Interval}s", _intervalSeconds);

        await InitializeSequenceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await PollForNewRecordsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    _logger.LogWarning(ex, "Database connection lost. Will retry...");
                    _isConnected = false;
                }
            }
        }
    }

    private async Task InitializeSequenceAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ICallRecordRepository>();
                _lastSequence = await repository.GetLastSequenceAsync(stoppingToken);
                _logger.LogInformation("Connected to database. Initial sequence: {Sequence}", _lastSequence);
                _isConnected = true;
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Database not available: {Message}. Retrying in {Interval}s...",
                    ex.Message, _intervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
        }
    }

    private async Task PollForNewRecordsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICallRecordRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var newRecords = await repository.GetCallsSinceAsync(_lastSequence, stoppingToken);

        if (!_isConnected)
        {
            _logger.LogInformation("Database connection restored");
            _isConnected = true;
        }

        if (newRecords.Count > 0)
        {
            _logger.LogInformation("Found {Count} new CDR records", newRecords.Count);
            _lastSequence = newRecords.Max(r => r.Sequence);

            var incomingLegs = newRecords
                .Where(r => r.DContext == "ext-queues" && r.Channel.Contains("_in-"))
                .GroupBy(r => r.LinkedId)
                .Select(g => g.OrderBy(r => r.Sequence).First())
                .ToList();

            foreach (var record in incomingLegs)
            {
                var answeredRow = newRecords
                    .Where(r => r.LinkedId == record.LinkedId
                                && r.Disposition == CallDisposition.Answered
                                && r.BillSec > 0)
                    .OrderByDescending(r => r.BillSec)
                    .FirstOrDefault();

                var representative = answeredRow ?? record;

                var dto = new CallRecordDto(
                    representative.Sequence,
                    representative.CallDate,
                    representative.Src,
                    representative.OperatorExtension,
                    GetExtLabel(representative.OperatorExtension),
                    representative.Duration,
                    representative.BillSec,
                    representative.Disposition,
                    true,
                    representative.RecordingFile
                );

                await _hubContext.Clients.All.ReceiveNewCall(dto);
            }

            var today = DateTime.Today;
            var now = DateTime.Now;

            var summary = await mediator.Send(new GetDashboardSummaryQuery(today, now), stoppingToken);
            await _hubContext.Clients.All.ReceiveDashboardUpdate(summary);

            var operatorStats = await mediator.Send(new GetOperatorStatsQuery(today, now), stoppingToken);
            await _hubContext.Clients.All.ReceiveOperatorStatsUpdate(operatorStats);
        }
    }

    private static string GetExtLabel(string ext) =>
        string.IsNullOrEmpty(ext) ? "" : $"Ext {ext}";
}
