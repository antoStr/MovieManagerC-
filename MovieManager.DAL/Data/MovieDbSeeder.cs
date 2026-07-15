using Microsoft.EntityFrameworkCore;
using MovieManager.DAL.Entities;

namespace MovieManager.DAL.Data
{
    /// <summary>
    /// Popola il database con dati di esempio.
    /// È idempotente: ogni riga viene inserita solo se manca, confrontando la chiave
    /// naturale (nome, titolo, ...) e non l'Id. Può quindi girare a ogni avvio senza
    /// duplicare nulla e senza toccare le righe già presenti.
    /// </summary>
    public static class MovieDbSeeder
    {
        public static async Task SeedAsync(MovieDbContext context, CancellationToken cancellationToken = default)
        {
            var genres    = await SeedGenresAsync(context, cancellationToken);
            var directors = await SeedDirectorsAsync(context, cancellationToken);
            var actors    = await SeedActorsAsync(context, cancellationToken);
            var movies    = await SeedMoviesAsync(context, genres, directors, cancellationToken);

            await SeedCastAsync(context, movies, actors, cancellationToken);
            await SeedReviewsAsync(context, movies, cancellationToken);
        }

        private static async Task<Dictionary<string, Genre>> SeedGenresAsync(MovieDbContext context, CancellationToken cancellationToken)
        {
            var existing = await context.Genres.ToDictionaryAsync(g => g.Name, cancellationToken);

            var wanted = new[]
            {
                new Genre { Name = "Fantascienza", Description = "Storie ambientate in futuri e mondi possibili." },
                new Genre { Name = "Dramma",       Description = "Racconti costruiti sul conflitto dei personaggi." },
                new Genre { Name = "Thriller",     Description = "Tensione e suspense fino all'ultima scena." },
                new Genre { Name = "Commedia",     Description = "Toni leggeri e ritmo brillante." },
                new Genre { Name = "Animazione",   Description = "Opere realizzate con tecniche di animazione." },
            };

            await AddMissingAsync(context, existing, wanted, g => g.Name, cancellationToken);
            return existing;
        }

        private static async Task<Dictionary<string, Director>> SeedDirectorsAsync(MovieDbContext context, CancellationToken cancellationToken)
        {
            var existing = await context.Directors.ToDictionaryAsync(d => FullName(d.FirstName, d.LastName), cancellationToken);

            var wanted = new[]
            {
                new Director { FirstName = "Denis",       LastName = "Villeneuve", BirthDate = new DateOnly(1967, 10, 3),  Country = "Canada",        Biography = "Regista canadese, noto per la fantascienza d'autore." },
                new Director { FirstName = "Christopher", LastName = "Nolan",      BirthDate = new DateOnly(1970, 7, 30),  Country = "Regno Unito",   Biography = "Regista britannico dalle narrazioni non lineari." },
                new Director { FirstName = "Greta",       LastName = "Gerwig",     BirthDate = new DateOnly(1983, 8, 4),   Country = "Stati Uniti",   Biography = "Attrice e regista statunitense." },
                new Director { FirstName = "Bong",        LastName = "Joon-ho",    BirthDate = new DateOnly(1969, 9, 14),  Country = "Corea del Sud", Biography = "Regista coreano, Oscar per il miglior film nel 2020." },
                new Director { FirstName = "Hayao",       LastName = "Miyazaki",   BirthDate = new DateOnly(1941, 1, 5),   Country = "Giappone",      Biography = "Cofondatore dello Studio Ghibli." },
            };

            await AddMissingAsync(context, existing, wanted, d => FullName(d.FirstName, d.LastName), cancellationToken);
            return existing;
        }

