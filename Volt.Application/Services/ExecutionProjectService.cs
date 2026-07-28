using Volt.Application.Dtos;
using Volt.Application.Dtos.ExecutionProjects;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services;

/// <summary>
/// Keeps delivery records separate from the public Projects catalogue. Warehouse entries are
/// audit records and update their selected product variant immediately.
/// </summary>
public sealed class ExecutionProjectService : IExecutionProjectService
{
    private readonly IUnitOfWork _uow;
    private readonly ITelegramTaskNotificationService _telegram;
    public ExecutionProjectService(IUnitOfWork uow, ITelegramTaskNotificationService telegram) { _uow = uow; _telegram = telegram; }

    public async Task<ApiResponse<ExecutionProjectBootstrapDto>> GetBootstrapAsync(CancellationToken ct = default)
    {
        var projects = await _uow.Repository<Project>().ListNoTrackingAsync(x => x.IsActive, ct);
        var languages = await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(x => x.IsActive, ct);
        var admins = await _uow.Repository<AdminUser>().ListNoTrackingAsync(x => x.IsActive, ct);
        var products = await _uow.Repository<Product>().ListNoTrackingAsync(x => x.IsActive, ct);
        var parameters = await _uow.Repository<ProductParametr>().ListNoTrackingAsync(x => x.IsActive, ct);

        return ApiResponse<ExecutionProjectBootstrapDto>.SuccessResponse(new ExecutionProjectBootstrapDto
        {
            ActiveProjects = projects.OrderBy(x => x.Id).Select(project => new ExecutionProjectSourceDto(project.Id, ProjectName(project, languages))).ToList(),
            AdminUsers = admins.OrderBy(x => x.DisplayName ?? x.Username).Select(x => new ExecutionAdminUserDto(x.Id, DisplayName(x))).ToList(),
            Products = products.OrderBy(x => x.ProductName).Select(product =>
            {
                var variants = parameters.Where(x => x.ProductId == product.Id).OrderBy(x => x.Id)
                    .Select(x => new ExecutionProductVariantDto(x.Id, string.IsNullOrWhiteSpace(x.TechnicalPower) ? $"Variant #{x.Id}" : x.TechnicalPower, x.Count ?? 0)).ToList();
                return new ExecutionProductDto(product.Id, product.ProductName, variants.Sum(x => x.InStockQuantity), variants);
            }).ToList()
        });
    }

