using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ServiceManagement
{
    public record ServiceCategorySettingDto(ServiceCategory Category, bool IsReadMoreEnabled);

    public class ServiceCategorySettingUpdateRequest
    {
        public bool IsReadMoreEnabled { get; set; }
    }
}
