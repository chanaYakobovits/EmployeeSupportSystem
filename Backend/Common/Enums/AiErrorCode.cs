namespace Common.Enums;

/// <summary>Why an AI call produced no usable analysis.</summary>
public enum AiErrorCode
{
    /// <summary>The attempt exceeded <c>Ai:TimeoutSeconds</c>. Transient.</summary>
    Timeout = 1,

    /// <summary>HTTP 429, also after the quick retry. Transient.</summary>
    RateLimited = 2,

    /// <summary>HTTP 5xx or a network failure, also after the quick retry. Transient.</summary>
    ServerError = 3,

    /// <summary>The response is not valid JSON / does not match the schema (spec 5.5). Transient.</summary>
    InvalidResponse = 4,

    /// <summary>HTTP 4xx other than 429 (bad key, unknown model, bad request). Final – retrying will not help.</summary>
    Rejected = 5,

    /// <summary><c>Ai:Enabled = false</c> or no API key; no call was made. Final.</summary>
    Disabled = 6
}
