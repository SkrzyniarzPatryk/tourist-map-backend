using System.ComponentModel.DataAnnotations;

namespace tourist_map_backend.Entities
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string? Username { get; set; }
        [Required]
        public required string Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? ExternalProvider { get; set; }
        public string? ExternalId { get; set; }
    }
}