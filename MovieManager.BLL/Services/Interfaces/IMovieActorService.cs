using MovieManager.BLL.Models;

namespace MovieManager.BLL.Services.Interfaces
{
    /// <summary>
    /// Servizio dedicato alla tabella ponte MovieActor (chiave composta MovieId + ActorId).
    /// Sostituisce il GenericService, che lavora solo con chiavi singole.
    /// </summary>
    public interface IMovieActorService
    {
        Task<MovieActorModel?> GetByIdsAsync(int movieId, int actorId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MovieActorModel>> GetByMovieIdAsync(int movieId, CancellationToken cancellationToken = default);

        Task<MovieActorModel> CreateAsync(MovieActorModel model, CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(MovieActorModel model, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(int movieId, int actorId, CancellationToken cancellationToken = default);
    }
}
