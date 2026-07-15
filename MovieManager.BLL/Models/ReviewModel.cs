using System.ComponentModel.DataAnnotations;

namespace MovieManager.BLL.Models
{
    public class ReviewModel : IModelWithId
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "MovieId deve riferirsi a un film esistente.")]
        public int MovieId { get; set; }

        [Required(ErrorMessage = "Il nome del recensore è obbligatorio.")]
        [StringLength(100, ErrorMessage = "Il nome del recensore non può superare i 100 caratteri.")]
        public string ReviewerName { get; set; } = string.Empty;

        // Stesso intervallo del check constraint CK_Review_Score sul database:
        // qui però l'errore diventa un 400 leggibile invece di un 500.
        [Range(1, 10, ErrorMessage = "Il punteggio deve essere compreso tra 1 e 10.")]
        public int Score { get; set; }

        public string? Comment      { get; set; }
        public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    }
}