        private static async Task<Dictionary<string, Actor>> SeedActorsAsync(MovieDbContext context, CancellationToken cancellationToken)
        {
            var existing = await context.Actors.ToDictionaryAsync(a => FullName(a.FirstName, a.LastName), cancellationToken);

            var wanted = new[]
            {
                new Actor { FirstName = "Timothee", LastName = "Chalamet", BirthDate = new DateOnly(1995, 12, 27), Country = "Stati Uniti",   Biography = "Attore statunitense, protagonista di Dune." },
                new Actor { FirstName = "Zendaya",  LastName = "Coleman",  BirthDate = new DateOnly(1996, 9, 1),   Country = "Stati Uniti",   Biography = "Attrice e cantante statunitense." },
                new Actor { FirstName = "Rebecca",  LastName = "Ferguson", BirthDate = new DateOnly(1983, 10, 19), Country = "Svezia",        Biography = "Attrice svedese." },
                new Actor { FirstName = "Cillian",  LastName = "Murphy",   BirthDate = new DateOnly(1976, 5, 25),  Country = "Irlanda",       Biography = "Attore irlandese, Oscar per Oppenheimer." },
                new Actor { FirstName = "Emily",    LastName = "Blunt",    BirthDate = new DateOnly(1983, 2, 23),  Country = "Regno Unito",   Biography = "Attrice britannica." },
                new Actor { FirstName = "Robert",   LastName = "Downey Jr", BirthDate = new DateOnly(1965, 4, 4),  Country = "Stati Uniti",   Biography = "Attore statunitense." },
                new Actor { FirstName = "Margot",   LastName = "Robbie",   BirthDate = new DateOnly(1990, 7, 2),   Country = "Australia",     Biography = "Attrice e produttrice australiana." },
                new Actor { FirstName = "Ryan",     LastName = "Gosling",  BirthDate = new DateOnly(1980, 11, 12), Country = "Canada",        Biography = "Attore canadese." },
                new Actor { FirstName = "Song",     LastName = "Kang-ho",  BirthDate = new DateOnly(1967, 1, 17),  Country = "Corea del Sud", Biography = "Attore coreano, volto ricorrente di Bong Joon-ho." },
                new Actor { FirstName = "Harrison", LastName = "Ford",     BirthDate = new DateOnly(1942, 7, 13),  Country = "Stati Uniti",   Biography = "Attore statunitense." },
            };

            await AddMissingAsync(context, existing, wanted, a => FullName(a.FirstName, a.LastName), cancellationToken);
            return existing;
        }

        private static async Task<Dictionary<string, Movie>> SeedMoviesAsync(
            MovieDbContext context,
            Dictionary<string, Genre> genres,
            Dictionary<string, Director> directors,
            CancellationToken cancellationToken)
        {
            var existing = await context.Movies.ToDictionaryAsync(m => m.Title, cancellationToken);

            var wanted = new[]
            {
                new Movie
                {
                    Title = "Dune", OriginalTitle = "Dune", ReleaseDate = new DateOnly(2021, 10, 22), DurationMinutes = 155,
                    Synopsis = "Paul Atreides guida la ribellione per liberare il pianeta Arrakis.",
                    Language = "Inglese", Country = "Stati Uniti", Budget = 165_000_000m, Revenue = 402_000_000m, AgeRating = "T",
                    Genre = genres["Fantascienza"], Director = directors["Denis Villeneuve"],
                },
                new Movie
                {
                    Title = "Blade Runner 2049", OriginalTitle = "Blade Runner 2049", ReleaseDate = new DateOnly(2017, 10, 5), DurationMinutes = 164,
                    Synopsis = "Il blade runner K scopre un segreto che puo far precipitare la societa nel caos.",
                    Language = "Inglese", Country = "Stati Uniti", Budget = 150_000_000m, Revenue = 267_000_000m, AgeRating = "VM14",
                    Genre = genres["Fantascienza"], Director = directors["Denis Villeneuve"],
                },
                new Movie
                {
                    Title = "Oppenheimer", OriginalTitle = "Oppenheimer", ReleaseDate = new DateOnly(2023, 7, 21), DurationMinutes = 180,
                    Synopsis = "La storia del fisico che diresse il Progetto Manhattan.",
                    Language = "Inglese", Country = "Stati Uniti", Budget = 100_000_000m, Revenue = 975_000_000m, AgeRating = "VM14",
                    Genre = genres["Dramma"], Director = directors["Christopher Nolan"],
                },
                new Movie
                {
                    Title = "Barbie", OriginalTitle = "Barbie", ReleaseDate = new DateOnly(2023, 7, 20), DurationMinutes = 114,
                    Synopsis = "Barbie lascia Barbieland per scoprire il mondo reale.",
                    Language = "Inglese", Country = "Stati Uniti", Budget = 145_000_000m, Revenue = 1_445_000_000m, AgeRating = "T",
                    Genre = genres["Commedia"], Director = directors["Greta Gerwig"],
                },
                new Movie
                {
                    Title = "Parasite", OriginalTitle = "Gisaengchung", ReleaseDate = new DateOnly(2019, 5, 30), DurationMinutes = 132,
                    Synopsis = "Una famiglia povera si insinua nella casa di una famiglia ricca.",
                    Language = "Coreano", Country = "Corea del Sud", Budget = 11_400_000m, Revenue = 263_000_000m, AgeRating = "VM14",
                    Genre = genres["Thriller"], Director = directors["Bong Joon-ho"],
                },
                new Movie
                {
                    Title = "La citta incantata", OriginalTitle = "Sen to Chihiro no kamikakushi", ReleaseDate = new DateOnly(2001, 7, 20), DurationMinutes = 125,
                    Synopsis = "Chihiro resta intrappolata in un mondo popolato da spiriti.",
                    Language = "Giapponese", Country = "Giappone", Budget = 19_000_000m, Revenue = 395_000_000m, AgeRating = "T",
                    Genre = genres["Animazione"], Director = directors["Hayao Miyazaki"],
                },
            };

            await AddMissingAsync(context, existing, wanted, m => m.Title, cancellationToken);
            return existing;
        }

