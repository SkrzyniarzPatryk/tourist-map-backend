// Entities/Point.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json; // Potrzebne dla serializacji/deserializacji Images

namespace tourist_map_backend.Entities
{
    public class Point
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        // Pozycja geograficzna
        [Required]
        public double Latitude { get; set; } // Dla position[0]

        [Required]
        public double Longitude { get; set; } // Dla position[1]

        // Przechowywanie listy obrazów jako JSON string
        // Można też stworzyć osobną tabelę Images z relacją one-to-many
        public string? ImagesJson { get; set; } // Przechowuje JSON string tablicy URLi obrazów

        [NotMapped] // Ta właściwość nie będzie mapowana do bazy danych bezpośrednio
        public List<string> Images
        {
            get => string.IsNullOrEmpty(ImagesJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(ImagesJson) ?? new List<string>();
            set => ImagesJson = JsonSerializer.Serialize(value);
        }

        public double Rating { get; set; } // Średnia ocena

        public int Reviews { get; set; } // Liczba recenzji/komentarzy

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        // Relacja z Użytkownikiem (twórcą punktu)
        [Required]
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Możesz dodać kolekcję komentarzy, jeśli chcesz łatwo nawigować z Point do Comments
        // public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}