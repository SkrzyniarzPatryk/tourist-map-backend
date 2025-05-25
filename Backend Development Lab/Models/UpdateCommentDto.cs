// Models/UpdateCommentDto.cs
using System.ComponentModel.DataAnnotations;

namespace tourist_map_backend.Models
{
    public class UpdateCommentDto
    {
        [StringLength(1000, MinimumLength = 10)]
        public string? Content { get; set; } // Opcjonalne - jeśli nie podane, nie zmieniaj

        [Range(1, 5)]
        public int? Rating { get; set; } // Opcjonalne
    }
}