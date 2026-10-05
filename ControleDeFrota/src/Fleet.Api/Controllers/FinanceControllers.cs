using Fleet.Api.Authorization;
using Fleet.Application.Common;
using Fleet.Application.Finance;
using Fleet.Domain.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

// Phase 6 — finance. Same shape as every other module: a thin controller, one [HasPermission] per action, no logic here.

[ApiController]
[Route("api/v1/cost-centers")]
public sealed class CostCentersController(CostCenterService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Finance.View, Permissions.Finance.ManageCostCenters)]
    public Task<IReadOnlyList<CostCenterResponse>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(includeInactive, ct);

    [HttpPost, HasPermission(Permissions.Finance.ManageCostCenters)]
    public async Task<ActionResult<CostCenterResponse>> Create(CostCenterRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Finance.ManageCostCenters)]
    public Task<CostCenterResponse> Update(Guid id, CostCenterRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Finance.ManageCostCenters)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/expense-categories")]
public sealed class ExpenseCategoriesController(ExpenseCategoryService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Finance.View, Permissions.Finance.ManageCategories)]
    public Task<IReadOnlyList<ExpenseCategoryResponse>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(includeInactive, ct);

    [HttpPost, HasPermission(Permissions.Finance.ManageCategories)]
    public async Task<ActionResult<ExpenseCategoryResponse>> Create(ExpenseCategoryRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Finance.ManageCategories)]
    public Task<ExpenseCategoryResponse> Update(Guid id, ExpenseCategoryRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Finance.ManageCategories)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/expenses")]
public sealed class ExpensesController(ExpenseService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Finance.View)]
    public Task<PagedResult<ExpenseResponse>> List([FromQuery] ExpenseListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Finance.View)]
    public Task<ExpenseResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Finance.Create)]
    public async Task<ActionResult<ExpenseResponse>> Create(ExpenseRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Finance.Edit)]
    public Task<ExpenseResponse> Update(Guid id, ExpenseRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/payment"), HasPermission(Permissions.Finance.RegisterPayment)]
    public Task<ExpenseResponse> RegisterPayment(Guid id, ExpensePaymentRequest request, CancellationToken ct) =>
        service.RegisterPaymentAsync(id, request, ct);

    [HttpPost("{id:guid}/cancel"), HasPermission(Permissions.Finance.Cancel)]
    public Task<ExpenseResponse> Cancel(Guid id, ExpenseCancelRequest request, CancellationToken ct) => service.CancelAsync(id, request, ct);
}

[ApiController]
[Route("api/v1/recurring-expenses")]
public sealed class RecurringExpensesController(RecurringExpenseService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Finance.View, Permissions.Finance.ManageRecurring)]
    public Task<IReadOnlyList<RecurringExpenseResponse>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(includeInactive, ct);

    [HttpPost, HasPermission(Permissions.Finance.ManageRecurring)]
    public async Task<ActionResult<RecurringExpenseResponse>> Create(RecurringExpenseRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Finance.ManageRecurring)]
    public Task<RecurringExpenseResponse> Update(Guid id, RecurringExpenseRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Finance.ManageRecurring)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/budgets")]
public sealed class BudgetsController(BudgetService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Finance.View, Permissions.Finance.ManageBudgets)]
    public Task<IReadOnlyList<BudgetResponse>> List([FromQuery] int year, CancellationToken ct) => service.ListAsync(year, ct);

    [HttpGet("vs-actual"), HasPermission(Permissions.Finance.ViewCosts)]
    public Task<IReadOnlyList<BudgetVsActualResponse>> VsActual([FromQuery] int year, [FromQuery] int? month, CancellationToken ct) =>
        service.ListVsActualAsync(year, month, ct);

    [HttpPost, HasPermission(Permissions.Finance.ManageBudgets)]
    public async Task<ActionResult<BudgetResponse>> Create(BudgetRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Finance.ManageBudgets)]
    public Task<BudgetResponse> Update(Guid id, BudgetRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Finance.ManageBudgets)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/finance")]
public sealed class FinanceAnalyticsController(FinanceAnalyticsService analytics, CostAggregationService costs) : ControllerBase
{
    [HttpGet("dashboard"), HasPermission(Permissions.Finance.View)]
    public Task<FinanceDashboardResponse> Dashboard(CancellationToken ct) => analytics.GetDashboardAsync(ct);

    [HttpGet("ranking"), HasPermission(Permissions.Finance.ViewCosts)]
    public Task<PagedResult<VehicleRankingRow>> Ranking([FromQuery] VehicleRankingRequest request, CancellationToken ct) =>
        analytics.GetVehicleRankingAsync(request, ct);

    [HttpGet("cost-by-category"), HasPermission(Permissions.Finance.ViewCosts)]
    public Task<PartialList<CategoryCost>> CostByCategory([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        costs.GetFleetCostByCategoryAsync(from, to, ct);

    [HttpGet("cost-by-cost-center"), HasPermission(Permissions.Finance.ViewCosts)]
    public Task<PartialList<CostCenterCost>> CostByCostCenter([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        costs.GetCostCenterBreakdownAsync(from, to, ct);

    [HttpGet("monthly-evolution"), HasPermission(Permissions.Finance.ViewCosts)]
    public Task<PartialList<MonthlyCost>> MonthlyEvolution(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? vehicleId, CancellationToken ct) =>
        costs.GetMonthlyCostEvolutionAsync(from, to, vehicleId, ct);

    [HttpGet("vehicles/{vehicleId:guid}/breakdown"), HasPermission(Permissions.Finance.View)]
    public Task<VehicleCostBreakdown> VehicleBreakdown(
        Guid vehicleId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        costs.GetVehicleCostBreakdownAsync(vehicleId, from, to, ct);

    [HttpGet("vehicles/{vehicleId:guid}/cost-per-km"), HasPermission(Permissions.Finance.View)]
    public Task<VehicleCostPerKmResponse> VehicleCostPerKm(
        Guid vehicleId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        analytics.GetVehicleCostPerKmAsync(vehicleId, from, to, ct);

    [HttpGet("vehicles/{vehicleId:guid}/tco"), HasPermission(Permissions.Finance.View)]
    public Task<VehicleTcoResponse> VehicleTco(Guid vehicleId, CancellationToken ct) => analytics.GetVehicleTcoAsync(vehicleId, ct);
}
