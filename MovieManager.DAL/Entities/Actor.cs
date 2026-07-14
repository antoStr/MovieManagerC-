namespace MovieManager.DAL.Entities
{
    public class Actor
    {
        public int Id                               { get; set; }
        public string FirstName                     { get; set; } = string.Empty;
        public string LastName                      { get; set; } = string.Empty;
        public DateOnly? BirthDate                  { get; set; }
        public string? Country                      { get; set; }
        public string? Biography                    { get; set; }
        public ICollection<MovieActor> MovieActors  { get; set; } = new List<MovieActor>();
    }
}
