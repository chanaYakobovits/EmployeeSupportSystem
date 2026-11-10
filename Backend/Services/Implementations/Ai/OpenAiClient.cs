using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Text.Json;
using Common.DTOs.Ai;
using Common.Enums;
using Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Services.Interfaces;

namespace Services.Implementations.Ai;

/// <summary>
/// OpenAI chat client with Structured Outputs (spec 5.4) and request-level resilience only:
/// a per-attempt timeout and at most one quick retry on 429/5xx. The retry schedule across jobs (spec 4.3)
/// and the circuit breaker (spec 5.6) belong to the background worker.
/// </summary>
/// <remarks>
/// Registered as a typed HttpClient (spec 10.3). The SDK's own retry policy is disabled so retries are not multiplied.
/// Never logs ticket text, AI output, error bodies or the API key (spec 6.4).
/// </remarks>
public sealed class OpenAiClient : IAiClient
{
    private const int MaxHttpAttempts = 2; // first attempt + one quick retry
    private const int MaxOutputTokens = 2000;

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly AiResponseValidator _validator;
    private readonly ILogger<OpenAiClient> _logger;

    public OpenAiClient(
        HttpClient httpClient, IOptions<AiOptions> options, AiResponseValidator validator, ILogger<OpenAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>Delay before the quick retry. Internal so tests can set it to zero.</summary>
    internal TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    public async Task<AiAnalysisResult> AnalyzeAsync(
        string redactedText, IReadOnlyList<AiCategory> categories, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redactedText);
        ArgumentNullException.ThrowIfNull(categories);
        if (categories.Count == 0)
        {
            throw new ArgumentException("At least one category is required.", nameof(categories));
        }

        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var disabled = AiAnalysisResult.Failure(AiErrorCode.Disabled, _options.Model, AiPrompt.CurrentVersion, 0, 0);
            _logger.LogDebug("AI analysis skipped: AI is disabled or no API key is configured");
            return disabled;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var result = await CallAsync(redactedText, categories, startedAt, cancellationToken);
        Log(result);
        return result;
    }

