using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Google.Apis.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Volt.Application.Dtos;
using Volt.Application.Dtos.CustomerAuth;
using Volt.Application.Interfaces;
using Volt.Application.Security;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class CustomerAuthService : ICustomerAuthService
    {
        private const string GoogleProvider = "google";
        private const string AppleProvider = "apple";
        private const string PasskeyRegisterCachePrefix = "passkey-register:";
        private const string PasskeyLoginCachePrefix = "passkey-login:";
        private static readonly TimeSpan PasskeyChallengeLifetime = TimeSpan.FromMinutes(5);

        private readonly IUnitOfWork _uow;
        private readonly ITokenService _tokenService;
        private readonly PasswordHelper _passwordHelper;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _appleConfigurationManager;

        public CustomerAuthService(
            IUnitOfWork uow,
            ITokenService tokenService,
            PasswordHelper passwordHelper,
            IConfiguration configuration,
            IMemoryCache cache)
        {
            _uow = uow;
            _tokenService = tokenService;
            _passwordHelper = passwordHelper;
            _configuration = configuration;
            _cache = cache;
            _appleConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                "https://appleid.apple.com/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever());
        }

        public async Task<ApiResponse<CustomerAuthResponse>> RegisterAsync(CustomerRegisterRequest request, CancellationToken ct = default)
        {
            var validationError = ValidateRegisterRequest(request);
            if (validationError is not null)
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, validationError);
            }

            var normalizedEmail = NormalizeEmail(request.Email);
            var normalizedPhone = NormalizePhone(request.Phone);
            var repo = _uow.Repository<CustomerUser>();

            if (await repo.AnyAsync(x => x.Email.ToLower() == normalizedEmail || x.Phone == normalizedPhone, ct))
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.CUSTOMER_ALREADY_EXISTS, ErrorCode.CUSTOMER_ALREADY_EXISTS);
            }

            _passwordHelper.CreatePasswordHash(request.Password, out var passwordHash, out var passwordSalt);

            var customer = new CustomerUser
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Email = normalizedEmail,
                Phone = normalizedPhone,
                Address = NormalizeNullable(request.Address) ?? string.Empty,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = Role.Customer,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await repo.AddAsync(customer, ct);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<CustomerAuthResponse>.SuccessResponse(CreateAuthResponse(customer));
        }

        public async Task<ApiResponse<CustomerAuthResponse>> LoginAsync(CustomerLoginRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, ErrorCode.INVALID_CUSTOMER_REQUEST);
            }

            var identifier = request.Identifier.Trim().ToLowerInvariant();
            var customer = await _uow.Repository<CustomerUser>()
                .FirstOrDefaultNoTrackingAsync(x => x.IsActive && (x.Email.ToLower() == identifier || x.Phone == request.Identifier.Trim()), ct);

            if (customer is null)
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_USERNAME, null);
            }

            if (customer.PasswordHash is null || customer.PasswordSalt is null ||
                !_passwordHelper.VerifyPassword(request.Password, customer.PasswordHash, customer.PasswordSalt))
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_PASSWORD, null);
            }

            return ApiResponse<CustomerAuthResponse>.SuccessResponse(CreateAuthResponse(customer));
        }

        public async Task<ApiResponse<CustomerAuthResponse>> LoginWithGoogleAsync(SocialTokenLoginRequest request, CancellationToken ct = default)
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(request?.IdToken))
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN, ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN);
            }

            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(
                    request.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });

                if (payload is null || payload.EmailVerified != true || string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email))
                {
                    return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN, ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN);
                }

                var nameParts = SplitName(payload.Name);
                var customer = await FindOrCreateExternalCustomerAsync(
                    GoogleProvider,
                    payload.Subject,
                    payload.Email,
                    nameParts.firstName,
                    nameParts.lastName,
                    ct);

                return ApiResponse<CustomerAuthResponse>.SuccessResponse(CreateAuthResponse(customer));
            }
            catch
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN, ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN);
            }
        }

        public async Task<ApiResponse<CustomerAuthResponse>> LoginWithAppleAsync(SocialTokenLoginRequest request, CancellationToken ct = default)
        {
            var clientId = _configuration["Authentication:Apple:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(request?.IdToken))
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN, ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN);
            }

            try
            {
                var appleConfig = await _appleConfigurationManager.GetConfigurationAsync(ct);
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "https://appleid.apple.com",
                    ValidateAudience = true,
                    ValidAudience = clientId,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = appleConfig.SigningKeys,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };

                var principal = new JwtSecurityTokenHandler().ValidateToken(request.IdToken, validationParameters, out _);
                var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                var email = principal.FindFirst(ClaimTypes.Email)?.Value
                    ?? principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

                if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
                {
                    return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN, ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN);
                }

                var firstName = NormalizeNullable(request.FirstName);
                var lastName = NormalizeNullable(request.LastName);
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                {
                    var parts = SplitName(request.Name);
                    firstName ??= parts.firstName;
                    lastName ??= parts.lastName;
                }

                var customer = await FindOrCreateExternalCustomerAsync(
                    AppleProvider,
                    subject,
                    email,
                    firstName,
                    lastName,
                    ct);

                return ApiResponse<CustomerAuthResponse>.SuccessResponse(CreateAuthResponse(customer));
            }
            catch
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN, ErrorCode.INVALID_EXTERNAL_AUTH_TOKEN);
            }
        }

        public async Task<ApiResponse<PasskeyOptionsResponse>> BeginPasskeyRegistrationAsync(PasskeyRegisterOptionsRequest request, string origin, CancellationToken ct = default)
        {
            var validationError = ValidatePasskeyRegisterRequest(request);
            if (validationError is not null)
            {
                return ApiResponse<PasskeyOptionsResponse>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, validationError);
            }

            var fido = CreateFido(origin, out var originError);
            if (originError is not null)
            {
                return ApiResponse<PasskeyOptionsResponse>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, originError);
            }

            var normalizedEmail = NormalizeEmail(request.Email);
            var customer = await _uow.Repository<CustomerUser>()
                .FirstOrDefaultNoTrackingAsync(x => x.IsActive && x.Email.ToLower() == normalizedEmail, ct);

            var existingCredentials = new List<PublicKeyCredentialDescriptor>();
            if (customer is not null)
            {
                var credentials = await _uow.Repository<CustomerPasskeyCredential>()
                    .ListNoTrackingAsync(x => x.CustomerUserId == customer.Id && x.IsActive, ct);
                existingCredentials = credentials.Select(x => new PublicKeyCredentialDescriptor(x.CredentialId)).ToList();
            }

            var userHandle = RandomBytes(32);
            var user = new Fido2User
            {
                Id = userHandle,
                Name = normalizedEmail,
                DisplayName = $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim()
            };

            var selection = new AuthenticatorSelection
            {
                RequireResidentKey = true,
                UserVerification = UserVerificationRequirement.Required
            };

            var options = fido.RequestNewCredential(
                user,
                existingCredentials,
                selection,
                AttestationConveyancePreference.None,
                null);

            var challengeId = Guid.NewGuid().ToString("N");
            _cache.Set(
                PasskeyRegisterCachePrefix + challengeId,
                new PasskeyRegisterChallenge(
                    options,
                    request.FirstName.Trim(),
                    request.LastName.Trim(),
                    normalizedEmail,
                    NormalizeNullable(request.Phone),
                    NormalizeNullable(request.Address),
                    userHandle,
                    origin),
                PasskeyChallengeLifetime);

            return ApiResponse<PasskeyOptionsResponse>.SuccessResponse(new PasskeyOptionsResponse(challengeId, options.ToJson()));
        }

        public async Task<ApiResponse<CustomerAuthResponse>> CompletePasskeyRegistrationAsync(PasskeyCompleteRequest request, CancellationToken ct = default)
        {
            if (!_cache.TryGetValue(PasskeyRegisterCachePrefix + request.ChallengeId, out var cachedRegisterChallenge) ||
                cachedRegisterChallenge is not PasskeyRegisterChallenge challenge)
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_PASSKEY_CHALLENGE, ErrorCode.INVALID_PASSKEY_CHALLENGE);
            }

            try
            {
                var fido = CreateFido(challenge.Origin, out var originError);
                if (originError is not null)
                {
                    return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, originError);
                }

                var credential = ParseAttestation(request.Credential);
                var result = await fido.MakeNewCredentialAsync(
                    credential,
                    challenge.Options,
                    async (args, cancellationToken) => await IsCredentialIdUniqueAsync(args.CredentialId, cancellationToken),
                    null,
                    ct);

                var customer = await FindOrCreatePasskeyCustomerAsync(challenge, ct);
                var passkey = new CustomerPasskeyCredential
                {
                    CustomerUserId = customer.Id,
                    CredentialId = credential.RawId,
                    CredentialIdBase64Url = EncodeBase64Url(credential.RawId),
                    PublicKey = result.Result.PublicKey,
                    UserHandle = challenge.UserHandle,
                    SignatureCounter = 0,
                    CredType = result.Result.CredType,
                    AaGuid = result.Result.Aaguid,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _uow.Repository<CustomerPasskeyCredential>().AddAsync(passkey, ct);
                await _uow.SaveChangesAsync(ct);
                _cache.Remove(PasskeyRegisterCachePrefix + request.ChallengeId);

                return ApiResponse<CustomerAuthResponse>.SuccessResponse(CreateAuthResponse(customer));
            }
            catch
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_PASSKEY_CHALLENGE, ErrorCode.INVALID_PASSKEY_CHALLENGE);
            }
        }

        public async Task<ApiResponse<PasskeyOptionsResponse>> BeginPasskeyLoginAsync(PasskeyLoginOptionsRequest request, string origin, CancellationToken ct = default)
        {
            var fido = CreateFido(origin, out var originError);
            if (originError is not null)
            {
                return ApiResponse<PasskeyOptionsResponse>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, originError);
            }

            var allowedCredentials = new List<PublicKeyCredentialDescriptor>();
            var normalizedEmail = NormalizeNullable(request?.Email)?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                var customer = await _uow.Repository<CustomerUser>()
                    .FirstOrDefaultNoTrackingAsync(x => x.IsActive && x.Email.ToLower() == normalizedEmail, ct);
                if (customer is not null)
                {
                    var credentials = await _uow.Repository<CustomerPasskeyCredential>()
                        .ListNoTrackingAsync(x => x.CustomerUserId == customer.Id && x.IsActive, ct);
                    allowedCredentials = credentials.Select(x => new PublicKeyCredentialDescriptor(x.CredentialId)).ToList();
                }
            }

            var options = fido.GetAssertionOptions(allowedCredentials, UserVerificationRequirement.Required, null);
            var challengeId = Guid.NewGuid().ToString("N");
            _cache.Set(PasskeyLoginCachePrefix + challengeId, new PasskeyLoginChallenge(options, origin), PasskeyChallengeLifetime);

            return ApiResponse<PasskeyOptionsResponse>.SuccessResponse(new PasskeyOptionsResponse(challengeId, options.ToJson()));
        }

        public async Task<ApiResponse<CustomerAuthResponse>> CompletePasskeyLoginAsync(PasskeyCompleteRequest request, CancellationToken ct = default)
        {
            if (!_cache.TryGetValue(PasskeyLoginCachePrefix + request.ChallengeId, out var cachedLoginChallenge) ||
                cachedLoginChallenge is not PasskeyLoginChallenge challenge)
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_PASSKEY_CHALLENGE, ErrorCode.INVALID_PASSKEY_CHALLENGE);
            }

            try
            {
                var fido = CreateFido(challenge.Origin, out var originError);
                if (originError is not null)
                {
                    return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, originError);
                }

                var assertion = ParseAssertion(request.Credential);
                var storedCredential = await FindPasskeyCredentialAsync(assertion.RawId, tracked: true, ct);
                if (storedCredential is null)
                {
                    return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.PASSKEY_NOT_FOUND, ErrorCode.PASSKEY_NOT_FOUND);
                }

                var verification = await fido.MakeAssertionAsync(
                    assertion,
                    challenge.Options,
                    storedCredential.PublicKey,
                    storedCredential.SignatureCounter,
                    async (args, cancellationToken) => await IsUserHandleOwnerOfCredentialAsync(args.UserHandle, args.CredentialId, cancellationToken),
                    null,
                    ct);

                storedCredential.SignatureCounter = verification.Counter;
                storedCredential.LastUsedAt = DateTime.UtcNow;
                _uow.Repository<CustomerPasskeyCredential>().Update(storedCredential);
                await _uow.SaveChangesAsync(ct);

                var customer = await _uow.Repository<CustomerUser>()
                    .FirstOrDefaultNoTrackingAsync(x => x.Id == storedCredential.CustomerUserId && x.IsActive, ct);
                if (customer is null)
                {
                    return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.CUSTOMER_NOT_FOUND, ErrorCode.CUSTOMER_NOT_FOUND);
                }

                _cache.Remove(PasskeyLoginCachePrefix + request.ChallengeId);
                return ApiResponse<CustomerAuthResponse>.SuccessResponse(CreateAuthResponse(customer));
            }
            catch
            {
                return ApiResponse<CustomerAuthResponse>.ErrorResponse(ErrorCode.INVALID_PASSKEY_CHALLENGE, ErrorCode.INVALID_PASSKEY_CHALLENGE);
            }
        }

        public async Task<ApiResponse<CustomerDto>> GetProfileAsync(int customerId, CancellationToken ct = default)
        {
            var customer = await _uow.Repository<CustomerUser>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == customerId && x.IsActive, ct);

            return customer is null
                ? ApiResponse<CustomerDto>.ErrorResponse(ErrorCode.CUSTOMER_NOT_FOUND, ErrorCode.CUSTOMER_NOT_FOUND)
                : ApiResponse<CustomerDto>.SuccessResponse(MapToDto(customer));
        }

        public async Task<ApiResponse<CustomerDto>> UpdateProfileAsync(int customerId, CustomerUpdateRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.Phone))
            {
                return ApiResponse<CustomerDto>.ErrorResponse(ErrorCode.INVALID_CUSTOMER_REQUEST, ErrorCode.INVALID_CUSTOMER_REQUEST);
            }

            var repo = _uow.Repository<CustomerUser>();
            var customer = await repo.FirstOrDefaultAsync(x => x.Id == customerId && x.IsActive, ct);
            if (customer is null)
            {
                return ApiResponse<CustomerDto>.ErrorResponse(ErrorCode.CUSTOMER_NOT_FOUND, ErrorCode.CUSTOMER_NOT_FOUND);
            }

            var normalizedPhone = NormalizePhone(request.Phone);
            if (await repo.AnyAsync(x => x.Id != customerId && x.Phone == normalizedPhone, ct))
            {
                return ApiResponse<CustomerDto>.ErrorResponse(ErrorCode.CUSTOMER_ALREADY_EXISTS, "Phone is already registered.");
            }

            customer.FirstName = request.FirstName.Trim();
            customer.LastName = request.LastName.Trim();
            customer.Phone = normalizedPhone;
            customer.Address = NormalizeNullable(request.Address) ?? string.Empty;
            customer.UpdatedAt = DateTime.UtcNow;
            repo.Update(customer);
            await _uow.SaveChangesAsync(ct);

            return ApiResponse<CustomerDto>.SuccessResponse(MapToDto(customer));
        }

        private async Task<CustomerUser> FindOrCreateExternalCustomerAsync(
            string provider,
            string providerSubject,
            string email,
            string firstName,
            string lastName,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var normalizedEmail = NormalizeEmail(email);
            var externalRepo = _uow.Repository<CustomerExternalLogin>();
            var existingLogin = await externalRepo.FirstOrDefaultAsync(
                x => x.Provider == provider && x.ProviderSubject == providerSubject,
                ct);

            if (existingLogin is not null)
            {
                existingLogin.Email = normalizedEmail;
                existingLogin.LastLoginAt = now;
                externalRepo.Update(existingLogin);
                await _uow.SaveChangesAsync(ct);

                return await _uow.Repository<CustomerUser>()
                    .FirstOrDefaultNoTrackingAsync(x => x.Id == existingLogin.CustomerUserId && x.IsActive, ct);
            }

            var customerRepo = _uow.Repository<CustomerUser>();
            var customer = await customerRepo.FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail && x.IsActive, ct);
            if (customer is null)
            {
                customer = new CustomerUser
                {
                    FirstName = NormalizeNullable(firstName) ?? "Volt",
                    LastName = NormalizeNullable(lastName) ?? "Customer",
                    Email = normalizedEmail,
                    Phone = null,
                    Address = string.Empty,
                    Role = Role.Customer,
                    IsActive = true,
                    CreatedAt = now
                };
                await customerRepo.AddAsync(customer, ct);
                await _uow.SaveChangesAsync(ct);
            }

            await externalRepo.AddAsync(new CustomerExternalLogin
            {
                CustomerUserId = customer.Id,
                Provider = provider,
                ProviderSubject = providerSubject,
                Email = normalizedEmail,
                CreatedAt = now,
                LastLoginAt = now
            }, ct);
            await _uow.SaveChangesAsync(ct);

            return customer;
        }

        private async Task<CustomerUser> FindOrCreatePasskeyCustomerAsync(PasskeyRegisterChallenge challenge, CancellationToken ct)
        {
            var repo = _uow.Repository<CustomerUser>();
            var customer = await repo.FirstOrDefaultAsync(x => x.Email.ToLower() == challenge.Email && x.IsActive, ct);
            if (customer is not null)
            {
                return customer;
            }

            if (!string.IsNullOrWhiteSpace(challenge.Phone) && await repo.AnyAsync(x => x.Phone == challenge.Phone, ct))
            {
                throw new InvalidOperationException("Phone is already registered.");
            }

            customer = new CustomerUser
            {
                FirstName = challenge.FirstName,
                LastName = challenge.LastName,
                Email = challenge.Email,
                Phone = challenge.Phone,
                Address = challenge.Address ?? string.Empty,
                Role = Role.Customer,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await repo.AddAsync(customer, ct);
            await _uow.SaveChangesAsync(ct);
            return customer;
        }

        private async Task<bool> IsCredentialIdUniqueAsync(byte[] credentialId, CancellationToken ct)
            => await FindPasskeyCredentialAsync(credentialId, tracked: false, ct) is null;

        private async Task<bool> IsUserHandleOwnerOfCredentialAsync(byte[] userHandle, byte[] credentialId, CancellationToken ct)
        {
            var credential = await FindPasskeyCredentialAsync(credentialId, tracked: false, ct);
            return credential is not null && credential.UserHandle.SequenceEqual(userHandle);
        }

        private async Task<CustomerPasskeyCredential> FindPasskeyCredentialAsync(byte[] credentialId, bool tracked, CancellationToken ct)
        {
            var repo = _uow.Repository<CustomerPasskeyCredential>();
            var credentialIdBase64Url = EncodeBase64Url(credentialId);
            if (tracked)
            {
                return await repo.FirstOrDefaultAsync(x => x.CredentialIdBase64Url == credentialIdBase64Url && x.IsActive, ct);
            }

            return await repo.FirstOrDefaultNoTrackingAsync(x => x.CredentialIdBase64Url == credentialIdBase64Url && x.IsActive, ct);
        }

        private CustomerAuthResponse CreateAuthResponse(CustomerUser customer)
        {
            var token = _tokenService.CreateCustomerAccessToken(customer);
            return new CustomerAuthResponse(token.AccessToken, MapToDto(customer));
        }

        private static CustomerDto MapToDto(CustomerUser customer)
        {
            var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
            return new CustomerDto(
                customer.Id,
                customer.FirstName,
                customer.LastName,
                fullName,
                customer.Email,
                customer.Phone ?? string.Empty,
                customer.Address ?? string.Empty,
                customer.Role.ToString(),
                customer.CreatedAt);
        }

        private Fido2NetLib.Fido2 CreateFido(string origin, out string error)
        {
            error = null;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            {
                error = "A valid Origin header is required for passkey auth.";
                return null;
            }

            var allowedOrigins = _configuration
                .GetSection("Authentication:Passkeys:Origins")
                .GetChildren()
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            var isLocalhost = originUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                              originUri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
            var isAllowedOrigin = allowedOrigins.Any(x => string.Equals(x?.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
            if (!isLocalhost && !isAllowedOrigin)
            {
                error = "Origin is not allowed for passkey auth.";
                return null;
            }

            var rpId = isLocalhost
                ? "localhost"
                : _configuration["Authentication:Passkeys:RpId"] ?? "volt.az";

            var config = new Fido2Configuration
            {
                ServerDomain = rpId,
                ServerName = _configuration["Authentication:Passkeys:ServerName"] ?? "Volt.az",
                Origins = new HashSet<string>(new[] { origin }, StringComparer.OrdinalIgnoreCase),
                Timeout = 60000,
                ChallengeSize = 32
            };

            return new Fido2NetLib.Fido2(config, null);
        }

        private static AuthenticatorAttestationRawResponse ParseAttestation(JsonElement credential)
            => new()
            {
                Id = DecodeRequired(credential, "id"),
                RawId = DecodeRequired(credential, "rawId"),
                Type = PublicKeyCredentialType.PublicKey,
                Response = new AuthenticatorAttestationRawResponse.ResponseData
                {
                    AttestationObject = DecodeRequired(credential.GetProperty("response"), "attestationObject"),
                    ClientDataJson = DecodeRequired(credential.GetProperty("response"), "clientDataJSON")
                }
            };

        private static AuthenticatorAssertionRawResponse ParseAssertion(JsonElement credential)
            => new()
            {
                Id = DecodeRequired(credential, "id"),
                RawId = DecodeRequired(credential, "rawId"),
                Type = PublicKeyCredentialType.PublicKey,
                Response = new AuthenticatorAssertionRawResponse.AssertionResponse
                {
                    AuthenticatorData = DecodeRequired(credential.GetProperty("response"), "authenticatorData"),
                    ClientDataJson = DecodeRequired(credential.GetProperty("response"), "clientDataJSON"),
                    Signature = DecodeRequired(credential.GetProperty("response"), "signature"),
                    UserHandle = DecodeOptional(credential.GetProperty("response"), "userHandle")
                }
            };

        private static byte[] DecodeRequired(JsonElement element, string propertyName)
        {
            var value = DecodeOptional(element, propertyName);
            if (value is null || value.Length == 0)
            {
                throw new InvalidOperationException($"{propertyName} is required.");
            }
            return value;
        }

        private static byte[] DecodeOptional(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            return Base64UrlEncoder.DecodeBytes(property.GetString());
        }

        private static string EncodeBase64Url(byte[] value)
            => Base64UrlEncoder.Encode(value);

        private static byte[] RandomBytes(int length)
        {
            var bytes = new byte[length];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }

        private static string ValidateRegisterRequest(CustomerRegisterRequest request)
        {
            if (request is null)
            {
                return "Customer details are required.";
            }

            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return "First name and last name are required.";
            }

            if (!new EmailAddressAttribute().IsValid(request.Email))
            {
                return "A valid email is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Phone))
            {
                return "Phone is required.";
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return "Password must be at least 6 characters.";
            }

            return null;
        }

        private static string ValidatePasskeyRegisterRequest(PasskeyRegisterOptionsRequest request)
        {
            if (request is null)
            {
                return "Customer details are required.";
            }
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                return "First name and last name are required.";
            }
            if (!new EmailAddressAttribute().IsValid(request.Email))
            {
                return "A valid email is required.";
            }
            return null;
        }

        private static string NormalizeEmail(string email)
            => email.Trim().ToLowerInvariant();

        private static string NormalizePhone(string phone)
            => string.IsNullOrWhiteSpace(phone)
                ? null
                : phone.Trim()
                    .Replace(" ", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("(", string.Empty)
                    .Replace(")", string.Empty);

        private static string NormalizeNullable(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static (string firstName, string lastName) SplitName(string name)
        {
            var normalized = NormalizeNullable(name);
            if (normalized is null) return ("Volt", "Customer");
            var parts = normalized.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 1 ? (parts[0], "Customer") : (parts[0], parts[1]);
        }

        private sealed record PasskeyRegisterChallenge(
            CredentialCreateOptions Options,
            string FirstName,
            string LastName,
            string Email,
            string Phone,
            string Address,
            byte[] UserHandle,
            string Origin);

        private sealed record PasskeyLoginChallenge(AssertionOptions Options, string Origin);
    }
}
