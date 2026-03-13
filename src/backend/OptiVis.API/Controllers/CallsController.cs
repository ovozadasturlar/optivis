using MediatR;
using Microsoft.AspNetCore.Mvc;
using OptiVis.Application.DTOs;
using OptiVis.Application.Features.CallRecords.Queries;

namespace OptiVis.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CallsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CallsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<CallSearchResultDto>>> Search(
        [FromQuery] string number,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (string.IsNullOrWhiteSpace(number))
            return BadRequest("Number is required");

        var fromDate = from ?? DateTime.Today.AddDays(-30);
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new SearchCallsQuery(number, fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<RecentCallDto>>> GetRecent([FromQuery] int count = 10)
    {
        var result = await _mediator.Send(new GetRecentCallsQuery(count));
        return Ok(result);
    }

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<CallLogDto>>> GetLogs(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? number)
    {
        var fromDate = from ?? DateTime.Today.AddDays(-30);
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetCallLogsQuery(fromDate, toDate, number));
        return Ok(result);
    }

    [HttpGet("phone-stats")]
    public async Task<ActionResult<IReadOnlyList<PhoneNumberStatsDto>>> GetPhoneStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.Today.AddDays(-30);
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetPhoneNumberStatsQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("phone-details")]
    public async Task<ActionResult<IReadOnlyList<PhoneNumberCallDetailDto>>> GetPhoneDetails(
        [FromQuery] string number,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (string.IsNullOrWhiteSpace(number))
            return BadRequest("Number is required");

        var fromDate = from ?? DateTime.Today.AddDays(-30);
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetPhoneNumberDetailsQuery(number, fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("raw")]
    public async Task<ActionResult<IReadOnlyList<RawCdrDto>>> GetRawCdr(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 1000)
    {
        var fromDate = from ?? DateTime.Today.AddDays(-7);
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetRawCdrQuery(fromDate, toDate, limit));
        return Ok(result);
    }
}
