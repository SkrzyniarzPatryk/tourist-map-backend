using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace tourist_map_backend.Entities
{
    public enum OrderStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Cancelled,
        Refunded
    }

    public class Order
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public string? PayPalOrderId { get; set; }
        public decimal Amount { get; set; }
        [Required]
        public required string Currency { get; set; }
        public string? Description { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Order()
        {
            Id = Guid.NewGuid();
            Status = OrderStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
