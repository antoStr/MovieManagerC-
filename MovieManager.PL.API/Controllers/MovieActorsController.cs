using Microsoft.AspNetCore.Mvc;
using MovieManager.BLL.Models;
using MovieManager.BLL.Services.Interfaces;

namespace MovieManager.PL.API.Controllers
{
    // Controller dedicato alla tabella ponte MovieActor: chiave composta (movieId, actorId),
    // quindi non usa IGenericService ma il servizio custom IMovieActorService.
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MovieActorsController : ControllerBase
    {
        private readonly IMovieActorService _service;

        public MovieActorsController(IMovieActorService service)
        {
            _service = service;
        }

        // GET api/movieactors/movie/5  -> tutte le associazioni (attori) di un film
        [HttpGet("movie/{movieId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<MovieActorModel>>> GetByMovie(int movieId, CancellationToken cancellationToken)
            => Ok(await _service.GetByMovieIdAsync(movieId, cancellationToken));

        // GET api/movieactors/5/8  -> singola associazione film-attore
        [HttpGet("{movieId:int}/{actorId:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MovieActorModel>> GetByIds(int movieId, int actorId, CancellationToken cancellationToken)
        {
            var model = await _service.GetByIdsAsync(movieId, actorId, cancellationToken);
            return model is null ? NotFound() : Ok(model);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MovieActorModel>> Create(MovieActorModel model, CancellationToken cancellationToken)
        {
            var created = await _service.CreateAsync(model, cancellationToken);
            return CreatedAtAction(nameof(GetByIds), new { movieId = created.MovieId, actorId = created.ActorId }, created);
        }

        [HttpPut("{movieId:int}/{actorId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int movieId, int actorId, MovieActorModel model, CancellationToken cancellationToken)
        {
            if (movieId != model.MovieId || actorId != model.ActorId)
                return BadRequest();

            var updated = await _service.UpdateAsync(model, cancellationToken);
            return updated ? NoContent() : NotFound();
        }

        [HttpDelete("{movieId:int}/{actorId:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int movieId, int actorId, CancellationToken cancellationToken)
        {
            var deleted = await _service.DeleteAsync(movieId, actorId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}
