using Fleet.Application.Finance;
using Microsoft.Extensions.Options;

namespace Fleet.Api.Infrastructure;

public sealed class RecurringExpenseGenerationJobOptions
{
    public const string SectionName = "Jobs:RecurringExpenseGeneration";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 360;
}

/// <summary>
/// Periodically turns active RecurringExpense templates into dated Expense rows ahead of time (ADR-040), the
/// same shape as DocumentExpirationJob. In-process and idempotent: with several API instances the scan may run
/// more than once, and the (RecurringExpenseId, DueDate) unique index still prevents a duplicate row.
/// </summary>
public sealed class RecurringExpenseGenerationJob(
    IServiceScopeFactory scopes, IOptions<RecurringExpenseGenerationJobOptions> options, ILogger<RecurringExpenseGenerationJob> logger)
    : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes));
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await ScanOnceAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var generated = await scope.ServiceProvider.GetRequiredService<RecurringExpenseGenerationScanner>().ScanAsync(ct);
            if (generated > 0) logger.LogInformation("Recurring expense generation emitted {ExpenseCount} expenses", generated);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed scan must not stop the API; the next run retries.
            logger.LogError(ex, "Recurring expense generation scan failed");
        }
    }
}
