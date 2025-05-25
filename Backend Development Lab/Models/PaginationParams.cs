// Models/PaginationParams.cs (dla parametrów paginacji)
namespace tourist_map_backend.Models
{
    public class PaginationParams
    {
        private const int MaxPageSize = 50;
        private int _page = 1;
        public int Page
        {
            get => _page;
            set => _page = (value <= 0) ? 1 : value;
        }

        private int _perPage = 10;
        public int PerPage
        {
            get => _perPage;
            set => _perPage = (value > MaxPageSize) ? MaxPageSize : (value <= 0 ? 10 : value);
        }

        public string? Sort { get; set; } // np. "name", "name:desc", "rating:asc"
        public string? Category { get; set; }
        public double? RatingGreaterThan { get; set; } // Dla rating_gt
        // Możesz dodać inne filtry, jeśli potrzebujesz
    }
}