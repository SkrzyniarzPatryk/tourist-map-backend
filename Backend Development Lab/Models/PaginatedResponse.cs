// Models/PaginatedResponse.cs (dla struktury odpowiedzi paginowanej)
namespace tourist_map_backend.Models
{
    public class PaginatedResponse<T> where T : class
    {
        public int First { get; set; }
        public int? Prev { get; set; }
        public int? Next { get; set; }
        public int Last { get; set; }
        public int Pages { get; set; } // Total pages
        public int Items { get; set; } // Total items matching filter
        public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();

        public PaginatedResponse(IEnumerable<T> data, int totalItems, int page, int pageSize)
        {
            Data = data;
            Items = totalItems;
            Pages = (int)Math.Ceiling((double)totalItems / pageSize);
            First = 1;
            Last = Pages == 0 ? 1 : Pages; // Handle case with 0 items/pages
            Prev = (page > 1 && page <= Pages) ? page - 1 : (int?)null;
            Next = (page < Pages) ? page + 1 : (int?)null;

            // Ensure Prev and Next are not out of bounds if Pages is 0 or 1
            if (Pages <= 1)
            {
                Prev = null;
                Next = null;
            }
            if (Pages == 0) // If no items, then no pages to navigate
            {
                First = 0; // Or 1, depending on desired representation for no data
                Last = 0;  // Or 1
            }
        }
    }
}