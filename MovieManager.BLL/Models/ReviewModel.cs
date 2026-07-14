namespace MovieManager.BLL.Models
{
    public class ReviewModel : IModelWithId
    {
        public int Id               { get; set; }
        public int MovieId          { get; set; }
        public string ReviewerName  { get; set; } = string.Empty;
        public int Score            { get; set; }
        public string? Comment      { get; set; }
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    }
}
