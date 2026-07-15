using System.ComponentModel.DataAnnotations;

namespace MovieManager.BLL.Models
{
    /// <summary>
    /// Model per la tabella ponte MovieActor. Ha chiave composta (MovieId, ActorId)
    /// e per questo NON implementa <see cref="IModelWithId"/>: non è gestibile dal
    /// GenericService basato su Id singolo, ma dal dedicato MovieActorService.
    /// </summary>
    public class MovieActorModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "MovieId deve riferirsi a un film esistente.")]
        public int MovieId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "ActorId deve riferirsi a un attore esistente.")]
        public int ActorId { get; set; }

        public string? CharacterName    { get; set; }
        public bool IsLeadRole          { get; set; }
        public int DisplayOrder         { get; set; }
    }
}
