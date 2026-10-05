using Fleet.Application.Common;
using Fleet.Application.Intelligence;
using Fleet.Domain.Intelligence;
using Fleet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fleet.Api.Infrastructure;

public sealed class AutomationJobOptions
{
    public const string SectionName = "Jobs:Automation";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
}

/// <summary>Runs the automation engine for one company inside its own DI scope, acting as the system (ADR-045).</summary>
public sealed class AutomationRunner(IServiceScopeFactory scopes)
{
    public async Task<AutomationRunResult> RunForCompanyAsync(Guid companyId, AutomationRunSource source, CancellationToken ct)
    {
        // A fresh scope: the caller's own identity (a request) never leaks into the system run, and vice versa.
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<SystemExecutionContext>().ActAsSystemFor(companyId);
        return await scope.ServiceProvider.GetRequiredService<AutomationEngine>().RunAsync(source, ct);
    }

    public async Task<IReadOnlyList<Guid>> ActiveCompaniesAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        return await db.Companies.Where(c => c.IsActive).Select(c => c.Id).ToListAsync(ct);
    }
}

/// <summary>
/// Periodic scan of the automation rules of every active company (same shape as DocumentExpirationJob). In-process;
/// open alerts are unique per (rule, dedup key) in the database, so overlapping runs cannot duplicate alerts.
/// </summary>
public sealed class AutomationJob(AutomationRunner runner, IOptions<AutomationJobOptions> options, ILogger<AutomationJob> logger)
    : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var interval = TimeSpan.FromMinutes(Math.Max(5, options.Value.IntervalMinutes));
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> companies;
        try
        {
            companies = await runner.ActiveCompaniesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Automation scan could not list companies");
            return;
        }
        foreach (var companyId in companies)
        {
            try
            {
                await runner.RunForCompanyAsync(companyId, AutomationRunSource.Scheduled, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One company failing must not stop the others; the next run retries.
                logger.LogError(ex, "Automation scan failed for company {CompanyId}", companyId);
            }
        }
    }
}
