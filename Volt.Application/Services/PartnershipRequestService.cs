using Volt.Application.Dtos;
using Volt.Application.Dtos.PartnershipRequest;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class PartnershipRequestService : IPartnershipRequestService
    {
        private const byte StatusNew = 1;
        private const byte StatusConnected = 2;
        private const byte StatusClosed = 3;

        private readonly IUnitOfWork _uow;

        public PartnershipRequestService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<IReadOnlyList<PartnershipRequestGetAllDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default)
        {
            var requests = await _uow.Repository<PartnershipRequest>().ListNoTrackingAsync(x => x.IsActive, ct);
            var partnershipTypeLanguages = await _uow.Repository<PartnershipTypeLanguage>().ListNoTrackingAsync(ct);

            if (status is not null)
            {
                requests = requests.Where(x => x.Status == status.Value).ToList();
            }

            var result = requests
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapToGetAllDto(x, partnershipTypeLanguages.FirstOrDefault(t => t.Id == x.PartnershipTypeId)?.Name))
                .ToList();

            return ApiResponse<IReadOnlyList<PartnershipRequestGetAllDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<PartnershipRequestDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var request = await _uow.Repository<PartnershipRequest>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (request is null)
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND);
            }

            return ApiResponse<PartnershipRequestDto>.SuccessResponse(MapToDto(request));
        }

        public async Task<ApiResponse<PartnershipRequestDto>> CreateAsync(PartnershipRequestCreateRequest request, CancellationToken ct = default)
        {
            if (!await IsPartnershipTypeValidAsync(request.PartnershipTypeId, ct))
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND);
            }

            try
            {
                var entity = new PartnershipRequest
                {
                    CompanyName = request.CompanyName.Trim(),
                    CompanyPerson = request.CompanyPerson.Trim(),
                    Email = request.Email.Trim(),
                    PhoneNumber = request.PhoneNumber.Trim(),
                    Message = request.Message.Trim(),
                    PartnershipTypeId = request.PartnershipTypeId,
                    Status = StatusNew,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await _uow.Repository<PartnershipRequest>().AddAsync(entity, ct);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<PartnershipRequestDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the partnership request.");
            }
        }

        public async Task<ApiResponse<PartnershipRequestDto>> UpdateAsync(int id, PartnershipRequestUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<PartnershipRequest>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND);
            }

            if (!await IsPartnershipTypeValidAsync(request.PartnershipTypeId, ct))
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND);
            }

            var statusValidationError = ValidateStatusTransition(entity.Status, request.Status);
            if (statusValidationError is not null)
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(statusValidationError, statusValidationError);
            }

            try
            {
                entity.CompanyName = request.CompanyName.Trim();
                entity.CompanyPerson = request.CompanyPerson.Trim();
                entity.Email = request.Email.Trim();
                entity.PhoneNumber = request.PhoneNumber.Trim();
                entity.Message = request.Message.Trim();
                entity.PartnershipTypeId = request.PartnershipTypeId;

                if (entity.Status != request.Status)
                {
                    entity.Status = request.Status;
                    entity.UpdatedAt = DateTime.UtcNow;
                }

                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<PartnershipRequestDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the partnership request.");
            }
        }

        public async Task<ApiResponse<PartnershipRequestDto>> UpdateStatusAsync(int id, PartnershipRequestStatusUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<PartnershipRequest>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND);
            }

            var statusValidationError = ValidateStatusTransition(entity.Status, request.Status);
            if (statusValidationError is not null)
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(statusValidationError, statusValidationError);
            }

            try
            {
                entity.Status = request.Status;
                entity.UpdatedAt = DateTime.UtcNow;

                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<PartnershipRequestDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating partnership request status.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<PartnershipRequest>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND);
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
                    "An error occurred while deleting the partnership request.");
            }
        }

        public async Task<ApiResponse<PartnershipRequestDto>> MarkViewedAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<PartnershipRequest>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<PartnershipRequestDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_REQUEST_NOT_FOUND);
            }

            if (!entity.IsViewedByAdmin)
            {
                entity.IsViewedByAdmin = true;
                entity.AdminViewedAt = DateTime.UtcNow;
                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);
            }

            return ApiResponse<PartnershipRequestDto>.SuccessResponse(MapToDto(entity));
        }

        private async Task<bool> IsPartnershipTypeValidAsync(int partnershipTypeId, CancellationToken ct)
            => await _uow.Repository<PartnershipType>()
                .AnyAsync(x => x.Id == partnershipTypeId && x.IsActive, ct);

        private static string? ValidateStatusTransition(byte currentStatus, byte targetStatus)
        {
            if (targetStatus is < StatusNew or > StatusClosed)
            {
                return ErrorCode.INVALID_STATUS_VALUE;
            }

            if (targetStatus == currentStatus)
            {
                return ErrorCode.PARTNERSHIP_REQUEST_STATUS_UNCHANGED;
            }

            if (currentStatus == StatusClosed)
            {
                return ErrorCode.INVALID_STATUS_TRANSITION;
            }

            if (currentStatus == StatusConnected && targetStatus == StatusNew)
            {
                return ErrorCode.INVALID_STATUS_TRANSITION;
            }

            if (targetStatus < currentStatus)
            {
                return ErrorCode.INVALID_STATUS_TRANSITION;
            }

            return null;
        }

        private static PartnershipRequestDto MapToDto(PartnershipRequest request)
            => new(
                request.Id,
                request.CompanyName,
                request.CompanyPerson,
                request.Email,
                request.PhoneNumber,
                request.Message,
                request.Status,
                request.PartnershipTypeId,
                request.CreatedAt,
                request.UpdatedAt,
                request.IsViewedByAdmin,
                request.AdminViewedAt);

        private static PartnershipRequestGetAllDto MapToGetAllDto(PartnershipRequest request, string? partnershipTypeName)
    => new(
        request.Id,
        request.CompanyName,
        request.PhoneNumber,
        partnershipTypeName,
        request.Status,
        request.CreatedAt,
        request.IsViewedByAdmin);
    }
}
