namespace MovieManager.BLL.Models
{
    public class MovieModel : IModelWithId
    {
        public int Id                   { get; set; }
        public string Title             { get; set; } = string.Empty;
        public string? OriginalTitle    { get; set; }
        public DateOnly ReleaseDate     { get; set; }
        public int DurationMinutes      { get; set; }
        public string? Synopsis         { get; set; }
        public string Language          { get; set; } = string.Empty;
        public string? Country          { get; set; }
        public decimal? Budget          { get; set; }
        public decimal? Revenue         { get; set; }
        public string? PosterUrl        { get; set; }
        public string? AgeRating        { get; set; }
        public int GenreId              { get; set; }
        public int DirectorId           { get; set; }
    }
}
