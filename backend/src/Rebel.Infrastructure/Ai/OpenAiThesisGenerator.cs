using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebel.Core.Abstractions;
using Rebel.Core.Ai;
using Rebel.Infrastructure.Configuration;

namespace Rebel.Infrastructure.Ai;

/// <summary>
/// Calls OpenAI with a strict JSON schema and enforces the no-invented-numbers
/// rule before the response is allowed anywhere near the database.
/// </summary>
public sealed class OpenAiThesisGenerator : IThesisGenerator
{
    public const string HttpClientName = "openai";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiThesisGenerator> _log;

    public OpenAiThesisGenerator(
        HttpClient http,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiThesisGenerator> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public string Model => _options.Model;
    public string PromptVersion => ThesisPrompt.Version;

    public async Task<ThesisResponse> GenerateAsync(ThesisRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userMessage = ThesisPrompt.BuildUserMessage(request);
        var allowed = NumberGuard.BuildAllowList(ThesisPrompt.SystemPrompt, userMessage);

        var messages = new List<object>
        {
            new { role = "system", content = ThesisPrompt.SystemPrompt },
            new { role = "user", content = userMessage }
        };

        var (response, raw, usage) = await CompleteAsync(messages, ct);
        var violations = Inspect(response, allowed);

        if (violations.Count > 0 && _options.RetryOnNumberViolation)
        {
            _log.LogWarning("Model returned {Count} unsupported numbers; retrying once: {Violations}",
                violations.Count, string.Join("; ", violations.Take(5)));

            messages.Add(new { role = "assistant", content = raw });
            messages.Add(new { role = "user", content = ThesisPrompt.BuildViolationRetry(violations) });

            var (retried, retriedRaw, retriedUsage) = await CompleteAsync(messages, ct);
            var retryViolations = Inspect(retried, allowed);

            if (retryViolations.Count == 0)
                return retried with
                {
                    RawJson = retriedRaw,
                    Model = _options.Model,
                    PromptTokens = usage.Prompt + retriedUsage.Prompt,
                    CompletionTokens = usage.Completion + retriedUsage.Completion
                };

            // Still fabricating after a correction: drop the offending entries
            // rather than surface a number the pipeline cannot stand behind.
            _log.LogError("Model still returned unsupported numbers after retry; dropping {Count} selections",
                retryViolations.Count);

            return DropOffenders(retried, allowed) with
            {
                RawJson = retriedRaw,
                Model = _options.Model,
                PromptTokens = usage.Prompt + retriedUsage.Prompt,
                CompletionTokens = usage.Completion + retriedUsage.Completion
            };
        }

        if (violations.Count > 0)
            return DropOffenders(response, allowed) with
            {
                RawJson = raw,
                Model = _options.Model,
                PromptTokens = usage.Prompt,
                CompletionTokens = usage.Completion
            };

        return response with
        {
            RawJson = raw,
            Model = _options.Model,
            PromptTokens = usage.Prompt,
            CompletionTokens = usage.Completion
        };
    }

    private async Task<(ThesisResponse Response, string Raw, (int Prompt, int Completion) Usage)> CompleteAsync(
        IReadOnlyList<object> messages, CancellationToken ct)
    {
        var body = new
        {
            model = _options.Model,
            temperature = _options.Temperature,
            max_completion_tokens = _options.MaxOutputTokens,
            messages,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "rebel_thesis",
                    strict = true,
                    schema = ThesisPrompt.ResponseSchema()
                }
            }
        };

        using var httpResponse = await _http.PostAsJsonAsync(
            $"{_options.BaseUrl}/chat/completions", body, Json, ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var error = await httpResponse.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"OpenAI request failed ({(int)httpResponse.StatusCode}): {Truncate(error, 500)}");
        }

        var completion = await httpResponse.Content.ReadFromJsonAsync<ChatCompletion>(Json, ct)
                         ?? throw new InvalidOperationException("OpenAI returned an empty response body.");

        var content = completion.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned no message content.");

        var parsed = JsonSerializer.Deserialize<SchemaResponse>(content, Json)
                     ?? throw new InvalidOperationException("OpenAI response did not match the thesis schema.");

        var response = new ThesisResponse
        {
            Selections = parsed.Selections?.Select(s => new ThesisSelection
            {
                Symbol = (s.Symbol ?? string.Empty).ToUpperInvariant(),
                Selected = s.Selected,
                Thesis = s.Thesis ?? string.Empty,
                BullCase = s.BullCase ?? string.Empty,
                BearCase = s.BearCase ?? string.Empty,
                Risks = s.Risks ?? string.Empty,
                CatalystSummary = s.CatalystSummary ?? string.Empty,
                WhyThisOne = s.WhyThisOne ?? string.Empty
            }).ToArray() ?? []
        };

        var usage = (completion.Usage?.PromptTokens ?? 0, completion.Usage?.CompletionTokens ?? 0);
        return (response, content, usage);
    }

    private static List<string> Inspect(ThesisResponse response, HashSet<decimal> allowed) =>
        response.Selections
            .Where(s => s.Selected)
            .SelectMany(s => NumberGuard.Inspect(s, allowed).Select(v => $"{s.Symbol} {v}"))
            .ToList();

    /// <summary>Keeps clean selections and de-selects the ones still quoting invented figures.</summary>
    private static ThesisResponse DropOffenders(ThesisResponse response, HashSet<decimal> allowed) =>
        response with
        {
            Selections = response.Selections
                .Select(s => s.Selected && NumberGuard.Inspect(s, allowed).Count > 0
                    ? s with { Selected = false }
                    : s)
                .ToArray()
        };

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "…";

    private sealed record ChatCompletion
    {
        public List<Choice>? Choices { get; init; }
        public UsageInfo? Usage { get; init; }
    }

    private sealed record Choice
    {
        public ResponseMessage? Message { get; init; }
    }

    private sealed record ResponseMessage
    {
        public string? Content { get; init; }
    }

    private sealed record UsageInfo
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; init; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; init; }
    }

    private sealed record SchemaResponse
    {
        public List<SchemaSelection>? Selections { get; init; }
    }

    private sealed record SchemaSelection
    {
        public string? Symbol { get; init; }
        public bool Selected { get; init; }
        public string? Thesis { get; init; }
        public string? BullCase { get; init; }
        public string? BearCase { get; init; }
        public string? Risks { get; init; }
        public string? CatalystSummary { get; init; }
        public string? WhyThisOne { get; init; }
    }
}
