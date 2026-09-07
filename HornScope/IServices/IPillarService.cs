using HornScope.Common.Models;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.PillarDto;
using HornScope.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HornScope.IServices
{
    public interface IPillarService
    {
        Task<List<GetPillarDTO>> GetAllAsync(int userId, UserRole userRole);
        Task<Pillar> GetByIdAsync(int id);
        Task<Pillar> AddAsync(Pillar pillar);
        Task<ResultResponseDto<Pillar>> AddPillarAsync(AddPillarDto pillar);
        Task<Pillar> UpdateAsync(int id, UpdatePillarDto pillar);
        Task<ResultResponseDto<List<PillarKpiMappingDto>>> GetPillarKpiMappingsAsync(int pillarId);
        Task<ResultResponseDto<bool>> DeleteAsync(int id);
        Task<Tuple<string, byte[]>> ExportPillarsHistoryByUserId(GetProgramPillarHistoryRequestDto requestDto);
        Task<PaginationResponse<PillarsHistroyResponseDto>> GetResponsesByUserId(GetPillarResponseHistoryRequestNewDto request, UserRole userRole);

    }
} 