using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;
using Volt.Application.Dtos;
using Volt.Domain.Enums;

namespace Volt.Application.Interfaces
{
    public interface IStepService
    {
        Task<ApiResponse<StepDto>> CreateAsync(StepCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<StepDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<StepDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<StepDto>> UpdateAsync(int id, StepUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<NoContentDto>> ReorderAsync(List<StepReorderRequest> request, CancellationToken ct = default);
    }
}
