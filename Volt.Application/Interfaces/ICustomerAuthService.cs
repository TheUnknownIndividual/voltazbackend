using Volt.Application.Dtos;
using Volt.Application.Dtos.CustomerAuth;

namespace Volt.Application.Interfaces
{
    public interface ICustomerAuthService
    {
        Task<ApiResponse<CustomerAuthResponse>> RegisterAsync(CustomerRegisterRequest request, CancellationToken ct = default);
        Task<ApiResponse<CustomerAuthResponse>> LoginAsync(CustomerLoginRequest request, CancellationToken ct = default);
        Task<ApiResponse<CustomerAuthResponse>> LoginWithGoogleAsync(SocialTokenLoginRequest request, CancellationToken ct = default);
        Task<ApiResponse<CustomerAuthResponse>> LoginWithAppleAsync(SocialTokenLoginRequest request, CancellationToken ct = default);
        Task<ApiResponse<PasskeyOptionsResponse>> BeginPasskeyRegistrationAsync(PasskeyRegisterOptionsRequest request, string origin, CancellationToken ct = default);
        Task<ApiResponse<CustomerAuthResponse>> CompletePasskeyRegistrationAsync(PasskeyCompleteRequest request, CancellationToken ct = default);
        Task<ApiResponse<PasskeyOptionsResponse>> BeginPasskeyLoginAsync(PasskeyLoginOptionsRequest request, string origin, CancellationToken ct = default);
        Task<ApiResponse<CustomerAuthResponse>> CompletePasskeyLoginAsync(PasskeyCompleteRequest request, CancellationToken ct = default);
        Task<ApiResponse<CustomerDto>> GetProfileAsync(int customerId, CancellationToken ct = default);
        Task<ApiResponse<CustomerDto>> UpdateProfileAsync(int customerId, CustomerUpdateRequest request, CancellationToken ct = default);
    }
}
