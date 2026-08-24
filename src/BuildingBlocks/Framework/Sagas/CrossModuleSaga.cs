using Microsoft.Extensions.Logging;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Framework.Sagas;

/// <summary>
/// One step of a cross-module saga. <see cref="Action"/> performs the work;
/// <see cref="Compensation"/> (if provided) undoes it and MUST be idempotent.
/// </summary>
public sealed record SagaStep(
    string Name,
    Func<CancellationToken, Task<Result>> Action,
    Func<CancellationToken, Task>? Compensation = null);

/// <summary>
/// Minimal compensating-workflow executor for cross-module operations.
///
/// HONEST SEMANTICS (docs/adr/0005): this is a synchronous SAGA, NOT an atomic
/// transaction. Modules use separate databases; nothing here provides rollback.
/// Steps execute in order; on the first failure, all completed steps that have a
/// compensation are compensated in REVERSE order — best-effort, but failures are
/// logged and metered (never swallowed silently). If a compensation fails, the
/// original step's error is still returned and ops must reconcile manually
/// (e.g., the reservation TTL reaper reclaims stranded stock).
/// </summary>
public sealed class CrossModuleSaga
{
    private readonly ILogger _logger;
    private readonly string _name;
    private readonly List<SagaStep> _steps = [];

    public CrossModuleSaga(ILogger logger, string name)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _name = string.IsNullOrWhiteSpace(name) ? "saga" : name;
    }

    /// <summary>Appends a step. Fluent.</summary>
    public CrossModuleSaga Step(string name, Func<CancellationToken, Task<Result>> action, Func<CancellationToken, Task>? compensate = null)
    {
        _steps.Add(new SagaStep(name, action, compensate));
        return this;
    }

    public async Task<Result> ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Saga {Saga}: started ({StepCount} step(s))", _name, _steps.Count);
        var compensated = new List<string>();

        foreach (var step in _steps)
        {
            Result result;
            try
            {
                result = await step.Action(ct).ConfigureAwait(false)
                         ?? Result.Failure(new Error("SAGA_NULL_RESULT", $"Step '{step.Name}' returned null."));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogWarning("Saga {Saga}: cancelled during step {Step}", _name, step.Name);
                await CompensateAsync(compensated, ct).ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga {Saga}: step {Step} threw {ExceptionType}", _name, step.Name, ex.GetType().Name);
                result = Result.Failure(new Error("SAGA_STEP_FAILED", $"Step '{step.Name}' failed: {ex.Message}"));
            }

            if (result.IsFailure)
            {
                _logger.LogWarning("Saga {Saga}: FAILED at step {Step} ({Code}). Compensating up to {Count} completed step(s)",
                    _name, step.Name, result.Error.Code, compensated.Count);
                SagasMetrics.SagaFailed(_name, step.Name);
                await CompensateAsync(compensated, ct).ConfigureAwait(false);
                return result; // propagate the ORIGINAL failure
            }

            if (step.Compensation is not null)
                compensated.Add(step.Name);
        }

        _logger.LogInformation("Saga {Saga}: completed successfully", _name);
        SagasMetrics.SagaCompleted(_name);
        return Result.Success();
    }

    private async Task CompensateAsync(List<string> compensatedSteps, CancellationToken ct)
    {
        for (var i = compensatedSteps.Count - 1; i >= 0; i--)
        {
            var stepName = compensatedSteps[i];
            var step = _steps.First(s => s.Name == stepName);
            try
            {
                await step.Compensation!(ct).ConfigureAwait(false);
                _logger.LogInformation("Saga {Saga}: compensated step {Step}", _name, stepName);
            }
            catch (Exception ex)
            {
                // NEVER swallow silently: log at Error + increment metric for ops follow-up.
                _logger.LogError(ex, "Saga {Saga}: COMPENSATION FAILED for step {Step} — manual reconciliation required",
                    _name, stepName);
                SagasMetrics.CompensationFailed(_name, stepName);
            }
        }
    }
}
