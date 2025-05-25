namespace tourist_map_backend.Models
{
    public class PointDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<double> Position { get; set; } = new List<double>(); // [Latitude, Longitude]
        public List<string> Images { get; set; } = new List<string>();
        public double Rating { get; set; }
        public int Reviews { get; set; }
        public Guid UserId { get; set; }
        public string? Username { get; set; } // Opcjonalnie, nazwa twórcy
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}