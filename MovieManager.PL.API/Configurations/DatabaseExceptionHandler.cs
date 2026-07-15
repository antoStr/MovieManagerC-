using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MovieManager.PL.API.Configurations
{
    /// <summary>
    /// Traduce le violazioni dei vincoli di SQL Server in risposte HTTP sensate.
    /// Senza questo gestore, una richiesta sbagliata dal client (un GenreId che non
    /// esiste, una coppia film/attore inserita due volte) arriverebbe fuori come 500,
    /// facendo sembrare un bug del server quello che è un errore della richiesta.
    /// Le eccezioni che non riconosce le lascia passare, così restano dei veri 500.
    /// </summary>
    public class DatabaseExceptionHandler : IExceptionHandler
    {
        // Numeri di errore di SQL Server.
        private const int ConstraintViolation = 547;    // foreign key o check constraint
        private const int UniqueIndexViolation = 2601;
        private const int PrimaryKeyViolation = 2627;

        private readonly ILogger<DatabaseExceptionHandler> _logger;

        public DatabaseExceptionHandler(ILogger<DatabaseExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not DbUpdateException { InnerException: SqlException sqlException })
                return false;

            var problem = sqlException.Number switch
            {
                ConstraintViolation => new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Vincolo di database non rispettato",
                    Detail = "La richiesta viola un vincolo del database. Di solito significa che un id "
                           + "collegato (GenreId, DirectorId, MovieId, ActorId) non corrisponde a nessuna "
                           + "riga esistente, oppure che un valore è fuori dall'intervallo ammesso.",
                },

                UniqueIndexViolation or PrimaryKeyViolation => new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Risorsa già esistente",
                    Detail = "Esiste già una riga con questa chiave. Per la tabella ponte MovieActors la "
                           + "chiave è la coppia (MovieId, ActorId): se il collegamento esiste già, usa PUT "
                           + "per modificarlo invece di POST.",
                },

                _ => null,
            };

            if (problem is null)
                return false;

            _logger.LogWarning(
                exception,
                "Violazione di vincolo SQL {Number} su {Method} {Path} -> risposta {Status}",
                sqlException.Number, httpContext.Request.Method, httpContext.Request.Path, problem.Status);

            problem.Instance = httpContext.Request.Path;
            httpContext.Response.StatusCode = problem.Status!.Value;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

            return true;
        }
    }
}
