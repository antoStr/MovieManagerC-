namespace MovieManager.DAL.Entities
{
    public class MovieActor
    {
        public int MovieId              { get; set; }
        public Movie Movie              { get; set; } = null!;
        public int ActorId              { get; set; }
        public Actor Actor              { get; set; } = null!;
        public string? CharacterName    { get; set; }
        public bool IsLeadRole          { get; set; }
        public int DisplayOrder         { get; set; }
    }
}
