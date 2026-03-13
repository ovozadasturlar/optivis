using MediatR;
using Microsoft.AspNetCore.Mvc;
using OptiVis.Application.DTOs;
using OptiVis.Application.Features.CallRecords.Queries;
using OptiVis.Application.Features.Operators.Queries;
using OptiVis.Domain.Interfaces;

namespace OptiVis.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OperatorsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICallRecordRepository _repository;

    public OperatorsController(IMediator mediator, ICallRecordRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<IReadOnlyList<OperatorStatsDto>>> GetStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.Today;
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetOperatorStatsQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("active-today")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetActiveToday(CancellationToken ct)
    {
        var extensions = await _repository.GetActiveExtensionsAsync(DateTime.Today, DateTime.Now, ct);
        return Ok(extensions);
    }

    [HttpGet("{extension}/calls")]
    public async Task<ActionResult<IReadOnlyList<OperatorCallDto>>> GetOperatorCalls(
        string extension,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.Today;
        var toDate = to ?? DateTime.Now;

        var result = await _mediator.Send(new GetOperatorCallsQuery(extension, fromDate, toDate));
        return Ok(result);
    }
}
