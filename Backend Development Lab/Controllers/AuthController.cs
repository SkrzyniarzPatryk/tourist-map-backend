using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend_Development_Lab.Dtos;
using Backend_Development_Lab.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using tourist_map_backend.Entities;

namespace Backend_Development_Lab.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        // private readonly ApplicationDbContext _dbContext; // Możesz wstrzyknąć, jeśli potrzebujesz bezpośredniego dostępu

        public AuthController(IUserService userService, IConfiguration configuration /*, ApplicationDbContext dbContext*/)
        {
            _userService = userService;
            _configuration = configuration;
            // _dbContext = dbContext;
        }

        // Metoda generująca token JWT (bez zmian, jeśli już działała poprawnie)
        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim> // Użyj List<Claim> dla elastyczności
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrEmpty(user.Username))
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.Username));
            }
            // Możesz dodać inne potrzebne claimy, np. role

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["TokenLifetimeMinutes"] ?? "15")), // Odczytaj czas życia z konfiguracji
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Endpoint rejestracji (dostosowany do zwracania danych użytkownika i ustawiania ciasteczka)
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existingUser = await _userService.GetUserByEmailAsync(registerDto.Email);
            if (existingUser != null) return Conflict("Email already exists.");

            if (!string.IsNullOrEmpty(registerDto.Username))
            {
                var existingUserByUsername = await _userService.GetUserByUsernameAsync(registerDto.Username);
                if (existingUserByUsername != null) return Conflict("Username already exists.");
            }


            var newUser = await _userService.RegisterUserAsync(registerDto.Username, registerDto.Email, registerDto.Password);
            if (newUser == null) return StatusCode(StatusCodes.Status500InternalServerError, "Failed to register user.");

            // Użytkownik zarejestrowany, teraz go zaloguj (ustaw ciasteczko i zwróć dane)
            var token = GenerateJwtToken(newUser);
            Response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = _configuration.GetValue<bool?>("CookieSettings:Secure") ?? !HttpContext.Request.Host.Host.Contains("localhost"), // Secure w produkcji
                SameSite = (SameSiteMode)Enum.Parse(typeof(SameSiteMode), _configuration.GetValue<string?>("CookieSettings:SameSite") ?? "Lax"),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration.GetValue<string?>("CookieSettings:ExpiresMinutes") ?? _configuration.GetValue<string?>("Jwt:TokenLifetimeMinutes") ?? "15")),
                Path = "/"
            });

            // Zwróć dane użytkownika, aby frontend wiedział, że jest zalogowany
            return Ok(new UserProfileDto // Stwórz DTO dla profilu użytkownika
            {
                Id = newUser.Id,
                Username = newUser.Username,
                Email = newUser.Email
            });
        }


        // Endpoint logowania (dostosowany do ustawiania ciasteczka i zwracania danych użytkownika)
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            User? user = await _userService.GetUserByUsernameAsync(loginDto.Login)
                         ?? await _userService.GetUserByEmailAsync(loginDto.Login);

            if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid credentials.");
            }

            var token = GenerateJwtToken(user);
            Response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = _configuration.GetValue<bool?>("CookieSettings:Secure") ?? !HttpContext.Request.Host.Host.Contains("localhost"),
                SameSite = (SameSiteMode)Enum.Parse(typeof(SameSiteMode), _configuration.GetValue<string?>("CookieSettings:SameSite") ?? "Lax"),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration.GetValue<string?>("CookieSettings:ExpiresMinutes") ?? _configuration.GetValue<string?>("Jwt:TokenLifetimeMinutes") ?? "15")),
                Path = "/"
            });

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email
            });
        }

        // Endpoint do wylogowania (usuwa ciasteczko)
        [HttpPost("logout")]
        [Authorize] // Tylko zalogowany użytkownik może się wylogować
        public IActionResult Logout()
        {
            // Usuń ciasteczko poprzez ustawienie go z przeszłą datą wygaśnięcia
            Response.Cookies.Delete("access_token", new CookieOptions
            {
                HttpOnly = true,
                Secure = _configuration.GetValue<bool?>("CookieSettings:Secure") ?? !HttpContext.Request.Host.Host.Contains("localhost"),
                SameSite = (SameSiteMode)Enum.Parse(typeof(SameSiteMode), _configuration.GetValue<string?>("CookieSettings:SameSite") ?? "Lax"),
                Path = "/"
                // Expires nie jest potrzebne przy Delete, ale upewnienie się, że opcje są takie same, jest dobrą praktyką
            });
            return Ok(new { message = "Logged out successfully." });
        }

        // Endpoint do sprawdzania statusu zalogowania i pobierania danych użytkownika
        [HttpGet("me")]
        [Authorize] // Wymaga ważnego ciasteczka JWT
        public async Task<IActionResult> GetMyInfo()
        {
            // ID użytkownika jest odczytywane z claimów w tokenie (dzięki [Authorize])
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier); // lub JwtRegisteredClaimNames.Sub
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized("Invalid token data.");
            }

            var user = await _userService.GetUserByIdAsync(userId); // Pobierz aktualne dane z bazy
            if (user == null)
            {
                // Użytkownik mógł zostać usunięty, a token jest jeszcze ważny
                // Wyloguj go (usuń ciasteczko)
                Response.Cookies.Delete("access_token", new CookieOptions { /* ... opcje jak w Logout ... */ Path = "/" });
                return Unauthorized("User not found.");
            }

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email
            });
        }


        // Endpointy logowania zewnętrznego (external-login, external-callback)
        // W ExternalLoginCallback, po pomyślnym uwierzytelnieniu Google i znalezieniu/rejestracji użytkownika:
        // Zamiast zwracać token JWT w ciele, ustaw ciasteczko i zwróć dane użytkownika lub przekieruj
        [HttpGet("external-login")]
        [AllowAnonymous]
        public IActionResult ExternalLogin([FromQuery] string provider, [FromQuery] string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(provider) || !provider.Equals(GoogleDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Unsupported external provider.");
            }
            // redirectUrl powinien teraz wskazywać na /api/auth/external-callback
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { ReturnUrl = returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("external-callback")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? error, string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(error))
            {
                // Przekieruj na stronę błędu w React lub zwróć błąd API
                // Np. return Redirect($"http://localhost:5173/login-error?message={error}");
                return BadRequest(new { message = "External login failed.", error = error });
            }

            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded || authenticateResult?.Principal == null)
            {
                // return Redirect($"http://localhost:5173/login-error?message=auth_failed");
                return BadRequest(new { message = "External authentication failed." });
            }

            var externalPrincipal = authenticateResult.Principal;
            var externalProvider = externalPrincipal.FindFirstValue(ClaimTypes.AuthenticationMethod) ?? externalPrincipal.Identity?.AuthenticationType;
            var externalUserId = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);
            var username = externalPrincipal.FindFirstValue(ClaimTypes.Name) ?? email?.Split('@')[0]; // Prosta heurystyka dla nazwy użytkownika

            // ... (walidacja danych z externalPrincipal) ...
            if (string.IsNullOrEmpty(externalUserId) || string.IsNullOrEmpty(email) /* ... */)
            {
                // return Redirect($"http://localhost:5173/login-error?message=missing_info");
                return BadRequest(new { message = "Could not retrieve required information from external provider." });
            }


            User? user = await _userService.GetUserByExternalIdAsync(externalProvider!, externalUserId);
            if (user == null)
            {
                // Sprawdź konflikt email, jeśli użytkownik z tym emailem już istnieje lokalnie
                var existingLocalUser = await _userService.GetUserByEmailAsync(email);
                if (existingLocalUser != null && string.IsNullOrEmpty(existingLocalUser.ExternalId)) // Ma email, ale nie jest to konto zewnętrzne
                {
                    // return Redirect($"http://localhost:5173/login-error?message=email_conflict");
                    return Conflict(new { message = $"An account with email {email} already exists. Please log in using your local credentials or link your accounts." });
                }
                user = await _userService.RegisterUserAsync(username!, email, null, externalProvider, externalUserId);
            }

            if (user == null)
            {
                // return Redirect($"http://localhost:5173/login-error?message=registration_failed");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not process external user." });
            }

            // Użytkownik znaleziony/zarejestrowany, teraz go zaloguj (ustaw ciasteczko JWT)
            var token = GenerateJwtToken(user);
            Response.Cookies.Append("access_token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = _configuration.GetValue<bool?>("CookieSettings:Secure") ?? !HttpContext.Request.Host.Host.Contains("localhost"),
                SameSite = (SameSiteMode)Enum.Parse(typeof(SameSiteMode), _configuration.GetValue<string?>("CookieSettings:SameSite") ?? "Lax"),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration.GetValue<string?>("CookieSettings:ExpiresMinutes") ?? _configuration.GetValue<string?>("Jwt:TokenLifetimeMinutes") ?? "15")),
                Path = "/"
            });

            // Wyloguj z tymczasowego ciasteczka OAuth
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Zamiast zwracać token, możesz przekierować na stronę sukcesu w React,
            // a React następnie wywoła /api/auth/me, aby pobrać dane użytkownika.
            // LUB zwrócić tu dane użytkownika, jeśli frontend potrafi to obsłużyć.
            // Najprościej jest przekierować.
            var frontendSuccessUrl = _configuration["FrontendUrls:LoginSuccess"] ?? "https://localhost:5173/"; // Odczytaj z konfiguracji
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) // Jeśli był returnUrl z oryginalnego żądania external-login
            {
                frontendSuccessUrl = returnUrl;
            }
            return Redirect(frontendSuccessUrl);
            // Alternatywnie, jeśli frontend potrafi obsłużyć bezpośrednią odpowiedź z tego callbacku:
            //return Ok(new UserProfileDto { Id = user.Id, Username = user.Username, Email = user.Email });
        }
    }
}
