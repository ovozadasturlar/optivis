using MediatR;
using Microsoft.AspNetCore.Mvc;
using OptiVis.Application.DTOs;
using OptiVis.Application.Features.Dashboard.Queries;

namespace OptiVis.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.Today;
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetDashboardSummaryQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("hourly")]
    public async Task<ActionResult<IReadOnlyList<HourlyCallsDto>>> GetHourlyCalls(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.Today;
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetHourlyCallsQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<TrendPointDto>>> GetTrend(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.Today;
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetCallTrendQuery(fromDate, toDate));
        return Ok(result);
    }
}
