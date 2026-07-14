using MovieManager.DAL.Entities;

namespace MovieManager.DAL.Repositories.Interfaces
{
    public interface IMovieActorRepository
    {
        Task<MovieActor?> GetByIdsAsync(int movieId, int actorId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MovieActor>> GetByMovieIdAsync(int movieId, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(int movieId, int actorId, CancellationToken cancellationToken = default);

        Task AddAsync(MovieActor entity, CancellationToken cancellationToken = default);

        void Update(MovieActor entity);

        void Remove(MovieActor entity);
    }
}
