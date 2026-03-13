using Microsoft.EntityFrameworkCore;
using OptiVis.Domain.Entities;
using OptiVis.Domain.Interfaces;
using OptiVis.Infrastructure.Persistence;
using System.Text.RegularExpressions;

namespace OptiVis.Infrastructure.Repositories;

public class CallRecordRepository : ICallRecordRepository
{
    private readonly CdrDbContext _context;

    public CallRecordRepository(CdrDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CallRecord>> GetCallsAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await _context.CallRecords
            .AsNoTracking()
            .Where(c => c.CallDate >= from && c.CallDate <= to)
            .OrderByDescending(c => c.CallDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CallRecord>> GetCallsSinceAsync(int lastSequence, CancellationToken ct = default)
    {
        return await _context.CallRecords
            .AsNoTracking()
            .Where(c => c.Sequence > lastSequence)
            .OrderBy(c => c.Sequence)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CallRecord>> SearchByNumberAsync(string number, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var cleanNumber = number.Replace("+", "").Replace(" ", "");

        return await _context.CallRecords
            .AsNoTracking()
            .Where(c => c.CallDate >= from && c.CallDate <= to &&
                       (c.Src.Contains(cleanNumber) || c.Dst.Contains(cleanNumber)))
            .OrderByDescending(c => c.CallDate)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CallRecord>> GetLogsAsync(DateTime from, DateTime to, string? number = null, CancellationToken ct = default)
    {
        var query = _context.CallRecords
            .AsNoTracking()
            .Where(c => c.CallDate >= from && c.CallDate <= to);

        if (!string.IsNullOrWhiteSpace(number))
        {
            var cleanNumber = number.Replace("+", "").Replace(" ", "");
            query = query.Where(c => c.Src.Contains(cleanNumber) || c.Dst.Contains(cleanNumber));
        }

        return await query
            .OrderByDescending(c => c.CallDate)
            .Take(2000)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CallRecord>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        return await _context.CallRecords
            .AsNoTracking()
            .OrderByDescending(c => c.CallDate)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<int> GetLastSequenceAsync(CancellationToken ct = default)
    {
        var maxSeq = await _context.CallRecords
            .AsNoTracking()
            .MaxAsync(c => (int?)c.Sequence, ct);
        return maxSeq ?? 0;
    }

    public async Task<IReadOnlyList<string>> GetActiveExtensionsAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var channels = await _context.CallRecords
            .AsNoTracking()
            .Where(c => c.CallDate >= from && c.CallDate <= to
                        && c.DContext == "from-internal"
                        && c.Channel.Contains("@from-queue"))
            .Select(c => c.Channel)
            .Distinct()
            .ToListAsync(ct);

        return channels
            .Select(ch =>
            {
                var m = Regex.Match(ch, @"Local/(\d{1,5})@from-queue");
                return m.Success ? CallRecord.NormalizeExtension(m.Groups[1].Value) : null;
            })
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .Order()
            .ToList()!;
    }
}
