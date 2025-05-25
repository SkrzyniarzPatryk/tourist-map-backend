// Services/ICommentService.cs

// Services/ICommentService.cs
using tourist_map_backend.Models;

namespace tourist_map_backend.Interfaces
{
    public interface ICommentService
    {
        Task<IEnumerable<CommentDto>> GetCommentsAsync(Guid? pointId, Guid? userId);
        Task<CommentDto?> GetCommentByIdAsync(Guid commentId);
        Task<CommentDto> AddCommentAsync(CreateCommentDto createCommentDto, Guid authorUserId);
        Task<(CommentDto? comment, bool success, bool forbidden)> UpdateCommentAsync(Guid commentId, UpdateCommentDto updateCommentDto, Guid currentUserId);
        Task<(bool success, bool forbidden)> DeleteCommentAsync(Guid commentId, Guid currentUserId);
    }
}