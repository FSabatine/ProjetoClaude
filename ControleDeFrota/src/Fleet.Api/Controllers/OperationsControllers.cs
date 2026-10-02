using Fleet.Api.Authorization;
using Fleet.Application.Assignments;
using Fleet.Application.Checklists;
using Fleet.Application.Common;
using Fleet.Application.Documents;
using Fleet.Application.Files;
using Fleet.Application.Mileage;
using Fleet.Application.Occurrences;
using Fleet.Application.Operations;
using Fleet.Domain.Authorization;
using Fleet.Domain.Documents;
using Fleet.Domain.Files;
using Microsoft.AspNetCore.Mvc;

namespace Fleet.Api.Controllers;

// Phase 2 — operational control. Controllers stay thin: permission gate + service call. Fine-grained rules
// (e.g. who may download a file, corrections vs. readings) live in the services.

[ApiController]
[Route("api/v1")]
public sealed class AssignmentsController(AssignmentService service) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:guid}/assignments"), HasPermission(Permissions.Assignments.View)]
    public Task<PagedResult<AssignmentResponse>> ListForVehicle(Guid vehicleId, [FromQuery] ListRequest request, CancellationToken ct) =>
        service.ListForVehicleAsync(vehicleId, request, ct);

    [HttpPost("vehicles/{vehicleId:guid}/assignments"), HasPermission(Permissions.Assignments.Manage)]
    public async Task<ActionResult<AssignmentResponse>> Assign(Guid vehicleId, AssignmentCreateRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.AssignAsync(vehicleId, request, ct));

    [HttpGet("drivers/{driverId:guid}/assignments"), HasPermission(Permissions.Assignments.View)]
    public Task<PagedResult<AssignmentResponse>> ListForDriver(Guid driverId, [FromQuery] ListRequest request, CancellationToken ct) =>
        service.ListForDriverAsync(driverId, request, ct);

    [HttpPost("assignments/{id:guid}/end"), HasPermission(Permissions.Assignments.Manage)]
    public Task<AssignmentResponse> End(Guid id, AssignmentEndRequest request, CancellationToken ct) => service.EndAsync(id, request, ct);
}

[ApiController]
[Route("api/v1")]
public sealed class MileageController(MileageService service) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:guid}/odometer-readings"), HasPermission(Permissions.Vehicles.View)]
    public Task<PagedResult<OdometerReadingResponse>> List(Guid vehicleId, [FromQuery] OdometerReadingListRequest request, CancellationToken ct) =>
        service.ListAsync(vehicleId, request, ct);

    /// <summary>Plain readings need mileage.record; corrections are checked in the service (mileage.manage).</summary>
    [HttpPost("vehicles/{vehicleId:guid}/odometer-readings"), HasPermission(Permissions.Mileage.Record, Permissions.Mileage.Manage)]
    public async Task<ActionResult<OdometerReadingResponse>> Record(Guid vehicleId, OdometerReadingRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.RecordAsync(vehicleId, request, ct));

    [HttpPost("odometer-readings/{id:guid}/approve"), HasPermission(Permissions.Mileage.Manage)]
    public Task<OdometerReadingResponse> Approve(Guid id, OdometerReviewRequest request, CancellationToken ct) => service.ApproveAsync(id, request, ct);

    [HttpPost("odometer-readings/{id:guid}/reject"), HasPermission(Permissions.Mileage.Manage)]
    public Task<OdometerReadingResponse> Reject(Guid id, OdometerReviewRequest request, CancellationToken ct) => service.RejectAsync(id, request, ct);
}

[ApiController]
[Route("api/v1")]
public sealed class HistoryController(OperationalHistoryService service) : ControllerBase
{
    [HttpGet("vehicles/{id:guid}/history"), HasPermission(Permissions.Vehicles.View)]
    public Task<PagedResult<HistoryEntryResponse>> Vehicle(Guid id, [FromQuery] HistoryRequest request, CancellationToken ct) =>
        service.ForVehicleAsync(id, request, ct);

    [HttpGet("drivers/{id:guid}/history"), HasPermission(Permissions.Drivers.View)]
    public Task<PagedResult<HistoryEntryResponse>> Driver(Guid id, [FromQuery] HistoryRequest request, CancellationToken ct) =>
        service.ForDriverAsync(id, request, ct);
}

