using System.ComponentModel.DataAnnotations;

namespace tourist_map_backend.Models
{
    public class CreateCommentDto
    {
        [Required]
        public Guid PointId { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 10)] // Przykładowa walidacja
        public string Content { get; set; } = string.Empty;

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }
    }
}