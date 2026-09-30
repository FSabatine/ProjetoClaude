using Fleet.Application.Documents;
using Microsoft.Extensions.Options;

namespace Fleet.Api.Infrastructure;

public sealed class DocumentExpirationJobOptions
{
    public const string SectionName = "Jobs:DocumentExpirationScan";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 360;
}

/// <summary>
/// Periodically emits DocumentExpiring/DocumentExpired events (ADR-025). In-process and idempotent: with several API
/// instances the scan may run more than once, and each state change is still announced once (LastAlertedStatus).
/// </summary>
public sealed class DocumentExpirationJob(
    IServiceScopeFactory scopes, IOptions<DocumentExpirationJobOptions> options, ILogger<DocumentExpirationJob> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

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
            var emitted = await scope.ServiceProvider.GetRequiredService<DocumentExpirationScanner>().ScanAsync(ct);
            if (emitted > 0) logger.LogInformation("Document expiration scan emitted {EventCount} events", emitted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed scan must not stop the API; the next run retries.
            logger.LogError(ex, "Document expiration scan failed");
        }
    }
}
