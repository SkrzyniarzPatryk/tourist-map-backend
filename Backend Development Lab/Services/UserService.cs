using Backend_Development_Lab.Interfaces;
using Microsoft.EntityFrameworkCore;
using tourist_map_backend.Data;
using tourist_map_backend.Entities;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context) // Wstrzyknij DbContext
    {
        _context = context;
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username != null && u.Username.Equals(username));
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.Equals(email));
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetUserByExternalIdAsync(string provider, string externalId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.ExternalProvider == provider && u.ExternalId == externalId);
    }

    public async Task<User?> RegisterUserAsync(string username, string email, string? password, string? externalProvider = null, string? externalId = null)
    {
        if (await GetUserByEmailAsync(email) != null) // Zawsze sprawdzaj po emailu, bo jest 'required'
        {
            return null; // Email już istnieje
        }

        if (string.IsNullOrEmpty(externalProvider) && await GetUserByUsernameAsync(username) != null)
        {
            // Dla lokalnej rejestracji, nazwa użytkownika też musi być unikalna (jeśli Username nie jest null)
            if (!string.IsNullOrEmpty(username)) return null;
        }


        string? passwordHash = null;
        if (!string.IsNullOrEmpty(password))
        {
            passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        }
        else if (string.IsNullOrEmpty(externalProvider))
        {
            // Dla lokalnej rejestracji hasło jest wymagane
            throw new ArgumentException("Password is required for local registration.");
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Username = username, // Może być null, jeśli nie podano
            Email = email,
            PasswordHash = passwordHash,
            ExternalProvider = externalProvider,
            ExternalId = externalId,
            IsPremium = false
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync(); // Zapisz zmiany do bazy danych

        return newUser;
    }
}