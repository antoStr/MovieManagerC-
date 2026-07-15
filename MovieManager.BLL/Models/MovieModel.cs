using System.ComponentModel.DataAnnotations;

namespace MovieManager.BLL.Models
{
    public class MovieModel : IModelWithId
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Il titolo è obbligatorio.")]
        [StringLength(200, ErrorMessage = "Il titolo non può superare i 200 caratteri.")]
        public string Title { get; set; } = string.Empty;

        public string? OriginalTitle    { get; set; }
        public DateOnly ReleaseDate     { get; set; }
        public int DurationMinutes      { get; set; }
        public string? Synopsis         { get; set; }

        [Required(ErrorMessage = "La lingua è obbligatoria.")]
        public string Language { get; set; } = string.Empty;

        public string? Country      { get; set; }
        public decimal? Budget      { get; set; }
        public decimal? Revenue     { get; set; }
        public string? PosterUrl    { get; set; }
        public string? AgeRating    { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "GenreId deve riferirsi a un genere esistente.")]
        public int GenreId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "DirectorId deve riferirsi a un regista esistente.")]
        public int DirectorId { get; set; }
    }
}
