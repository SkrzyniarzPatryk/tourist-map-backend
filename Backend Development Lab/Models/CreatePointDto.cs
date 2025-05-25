using System.ComponentModel.DataAnnotations;

namespace tourist_map_backend.Models
{
    public class CreatePointDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [MinLength(2), MaxLength(2)] // Upewnia się, że są dokładnie dwie wartości
        public List<double> Position { get; set; } = new List<double>(); // [Latitude, Longitude]

        public List<string>? Images { get; set; } = new List<string>();
    }
}