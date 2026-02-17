using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.AiProvider.Claude;

/// <summary>
/// Tracks Anthropic API overload errors and provides temporary model fallback.
/// When 3+ overload errors occur within 3 minutes, Opus requests fall back to Sonnet for 5 minutes.
/// </summary>
public static class ModelFallbackCache
{
    private static readonly List<DateTime> _overloadTimestamps = new();
    private static DateTime? _fallbackUntil;
    private static readonly object _lock = new();

    private const int OverloadThreshold = 3;
    private static readonly TimeSpan OverloadWindow = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan FallbackDuration = TimeSpan.FromMinutes(5);

    private const string OpusModel = "claude-opus-4-6";
    private const string SonnetFallback = "claude-sonnet-4-5-20250929";

    /// <summary>
    /// Records an overload error. If threshold is reached, activates Sonnet fallback.
    /// </summary>
    public static void RecordOverload()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            _overloadTimestamps.Add(now);

            // Remove timestamps older than the window
            _overloadTimestamps.RemoveAll(t => now - t > OverloadWindow);

            if (_overloadTimestamps.Count >= OverloadThreshold)
            {
                _fallbackUntil = now + FallbackDuration;
                Logger.LogWarning($"[ModelFallback] {_overloadTimestamps.Count} overloads in {OverloadWindow.TotalMinutes} min - falling back to Sonnet until {_fallbackUntil.Value:HH:mm:ss} UTC");
                _overloadTimestamps.Clear();
            }
        }
    }

    /// <summary>
    /// Returns the model to use. If Opus is overloaded, returns Sonnet instead.
    /// </summary>
    public static string GetModel(string configuredModel)
    {
        if (!IsOpusModel(configuredModel))
            return configuredModel;

        lock (_lock)
        {
            if (_fallbackUntil.HasValue && DateTime.UtcNow < _fallbackUntil.Value)
            {
                Logger.Log($"[ModelFallback] Opus overloaded - using Sonnet fallback (until {_fallbackUntil.Value:HH:mm:ss} UTC)");
                return SonnetFallback;
            }

            // Fallback expired
            if (_fallbackUntil.HasValue)
            {
                Logger.Log("[ModelFallback] Fallback expired - returning to Opus");
                _fallbackUntil = null;
            }
        }

        return configuredModel;
    }

    /// <summary>
    /// Whether the fallback is currently active.
    /// </summary>
    public static bool IsFallbackActive
    {
        get
        {
            lock (_lock)
            {
                return _fallbackUntil.HasValue && DateTime.UtcNow < _fallbackUntil.Value;
            }
        }
    }

    private static bool IsOpusModel(string model)
    {
        return model?.Contains("opus", StringComparison.OrdinalIgnoreCase) == true;
    }
}
