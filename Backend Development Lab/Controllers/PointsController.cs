// Controllers/PointsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using tourist_map_backend.Interfaces;
using tourist_map_backend.Models;

namespace tourist_map_backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PointsController : ControllerBase
    {
        private readonly IPointService _pointService;
        private readonly ILogger<PointsController> _logger;

        public PointsController(IPointService pointService, ILogger<PointsController> logger)
        {
            _pointService = pointService;
            _logger = logger;
        }

        /// <summary>
        /// Pobiera wszystkie punkty.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PointDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllPoints()
        {
            var points = await _pointService.GetAllPointsAsync();
            return Ok(points);
        }

        /// <summary>
        /// Pobiera punkty z paginacją i filtrami.
        /// Query params: _page, _per_page, _sort (np. name:asc), category, rating_gt
        /// </summary>
        [HttpGet("paginated")]
        [ProducesResponseType(typeof(PaginatedResponse<PointDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPaginatedPoints(
            [FromQuery(Name = "_page")] int page = 1,
            [FromQuery(Name = "_per_page")] int perPage = 10,
            [FromQuery(Name = "_sort")] string? sort = null,
            [FromQuery] string? category = null,
            [FromQuery(Name = "rating_gt")] double? ratingGreaterThan = null)
        {
            var paginationParams = new PaginationParams
            {
                Page = page,
                PerPage = perPage,
                Sort = sort,
                Category = category,
                RatingGreaterThan = ratingGreaterThan
            };
            var paginatedResult = await _pointService.GetPaginatedPointsAsync(paginationParams);
            return Ok(paginatedResult);
        }

        /// <summary>
        /// Pobiera punkt po identyfikatorze.
        /// </summary>
        [HttpGet("{id:guid}")] // Zmieniono na guid
        [ProducesResponseType(typeof(PointDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPointById(Guid id) // Zmieniono typ na Guid
        {
            var point = await _pointService.GetPointByIdAsync(id);
            if (point == null)
            {
                return NotFound();
            }
            return Ok(point);
        }

        /// <summary>
        /// Pobiera punkty stworzone przez konkretnego użytkownika.
        /// </summary>
        [HttpGet("user/{userId:guid}")]
        [ProducesResponseType(typeof(IEnumerable<PointDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPointsByUserId(Guid userId)
        {
            var points = await _pointService.GetPointsByUserIdAsync(userId);
            return Ok(points);
        }

        /// <summary>
        /// Tworzy nowy punkt.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(PointDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreatePoint([FromBody] CreatePointDto createPointDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (createPointDto.Position == null || createPointDto.Position.Count != 2)
            {
                ModelState.AddModelError("Position", "Position must contain exactly two double values (Latitude, Longitude).");
                return BadRequest(ModelState);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid creatorUserId))
            {
                _logger.LogWarning("CreatePoint: User ID not found in token or invalid.");
                return Unauthorized("User ID not found or invalid.");
            }

            var newPoint = await _pointService.CreatePointAsync(createPointDto, creatorUserId);
            return CreatedAtAction(nameof(GetPointById), new { id = newPoint.Id }, newPoint);
        }

        /// <summary>
        /// Aktualizuje istniejący punkt.
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(PointDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePoint(Guid id, [FromBody] UpdatePointDto updatePointDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (updatePointDto.Position != null && updatePointDto.Position.Count != 2)
            {
                ModelState.AddModelError("Position", "If provided, Position must contain exactly two double values (Latitude, Longitude).");
                return BadRequest(ModelState);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                _logger.LogWarning("UpdatePoint: User ID not found in token or invalid.");
                return Unauthorized("User ID not found or invalid.");
            }

            var (updatedPoint, success, forbidden) = await _pointService.UpdatePointAsync(id, updatePointDto, currentUserId);

            if (forbidden) return Forbid();
            if (!success) return NotFound();

            return Ok(updatedPoint);
        }

        /// <summary>
        /// Aktualizuje ocenę i liczbę recenzji punktu.
        /// </summary>
        [HttpPatch("{id:guid}/rating")] // Zgodnie z frontendowym service `patch<PointModel>(`/${pointId}`, { rating, reviews })`
                                        // Tutaj zrobiłem dedykowany endpoint dla większej klarowności.
                                        // Alternatywnie, można by to obsłużyć w ogólnym PUT/PATCH jeśli UpdatePointDto miałoby pola Rating i Reviews.
        [Authorize] // Prawdopodobnie tylko określone role lub system powinien to robić, nie dowolny użytkownik.
                    // Chyba, że to jest wywoływane przez system po dodaniu komentarza.
                    // Dla przykładu, zostawiam [Authorize], ale przemyśl, kto ma mieć dostęp.
        [ProducesResponseType(typeof(PointDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePointRating(Guid id, [FromBody] UpdatePointRatingDto ratingDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Tutaj można dodać logikę autoryzacji, np. czy użytkownik może aktualizować rating
            // (np. tylko admin, albo system po dodaniu nowego komentarza).
            // Dla uproszczenia, zakładam, że jeśli użytkownik jest autoryzowany, to może to zrobić.

            var success = await _pointService.UpdatePointRatingAsync(id, ratingDto);
            if (!success)
            {
                return NotFound($"Point with id {id} not found.");
            }

            var updatedPoint = await _pointService.GetPointByIdAsync(id); // Pobierz zaktualizowany punkt
            return Ok(updatedPoint);
        }


        /// <summary>
        /// Usuwa punkt.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePoint(Guid id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid currentUserId))
            {
                _logger.LogWarning("DeletePoint: User ID not found in token or invalid.");
                return Unauthorized("User ID not found or invalid.");
            }

            var (success, forbidden) = await _pointService.DeletePointAsync(id, currentUserId);

            if (forbidden) return Forbid();
            if (!success) return NotFound();

            return NoContent();
        }
    }
}