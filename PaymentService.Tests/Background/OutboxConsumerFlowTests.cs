namespace PaymentService.Tests.Background;

/// <summary>
/// Section 4.3 (TC-OBX-*) of the PaymentService test plan covers the OutboxConsumer claim/lock
/// loop inside ExecuteAsync. Those cases are deferred from this unit-test pass because:
///
///  1. ExecuteAsync uses DbContext.Database.ExecuteSqlRawAsync with a SQL UPDATE/LIMIT statement.
///     EF Core's InMemory provider does not implement raw SQL, so an InMemory DbContext throws.
///     Running against SQLite-in-memory works but moves these into integration territory.
///
///  2. ExecuteAsync is a hosted BackgroundService that loops on Task.Delay(_interval). Driving
///     a single iteration deterministically requires either a clock abstraction or a refactor
///     extracting the iteration body into an internal method.
///
/// Suggested follow-up:
///   - Refactor: extract the loop body into `internal Task PollOnceAsync(CancellationToken)` and
///     mark `OutboxConsumer` testable via [InternalsVisibleTo("PaymentService.Tests")].
///   - Or add SQLite-in-memory integration tests in a sibling PaymentService.IntegrationTests project.
///
/// The skipped facts below keep the test runner honest about what's missing.
/// </summary>
public class OutboxConsumerFlowTests
{
    [Fact(Skip = "TC-OBX-02: needs SQLite + refactor to expose single-iteration entry point.")]
    public void Claims_SingleProcessPaymentCommand_AndMarksProcessed() { }

    [Fact(Skip = "TC-OBX-03: needs SQLite + iteration entry point. Verify only ProcessPaymentCommand rows are claimed.")]
    public void Ignores_NonProcessPaymentCommand_OutboxRows() { }

    [Fact(Skip = "TC-OBX-04: needs SQLite + iteration entry point. Verify active lock prevents re-claim.")]
    public void Skips_RowsWithActiveLock() { }

    [Fact(Skip = "TC-OBX-05: needs SQLite + iteration entry point. Verify expired locks are reclaimable.")]
    public void Reclaims_RowsWithExpiredLock() { }

    [Fact(Skip = "TC-OBX-06: needs SQLite + iteration entry point. Verify LIMIT 20 per iteration.")]
    public void Claims_AtMost20RowsPerIteration() { }

    [Fact(Skip = "TC-OBX-07: needs SQLite + iteration entry point. Verify LastError is set and row retained for retry.")]
    public void MalformedPayload_SetsLastError_AndKeepsForRetry() { }

    [Fact(Skip = "TC-OBX-08: needs SQLite + iteration entry point. Verify poison-message eviction at MaxAttempts.")]
    public void PoisonMessage_AtMaxAttempts_IsMarkedProcessedToEvict() { }
}