    private async Task<AiAnalysisResult> CallAsync(
        string redactedText, IReadOnlyList<AiCategory> categories, long startedAt, CancellationToken cancellationToken)
    {
        var codes = categories.Select(c => c.Code).ToArray();
        var chatClient = CreateChatClient();
        ChatMessage[] messages =
        [
            new SystemChatMessage(AiPrompt.BuildSystemMessage(categories)),
            new UserChatMessage(redactedText) // the redacted text exactly as received – nothing else from the ticket
        ];
        var completionOptions = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                AiResponseSchema.Name, AiResponseSchema.Build(codes), jsonSchemaIsStrict: true),
            StoredOutputEnabled = false, // spec 5.3: OpenAI does not keep the conversation
            Temperature = _options.Temperature, // null → omitted from the request
            MaxOutputTokenCount = MaxOutputTokens
        };

        for (var attempt = 1; ; attempt++)
        {
            AttemptFailure failure;
            using (var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                attemptCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
                try
                {
                    ChatCompletion completion = await chatClient.CompleteChatAsync(messages, completionOptions, attemptCts.Token);
                    return InterpretCompletion(completion, codes, startedAt, attempt);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Our per-attempt timeout (or the SDK network timeout) fired – not the caller. No retry on timeout.
                    return Failure(AiErrorCode.Timeout, startedAt, attempt);
                }
                catch (ClientResultException exception)
                {
                    failure = Classify(exception.Status);
                }
                catch (HttpRequestException)
                {
                    failure = new AttemptFailure(AiErrorCode.ServerError, null, Retryable: true);
                }
                catch (IOException)
                {
                    failure = new AttemptFailure(AiErrorCode.ServerError, null, Retryable: true);
                }
                catch (JsonException)
                {
                    // The response envelope itself could not be parsed.
                    return Failure(AiErrorCode.InvalidResponse, startedAt, attempt);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Unexpected SDK failure: still a defined result. Only the type is logged – the message may echo content.
                    _logger.LogWarning("AI call failed with unexpected {ExceptionType}", exception.GetType().Name);
                    return Failure(AiErrorCode.ServerError, startedAt, attempt);
                }
            }

            if (!failure.Retryable || attempt >= MaxHttpAttempts)
            {
                return Failure(failure.Code, startedAt, attempt, failure.HttpStatus);
            }

            if (RetryDelay > TimeSpan.Zero)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    private ChatClient CreateChatClient()
    {
        var clientOptions = new OpenAIClientOptions
        {
            // HttpClient comes from IHttpClientFactory; the transport does not own or dispose it.
            Transport = new HttpClientPipelineTransport(_httpClient),
            // The SDK retries 3 times by default; disabled so the quick retry above is the only one.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };

        return new ChatClient(_options.Model, new ApiKeyCredential(_options.ApiKey!), clientOptions);
    }

    private AiAnalysisResult InterpretCompletion(ChatCompletion completion, string[] codes, long startedAt, int attempt)
    {
        var model = string.IsNullOrEmpty(completion.Model) ? _options.Model : completion.Model;
        var inputTokens = completion.Usage?.InputTokenCount ?? 0;
        var outputTokens = completion.Usage?.OutputTokenCount ?? 0;
        var content = string.Concat(completion.Content.Select(part => part.Text));

        if (completion.FinishReason != ChatFinishReason.Stop
            || !string.IsNullOrEmpty(completion.Refusal)
            || string.IsNullOrWhiteSpace(content))
        {
            return AiAnalysisResult.Failure(AiErrorCode.InvalidResponse, model, AiPrompt.CurrentVersion,
                ElapsedMs(startedAt), attempt, inputTokens: inputTokens, outputTokens: outputTokens);
        }

        var outcome = _validator.Validate(content, codes, _options.MinConfidence);
        if (outcome.Verdict == AiValidationVerdict.Invalid || outcome.Analysis is null)
        {
            return AiAnalysisResult.Failure(AiErrorCode.InvalidResponse, model, AiPrompt.CurrentVersion,
                ElapsedMs(startedAt), attempt, inputTokens: inputTokens, outputTokens: outputTokens);
        }

        return AiAnalysisResult.Success(outcome.Analysis, outcome.Verdict, model, AiPrompt.CurrentVersion,
            inputTokens, outputTokens, ElapsedMs(startedAt), attempt);
    }

    private static AttemptFailure Classify(int status) => status switch
    {
        429 => new AttemptFailure(AiErrorCode.RateLimited, status, Retryable: true),
        >= 500 => new AttemptFailure(AiErrorCode.ServerError, status, Retryable: true),
        0 => new AttemptFailure(AiErrorCode.ServerError, null, Retryable: true), // no response: network failure
        _ => new AttemptFailure(AiErrorCode.Rejected, status, Retryable: false)
    };

    private AiAnalysisResult Failure(AiErrorCode code, long startedAt, int attempt, int? httpStatus = null) =>
        AiAnalysisResult.Failure(code, _options.Model, AiPrompt.CurrentVersion, ElapsedMs(startedAt), attempt, httpStatus);

    private static int ElapsedMs(long startedAt) => (int)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    private void Log(AiAnalysisResult result)
    {
        const string template =
            "AI analysis {Outcome}: Model={Model} PromptVersion={PromptVersion} Verdict={Verdict} ErrorCode={ErrorCode} " +
            "HttpStatus={HttpStatus} Attempts={Attempts} InputTokens={InputTokens} OutputTokens={OutputTokens} DurationMs={DurationMs}";

        _logger.Log(
            result.IsSuccess ? LogLevel.Information : LogLevel.Warning,
            template,
            result.IsSuccess ? "succeeded" : "failed",
            result.Model, result.PromptVersion, result.Verdict, result.ErrorCode, result.HttpStatus,
            result.Attempts, result.InputTokens, result.OutputTokens, result.DurationMs);
    }

    private readonly record struct AttemptFailure(AiErrorCode Code, int? HttpStatus, bool Retryable);
}