        private static async Task SeedCastAsync(
            MovieDbContext context,
            Dictionary<string, Movie> movies,
            Dictionary<string, Actor> actors,
            CancellationToken cancellationToken)
        {
            var existing = (await context.MovieActors
                    .Select(ma => new { ma.MovieId, ma.ActorId })
                    .ToListAsync(cancellationToken))
                .Select(ma => (ma.MovieId, ma.ActorId))
                .ToHashSet();

            var wanted = new (string Movie, string Actor, string Character, bool Lead, int Order)[]
            {
                ("Dune",              "Timothee Chalamet",  "Paul Atreides",         true,  1),
                ("Dune",              "Zendaya Coleman",    "Chani",                 false, 2),
                ("Dune",              "Rebecca Ferguson",   "Lady Jessica",          false, 3),
                ("Blade Runner 2049", "Ryan Gosling",       "K",                     true,  1),
                ("Blade Runner 2049", "Harrison Ford",      "Rick Deckard",          false, 2),
                ("Oppenheimer",       "Cillian Murphy",     "J. Robert Oppenheimer", true,  1),
                ("Oppenheimer",       "Emily Blunt",        "Kitty Oppenheimer",     false, 2),
                ("Oppenheimer",       "Robert Downey Jr",   "Lewis Strauss",         false, 3),
                ("Barbie",            "Margot Robbie",      "Barbie",                true,  1),
                ("Barbie",            "Ryan Gosling",       "Ken",                   false, 2),
                ("Parasite",          "Song Kang-ho",       "Kim Ki-taek",           true,  1),
            };

            foreach (var (title, name, character, lead, order) in wanted)
            {
                var movie = movies[title];
                var actor = actors[name];

                if (!existing.Add((movie.Id, actor.Id)))
                    continue;

                context.MovieActors.Add(new MovieActor
                {
                    Movie = movie,
                    Actor = actor,
                    CharacterName = character,
                    IsLeadRole = lead,
                    DisplayOrder = order,
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedReviewsAsync(MovieDbContext context, Dictionary<string, Movie> movies, CancellationToken cancellationToken)
        {
            var existing = (await context.Reviews
                    .Select(r => new { r.MovieId, r.ReviewerName })
                    .ToListAsync(cancellationToken))
                .Select(r => (r.MovieId, r.ReviewerName))
                .ToHashSet();

            var wanted = new (string Movie, string Reviewer, int Score, string Comment)[]
            {
                ("Dune",              "Giulia",  9,  "Fotografia e sonoro da vedere al cinema."),
                ("Blade Runner 2049", "Luca",    9,  "Lento ma ipnotico, all'altezza dell'originale."),
                ("Oppenheimer",       "Anna",    10, "Tre ore che volano, montaggio perfetto."),
                ("Oppenheimer",       "Marco",   7,  "Ottimo cast, secondo tempo un po' dispersivo."),
                ("Barbie",            "Sara",    7,  "Piu profondo di quanto la locandina suggerisca."),
                ("Parasite",          "Davide",  10, "Cambia genere tre volte e non sbaglia un colpo."),
                ("La citta incantata", "Elena",  10, "Un classico dell'animazione, adatto a ogni eta."),
            };

            foreach (var (title, reviewer, score, comment) in wanted)
            {
                var movie = movies[title];

                if (!existing.Add((movie.Id, reviewer)))
                    continue;

                context.Reviews.Add(new Review
                {
                    Movie = movie,
                    ReviewerName = reviewer,
                    Score = score,
                    Comment = comment,
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Aggiunge le entità la cui chiave naturale non è già presente e aggiorna il
        /// dizionario, che dopo il salvataggio contiene sia le righe vecchie sia le nuove
        /// (con l'Id assegnato dal database) pronte per essere referenziate.
        /// </summary>
        private static async Task AddMissingAsync<T>(
            MovieDbContext context,
            Dictionary<string, T> existing,
            IEnumerable<T> wanted,
            Func<T, string> keySelector,
            CancellationToken cancellationToken) where T : class
        {
            foreach (var entity in wanted)
            {
                var key = keySelector(entity);
                if (existing.ContainsKey(key))
                    continue;

                context.Add(entity);
                existing[key] = entity;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static string FullName(string firstName, string lastName) => $"{firstName} {lastName}";
    }
}
