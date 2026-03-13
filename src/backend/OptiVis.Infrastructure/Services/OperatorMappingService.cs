using Microsoft.Extensions.Configuration;
using OptiVis.Application.Interfaces;

namespace OptiVis.Infrastructure.Services;

public class OperatorMappingService : IOperatorMappingService
{
    private readonly HashSet<string> _primaryExtensions;
    private readonly Dictionary<string, string> _forwardMapping;

    public OperatorMappingService(IConfiguration configuration)
    {
        var section = configuration.GetSection("OperatorMapping");
        
        _primaryExtensions = section.GetSection("PrimaryExtensions")
            .Get<string[]>()?.ToHashSet() ?? new HashSet<string> { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10" };

        _forwardMapping = section.GetSection("ForwardMapping")
            .Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();
    }

    public IReadOnlySet<string> PrimaryExtensions => _primaryExtensions;

    public string GetPrimaryExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return string.Empty;
        
        var normalized = NormalizeExtension(extension);
        
        if (_primaryExtensions.Contains(normalized))
            return normalized;

        if (_forwardMapping.TryGetValue(extension, out var primary))
            return NormalizeExtension(primary);

        if (_forwardMapping.TryGetValue(normalized, out primary))
            return NormalizeExtension(primary);

        return string.Empty;
    }

    public bool IsPrimaryExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return false;
        return _primaryExtensions.Contains(NormalizeExtension(extension));
    }

    private static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext)) return string.Empty;
        var trimmed = ext.TrimStart('0');
        if (trimmed.Length == 0) return "01";
        if (trimmed.Length == 1) return "0" + trimmed;
        return trimmed;
    }
}
