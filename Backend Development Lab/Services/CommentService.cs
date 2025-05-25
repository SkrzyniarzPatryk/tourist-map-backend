// Services/CommentService.cs
using Microsoft.EntityFrameworkCore;
using tourist_map_backend.Data;
using tourist_map_backend.Entities;
using tourist_map_backend.Interfaces;
using tourist_map_backend.Models;

namespace tourist_map_backend.Services
{
    public class CommentService : ICommentService
    {
        private readonly ApplicationDbContext _context;

        public CommentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CommentDto>> GetCommentsAsync(Guid? pointId, Guid? userId)
        {
            var query = _context.Comments.AsQueryable();

            if (pointId.HasValue)
            {
                query = query.Where(c => c.PointId == pointId.Value);
            }

            if (userId.HasValue)
            {
                query = query.Where(c => c.UserId == userId.Value);
            }

            // Dołączanie nazwy użytkownika, jeśli chcesz ją zwracać
            return await query
                .Include(c => c.User) // Załaduj powiązanego użytkownika
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CommentDto
                {
                    Id = c.Id,
                    Content = c.Content,
                    Rating = c.Rating,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    UserId = c.UserId,
                    Username = c.User != null ? c.User.Username : "Unknown", // Pobierz nazwę użytkownika
                    PointId = c.PointId
                })
                .ToListAsync();
        }

        public async Task<CommentDto?> GetCommentByIdAsync(Guid commentId)
        {
            var comment = await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null)
            {
                return null;
            }

            return new CommentDto
            {
                Id = comment.Id,
                Content = comment.Content,
                Rating = comment.Rating,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                UserId = comment.UserId,
                Username = comment.User != null ? comment.User.Username : "Unknown",
                PointId = comment.PointId
            };
        }

        public async Task<CommentDto> AddCommentAsync(CreateCommentDto createCommentDto, Guid authorUserId)
        {
            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                Content = createCommentDto.Content,
                Rating = createCommentDto.Rating,
                PointId = createCommentDto.PointId,
                UserId = authorUserId, // Użyj ID użytkownika z tokenu
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            // Możesz chcieć pobrać komentarz z bazy ponownie, aby załadować powiązane dane (User)
            // Lub ręcznie przypisać Username jeśli masz dostęp do obiektu User
            var createdComment = await GetCommentByIdAsync(comment.Id); // Prostsze
            return createdComment!; // Wiemy, że nie będzie null
        }

        public async Task<(CommentDto? comment, bool success, bool forbidden)> UpdateCommentAsync(Guid commentId, UpdateCommentDto updateCommentDto, Guid currentUserId)
        {
            var comment = await _context.Comments.FindAsync(commentId);

            if (comment == null)
            {
                return (null, false, false); // Not found
            }

            // Sprawdzenie, czy użytkownik jest właścicielem komentarza
            if (comment.UserId != currentUserId)
            {
                // Możesz dodać logikę dla admina, np. if (!User.IsInRole("Admin"))
                return (null, false, true); // Forbidden
            }

            bool changed = false;
            if (updateCommentDto.Content != null)
            {
                comment.Content = updateCommentDto.Content;
                changed = true;
            }
            if (updateCommentDto.Rating.HasValue)
            {
                comment.Rating = updateCommentDto.Rating.Value;
                changed = true;
            }

            if (changed)
            {
                comment.UpdatedAt = DateTime.UtcNow;
                _context.Comments.Update(comment);
                await _context.SaveChangesAsync();
            }

            var updatedCommentDto = await GetCommentByIdAsync(comment.Id); // Aby uzyskać zaktualizowany Username
            return (updatedCommentDto, true, false);
        }

        public async Task<(bool success, bool forbidden)> DeleteCommentAsync(Guid commentId, Guid currentUserId)
        {
            var comment = await _context.Comments.FindAsync(commentId);

            if (comment == null)
            {
                return (false, false); // Not found
            }

            if (comment.UserId != currentUserId)
            {
                // Możesz dodać logikę dla admina
                return (false, true); // Forbidden
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            return (true, false); // Success
        }
    }
}