// Entities/Comment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace tourist_map_backend.Entities
{
    public class Comment
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Range(1, 5)] // Zakładam, że ocena jest w skali 1-5
        public int Rating { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Relacja z Użytkownikiem
        [Required]
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; } // Właściciel komentarza

        // Relacja z Punktem (zakładam, że masz encję Point)
        // Jeśli nie masz jeszcze encji Point, możesz na razie używać tylko PointId
        [Required]
        public Guid PointId { get; set; }
        // Jeśli masz encję Point, dodaj:
        // [ForeignKey("PointId")]
        // public virtual Point? Point { get; set; }
    }
}