[ApiController]
[Route("api/v1/document-types")]
public sealed class DocumentTypesController(DocumentTypeService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Documents.View, Permissions.Operations.Configure)]
    public Task<IReadOnlyList<DocumentTypeResponse>> List([FromQuery] DocumentOwnerType? ownerType, [FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(ownerType, includeInactive, ct);

    [HttpPost, HasPermission(Permissions.Operations.Configure)]
    public async Task<ActionResult<DocumentTypeResponse>> Create(DocumentTypeRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(Permissions.Operations.Configure)]
    public Task<DocumentTypeResponse> Update(Guid id, DocumentTypeRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Operations.Configure)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/documents")]
public sealed class DocumentsController(DocumentService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Documents.View)]
    public Task<PagedResult<DocumentListItemResponse>> List([FromQuery] DocumentListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Documents.View)]
    public Task<DocumentResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Documents.Manage)]
    public async Task<ActionResult<DocumentResponse>> Create(DocumentCreateRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Documents.Manage)]
    public Task<DocumentResponse> Update(Guid id, DocumentUpdateRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Documents.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/files")]
public sealed class FilesController(FileService service) : ControllerBase
{
    /// <summary>Multipart upload; the body limit is enforced here and the size/format again in the service.</summary>
    [HttpPost, RequestSizeLimit(FileRules.MaxSizeBytes + 64 * 1024),
     HasPermission(Permissions.Documents.Manage, Permissions.Checklists.Execute, Permissions.Occurrences.Create, Permissions.Occurrences.Manage,
         Permissions.Fuel.Create, Permissions.Fuel.Correct)]
    public async Task<ActionResult<FileResponse>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null) throw ValidationErrors.ForField("file", "Selecione um arquivo para enviar.");
        await using var stream = file.OpenReadStream();
        return StatusCode(StatusCodes.Status201Created, await service.UploadAsync(stream, file.FileName, ct));
    }

    /// <summary>Coarse gate here; the service checks the permission of the record the file belongs to.</summary>
    [HttpGet("{id:guid}"),
     HasPermission(Permissions.Documents.View, Permissions.Checklists.View, Permissions.Occurrences.View,
         Permissions.Documents.Manage, Permissions.Checklists.Execute, Permissions.Occurrences.Create, Permissions.Fuel.View)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var download = await service.OpenAsync(id, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpDelete("{id:guid}"),
     HasPermission(Permissions.Documents.Manage, Permissions.Occurrences.Manage, Permissions.Checklists.Execute, Permissions.Occurrences.Create,
         Permissions.Fuel.Create, Permissions.Fuel.Correct)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.RemoveAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/checklist-templates")]
public sealed class ChecklistTemplatesController(ChecklistTemplateService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Checklists.View, Permissions.Checklists.Execute, Permissions.Operations.Configure)]
    public Task<IReadOnlyList<ChecklistTemplateListItemResponse>> List([FromQuery] bool includeInactive, CancellationToken ct) =>
        service.ListAsync(includeInactive, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Checklists.View, Permissions.Checklists.Execute, Permissions.Operations.Configure)]
    public Task<ChecklistTemplateResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Operations.Configure)]
    public async Task<ActionResult<ChecklistTemplateResponse>> Create(ChecklistTemplateRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Operations.Configure)]
    public Task<ChecklistTemplateResponse> Update(Guid id, ChecklistTemplateRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Operations.Configure)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/checklists")]
public sealed class ChecklistsController(ChecklistService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Checklists.View)]
    public Task<PagedResult<ChecklistListItemResponse>> List([FromQuery] ChecklistListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("pending"), HasPermission(Permissions.Checklists.View, Permissions.Checklists.Execute)]
    public Task<IReadOnlyList<PendingChecklistResponse>> Pending(CancellationToken ct) => service.PendingAsync(ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Checklists.View)]
    public Task<ChecklistExecutionResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Checklists.Execute)]
    public async Task<ActionResult<ChecklistExecutionResponse>> Submit(ChecklistExecutionRequest request, CancellationToken ct)
    {
        var created = await service.SubmitAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }
}

[ApiController]
[Route("api/v1/occurrences")]
public sealed class OccurrencesController(OccurrenceService service) : ControllerBase
{
    [HttpGet, HasPermission(Permissions.Occurrences.View)]
    public Task<PagedResult<OccurrenceListItemResponse>> List([FromQuery] OccurrenceListRequest request, CancellationToken ct) =>
        service.ListAsync(request, ct);

    [HttpGet("{id:guid}"), HasPermission(Permissions.Occurrences.View)]
    public Task<OccurrenceResponse> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost, HasPermission(Permissions.Occurrences.Create)]
    public async Task<ActionResult<OccurrenceResponse>> Create(OccurrenceRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Occurrences.Manage)]
    public Task<OccurrenceResponse> Update(Guid id, OccurrenceRequest request, CancellationToken ct) => service.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/status"), HasPermission(Permissions.Occurrences.Manage)]
    public Task<OccurrenceResponse> ChangeStatus(Guid id, OccurrenceStatusRequest request, CancellationToken ct) =>
        service.ChangeStatusAsync(id, request, ct);
}
