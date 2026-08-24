using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModularMonolith.Framework.Results;
using ModularMonolith.Framework.Sagas;
using Xunit;

namespace ModularMonolith.UnitTests;

/// <summary>
/// Contract tests for the CrossModuleSaga executor — the load-bearing primitive
/// for all cross-module consistency in this system (docs/adr/0005).
/// Pure delegates, no mocking framework required.
/// </summary>
public class CrossModuleSagaTests
{
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public async Task ExecuteAsync_With_All_Steps_Succeeding_Returns_Success()
    {
        var executed = new List<string>();

        var result = await new CrossModuleSaga(Logger, "happy-path")
            .Step("s1", _ => { executed.Add("s1"); return Task.FromResult(Result.Success()); })
            .Step("s2", _ => { executed.Add("s2"); return Task.FromResult(Result.Success()); })
            .ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        executed.Should().Equal("s1", "s2");
    }

    [Fact]
    public async Task ExecuteAsync_On_Failure_Compensates_Completed_Steps_In_Reverse_Order()
    {
        var compensated = new List<string>();

        var result = await new CrossModuleSaga(Logger, "reverse-order")
            .Step("s1", _ => Task.FromResult(Result.Success()),
                  _ => { compensated.Add("c1"); return Task.CompletedTask; })
            .Step("s2", _ => Task.FromResult(Result.Success()),
                  _ => { compensated.Add("c2"); return Task.CompletedTask; })
            .Step("s3", _ => Task.FromResult(Result.Failure(new Error("BOOM", "step three failed"))))
            .ExecuteAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BOOM"); // original error propagates
        compensated.Should().Equal("c2", "c1"); // reverse order
    }

    [Fact]
    public async Task ExecuteAsync_Does_Not_Compensate_Steps_Without_Compensation()
    {
        var compensated = new List<string>();

        var result = await new CrossModuleSaga(Logger, "no-comp")
            .Step("readonly-step", _ => Task.FromResult(Result.Success())) // no compensation registered
            .Step("failing", _ => Task.FromResult(Result.Failure(Error.Validation)))
            .ExecuteAsync();

        result.IsFailure.Should().BeTrue();
        compensated.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Swallows_And_Logs_Compensation_Failures_While_Preserving_Original_Error()
    {
        var secondCompensationRan = false;

        var result = await new CrossModuleSaga(Logger, "comp-fails")
            .Step("s1", _ => Task.FromResult(Result.Success()),
                  _ => throw new InvalidOperationException("release endpoint down"))
            .Step("s2", _ => Task.FromResult(Result.Success()),
                  _ => { secondCompensationRan = true; return Task.CompletedTask; })
            .Step("s3", _ => Task.FromResult(Result.Failure(new Error("ORIGINAL", "the real failure"))))
            .ExecuteAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORIGINAL"); // NOT the compensation exception
        secondCompensationRan.Should().BeTrue();   // later compensations still run
    }

    [Fact]
    public async Task ExecuteAsync_Treats_Thrown_Exception_As_Step_Failure_And_Compensates()
    {
        var compensated = new List<string>();

        var result = await new CrossModuleSaga(Logger, "throws")
            .Step("s1", _ => Task.FromResult(Result.Success()),
                  _ => { compensated.Add("c1"); return Task.CompletedTask; })
            .Step("s2", _ => throw new HttpRequestException("catalog unreachable"))
            .ExecuteAsync();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SAGA_STEP_FAILED");
        (result.Error.Message.Contains("HttpRequestException") || result.Error.Message.Contains("unreachable"))
            .Should().BeTrue();
        compensated.Should().Equal("c1");
    }

    [Fact]
    public void Step_Returns_Fluent_Builder_For_Chaining()
    {
        var saga = new CrossModuleSaga(Logger, "fluent");
        saga.Step("a", _ => Task.FromResult(Result.Success())).Should().BeSameAs(saga);
    }
}
