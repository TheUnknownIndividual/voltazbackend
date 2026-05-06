using Volt.Application.Dtos;
using Volt.Application.Dtos.ServiceRequest;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ServiceRequestService : IServiceRequestService
    {
        private readonly IUnitOfWork _uow;

        public ServiceRequestService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<IReadOnlyList<ServiceRequestDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default)
        {
            var requests = await _uow.Repository<ServiceRequest>().ListNoTrackingAsync(x => x.IsActive, ct);

            if (status is not null)
            {
                requests = requests.Where(x => x.Status == status.Value).ToList();
            }

            var result = requests
                .OrderByDescending(x => x.CreatedAt)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<IReadOnlyList<ServiceRequestDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<ServiceRequestDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var request = await _uow.Repository<ServiceRequest>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (request is null)
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND,
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND);
            }

            return ApiResponse<ServiceRequestDto>.SuccessResponse(MapToDto(request));
        }

        public async Task<ApiResponse<ServiceRequestDto>> CreateAsync(ServiceRequestCreateRequest request, CancellationToken ct = default)
        {
            if (!await IsServiceManagementValidAsync(request.ServiceManagementId, ct))
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVICE_NOT_FOUND,
                    ErrorCode.SERVICE_NOT_FOUND);
            }

            try
            {
                var entity = new ServiceRequest
                {
                    Name = request.Name.Trim(),
                    Surname = request.Surname.Trim(),
                    Email = request.Email.Trim(),
                    Phone = request.Phone.Trim(),
                    Message = request.Message.Trim(),
                    ServiceManagementId = request.ServiceManagementId,
                    Status = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await _uow.Repository<ServiceRequest>().AddAsync(entity, ct);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ServiceRequestDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the service request.");
            }
        }

        public async Task<ApiResponse<ServiceRequestDto>> UpdateAsync(int id, ServiceRequestUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ServiceRequest>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND,
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND);
            }

            if (!await IsServiceManagementValidAsync(request.ServiceManagementId, ct))
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVICE_NOT_FOUND,
                    ErrorCode.SERVICE_NOT_FOUND);
            }

            var statusValidationError = ValidateStatusTransition(entity.Status, request.Status);
            if (statusValidationError is not null)
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(statusValidationError, statusValidationError);
            }

            try
            {
                entity.Name = request.Name.Trim();
                entity.Surname = request.Surname.Trim();
                entity.Email = request.Email.Trim();
                entity.Phone = request.Phone.Trim();
                entity.Message = request.Message.Trim();
                entity.Status = request.Status;
                entity.ServiceManagementId = request.ServiceManagementId;
                entity.UpdatedAt = DateTime.UtcNow;

                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ServiceRequestDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the service request.");
            }
        }

        public async Task<ApiResponse<ServiceRequestDto>> UpdateStatusAsync(int id, ServiceRequestStatusUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ServiceRequest>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND,
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND);
            }

            var statusValidationError = ValidateStatusTransition(entity.Status, request.Status);
            if (statusValidationError is not null)
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(statusValidationError, statusValidationError);
            }

            try
            {
                entity.Status = request.Status;
                entity.UpdatedAt = DateTime.UtcNow;

                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ServiceRequestDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<ServiceRequestDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating service request status.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ServiceRequest>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND,
                    ErrorCode.SERVICE_REQUEST_NOT_FOUND);
            }

            try
            {
                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;
                repo.Update(entity);

                await _uow.SaveChangesAsync(ct);
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the service request.");
            }
        }

        private async Task<bool> IsServiceManagementValidAsync(int serviceManagementId, CancellationToken ct)
            => await _uow.Repository<ServiceManagement>()
                .AnyAsync(x => x.Id == serviceManagementId && x.IsActive, ct);

        private static string? ValidateStatusTransition(byte currentStatus, byte targetStatus)
        {
            if (targetStatus < 1 || targetStatus > 3)
            {
                return ErrorCode.INVALID_STATUS_VALUE;
            }

            if (targetStatus < currentStatus)
            {
                return ErrorCode.INVALID_STATUS_TRANSITION;
            }

            return null;
        }

        private static ServiceRequestDto MapToDto(ServiceRequest request)
            => new(
                request.Id,
                request.Name,
                request.Surname,
                request.Email,
                request.Phone,
                request.Message,
                request.Status,
                request.ServiceManagementId,
                request.CreatedAt,
                request.UpdatedAt);
    }
}
