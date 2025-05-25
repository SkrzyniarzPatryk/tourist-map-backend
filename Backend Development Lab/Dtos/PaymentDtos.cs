using System.ComponentModel.DataAnnotations;

namespace Backend_Development_Lab.Dtos
{
    public class CreatePaymentRequestDto
    {
        [Required]
        public required string ProductName { get; set; } // Np. "PREMIUM"
    }

    // CreatePaymentResponseDto pozostaje takie samo lub podobne
    public class CreatePaymentResponseDto
    {
        public Guid InternalOrderId { get; set; }
        public string? PayPalOrderId { get; set; }
        public required string ApprovalUrl { get; set; }
    }
}
