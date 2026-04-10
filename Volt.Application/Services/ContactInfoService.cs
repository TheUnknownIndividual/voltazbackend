using Volt.Application.Dtos;
using Volt.Application.Dtos.ContactInfo;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ContactInfoService : IContactInfoService
    {
        private readonly IUnitOfWork _uow;

        public ContactInfoService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<IReadOnlyList<ContactInfoDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var contacts = await _uow.Repository<ContactInfo>().ListNoTrackingAsync(ct);
            var langs = await _uow.Repository<ContactLanguage>().ListNoTrackingAsync(ct);
            var phones = await _uow.Repository<PhoneNumber>().ListNoTrackingAsync(ct);
            var emails = await _uow.Repository<EmailAddress>().ListNoTrackingAsync(ct);

            var result = contacts
                .OrderByDescending(x => x.Id)
                .Select(x => MapToDto(
                    x,
                    langs.Where(l => l.ContactInfoId == x.Id).OrderBy(l => l.Id).ToList(),
                    phones.Where(p => p.ContactInfoId == x.Id).OrderBy(p => p.Id).ToList(),
                    emails.Where(e => e.ContactInfoId == x.Id).OrderBy(e => e.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<ContactInfoDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<ContactInfoDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var contact = await _uow.Repository<ContactInfo>().FirstOrDefaultNoTrackingAsync(x => x.Id == id, ct);
            if (contact is null)
            {
                return ApiResponse<ContactInfoDto>.ErrorResponse(ErrorCode.CONTACT_INFO_NOT_FOUND, ErrorCode.CONTACT_INFO_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, languageCode, ct);
            return ApiResponse<ContactInfoDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<ContactInfoDto>> CreateAsync(ContactInfoCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var validationError = ValidateRequest(request.Languages, request.PhoneNumbers, request.EmailAddresses);
            if (validationError is not null)
            {
                return ApiResponse<ContactInfoDto>.ErrorResponse(validationError, validationError);
            }

            try
            {
                var contact = new ContactInfo
                {
                    Languages = new List<ContactLanguage>(),
                    PhoneNumbers = new List<PhoneNumber>(),
                    EmailAddresses = new List<EmailAddress>()
                };

                await _uow.Repository<ContactInfo>().AddAsync(contact, ct);
                await _uow.SaveChangesAsync(ct);

                await CreateLanguagesAsync(contact.Id, request.Languages, ct);
                await CreatePhonesAsync(contact.Id, request.PhoneNumbers, ct);
                await CreateEmailsAsync(contact.Id, request.EmailAddresses, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(contact.Id, languageCode, ct);
                return ApiResponse<ContactInfoDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<ContactInfoDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occurred while creating contact info.");
            }
        }

        public async Task<ApiResponse<ContactInfoDto>> UpdateAsync(int id, ContactInfoUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var contactRepo = _uow.Repository<ContactInfo>();
            var contact = await contactRepo.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (contact is null)
            {
                return ApiResponse<ContactInfoDto>.ErrorResponse(ErrorCode.CONTACT_INFO_NOT_FOUND, ErrorCode.CONTACT_INFO_NOT_FOUND);
            }

            var validationError = ValidateRequest(request.Languages, request.PhoneNumbers, request.EmailAddresses);
            if (validationError is not null)
            {
                return ApiResponse<ContactInfoDto>.ErrorResponse(validationError, validationError);
            }

            try
            {
                await ReplaceLanguagesAsync(id, request.Languages, ct);
                await ReplacePhonesAsync(id, request.PhoneNumbers, ct);
                await ReplaceEmailsAsync(id, request.EmailAddresses, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<ContactInfoDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<ContactInfoDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occurred while updating contact info.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var contactRepo = _uow.Repository<ContactInfo>();
            var contact = await contactRepo.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (contact is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.CONTACT_INFO_NOT_FOUND, ErrorCode.CONTACT_INFO_NOT_FOUND);
            }

            try
            {
                contactRepo.Remove(contact);
                await _uow.SaveChangesAsync(ct);
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occurred while deleting contact info.");
            }
        }

        private string? ValidateRequest(
            List<ContactLanguageCreateRequest> createLanguages,
            List<PhoneNumberCreateRequest>? createPhones,
            List<EmailAddressCreateRequest>? createEmails)
        {
            if (createLanguages is null || !createLanguages.Any())
                return ErrorCode.INVALID_CONTACT_INFO_REQUEST;

            var duplicateLang = createLanguages.GroupBy(x => x.LanguageCode).Any(g => g.Count() > 1);
            if (duplicateLang)
                return ErrorCode.CONTACT_LANGUAGE_DUPLICATE;

            var hasPhone = createPhones is not null && createPhones.Any(x => !string.IsNullOrWhiteSpace(x.Number));
            var hasEmail = createEmails is not null && createEmails.Any(x => !string.IsNullOrWhiteSpace(x.Email));

            if (!hasPhone && !hasEmail)
                return ErrorCode.CONTACT_CHANNEL_REQUIRED;

            return null;
        }

        private string? ValidateRequest(
            List<ContactLanguageUpdateRequest> updateLanguages,
            List<PhoneNumberUpdateRequest>? updatePhones,
            List<EmailAddressUpdateRequest>? updateEmails)
        {
            if (updateLanguages is null || !updateLanguages.Any())
                return ErrorCode.INVALID_CONTACT_INFO_REQUEST;

            var duplicateLang = updateLanguages.GroupBy(x => x.LanguageCode).Any(g => g.Count() > 1);
            if (duplicateLang)
                return ErrorCode.CONTACT_LANGUAGE_DUPLICATE;

            var hasPhone = updatePhones is not null && updatePhones.Any(x => !string.IsNullOrWhiteSpace(x.Number));
            var hasEmail = updateEmails is not null && updateEmails.Any(x => !string.IsNullOrWhiteSpace(x.Email));

            if (!hasPhone && !hasEmail)
                return ErrorCode.CONTACT_CHANNEL_REQUIRED;

            return null;
        }

        private async Task CreateLanguagesAsync(int contactId, List<ContactLanguageCreateRequest> languages, CancellationToken ct)
        {
            var repo = _uow.Repository<ContactLanguage>();
            foreach (var item in languages)
            {
                await repo.AddAsync(new ContactLanguage
                {
                    ContactInfoId = contactId,
                    LanguageCode = item.LanguageCode,
                    Address = item.Address.Trim(),
                    WorkingHoursDescription = item.WorkingHoursDescription.Trim()
                }, ct);
            }
        }

        private async Task CreatePhonesAsync(int contactId, List<PhoneNumberCreateRequest>? phones, CancellationToken ct)
        {
            if (phones is null) return;
            var repo = _uow.Repository<PhoneNumber>();
            foreach (var item in phones.Where(x => !string.IsNullOrWhiteSpace(x.Number)))
            {
                await repo.AddAsync(new PhoneNumber
                {
                    ContactInfoId = contactId,
                    Number = item.Number.Trim()
                }, ct);
            }
        }

        private async Task CreateEmailsAsync(int contactId, List<EmailAddressCreateRequest>? emails, CancellationToken ct)
        {
            if (emails is null) return;
            var repo = _uow.Repository<EmailAddress>();
            foreach (var item in emails.Where(x => !string.IsNullOrWhiteSpace(x.Email)))
            {
                await repo.AddAsync(new EmailAddress
                {
                    ContactInfoId = contactId,
                    Email = item.Email.Trim()
                }, ct);
            }
        }

        private async Task ReplaceLanguagesAsync(int contactId, List<ContactLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var repo = _uow.Repository<ContactLanguage>();
            var oldItems = await repo.ListNoTrackingAsync(x => x.ContactInfoId == contactId, ct);
            foreach (var oldItem in oldItems)
            {
                var tracked = await repo.FirstOrDefaultAsync(x => x.Id == oldItem.Id, ct);
                if (tracked is not null)
                    repo.Remove(tracked);
            }

            foreach (var item in languages)
            {
                await repo.AddAsync(new ContactLanguage
                {
                    ContactInfoId = contactId,
                    LanguageCode = item.LanguageCode,
                    Address = item.Address.Trim(),
                    WorkingHoursDescription = item.WorkingHoursDescription.Trim()
                }, ct);
            }
        }

        private async Task ReplacePhonesAsync(int contactId, List<PhoneNumberUpdateRequest>? phones, CancellationToken ct)
        {
            var repo = _uow.Repository<PhoneNumber>();
            var oldItems = await repo.ListNoTrackingAsync(x => x.ContactInfoId == contactId, ct);
            foreach (var oldItem in oldItems)
            {
                var tracked = await repo.FirstOrDefaultAsync(x => x.Id == oldItem.Id, ct);
                if (tracked is not null)
                    repo.Remove(tracked);
            }

            if (phones is null) return;
            foreach (var item in phones.Where(x => !string.IsNullOrWhiteSpace(x.Number)))
            {
                await repo.AddAsync(new PhoneNumber
                {
                    ContactInfoId = contactId,
                    Number = item.Number.Trim()
                }, ct);
            }
        }

        private async Task ReplaceEmailsAsync(int contactId, List<EmailAddressUpdateRequest>? emails, CancellationToken ct)
        {
            var repo = _uow.Repository<EmailAddress>();
            var oldItems = await repo.ListNoTrackingAsync(x => x.ContactInfoId == contactId, ct);
            foreach (var oldItem in oldItems)
            {
                var tracked = await repo.FirstOrDefaultAsync(x => x.Id == oldItem.Id, ct);
                if (tracked is not null)
                    repo.Remove(tracked);
            }

            if (emails is null) return;
            foreach (var item in emails.Where(x => !string.IsNullOrWhiteSpace(x.Email)))
            {
                await repo.AddAsync(new EmailAddress
                {
                    ContactInfoId = contactId,
                    Email = item.Email.Trim()
                }, ct);
            }
        }

        private async Task<ContactInfoDto> BuildDtoAsync(int contactId, LanguageCode? languageCode, CancellationToken ct)
        {
            var contact = await _uow.Repository<ContactInfo>().FirstOrDefaultNoTrackingAsync(x => x.Id == contactId, ct);
            var langs = await _uow.Repository<ContactLanguage>().ListNoTrackingAsync(x => x.ContactInfoId == contactId, ct);
            var phones = await _uow.Repository<PhoneNumber>().ListNoTrackingAsync(x => x.ContactInfoId == contactId, ct);
            var emails = await _uow.Repository<EmailAddress>().ListNoTrackingAsync(x => x.ContactInfoId == contactId, ct);

            return MapToDto(
                contact,
                langs.OrderBy(x => x.Id).ToList(),
                phones.OrderBy(x => x.Id).ToList(),
                emails.OrderBy(x => x.Id).ToList(),
                languageCode);
        }

        private static ContactInfoDto MapToDto(
            ContactInfo contact,
            IReadOnlyList<ContactLanguage> langs,
            IReadOnlyList<PhoneNumber> phones,
            IReadOnlyList<EmailAddress> emails,
            LanguageCode? languageCode)
        {
            var filteredLangs = languageCode is null
                ? langs
                : langs.Where(x => x.LanguageCode == languageCode).ToList();

            return new ContactInfoDto(
                contact.Id,
                filteredLangs.Select(x => new ContactLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Address,
                    x.WorkingHoursDescription)).ToList(),
                phones.Select(x => new PhoneNumberDto(
                    x.Id,
                    x.Number)).ToList(),
                emails.Select(x => new EmailAddressDto(
                    x.Id,
                    x.Email)).ToList());
        }
    }
}

