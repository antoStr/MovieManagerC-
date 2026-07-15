using System.ComponentModel.DataAnnotations;

namespace MovieManager.BLL.Models
{
    public class ActorModel : IModelWithId
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Il nome è obbligatorio.")]
        [StringLength(100, ErrorMessage = "Il nome non può superare i 100 caratteri.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Il cognome è obbligatorio.")]
        [StringLength(100, ErrorMessage = "Il cognome non può superare i 100 caratteri.")]
        public string LastName { get; set; } = string.Empty;

        public DateOnly? BirthDate  { get; set; }
        public string? Country      { get; set; }
        public string? Biography    { get; set; }
    }
}
