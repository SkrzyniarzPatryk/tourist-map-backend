using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using Backend_Development_Lab.Interfaces;
using tourist_map_backend.Data;
using Microsoft.EntityFrameworkCore;
using tourist_map_backend.Entities;

namespace Backend_Development_Lab.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _dbContext; // Wstrzyknij DbContext
        private readonly string _payPalApiBaseUrl;
        private readonly string _payPalClientId;
        private readonly string _payPalClientSecret;

        // Definicje produktów (można przenieść do konfiguracji lub bazy danych)
        private static readonly Dictionary<string, (decimal Amount, string Currency, string Description)> _productDefinitions =
            new Dictionary<string, (decimal, string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                { "PREMIUM", (Amount: 19.99m, Currency: "PLN", Description: "Subskrypcja Premium") }
                // Możesz dodać inne produkty
            };

        public PaymentService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ApplicationDbContext dbContext)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient("PayPalApiClient");
            _dbContext = dbContext; // Przypisz wstrzyknięty DbContext

            _payPalApiBaseUrl = _configuration["PayPal:ApiBaseUrl"] ?? "https://api-m.sandbox.paypal.com";
            _payPalClientId = _configuration["PayPal:ClientId"] ?? throw new InvalidOperationException("PayPal ClientId not configured");
            _payPalClientSecret = _configuration["PayPal:ClientSecret"] ?? throw new InvalidOperationException("PayPal ClientSecret not configured");
        }

        private async Task<string?> GetPayPalAccessTokenAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_payPalApiBaseUrl}/v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_payPalClientId}:{_payPalClientSecret}")));
            var content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
            request.Content = content;
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<PayPalAuthResponse>();
                return authResponse?.access_token;
            }
            Console.WriteLine($"Error getting PayPal access token: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            return null;
        }

        public async Task<(Order? order, string? approvalUrl, string? errorMessage)> CreatePayPalOrderAsync(Guid userId, string productName, string returnUrl, string cancelUrl)
        {
            if (!_productDefinitions.TryGetValue(productName, out var productInfo))
            {
                return (null, null, $"Product '{productName}' not found or not configured.");
            }

            var accessToken = await GetPayPalAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return (null, null, "Failed to authenticate with PayPal.");
            }

            var internalOrder = new Order
            {
                UserId = userId,
                Amount = productInfo.Amount,
                Currency = productInfo.Currency,
                Description = productInfo.Description,
                Status = OrderStatus.Pending
            };

            _dbContext.Orders.Add(internalOrder); // Dodaj do DbContext
            // Na razie nie zapisujemy - zapiszemy razem z PayPalOrderId lub w razie błędu


            var payPalOrderRequest = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        amount = new { currency_code = internalOrder.Currency, value = internalOrder.Amount.ToString("F2").Replace(',','.') },
                        description = internalOrder.Description
                    }
                },
                application_context = new
                {
                    return_url = returnUrl,
                    cancel_url = cancelUrl,
                    brand_name = _configuration["PayPal:BrandName"] ?? "Moja Aplikacja",
                    user_action = "PAY_NOW"
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_payPalApiBaseUrl}/v2/checkout/orders");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payPalOrderRequest), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var payPalOrderResponse = JsonSerializer.Deserialize<PayPalOrderResponse>(responseContent);
                if (payPalOrderResponse != null && !string.IsNullOrEmpty(payPalOrderResponse.id))
                {
                    internalOrder.PayPalOrderId = payPalOrderResponse.id;
                    internalOrder.Status = OrderStatus.Processing;
                    await _dbContext.SaveChangesAsync(); // Zapisz zamówienie z PayPalOrderId

                    var approvalLink = payPalOrderResponse.links?.FirstOrDefault(l => l.rel == "approve");
                    return (internalOrder, approvalLink?.href, approvalLink == null ? "Approval link not found." : null);
                }
                // Nie zapisuj zamówienia, jeśli nie udało się uzyskać PayPalOrderId
                return (null, null, $"Failed to parse PayPal order ID from response: {responseContent}");
            }
            // Nie zapisuj zamówienia, jeśli wystąpił błąd z PayPal
            Console.WriteLine($"Error creating PayPal order: {response.StatusCode} - {responseContent}");
            return (null, null, $"Error creating PayPal order: {response.StatusCode}");
        }

        public async Task<Order?> CapturePayPalOrderAsync(string payPalOrderId)
        {
            var accessToken = await GetPayPalAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken)) return null;

            var internalOrder = await _dbContext.Orders
                                        .Include(o => o.User) // Dołącz użytkownika, aby móc go zaktualizować
                                        .FirstOrDefaultAsync(o => o.PayPalOrderId == payPalOrderId);

            if (internalOrder == null)
            {
                Console.WriteLine($"Internal order not found for PayPal Order ID: {payPalOrderId}");
                return null;
            }

            if (internalOrder.Status == OrderStatus.Completed) return internalOrder; // Już przetworzone

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_payPalApiBaseUrl}/v2/checkout/orders/{payPalOrderId}/capture");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var captureResponse = JsonSerializer.Deserialize<PayPalCaptureResponse>(responseContent);
                if (captureResponse?.status == "COMPLETED")
                {
                    internalOrder.Status = OrderStatus.Completed;
                    internalOrder.UpdatedAt = DateTime.UtcNow;

                    // AKTUALIZACJA STATUSU UŻYTKOWNIKA
                    if (internalOrder.User != null)
                    {
                        internalOrder.User.IsPremium = true;
                        _dbContext.Users.Update(internalOrder.User); // Oznacz użytkownika jako zmodyfikowanego
                    }
                    await _dbContext.SaveChangesAsync(); // Zapisz zmiany w zamówieniu i użytkowniku
                    return internalOrder;
                }
                else
                {
                    internalOrder.Status = OrderStatus.Failed;
                    Console.WriteLine($"PayPal capture status for {payPalOrderId} was not COMPLETED: {captureResponse?.status}");
                }
            }
            else
            {
                internalOrder.Status = OrderStatus.Failed;
                Console.WriteLine($"Error capturing PayPal order {payPalOrderId}: {response.StatusCode} - {responseContent}");
            }

            internalOrder.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(); // Zapisz status błędu
            return internalOrder;
        }


        public async Task<Order?> GetOrderByIdAsync(Guid orderId)
        {
            return await _dbContext.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<Order?> GetOrderByPayPalIdAsync(string payPalOrderId)
        {
            return await _dbContext.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.PayPalOrderId == payPalOrderId);
        }
    }

    // Pomocnicze klasy DTO dla odpowiedzi PayPal
    public class PayPalAuthResponse
    {
        public string? scope { get; set; }
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public string? app_id { get; set; }
        public int expires_in { get; set; }
        public string? nonce { get; set; }
    }

    public class PayPalOrderResponse
    {
        public string? id { get; set; }
        public string? status { get; set; } // Np. CREATED, SAVED, APPROVED, VOIDED, COMPLETED
        public List<PayPalLinkDescription>? links { get; set; }
    }

    public class PayPalCaptureResponse
    {
        public string? id { get; set; } // To jest ID capture, nie order ID
        public string? status { get; set; } // Np. COMPLETED, DECLINED, PARTIALLY_REFUNDED, PENDING, REFUNDED
                                            // ... inne pola, które mogą być przydatne ...
    }


    public class PayPalLinkDescription
    {
        public string? href { get; set; }
        public string? rel { get; set; } // Np. "self", "approve", "capture"
        public string? method { get; set; }
    }
}
