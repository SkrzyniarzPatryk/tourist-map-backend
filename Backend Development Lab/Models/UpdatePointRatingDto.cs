// Models/UpdatePointRatingDto.cs (dla PATCH /points/{id}/rating)
using System.ComponentModel.DataAnnotations;

namespace tourist_map_backend.Models
{
    public class UpdatePointRatingDto
    {
        [Required]
        [Range(0, 5)] // Zakładając, że rating jest od 0 do 5
        public double Rating { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Reviews { get; set; }
    }
}