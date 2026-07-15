using System.ComponentModel.DataAnnotations;

namespace MovieManager.BLL.Models
{
    public class GenreModel : IModelWithId
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Il nome del genere è obbligatorio.")]
        [StringLength(100, ErrorMessage = "Il nome non può superare i 100 caratteri.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
