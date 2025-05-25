using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Backend_Development_Lab.Dtos;
using Backend_Development_Lab.Interfaces;
using Backend_Development_Lab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tourist_map_backend.Data;
using tourist_map_backend.Entities;

namespace Backend_Development_Lab.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IConfiguration _configuration; // Do odczytu URL-i frontendu
        private readonly ApplicationDbContext _dbContext; // Zakładam, że masz DbContext do komunikacji z bazą danych

        public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger, IConfiguration configuration, ApplicationDbContext dbContext)
        {
            _paymentService = paymentService;
            _logger = logger;
            _configuration = configuration;
            _dbContext = dbContext;
        }

        // 1. Endpoint inicjujący płatność
        [HttpPost("create-order")] // Zmieniona nazwa, aby było jasne
        [Authorize] // Wymaga zalogowanego użytkownika
        public async Task<IActionResult> CreatePaymentOrder([FromBody] CreatePaymentRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // Pobierz ID zalogowanego użytkownika
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized("User ID not found in token.");
            }

            // URL-e powrotu do Twojej aplikacji frontendowej (React)
            string frontendBaseUrl = _configuration["FrontendUrls:BaseUrl"] ?? "http://localhost:5173"; // Odczytaj z konfiguracji
            string returnUrl = $"{frontendBaseUrl}/user-profile?status=success"; // Ścieżka w React
            string cancelUrl = $"{frontendBaseUrl}/user-profile?status=failure";  // Ścieżka w React

            _logger.LogInformation($"Creating PayPal order for user {userId}, product: {requestDto.ProductName}. Return: {returnUrl}, Cancel: {cancelUrl}");

            var (order, approvalUrl, errorMessage) = await _paymentService.CreatePayPalOrderAsync(
                userId,
                requestDto.ProductName, // Przekazujemy nazwę produktu
                returnUrl,
                cancelUrl);

            if (order == null || string.IsNullOrEmpty(approvalUrl))
            {
                _logger.LogError($"Failed to create PayPal order: {errorMessage}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to create payment order.", error = errorMessage });
            }

            _logger.LogInformation($"PayPal order created for user {userId}. Internal ID: {order.Id}, PayPal ID: {order.PayPalOrderId}, Approval URL: {approvalUrl}");

            return Ok(new CreatePaymentResponseDto
            {
                InternalOrderId = order.Id,
                PayPalOrderId = order.PayPalOrderId,
                ApprovalUrl = approvalUrl
            });
        }

        // 2. Endpoint, który frontend wywoła po pomyślnym powrocie z PayPal
        //    Użytkownik jest przekierowywany przez PayPal na /payment-success w React,
        //    a React następnie wywołuje ten endpoint.
        [HttpPost("capture-order")]
        [Authorize] // Wymaga zalogowanego użytkownika (tego samego, który inicjował)
        public async Task<IActionResult> CapturePaymentOrder([FromBody] CaptureOrderRequestDto captureRequestDto)
        {
            // CaptureOrderRequestDto to proste DTO z PayPalOrderId
            // public class CaptureOrderRequestDto { public string PayPalOrderId { get; set; } }

            if (string.IsNullOrEmpty(captureRequestDto.PayPalOrderId))
            {
                _logger.LogWarning("CapturePaymentOrder: PayPal Order ID is missing in request body.");
                return BadRequest("PayPal Order ID is required.");
            }

            // Opcjonalnie: Sprawdź, czy zalogowany użytkownik jest właścicielem tego PayPalOrderId
            // (wymagałoby pobrania zamówienia i porównania UserId)
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                return Unauthorized("User ID not found in token.");
            }

            _logger.LogInformation($"Attempting to capture PayPal order: {captureRequestDto.PayPalOrderId} for user: {currentUserId}");

            Order? capturedOrder = await _paymentService.CapturePayPalOrderAsync(captureRequestDto.PayPalOrderId);

            if (capturedOrder == null)
            {
                _logger.LogError($"CapturePaymentOrder: Failed to capture or find order for PayPal Order ID: {captureRequestDto.PayPalOrderId}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error processing payment after confirmation." });
            }

            // Sprawdzenie, czy użytkownik z tokenu zgadza się z użytkownikiem zamówienia
            if (capturedOrder.UserId != currentUserId)
            {
                _logger.LogWarning($"User {currentUserId} attempted to capture order {capturedOrder.Id} belonging to user {capturedOrder.UserId}.");
                return Forbid("You are not authorized to capture this order.");
            }

            if (capturedOrder.Status == OrderStatus.Completed)
            {
                _logger.LogInformation($"Payment completed successfully for PayPal Order ID: {capturedOrder.PayPalOrderId}, Internal Order ID: {capturedOrder.Id}. User {capturedOrder.UserId} is now premium: {capturedOrder.User?.IsPremium}");
                return Ok(new { transactionStatus = "confirmed", message = "Payment completed successfully! Your account is now Premium.", orderId = capturedOrder.Id, status = capturedOrder.Status, isPremium = capturedOrder.User?.IsPremium });
            }
            else
            {
                _logger.LogWarning($"Payment for PayPal Order ID: {capturedOrder.PayPalOrderId} was not completed successfully after capture. Status: {capturedOrder.Status}");
                return BadRequest(new { message = "Payment was not completed successfully.", orderId = capturedOrder.Id, status = capturedOrder.Status, isPremium = capturedOrder.User?.IsPremium });
            }
        }

        // 3. Endpoint, który frontend może wywołać po anulowaniu płatności w PayPal
        [HttpPost("order-cancelled")]
        [Authorize]
        public async Task<IActionResult> PaymentOrderCancelled([FromBody] CancelOrderRequestDto cancelRequestDto)
        {
            // public class CancelOrderRequestDto { public string PayPalOrderId { get; set; } }
            if (string.IsNullOrEmpty(cancelRequestDto.PayPalOrderId))
            {
                _logger.LogWarning("PaymentOrderCancelled: PayPal Order ID is missing.");
                return BadRequest("PayPal Order ID is required.");
            }

            var order = await _paymentService.GetOrderByPayPalIdAsync(cancelRequestDto.PayPalOrderId);
            if (order != null)
            {
                // Sprawdź, czy zalogowany użytkownik jest właścicielem zamówienia
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(userIdString, out Guid currentUserId) && order.UserId == currentUserId)
                {
                    if (order.Status != OrderStatus.Completed && order.Status != OrderStatus.Refunded)
                    {
                        order.Status = OrderStatus.Cancelled;
                        order.UpdatedAt = DateTime.UtcNow;
                        // _dbContext.Orders.Update(order); // Jeśli PaymentService nie robi SaveChanges
                        await _dbContext.SaveChangesAsync(); // Bezpośrednio lub przez serwis
                        _logger.LogInformation($"Internal order {order.Id} (PayPal ID: {cancelRequestDto.PayPalOrderId}) marked as Cancelled by user {currentUserId}.");
                        return Ok(new { message = "Payment was cancelled.", payPalOrderId = cancelRequestDto.PayPalOrderId, status = order.Status });
                    }
                    return Ok(new { message = "Order already processed.", payPalOrderId = cancelRequestDto.PayPalOrderId, status = order.Status });
                }
                else
                {
                    return Forbid("You are not authorized to cancel this order.");
                }
            }
            _logger.LogWarning($"PaymentOrderCancelled: Could not find internal order for PayPal ID {cancelRequestDto.PayPalOrderId}.");
            return NotFound(new { message = "Order not found." });
        }
    }

    // Dodaj te DTOs (np. w PaymentDtos.cs lub nowym pliku)
    public class CaptureOrderRequestDto
    {
        [Required]
        public required string PayPalOrderId { get; set; }
    }

    public class CancelOrderRequestDto
    {
        [Required]
        public required string PayPalOrderId { get; set; }
    }
}