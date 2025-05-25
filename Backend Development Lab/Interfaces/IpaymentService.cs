using tourist_map_backend.Entities;

namespace Backend_Development_Lab.Interfaces
{
    public interface IPaymentService
    {
        Task<(Order? order, string? approvalUrl, string? errorMessage)> CreatePayPalOrderAsync(Guid userId, string productName, string returnUrl, string cancelUrl);
        Task<Order?> CapturePayPalOrderAsync(string payPalOrderId); // userId może być potrzebne do aktualizacji
        Task<Order?> GetOrderByIdAsync(Guid orderId);
        Task<Order?> GetOrderByPayPalIdAsync(string payPalOrderId);
    }
}
