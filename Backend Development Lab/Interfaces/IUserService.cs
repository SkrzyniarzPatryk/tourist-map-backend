using tourist_map_backend.Entities;

namespace Backend_Development_Lab.Interfaces
{
    public interface IUserService
    {
        Task<User?> RegisterUserAsync(string username, string email, string? password, string? externalProvider = null, string? externalId = null);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByIdAsync(Guid id);
        Task<User?> GetUserByExternalIdAsync(string provider, string externalId);
    }
}
