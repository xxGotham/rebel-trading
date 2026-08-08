namespace Rebel.Core.Domain;

/// <summary>A headline attached to one or more instruments.</summary>
public class NewsItem
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    /// <summary>Provider-native id, used to de-duplicate across runs.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public DateTime PublishedAtUtc { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime FetchedAtUtc { get; set; }
}
