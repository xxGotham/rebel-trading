using System.Reflection;
using Rebel.Core.Abstractions;

namespace Rebel.Infrastructure.Universe;

/// <summary>
/// Reads the V1 universe from an embedded CSV.
/// <para>
/// A static list is the right call for V1 and it has a real cost worth naming:
/// it is survivorship-biased. Every name in it is a company that still exists
/// and still trades today, so any backtest run over this universe will look
/// better than reality. When backtesting arrives, this needs to become a
/// point-in-time constituent list.
/// </para>
/// </summary>
public sealed class CsvUniverseProvider : IUniverseProvider
{
    private const string ResourceSuffix = "Universe.universe.csv";

    private readonly Lazy<IReadOnlyList<UniverseEntry>> _entries;

    public CsvUniverseProvider(string? csvPath = null) =>
        _entries = new Lazy<IReadOnlyList<UniverseEntry>>(() => Load(csvPath));

    public Task<IReadOnlyList<UniverseEntry>> GetUniverseAsync(CancellationToken ct = default) =>
        Task.FromResult(_entries.Value);

    private static IReadOnlyList<UniverseEntry> Load(string? csvPath)
    {
        var text = csvPath is not null && File.Exists(csvPath)
            ? File.ReadAllText(csvPath)
            : ReadEmbedded();

        return Parse(text);
    }

    private static string ReadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded universe '{ResourceSuffix}' not found.");

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static IReadOnlyList<UniverseEntry> Parse(string csv)
    {
        var entries = new List<UniverseEntry>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in lines.Skip(1))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var parts = line.Split(',');
            if (parts.Length < 6) continue;

            entries.Add(new UniverseEntry(
                Symbol: parts[0].Trim().ToUpperInvariant(),
                Name: parts[1].Trim(),
                Sector: parts[2].Trim(),
                SectorEtf: parts[3].Trim().ToUpperInvariant(),
                Exchange: parts[4].Trim(),
                IsBenchmark: bool.TryParse(parts[5].Trim(), out var b) && b));
        }

        return entries;
    }
}
