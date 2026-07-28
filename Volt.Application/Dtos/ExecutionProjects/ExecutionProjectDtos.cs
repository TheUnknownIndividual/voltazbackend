namespace Volt.Application.Dtos.ExecutionProjects;

public sealed class ExecutionProjectDto
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public int? AdminTrackedProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? SourceOfferPrice { get; set; }
    public int ProjectManagerAdminUserId { get; set; }
    public string ProjectManagerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public IReadOnlyList<ExecutionStaffDto> Staff { get; set; } = Array.Empty<ExecutionStaffDto>();
    public IReadOnlyList<ExecutionExternalWorkerDto> ExternalWorkers { get; set; } = Array.Empty<ExecutionExternalWorkerDto>();
    public IReadOnlyList<ExecutionBoqItemDto> BoqItems { get; set; } = Array.Empty<ExecutionBoqItemDto>();
    public IReadOnlyList<ExecutionWarehouseMovementDto> WarehouseMovements { get; set; } = Array.Empty<ExecutionWarehouseMovementDto>();
    public IReadOnlyList<ExecutionTaskDto> Tasks { get; set; } = Array.Empty<ExecutionTaskDto>();
}

public sealed record ExecutionStaffDto(int Id, int AdminUserId, string DisplayName, string RoleName, DateTime? StartDate, DateTime? EndDate);
public sealed record ExecutionExternalWorkerDto(int Id, string FirstName, string LastName, DateTime StartDate, DateTime EndDate, decimal? AmountPaid, string Note);
public sealed record ExecutionBoqItemDto(int Id, int? ProductId, string ItemName, string Unit, decimal PlannedQuantity, decimal? UnitCost, string Note);
public sealed record ExecutionWarehouseMovementDto(int Id, int? ProductId, int? ProductParametrId, string ItemName, string Unit, decimal Quantity, decimal? UnitCost, string Direction, DateTime MovedAt, int RecordedByAdminUserId, string RecordedByName, string ApprovalStatus, int? ApprovedByAdminUserId, string? ApprovedByName, DateTime? ApprovedAt, string ApprovalNote);
public sealed record ExecutionTaskDto(int Id, int AssignedAdminUserId, string AssignedToName, string Title, string Description, DateTime? DueAt, string Status, string NotificationStatus, DateTime CreatedAt, DateTime? CompletedAt);
public sealed record ExecutionProjectSourceDto(int Id, string Name);
public sealed record ExecutionAdminUserDto(int Id, string DisplayName);
public sealed record ExecutionProductVariantDto(int Id, string Label, int InStockQuantity);
public sealed record ExecutionProductDto(int Id, string Name, int InStockQuantity, IReadOnlyList<ExecutionProductVariantDto> Variants);

public sealed class ExecutionProjectBootstrapDto
{
    public IReadOnlyList<ExecutionProjectSourceDto> ActiveProjects { get; set; } = Array.Empty<ExecutionProjectSourceDto>();
    public IReadOnlyList<ExecutionAdminUserDto> AdminUsers { get; set; } = Array.Empty<ExecutionAdminUserDto>();
    public IReadOnlyList<ExecutionProductDto> Products { get; set; } = Array.Empty<ExecutionProductDto>();
}

public sealed class CreateExecutionProjectRequest
{
    public int ProjectId { get; set; }
    public int ProjectManagerAdminUserId { get; set; }
    public string Status { get; set; } = "Aktiv";
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
}

public sealed class UpdateExecutionProjectRequest
{
    public int ProjectManagerAdminUserId { get; set; }
    public string Status { get; set; } = "Aktiv";
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
}

public sealed class AddExecutionStaffRequest { public int AdminUserId { get; set; } public string RoleName { get; set; } = string.Empty; public DateTime? StartDate { get; set; } public DateTime? EndDate { get; set; } }
public sealed class AddExecutionExternalWorkerRequest { public string FirstName { get; set; } = string.Empty; public string LastName { get; set; } = string.Empty; public decimal AmountPaid { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public string Note { get; set; } = string.Empty; }
public sealed class AddExecutionExpenseRequest { public string Category { get; set; } = "Müxtəlif xərclər"; public string Name { get; set; } = string.Empty; public decimal Amount { get; set; } public DateTime ExpenseDate { get; set; } public string ReceiptUrl { get; set; } = string.Empty; public string Note { get; set; } = string.Empty; }
public sealed class AddExecutionBoqItemRequest { public int? ProductId { get; set; } public string ItemName { get; set; } = string.Empty; public string Unit { get; set; } = "ədəd"; public decimal PlannedQuantity { get; set; } public decimal? UnitCost { get; set; } public string Note { get; set; } = string.Empty; }
public sealed class AddExecutionWarehouseMovementRequest { public int? ProductId { get; set; } public int? ProductParametrId { get; set; } public string ItemName { get; set; } = string.Empty; public string Unit { get; set; } = "ədəd"; public decimal Quantity { get; set; } public decimal? UnitCost { get; set; } public string Direction { get; set; } = "OUT"; public DateTime MovedAt { get; set; } }
public sealed class ApproveExecutionWarehouseMovementRequest { public bool Approved { get; set; } public string Note { get; set; } = string.Empty; }
public sealed class AddExecutionTaskRequest { public int AssignedAdminUserId { get; set; } public string Title { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public DateTime? DueAt { get; set; } }
