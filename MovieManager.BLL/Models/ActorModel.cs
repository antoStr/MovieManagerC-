namespace MovieManager.BLL.Models
{
    public class ActorModel : IModelWithId
    {
        public int Id               { get; set; }
        public string FirstName     { get; set; } = string.Empty;
        public string LastName      { get; set; } = string.Empty;
        public DateOnly? BirthDate  { get; set; }
        public string? Country      { get; set; }
        public string? Biography    { get; set; }
    }
}
