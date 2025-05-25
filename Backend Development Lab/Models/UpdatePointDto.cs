using System.ComponentModel.DataAnnotations;

namespace tourist_map_backend.Models
{
    public class UpdatePointDto
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        [MinLength(2), MaxLength(2)]
        public List<double>? Position { get; set; } // [Latitude, Longitude]

        public List<string>? Images { get; set; }
    }
}