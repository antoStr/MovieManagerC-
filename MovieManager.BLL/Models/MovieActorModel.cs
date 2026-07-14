namespace MovieManager.BLL.Models
{
    /// <summary>
    /// Model per la tabella ponte MovieActor. Ha chiave composta (MovieId, ActorId)
    /// e per questo NON implementa <see cref="IModelWithId"/>: non è gestibile dal
    /// GenericService basato su Id singolo, ma dal dedicato MovieActorService.
    /// </summary>
    public class MovieActorModel
    {
        public int MovieId              { get; set; }
        public int ActorId              { get; set; }
        public string? CharacterName    { get; set; }
        public bool IsLeadRole          { get; set; }
        public int DisplayOrder         { get; set; }
    }
}
