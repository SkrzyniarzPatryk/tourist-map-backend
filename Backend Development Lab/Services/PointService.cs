// Services/PointService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using tourist_map_backend.Data;
using tourist_map_backend.Entities;
using tourist_map_backend.Interfaces;
using tourist_map_backend.Models;

namespace tourist_map_backend.Services
{
    public class PointService : IPointService
    {
        private readonly ApplicationDbContext _context;

        public PointService(ApplicationDbContext context)
        {
            _context = context;
        }

        private static PointDto MapPointToDto(Point point)
        {
            return new PointDto
            {
                Id = point.Id,
                Name = point.Name,
                Description = point.Description,
                Category = point.Category,
                Position = new List<double> { point.Latitude, point.Longitude },
                Images = point.Images, // Używa gettera z encji Point
                Rating = point.Rating,
                Reviews = point.Reviews,
                UserId = point.UserId,
                Username = point.User?.Username, // Załaduj User, jeśli potrzebne
                CreatedAt = point.CreatedAt,
                UpdatedAt = point.UpdatedAt
            };
        }

        public async Task<IEnumerable<PointDto>> GetAllPointsAsync()
        {
            return await _context.Points
                .Include(p => p.User) // Aby uzyskać Username
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => MapPointToDto(p))
                .ToListAsync();
        }

        public async Task<PaginatedResponse<PointDto>> GetPaginatedPointsAsync(PaginationParams paginationParams)
        {
            var query = _context.Points.Include(p => p.User).AsQueryable();

            // Filtrowanie
            if (!string.IsNullOrEmpty(paginationParams.Category))
            {
                query = query.Where(p => p.Category.ToLower() == paginationParams.Category.ToLower());
            }

            if (paginationParams.RatingGreaterThan.HasValue)
            {
                query = query.Where(p => p.Rating > paginationParams.RatingGreaterThan.Value);
            }

            // Sortowanie
            if (!string.IsNullOrEmpty(paginationParams.Sort))
            {
                var sortParts = paginationParams.Sort.Split(':');
                var sortBy = sortParts[0];
                var sortDirection = sortParts.Length > 1 && sortParts[1].ToLower() == "desc" ? "desc" : "asc";

                // Prosta obsługa sortowania - można rozbudować o więcej pól
                // Należy uważać na SQL Injection, jeśli nazwy kolumn pochodzą bezpośrednio z inputu bez walidacji
                // Lepszym podejściem jest mapa string -> Expression<Func<Point, object>>
                // Dla uproszczenia użyję if/else
                Expression<Func<Point, object>> keySelector = sortBy.ToLower() switch
                {
                    "name" => p => p.Name,
                    "rating" => p => p.Rating,
                    "createdat" => p => p.CreatedAt,
                    _ => p => p.CreatedAt // Domyślne sortowanie
                };

                query = sortDirection == "asc"
                    ? query.OrderBy(keySelector)
                    : query.OrderByDescending(keySelector);
            }
            else
            {
                query = query.OrderByDescending(p => p.CreatedAt); // Domyślne sortowanie
            }

            var totalItems = await query.CountAsync();

            var points = await query
                .Skip((paginationParams.Page - 1) * paginationParams.PerPage)
                .Take(paginationParams.PerPage)
                .Select(p => MapPointToDto(p))
                .ToListAsync();

            return new PaginatedResponse<PointDto>(points, totalItems, paginationParams.Page, paginationParams.PerPage);
        }


        public async Task<PointDto?> GetPointByIdAsync(Guid id)
        {
            var point = await _context.Points
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            return point == null ? null : MapPointToDto(point);
        }

        public async Task<IEnumerable<PointDto>> GetPointsByUserIdAsync(Guid userId)
        {
            return await _context.Points
                .Include(p => p.User)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => MapPointToDto(p))
                .ToListAsync();
        }


        public async Task<PointDto> CreatePointAsync(CreatePointDto createPointDto, Guid creatorUserId)
        {
            var point = new Point
            {
                Id = Guid.NewGuid(),
                Name = createPointDto.Name,
                Description = createPointDto.Description,
                Category = createPointDto.Category,
                Latitude = createPointDto.Position[0],
                Longitude = createPointDto.Position[1],
                Images = createPointDto.Images ?? new List<string>(), // Używa settera z encji Point
                Rating = 0, // Domyślne wartości jak w frontendzie
                Reviews = 0,
                UserId = creatorUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Points.Add(point);
            await _context.SaveChangesAsync();

            // Pobierz z Userem, aby Username był w DTO
            var createdPointWithUser = await _context.Points.Include(p => p.User).FirstAsync(p => p.Id == point.Id);
            return MapPointToDto(createdPointWithUser);
        }

        public async Task<(PointDto? point, bool success, bool forbidden)> UpdatePointAsync(Guid id, UpdatePointDto updatePointDto, Guid currentUserId)
        {
            var point = await _context.Points.Include(p => p.User).FirstOrDefaultAsync(x => x.Id == id);

            if (point == null)
            {
                return (null, false, false); // Not found
            }

            if (point.UserId != currentUserId)
            {
                // Można dodać sprawdzanie roli admina
                return (null, false, true); // Forbidden
            }

            bool changed = false;
            if (updatePointDto.Name != null) { point.Name = updatePointDto.Name; changed = true; }
            if (updatePointDto.Description != null) { point.Description = updatePointDto.Description; changed = true; }
            if (updatePointDto.Category != null) { point.Category = updatePointDto.Category; changed = true; }
            if (updatePointDto.Position != null && updatePointDto.Position.Count == 2)
            {
                point.Latitude = updatePointDto.Position[0];
                point.Longitude = updatePointDto.Position[1];
                changed = true;
            }
            if (updatePointDto.Images != null) { point.Images = updatePointDto.Images; changed = true; }

            if (changed)
            {
                point.UpdatedAt = DateTime.UtcNow;
                _context.Points.Update(point);
                await _context.SaveChangesAsync();
            }

            return (MapPointToDto(point), true, false);
        }

        public async Task<bool> UpdatePointRatingAsync(Guid pointId, UpdatePointRatingDto ratingDto)
        {
            var point = await _context.Points.FindAsync(pointId);
            if (point == null)
            {
                return false; // Not found
            }

            point.Rating = ratingDto.Rating;
            point.Reviews = ratingDto.Reviews;
            point.UpdatedAt = DateTime.UtcNow;

            _context.Points.Update(point);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(bool success, bool forbidden)> DeletePointAsync(Guid id, Guid currentUserId)
        {
            var point = await _context.Points.FindAsync(id);

            if (point == null)
            {
                return (false, false); // Not found
            }

            if (point.UserId != currentUserId)
            {
                // Można dodać sprawdzanie roli admina
                return (false, true); // Forbidden
            }

            _context.Points.Remove(point);
            await _context.SaveChangesAsync();
            return (true, false); // Success
        }
    }
}