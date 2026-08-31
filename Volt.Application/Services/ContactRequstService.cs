using Volt.Application.Dtos;
using Volt.Application.Dtos.ContactRequst;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ContactRequstService : IContactRequstService
    {
        private readonly IUnitOfWork _uow;

        public ContactRequstService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<IReadOnlyList<ContactRequstDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default)
        {
            var requests = await _uow.Repository<ContactRequst>().ListNoTrackingAsync(x => x.IsActive, ct);

            if (status is not null)
            {
                requests = requests.Where(x => x.Status == status.Value).ToList();
            }

            var result = requests
                .OrderByDescending(x => x.CreatedAt)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<IReadOnlyList<ContactRequstDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<ContactRequstDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _uow.Repository<ContactRequst>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.CONTACT_REQUST_NOT_FOUND,
                    ErrorCode.CONTACT_REQUST_NOT_FOUND);
            }

            return ApiResponse<ContactRequstDto>.SuccessResponse(MapToDto(entity));
        }

        public async Task<ApiResponse<ContactRequstDto>> CreateAsync(ContactRequstCreateRequest request, CancellationToken ct = default)
        {
            if (!await IsApplicationTypeValidAsync(request.ApplicationTypeId, ct))
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND,
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND);
            }

            try
            {
                var entity = new ContactRequst
                {
                    Name = request.Name.Trim(),
                    Surname = request.Surname.Trim(),
                    Email = request.Email.Trim(),
                    Phone = request.Phone.Trim(),
                    Message = request.Message.Trim(),
                    ApplicationTypeId = request.ApplicationTypeId,
                    Status = 1,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _uow.Repository<ContactRequst>().AddAsync(entity, ct);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ContactRequstDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the contact requst.");
            }
        }

        public async Task<ApiResponse<ContactRequstDto>> UpdateAsync(int id, ContactRequstUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ContactRequst>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.CONTACT_REQUST_NOT_FOUND,
                    ErrorCode.CONTACT_REQUST_NOT_FOUND);
            }

            if (!await IsApplicationTypeValidAsync(request.ApplicationTypeId, ct))
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND,
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND);
            }

            var statusValidationError = ValidateStatusTransition(entity.Status, request.Status);
            if (statusValidationError is not null)
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(statusValidationError, statusValidationError);
            }

            try
            {
                entity.Name = request.Name.Trim();
                entity.Surname = request.Surname.Trim();
                entity.Email = request.Email.Trim();
                entity.Phone = request.Phone.Trim();
                entity.Message = request.Message.Trim();
                entity.ApplicationTypeId = request.ApplicationTypeId;
                entity.Status = request.Status;
                entity.IsActive = request.IsActive;

                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ContactRequstDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the contact requst.");
            }
        }

        public async Task<ApiResponse<ContactRequstDto>> UpdateStatusAsync(int id, ContactRequstStatusUpdateRequest request, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ContactRequst>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.CONTACT_REQUST_NOT_FOUND,
                    ErrorCode.CONTACT_REQUST_NOT_FOUND);
            }

            var statusValidationError = ValidateStatusTransition(entity.Status, request.Status);
            if (statusValidationError is not null)
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(statusValidationError, statusValidationError);
            }

            try
            {
                entity.Status = request.Status;

                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ContactRequstDto>.SuccessResponse(MapToDto(entity));
            }
            catch
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating contact requst status.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ContactRequst>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.CONTACT_REQUST_NOT_FOUND,
                    ErrorCode.CONTACT_REQUST_NOT_FOUND);
            }

            try
            {
                entity.IsActive = false;
                repo.Update(entity);

                await _uow.SaveChangesAsync(ct);
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the contact requst.");
            }
        }

        public async Task<ApiResponse<ContactRequstDto>> MarkViewedAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ContactRequst>();
            var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (entity is null)
            {
                return ApiResponse<ContactRequstDto>.ErrorResponse(
                    ErrorCode.CONTACT_REQUST_NOT_FOUND,
                    ErrorCode.CONTACT_REQUST_NOT_FOUND);
            }

            if (!entity.IsViewedByAdmin)
            {
                entity.IsViewedByAdmin = true;
                entity.AdminViewedAt = DateTime.UtcNow;
                repo.Update(entity);
                await _uow.SaveChangesAsync(ct);
            }

            return ApiResponse<ContactRequstDto>.SuccessResponse(MapToDto(entity));
        }

        private async Task<bool> IsApplicationTypeValidAsync(int applicationTypeId, CancellationToken ct)
            => await _uow.Repository<ApplicationType>()
                .AnyAsync(x => x.Id == applicationTypeId && x.IsActive, ct);

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

        private static ContactRequstDto MapToDto(ContactRequst entity)
            => new(
                entity.Id,
                entity.Name,
                entity.Surname,
                entity.Email,
                entity.Phone,
                entity.Message,
                entity.CreatedAt,
                entity.Status,
                entity.IsActive,
                entity.ApplicationTypeId,
                entity.IsViewedByAdmin,
                entity.AdminViewedAt);
    }
}
