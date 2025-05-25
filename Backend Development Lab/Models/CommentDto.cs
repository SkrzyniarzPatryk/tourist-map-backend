// Models/CommentDto.cs
namespace tourist_map_backend.Models
{
    public class CommentDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid UserId { get; set; }
        public string? Username { get; set; } // Opcjonalnie, aby wyświetlić nazwę użytkownika
        public Guid PointId { get; set; }
    }
}