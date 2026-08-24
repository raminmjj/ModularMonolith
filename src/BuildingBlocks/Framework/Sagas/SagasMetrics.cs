using System.Diagnostics.Metrics;

namespace ModularMonolith.Framework.Sagas;

/// <summary>
/// Metrics for cross-module saga operations (meter name: "ModularMonolith.Sagas").
/// Wired into OpenTelemetry in the Host — compensation failures are the leading
/// incident indicator in a saga-based architecture, so they are counted explicitly.
/// </summary>
public static class SagasMetrics
{
    public static readonly Meter Meter = new("ModularMonolith.Sagas", "1.0");

    private static readonly Counter<long> Completed = Meter.CreateCounter<long>(
        "sagas.completed", description: "Cross-module sagas that finished all steps successfully.");

    private static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "sagas.failed", description: "Cross-module sagas whose step failed (compensation was attempted).");

    private static readonly Counter<long> CompensationFailures = Meter.CreateCounter<long>(
        "sagas.compensation_failures", description: "Compensations that threw — manual follow-up required.");

    public static void SagaCompleted(string saga) =>
        Completed.Add(1, new KeyValuePair<string, object?>("saga", saga));

    public static void SagaFailed(string saga, string step) => Failed.Add(1,
        new KeyValuePair<string, object?>("saga", saga),
        new KeyValuePair<string, object?>("step", step));

    public static void CompensationFailed(string saga, string step) =>
        CompensationFailures.Add(1,
            new KeyValuePair<string, object?>("saga", saga),
            new KeyValuePair<string, object?>("step", step));
}