    public async Task<ApiResponse<IReadOnlyList<ExecutionProjectDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var projects = await _uow.Repository<ExecutionProject>().ListNoTrackingAsync(x => !x.ArchivedAt.HasValue, ct);
        var results = new List<ExecutionProjectDto>(projects.Count);
        foreach (var project in projects.OrderByDescending(x => x.CreatedAt)) results.Add(await MapAsync(project, ct));
        return ApiResponse<IReadOnlyList<ExecutionProjectDto>>.SuccessResponse(results);
    }

    public async Task<ApiResponse<ExecutionProjectDto>> CreateAsync(CreateExecutionProjectRequest request, CancellationToken ct = default)
    {
        if (request.ProjectId <= 0 || request.ProjectManagerAdminUserId <= 0)
            return Invalid<ExecutionProjectDto>("Active project and project manager are required.");
        if (await _uow.Repository<ExecutionProject>().AnyAsync(x => x.ProjectId == request.ProjectId, ct))
            return Invalid<ExecutionProjectDto>("This active project is already in the execution list.");
        if (!await _uow.Repository<Project>().AnyAsync(x => x.Id == request.ProjectId && x.IsActive, ct))
            return Invalid<ExecutionProjectDto>("The selected public project is not active.");
        if (!await _uow.Repository<AdminUser>().AnyAsync(x => x.Id == request.ProjectManagerAdminUserId && x.IsActive, ct))
            return Invalid<ExecutionProjectDto>("The selected project manager is not active.");

        var entity = new ExecutionProject
        {
            ProjectId = request.ProjectId,
            ProjectManagerAdminUserId = request.ProjectManagerAdminUserId,
            Status = Normalize(request.Status, 40, "Aktiv"),
            PlannedStartDate = request.PlannedStartDate?.ToUniversalTime(),
            PlannedEndDate = request.PlannedEndDate?.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _uow.Repository<ExecutionProject>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        await PrefillPublicProjectBoqAsync(entity, request.ProjectId, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(entity, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> UpdateAsync(int id, UpdateExecutionProjectRequest request, CancellationToken ct = default)
    {
        var entity = await FindAsync(id, ct);
        if (entity is null) return NotFound<ExecutionProjectDto>();
        if (request.ProjectManagerAdminUserId <= 0 || !await _uow.Repository<AdminUser>().AnyAsync(x => x.Id == request.ProjectManagerAdminUserId && x.IsActive, ct))
            return Invalid<ExecutionProjectDto>("The selected project manager is not active.");
        entity.ProjectManagerAdminUserId = request.ProjectManagerAdminUserId;
        entity.Status = Normalize(request.Status, 40, "Aktiv");
        entity.PlannedStartDate = request.PlannedStartDate?.ToUniversalTime();
        entity.PlannedEndDate = request.PlannedEndDate?.ToUniversalTime();
        entity.UpdatedAt = DateTime.UtcNow;
        _uow.Repository<ExecutionProject>().Update(entity);
        await _uow.SaveChangesAsync(ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(entity, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> AddStaffAsync(int id, AddExecutionStaffRequest request, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        if (request.AdminUserId <= 0 || string.IsNullOrWhiteSpace(request.RoleName)) return Invalid<ExecutionProjectDto>("Staff member and role are required.");
        if (!await _uow.Repository<AdminUser>().AnyAsync(x => x.Id == request.AdminUserId && x.IsActive, ct)) return Invalid<ExecutionProjectDto>("The selected staff account is not active.");
        if (await _uow.Repository<ExecutionProjectStaff>().AnyAsync(x => x.ExecutionProjectId == id && x.AdminUserId == request.AdminUserId, ct)) return Invalid<ExecutionProjectDto>("This staff member is already assigned.");
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate.Value.Date < request.StartDate.Value.Date) return Invalid<ExecutionProjectDto>("Staff end date cannot be before the start date.");
        await _uow.Repository<ExecutionProjectStaff>().AddAsync(new ExecutionProjectStaff { ExecutionProjectId = id, AdminUserId = request.AdminUserId, RoleName = Normalize(request.RoleName, 100), StartDate = request.StartDate?.ToUniversalTime(), EndDate = request.EndDate?.ToUniversalTime(), AddedAt = DateTime.UtcNow }, ct);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> RemoveStaffAsync(int id, int staffId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        var staff = await _uow.Repository<ExecutionProjectStaff>().FirstOrDefaultAsync(x => x.Id == staffId && x.ExecutionProjectId == id, ct);
        if (staff is null) return Invalid<ExecutionProjectDto>("Staff record was not found.");
        _uow.Repository<ExecutionProjectStaff>().Remove(staff);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> AddExternalWorkerAsync(int id, AddExecutionExternalWorkerRequest request, int actorAdminUserId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || request.AmountPaid <= 0)
            return Invalid<ExecutionProjectDto>("First name, last name and a positive payment are required.");
        if (request.EndDate.Date < request.StartDate.Date) return Invalid<ExecutionProjectDto>("End date cannot be before the start date.");
        await _uow.Repository<ExecutionProjectExternalWorker>().AddAsync(new ExecutionProjectExternalWorker
        {
            ExecutionProjectId = id, FirstName = Normalize(request.FirstName, 100), LastName = Normalize(request.LastName, 100),
            AmountPaid = request.AmountPaid, StartDate = request.StartDate.ToUniversalTime(), EndDate = request.EndDate.ToUniversalTime(),
            Note = Normalize(request.Note, 500), CreatedByAdminUserId = actorAdminUserId, CreatedAt = DateTime.UtcNow
        }, ct);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> RemoveExternalWorkerAsync(int id, int workerId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        var worker = await _uow.Repository<ExecutionProjectExternalWorker>().FirstOrDefaultAsync(x => x.Id == workerId && x.ExecutionProjectId == id, ct);
        if (worker is null) return Invalid<ExecutionProjectDto>("External worker was not found.");
        _uow.Repository<ExecutionProjectExternalWorker>().Remove(worker);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> AddBoqItemAsync(int id, AddExecutionBoqItemRequest request, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        if (string.IsNullOrWhiteSpace(request.ItemName) || request.PlannedQuantity <= 0) return Invalid<ExecutionProjectDto>("BoQ item name and quantity must be provided.");
        if (request.ProductId.HasValue && !await _uow.Repository<Product>().AnyAsync(x => x.Id == request.ProductId && x.IsActive, ct)) return Invalid<ExecutionProjectDto>("The selected warehouse product is not active.");
        await _uow.Repository<ExecutionProjectBoqItem>().AddAsync(new ExecutionProjectBoqItem { ExecutionProjectId = id, ProductId = request.ProductId, ItemName = Normalize(request.ItemName, 200), Unit = Normalize(request.Unit, 24, "ədəd"), PlannedQuantity = request.PlannedQuantity, UnitCost = request.UnitCost, Note = Normalize(request.Note, 500), CreatedAt = DateTime.UtcNow }, ct);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> UpdateBoqItemAsync(int id, int boqItemId, AddExecutionBoqItemRequest request, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        if (string.IsNullOrWhiteSpace(request.ItemName) || request.PlannedQuantity <= 0) return Invalid<ExecutionProjectDto>("BoQ item name and quantity must be provided.");
        if (request.ProductId.HasValue && !await _uow.Repository<Product>().AnyAsync(x => x.Id == request.ProductId && x.IsActive, ct)) return Invalid<ExecutionProjectDto>("The selected warehouse product is not active.");
        var item = await _uow.Repository<ExecutionProjectBoqItem>().FirstOrDefaultAsync(x => x.Id == boqItemId && x.ExecutionProjectId == id, ct);
        if (item is null) return Invalid<ExecutionProjectDto>("BoQ item was not found.");
        item.ProductId = request.ProductId;
        item.ItemName = Normalize(request.ItemName, 200);
        item.Unit = Normalize(request.Unit, 24, "ədəd");
        item.PlannedQuantity = request.PlannedQuantity;
        item.UnitCost = request.UnitCost;
        item.Note = Normalize(request.Note, 500);
        _uow.Repository<ExecutionProjectBoqItem>().Update(item);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> RemoveBoqItemAsync(int id, int boqItemId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        var item = await _uow.Repository<ExecutionProjectBoqItem>().FirstOrDefaultAsync(x => x.Id == boqItemId && x.ExecutionProjectId == id, ct);
        if (item is null) return Invalid<ExecutionProjectDto>("BoQ item was not found.");
        _uow.Repository<ExecutionProjectBoqItem>().Remove(item);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> PrefillBoqAsync(int id, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        if (!await _uow.Repository<ExecutionProjectBoqItem>().AnyAsync(x => x.ExecutionProjectId == id, ct))
        {
            if (project.AdminTrackedProjectId is int trackedId)
            {
                var tracked = await _uow.Repository<AdminTrackedProject>().FirstOrDefaultNoTrackingAsync(x => x.Id == trackedId, ct);
                if (tracked is not null) await AddTrackedSourceBoqAsync(project, tracked, ct);
            }
            else if (project.ProjectId is int sourceId)
            {
                await PrefillPublicProjectBoqAsync(project, sourceId, ct);
            }
            await TouchAsync(project, ct);
        }
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> AddWarehouseMovementAsync(int id, AddExecutionWarehouseMovementRequest request, int actorAdminUserId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        var direction = Normalize(request.Direction, 3).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(request.ItemName) || request.Quantity <= 0 || (direction != "OUT" && direction != "IN")) return Invalid<ExecutionProjectDto>("Movement must include item, positive quantity, and OUT or IN direction.");
        if (request.ProductId.HasValue)
        {
            if (!request.ProductParametrId.HasValue) return Invalid<ExecutionProjectDto>("Select the warehouse product variant so stock is updated safely.");
            if (decimal.Truncate(request.Quantity) != request.Quantity) return Invalid<ExecutionProjectDto>("Warehouse variants use whole-unit quantities.");
            if (!await _uow.Repository<Product>().AnyAsync(x => x.Id == request.ProductId && x.IsActive, ct)) return Invalid<ExecutionProjectDto>("The selected warehouse product is not active.");
            if (!await _uow.Repository<ProductParametr>().AnyAsync(x => x.Id == request.ProductParametrId && x.ProductId == request.ProductId && x.IsActive, ct)) return Invalid<ExecutionProjectDto>("The selected warehouse variant does not belong to this product.");
        }
        else if (request.ProductParametrId.HasValue) return Invalid<ExecutionProjectDto>("A warehouse variant cannot be selected without its product.");
        if (request.UnitCost is < 0) return Invalid<ExecutionProjectDto>("Unit cost cannot be negative.");
        if (request.ProductParametrId.HasValue)
        {
            var stockVariant = await _uow.Repository<ProductParametr>().FirstOrDefaultAsync(x => x.Id == request.ProductParametrId.Value && x.IsActive, ct);
            if (stockVariant is null) return Invalid<ExecutionProjectDto>("The warehouse variant no longer exists.");
            var amount = (int)request.Quantity;
            var inStock = stockVariant.Count ?? 0;
            if (direction == "OUT" && inStock < amount) return Invalid<ExecutionProjectDto>($"Insufficient stock. Available: {inStock}.");
            stockVariant.Count = direction == "OUT" ? inStock - amount : inStock + amount;
            _uow.Repository<ProductParametr>().Update(stockVariant);
        }
        var recordedAt = DateTime.UtcNow;
        await _uow.Repository<ExecutionWarehouseMovement>().AddAsync(new ExecutionWarehouseMovement { ExecutionProjectId = id, ProductId = request.ProductId, ProductParametrId = request.ProductParametrId, ItemName = Normalize(request.ItemName, 200), Unit = Normalize(request.Unit, 24, "ədəd"), Quantity = request.Quantity, UnitCost = request.UnitCost, Direction = direction, MovedAt = (request.MovedAt == default ? recordedAt : request.MovedAt).ToUniversalTime(), RecordedByAdminUserId = actorAdminUserId, ApprovalStatus = "Approved", ApprovedByAdminUserId = actorAdminUserId, ApprovedAt = recordedAt }, ct);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> ApproveWarehouseMovementAsync(int id, int movementId, ApproveExecutionWarehouseMovementRequest request, int actorAdminUserId, bool actorCanApproveWarehouseMovements, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        if (!actorCanApproveWarehouseMovements && project.ProjectManagerAdminUserId != actorAdminUserId) return Invalid<ExecutionProjectDto>("Only the project manager or Head of Engineering may approve a warehouse movement.");
        var movement = await _uow.Repository<ExecutionWarehouseMovement>().FirstOrDefaultAsync(x => x.Id == movementId && x.ExecutionProjectId == id, ct);
        if (movement is null) return Invalid<ExecutionProjectDto>("Warehouse movement was not found.");
        if (movement.ApprovalStatus != "Pending") return Invalid<ExecutionProjectDto>("This movement has already been reviewed.");
        if (request.Approved && movement.ProductParametrId.HasValue)
        {
            var stockVariant = await _uow.Repository<ProductParametr>().FirstOrDefaultAsync(x => x.Id == movement.ProductParametrId.Value && x.IsActive, ct);
            if (stockVariant is null) return Invalid<ExecutionProjectDto>("The warehouse variant no longer exists.");
            var amount = (int)movement.Quantity;
            var inStock = stockVariant.Count ?? 0;
            if (movement.Direction == "OUT" && inStock < amount) return Invalid<ExecutionProjectDto>($"Insufficient stock for approval. Available: {inStock}.");
            stockVariant.Count = movement.Direction == "OUT" ? inStock - amount : inStock + amount;
            _uow.Repository<ProductParametr>().Update(stockVariant);
        }
        movement.ApprovalStatus = request.Approved ? "Approved" : "Rejected";
        movement.ApprovedByAdminUserId = actorAdminUserId;
        movement.ApprovedAt = DateTime.UtcNow;
        movement.ApprovalNote = Normalize(request.Note, 500);
        _uow.Repository<ExecutionWarehouseMovement>().Update(movement);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> AddTaskAsync(int id, AddExecutionTaskRequest request, int actorAdminUserId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        if (request.AssignedAdminUserId <= 0 || string.IsNullOrWhiteSpace(request.Title)) return Invalid<ExecutionProjectDto>("Assignee and task title are required.");
        var isMember = project.ProjectManagerAdminUserId == request.AssignedAdminUserId || await _uow.Repository<ExecutionProjectStaff>().AnyAsync(x => x.ExecutionProjectId == id && x.AdminUserId == request.AssignedAdminUserId, ct);
        if (!isMember) return Invalid<ExecutionProjectDto>("Tasks can be assigned only to the project manager or listed staff.");
        var assignedTask = new ExecutionProjectTask { ExecutionProjectId = id, AssignedAdminUserId = request.AssignedAdminUserId, CreatedByAdminUserId = actorAdminUserId, Title = Normalize(request.Title, 180), Description = Normalize(request.Description, 2000), DueAt = request.DueAt?.ToUniversalTime(), Status = "Assigned", NotificationStatus = "PendingRecipientLink", CreatedAt = DateTime.UtcNow };
        await _uow.Repository<ExecutionProjectTask>().AddAsync(assignedTask, ct);
        await TouchAsync(project, ct);
        var assignee = await _uow.Repository<AdminUser>().FirstOrDefaultNoTrackingAsync(x => x.Id == request.AssignedAdminUserId && x.IsActive, ct);
        if (assignee?.TelegramChatId is long chatId)
        {
            assignedTask.NotificationStatus = await _telegram.SendTaskAssignedAsync(chatId, await ResolveProjectNameAsync(project, ct), assignedTask.Title, assignedTask.Description, assignedTask.DueAt, ct);
            _uow.Repository<ExecutionProjectTask>().Update(assignedTask);
            await _uow.SaveChangesAsync(ct);
        }
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> CompleteTaskAsync(int id, int taskId, int actorAdminUserId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        var task = await _uow.Repository<ExecutionProjectTask>().FirstOrDefaultAsync(x => x.Id == taskId && x.ExecutionProjectId == id, ct);
        if (task is null) return Invalid<ExecutionProjectDto>("Task was not found.");
        if (task.AssignedAdminUserId != actorAdminUserId && project.ProjectManagerAdminUserId != actorAdminUserId) return Invalid<ExecutionProjectDto>("Only the assignee or project manager may complete the task.");
        task.Status = "Completed"; task.CompletedAt = DateTime.UtcNow;
        _uow.Repository<ExecutionProjectTask>().Update(task);
        await TouchAsync(project, ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    public async Task<ApiResponse<ExecutionProjectDto>> RetryTaskNotificationAsync(int id, int taskId, CancellationToken ct = default)
    {
        var project = await FindAsync(id, ct);
        if (project is null) return NotFound<ExecutionProjectDto>();
        var task = await _uow.Repository<ExecutionProjectTask>().FirstOrDefaultAsync(x => x.Id == taskId && x.ExecutionProjectId == id, ct);
        if (task is null) return Invalid<ExecutionProjectDto>("Task was not found.");
        var assignee = await _uow.Repository<AdminUser>().FirstOrDefaultNoTrackingAsync(x => x.Id == task.AssignedAdminUserId && x.IsActive, ct);
        if (assignee?.TelegramChatId is not long chatId) { task.NotificationStatus = "PendingRecipientLink"; }
        else
        {
            task.NotificationStatus = await _telegram.SendTaskAssignedAsync(chatId, await ResolveProjectNameAsync(project, ct), task.Title, task.Description, task.DueAt, ct);
        }
        _uow.Repository<ExecutionProjectTask>().Update(task);
        await _uow.SaveChangesAsync(ct);
        return ApiResponse<ExecutionProjectDto>.SuccessResponse(await MapAsync(project, ct));
    }

    private async Task<ExecutionProject?> FindAsync(int id, CancellationToken ct) => await _uow.Repository<ExecutionProject>().FirstOrDefaultAsync(x => x.Id == id && !x.ArchivedAt.HasValue, ct);
    private async Task<string> ResolveProjectNameAsync(ExecutionProject project, CancellationToken ct)
    {
        if (project.ProjectId.HasValue)
        {
            var source = await _uow.Repository<Project>().FirstOrDefaultNoTrackingAsync(x => x.Id == project.ProjectId.Value, ct);
            if (source is not null)
            {
                var languages = await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(x => x.ProjectId == project.ProjectId.Value && x.IsActive, ct);
                return ProjectName(source, languages);
            }
        }
        if (project.AdminTrackedProjectId.HasValue)
            return (await _uow.Repository<AdminTrackedProject>().FirstOrDefaultNoTrackingAsync(x => x.Id == project.AdminTrackedProjectId.Value, ct))?.Name ?? $"Project #{project.Id}";
        return $"Project #{project.Id}";
    }
    private async Task TouchAsync(ExecutionProject project, CancellationToken ct) { project.UpdatedAt = DateTime.UtcNow; _uow.Repository<ExecutionProject>().Update(project); await _uow.SaveChangesAsync(ct); }

    private async Task PrefillPublicProjectBoqAsync(ExecutionProject execution, int projectId, CancellationToken ct)
    {
        var offers = await _uow.Repository<ProjectOffer>().ListNoTrackingAsync(x => x.ProjectId == projectId && x.IsActive, ct);
        foreach (var offer in offers.OrderBy(x => x.Id))
        {
            await _uow.Repository<ExecutionProjectBoqItem>().AddAsync(new ExecutionProjectBoqItem
            {
                ExecutionProjectId = execution.Id,
                ItemName = $"Mənbə təklifi #{offer.Id}" + (string.IsNullOrWhiteSpace(offer.AreaType) ? string.Empty : $" · {offer.AreaType}"),
                Unit = "kW",
                PlannedQuantity = offer.Power,
                Note = "Layihə təklifindən ilkin BOQ sətri; məhsulu icra zamanı seçin.",
                CreatedAt = DateTime.UtcNow
            }, ct);
        }
        if (offers.Count > 0) await _uow.SaveChangesAsync(ct);
    }

    private async Task AddTrackedSourceBoqAsync(ExecutionProject execution, AdminTrackedProject source, CancellationToken ct)
    {
        var offers = await _uow.Repository<AdminTrackedProjectOffer>().ListNoTrackingAsync(x => x.AdminTrackedProjectId == source.Id && x.IsActive, ct);
        foreach (var offer in offers.OrderBy(x => x.Id))
        {
            await _uow.Repository<ExecutionProjectBoqItem>().AddAsync(new ExecutionProjectBoqItem
            {
                ExecutionProjectId = execution.Id,
                ItemName = $"Mənbə təklifi #{offer.Id}" + (string.IsNullOrWhiteSpace(offer.AreaType) ? string.Empty : $" · {offer.AreaType}"),
                Unit = "kW",
                PlannedQuantity = offer.Power,
                UnitCost = offer.Power > 0 && offer.ExtraAmount > 0 ? offer.ExtraAmount / offer.Power : null,
                Note = $"Layihə təklifindən ilkin BOQ sətri; montaj: {offer.MountType}. Məhsulu icra zamanı seçin.",
                CreatedAt = DateTime.UtcNow
            }, ct);
        }
    }

    private async Task<ExecutionProjectDto> MapAsync(ExecutionProject project, CancellationToken ct)
    {
        var projectSource = project.ProjectId.HasValue
            ? await _uow.Repository<Project>().FirstOrDefaultNoTrackingAsync(x => x.Id == project.ProjectId.Value, ct)
            : null;
        var trackedSource = project.AdminTrackedProjectId.HasValue
            ? await _uow.Repository<AdminTrackedProject>().FirstOrDefaultNoTrackingAsync(x => x.Id == project.AdminTrackedProjectId.Value, ct)
            : null;
        IEnumerable<ProjectLanguage> languages = project.ProjectId.HasValue
            ? await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(x => x.ProjectId == project.ProjectId.Value && x.IsActive, ct)
            : Array.Empty<ProjectLanguage>();
        var admins = await _uow.Repository<AdminUser>().ListNoTrackingAsync(ct);
        var adminNames = admins.ToDictionary(x => x.Id, DisplayName);
        var staff = await _uow.Repository<ExecutionProjectStaff>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var boq = await _uow.Repository<ExecutionProjectBoqItem>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var movements = await _uow.Repository<ExecutionWarehouseMovement>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var tasks = await _uow.Repository<ExecutionProjectTask>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var externalWorkers = await _uow.Repository<ExecutionProjectExternalWorker>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var sourceLanguage = languages.FirstOrDefault(x => x.LanguageCode == LanguageCode.AZ) ?? languages.FirstOrDefault();
        return new ExecutionProjectDto
        {
            Id = project.Id, ProjectId = project.ProjectId, AdminTrackedProjectId = project.AdminTrackedProjectId,
            ProjectName = projectSource is not null ? ProjectName(projectSource, languages) : trackedSource?.Name ?? $"Project #{project.Id}",
            Location = projectSource is not null ? sourceLanguage?.Location ?? string.Empty : trackedSource?.Location ?? string.Empty,
            Description = projectSource is not null ? sourceLanguage?.Description ?? string.Empty : trackedSource?.Description ?? string.Empty,
            SourceOfferPrice = projectSource is not null ? projectSource.OfferAmountAzn : trackedSource?.OfferPrice,
            ProjectManagerAdminUserId = project.ProjectManagerAdminUserId, ProjectManagerName = adminNames.GetValueOrDefault(project.ProjectManagerAdminUserId, "Unknown"), Status = project.Status, PlannedStartDate = project.PlannedStartDate, PlannedEndDate = project.PlannedEndDate,
            Staff = staff.OrderBy(x => x.AddedAt).Select(x => new ExecutionStaffDto(x.Id, x.AdminUserId, adminNames.GetValueOrDefault(x.AdminUserId, "Unknown"), x.RoleName, x.StartDate, x.EndDate)).ToList(),
            ExternalWorkers = externalWorkers.OrderBy(x => x.StartDate).Select(x => new ExecutionExternalWorkerDto(x.Id, x.FirstName, x.LastName, x.StartDate, x.EndDate, null, x.Note)).ToList(),
            BoqItems = boq.OrderBy(x => x.Id).Select(x => new ExecutionBoqItemDto(x.Id, x.ProductId, x.ItemName, x.Unit, x.PlannedQuantity, x.UnitCost, x.Note)).ToList(),
            WarehouseMovements = movements.OrderByDescending(x => x.MovedAt).Select(x => new ExecutionWarehouseMovementDto(x.Id, x.ProductId, x.ProductParametrId, x.ItemName, x.Unit, x.Quantity, x.UnitCost, x.Direction, x.MovedAt, x.RecordedByAdminUserId, adminNames.GetValueOrDefault(x.RecordedByAdminUserId, "Unknown"), x.ApprovalStatus, x.ApprovedByAdminUserId, x.ApprovedByAdminUserId.HasValue ? adminNames.GetValueOrDefault(x.ApprovedByAdminUserId.Value, "Unknown") : null, x.ApprovedAt, x.ApprovalNote)).ToList(),
            Tasks = tasks.OrderByDescending(x => x.CreatedAt).Select(x => new ExecutionTaskDto(x.Id, x.AssignedAdminUserId, adminNames.GetValueOrDefault(x.AssignedAdminUserId, "Unknown"), x.Title, x.Description, x.DueAt, x.Status, x.NotificationStatus, x.CreatedAt, x.CompletedAt)).ToList()
        };
    }
    private static string ProjectName(Project project, IEnumerable<ProjectLanguage> languages) => languages.FirstOrDefault(x => x.LanguageCode == LanguageCode.AZ)?.Title ?? languages.FirstOrDefault()?.Title ?? $"Project #{project.Id}";
    private static string DisplayName(AdminUser user) => string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
    private static string Normalize(string? value, int max = int.MaxValue, string fallback = "") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static ApiResponse<T> Invalid<T>(string message) => ApiResponse<T>.ErrorResponse(ErrorCode.VALIDATION_ERROR, message);
    private static ApiResponse<T> NotFound<T>() => ApiResponse<T>.ErrorResponse(ErrorCode.PROJECT_NOT_FOUND, "Execution project was not found.");
}
