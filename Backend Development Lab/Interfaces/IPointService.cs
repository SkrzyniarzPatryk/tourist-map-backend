// Services/IPointService.cs

// Services/IPointService.cs
using tourist_map_backend.Models;

namespace tourist_map_backend.Interfaces
{
    public interface IPointService
    {
        Task<IEnumerable<PointDto>> GetAllPointsAsync();
        Task<PaginatedResponse<PointDto>> GetPaginatedPointsAsync(PaginationParams paginationParams);
        Task<PointDto?> GetPointByIdAsync(Guid id);
        Task<IEnumerable<PointDto>> GetPointsByUserIdAsync(Guid userId);
        Task<PointDto> CreatePointAsync(CreatePointDto createPointDto, Guid creatorUserId);
        Task<(PointDto? point, bool success, bool forbidden)> UpdatePointAsync(Guid id, UpdatePointDto updatePointDto, Guid currentUserId);
        Task<bool> UpdatePointRatingAsync(Guid pointId, UpdatePointRatingDto ratingDto);
        Task<(bool success, bool forbidden)> DeletePointAsync(Guid id, Guid currentUserId);
    }
}