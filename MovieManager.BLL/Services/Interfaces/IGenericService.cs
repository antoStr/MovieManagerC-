namespace MovieManager.BLL.Services.Interfaces
{
    public interface IGenericService<TModel> where TModel : class
    {
        Task<TModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TModel>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default);

        // true = aggiornato/eliminato, false = record non trovato
        Task<bool> UpdateAsync(TModel model, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
