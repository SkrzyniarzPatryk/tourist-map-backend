// Controllers/CommentsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using tourist_map_backend.Interfaces;
using tourist_map_backend.Models;

namespace tourist_map_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(ICommentService commentService, ILogger<CommentsController> logger)
        {
            _commentService = commentService;
            _logger = logger;
        }

        /// <summary>
        /// Pobiera komentarze z opcjonalnymi filtrami:
        /// - pointId: identyfikator punktu
        /// - userId: identyfikator użytkownika
        /// Jeśli żaden parametr nie zostanie podany, pobierze wszystkie komentarze.
        /// </summary>
        /// <param name="pointId">Opcjonalny identyfikator punktu (Guid)</param>
        /// <param name="userId">Opcjonalny identyfikator użytkownika (Guid)</param>
        /// <returns>Lista komentarzy</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CommentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetComments([FromQuery] Guid? pointId, [FromQuery] Guid? userId)
        {
            var comments = await _commentService.GetCommentsAsync(pointId, userId);
            return Ok(comments);
        }

        /// <summary>
        /// Pobiera pojedynczy komentarz na podstawie identyfikatora.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCommentById(Guid id)
        {
            var comment = await _commentService.GetCommentByIdAsync(id);
            if (comment == null)
            {
                return NotFound();
            }
            return Ok(comment);
        }

        /// <summary>
        /// Dodaje nowy komentarz do bazy danych.
        /// </summary>
        [HttpPost]
        [Authorize] // Wymaga zalogowanego użytkownika
        [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddComment([FromBody] CreateCommentDto createCommentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid authorUserId))
            {
                _logger.LogWarning("AddComment: User ID not found in token or invalid.");
                return Unauthorized("User ID not found or invalid.");
            }

            // Sprawdzenie, czy PointId z DTO jest prawidłowym Guid (choć walidacja modelu powinna to złapać)
            // Możesz dodać dodatkową logikę, np. sprawdzanie, czy Point o danym ID istnieje

            var newComment = await _commentService.AddCommentAsync(createCommentDto, authorUserId);
            return CreatedAtAction(nameof(GetCommentById), new { id = newComment.Id }, newComment);
        }

        /// <summary>
        /// Aktualizuje istniejący komentarz.
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize] // Wymaga zalogowanego użytkownika
        [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateComment(Guid id, [FromBody] UpdateCommentDto updateCommentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                _logger.LogWarning("UpdateComment: User ID not found in token or invalid.");
                return Unauthorized("User ID not found or invalid.");
            }

            var (updatedComment, success, forbidden) = await _commentService.UpdateCommentAsync(id, updateCommentDto, currentUserId);

            if (forbidden)
            {
                _logger.LogWarning($"User {currentUserId} attempted to update comment {id} without permission.");
                return Forbid();
            }
            if (!success)
            {
                return NotFound();
            }
            if (updatedComment == null) // Powinno być obsłużone przez !success, ale dla pewności
            {
                _logger.LogError($"UpdateComment: Comment {id} update resulted in null DTO despite success flag.");
                return Problem("Failed to retrieve updated comment details.");
            }

            return Ok(updatedComment);
        }


        /// <summary>
        /// Usuwa komentarz na podstawie identyfikatora.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize] // Wymaga zalogowanego użytkownika
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteComment(Guid id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                _logger.LogWarning("DeleteComment: User ID not found in token or invalid.");
                return Unauthorized("User ID not found or invalid.");
            }

            var (success, forbidden) = await _commentService.DeleteCommentAsync(id, currentUserId);

            if (forbidden)
            {
                _logger.LogWarning($"User {currentUserId} attempted to delete comment {id} without permission.");
                return Forbid();
            }
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}