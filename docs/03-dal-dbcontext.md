# 3) DAL — Il DbContext (Entity Framework Core)

[⬅ Torna all'indice](../README.md)

Ho le entità, ma da sole non parlano col database. Serve il **DbContext**: la classe che fa da ponte tra le mie classi C# e le tabelle SQL. Va in `MovieManager.DAL/Data/MovieDbContext.cs`, namespace `MovieManager.DAL.Data`.

---

## (?) Che cosa è Entity Framework Core? Che cosa è un ORM?

**Entity Framework Core (EF Core)** è un **ORM** (Object-Relational Mapper): un componente che traduce automaticamente tra il mondo a **oggetti** (le mie classi C#) e il mondo **relazionale** (tabelle e righe SQL).

Nel gestionale Java scrivevo a mano ogni query (`INSERT INTO dipendenti ...`), aprivo la connessione, usavo `PreparedStatement`, gestivo il `ResultSet`... Con un ORM tutto questo sparisce: scrivo `_dbSet.ToListAsync()` e EF genera ed esegue lui la `SELECT`, mappando ogni riga in un oggetto. Meno codice, meno errori, meno SQL scritto a mano.

## (?) Che cosa è il DbContext?

Il `DbContext` è la **sessione** con il database. Rappresenta la connessione e tiene traccia degli oggetti che sto leggendo o modificando (il cosiddetto *change tracking*). È l'equivalente concettuale, ma molto più potente, della vecchia classe `DatabaseConnection`.

---

## 3.1 La classe e i DbSet

```csharp
using Microsoft.EntityFrameworkCore;
using MovieManager.DAL.Entities;

namespace MovieManager.DAL.Data
{
    public class MovieDbContext : DbContext
    {
        public MovieDbContext(DbContextOptions<MovieDbContext> options) : base(options)
        {
        }

        public DbSet<Movie> Movies              { get; set; }
        public DbSet<Genre> Genres              { get; set; }
        public DbSet<Director> Directors        { get; set; }
        public DbSet<Actor> Actors              { get; set; }
        public DbSet<MovieActor> MovieActors    { get; set; }
        public DbSet<Review> Reviews            { get; set; }

        // ... OnModelCreating (sotto) ...
    }
}
```

### (?) Che cosa è un `DbSet<T>`?

Un `DbSet<Movie>` rappresenta la **tabella dei film**. Da lì partono tutte le query: `Movies.ToListAsync()`, `Movies.FindAsync(id)`, `Movies.Add(...)`, ecc. Ogni entità che voglio gestire deve avere il suo `DbSet`.

### Il costruttore e `DbContextOptions`

Il costruttore riceve un `DbContextOptions<MovieDbContext>`. È qui che, all'avvio dell'app, gli dirò **quale database usare** e **con quale stringa di connessione** — ma quella configurazione non sta nel DAL: sta nel `Program.cs` del PL, così il DAL resta indipendente dal provider (SQL Server, SQLite, ecc.). Vedi [capitolo 9](09-plapi-program-di-scalar.md).

---

## 3.2 OnModelCreating e la Fluent API

Alcune cose EF le capisce da solo per convenzione (le chiavi `Id`, le relazioni semplici). Altre gliele devo dire esplicitamente, e lo faccio dentro `OnModelCreating` usando la **Fluent API** (una serie di metodi concatenati che configurano il modello).

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Chiave composta di MovieActor (tabella ponte molti-a-molti Movie <-> Actor)
    modelBuilder.Entity<MovieActor>()
        .HasKey(ma => new { ma.MovieId, ma.ActorId });

    // Relazione MovieActor -> Movie (molti-a-uno)
    modelBuilder.Entity<MovieActor>()
        .HasOne(ma => ma.Movie)
        .WithMany(m => m.MovieActors)
        .HasForeignKey(ma => ma.MovieId);

    // Relazione MovieActor -> Actor (molti-a-uno)
    modelBuilder.Entity<MovieActor>()
        .HasOne(ma => ma.Actor)
        .WithMany(a => a.MovieActors)
        .HasForeignKey(ma => ma.ActorId);

    // Lunghezza e obbligatorietà sui campi principali
    modelBuilder.Entity<Movie>()
        .Property(m => m.Title).IsRequired().HasMaxLength(200);
    modelBuilder.Entity<Genre>()
        .Property(g => g.Name).IsRequired().HasMaxLength(100);
    modelBuilder.Entity<Director>()
        .Property(d => d.FirstName).IsRequired().HasMaxLength(100);
    modelBuilder.Entity<Director>()
        .Property(d => d.LastName).IsRequired().HasMaxLength(100);
    modelBuilder.Entity<Actor>()
        .Property(a => a.FirstName).IsRequired().HasMaxLength(100);
    modelBuilder.Entity<Actor>()
        .Property(a => a.LastName).IsRequired().HasMaxLength(100);
    modelBuilder.Entity<Review>()
        .Property(r => r.ReviewerName).IsRequired().HasMaxLength(100);

    // Vincolo sul punteggio della recensione: da 1 a 10
    modelBuilder.Entity<Review>()
        .ToTable(t => t.HasCheckConstraint("CK_Review_Score", "[Score] >= 1 AND [Score] <= 10"));

    base.OnModelCreating(modelBuilder);
}
```

### La chiave composta

```csharp
.HasKey(ma => new { ma.MovieId, ma.ActorId });
```

Con `new { ... }` creo un tipo anonimo che rappresenta la coppia di colonne: è così che dico a EF "la chiave primaria di questa tabella sono **due** colonne insieme". Senza questa riga EF cercherebbe un `Id` singolo che qui non esiste.

### Le due relazioni (occhio a questo!)

`MovieActor` è collegata **sia** a `Movie` **sia** a `Actor`. Le due relazioni si leggono così: "un `MovieActor` **ha uno** (`HasOne`) `Movie`, che **ha molti** (`WithMany`) `MovieActors`, tramite la chiave esterna `MovieId`". Idem per `Actor` con `ActorId`.

> ⚠️ **Errore classico da evitare:** è facilissimo, copiando la prima relazione per fare la seconda, dimenticarsi di cambiare `Movie` in `Actor` e lasciare `.HasOne(ma => ma.Movie)` due volte. Il progetto compilerebbe lo stesso, ma la relazione con l'attore sarebbe sbagliata e il collegamento film-attore non funzionerebbe. Ho fatto attenzione a mappare la seconda relazione verso **`Actor`** (`HasOne(ma => ma.Actor)` / `WithMany(a => a.MovieActors)` / `HasForeignKey(ma => ma.ActorId)`).

### `IsRequired()` e `HasMaxLength()`

Sono i vincoli sui campi principali: `IsRequired()` genera una colonna `NOT NULL`, `HasMaxLength(200)` genera un `nvarchar(200)` invece di un `nvarchar(max)`. È lo stesso ragionamento dei `not null` e `varchar(50)` che mettevo a mano nella `CREATE TABLE` del gestionale Java — solo che qui lo dichiaro in C# e ci pensa EF a generare il DDL.

### Il check constraint sul punteggio

```csharp
.ToTable(t => t.HasCheckConstraint("CK_Review_Score", "[Score] >= 1 AND [Score] <= 10"));
```

Questo è un **vincolo di controllo** vero, applicato dal database: qualsiasi tentativo di salvare una recensione con punteggio fuori dall'intervallo 1–10 viene **rifiutato** da SQL Server, da qualunque parte arrivi — anche da un `INSERT` scritto a mano che scavalca l'applicazione.

> **Aggiornamento.** All'inizio questo vincolo era l'**unica** difesa, e si vedeva: inviando `score = 50` l'API rispondeva `500`, perché l'eccezione del database arrivava fino al client come errore del server. Ho poi aggiunto `[Range(1, 10)]` sul `ReviewModel` ([capitolo 5](05-bll-models.md)), così la richiesta viene fermata **prima** con un `400` e un messaggio leggibile. Il check constraint resta comunque: è la rete di sicurezza che vale anche per chi non passa dall'API. Due difese sullo stesso vincolo a due livelli diversi — quella applicativa spiega, quella del database garantisce.

> **Perché serviva il pacchetto `Microsoft.EntityFrameworkCore.Relational`?** I metodi `ToTable(...)` e `HasCheckConstraint(...)` sono estensioni "relazionali" (riguardano tabelle e SQL). Il pacchetto base `Microsoft.EntityFrameworkCore` non le contiene, quindi ho aggiunto `Relational` al DAL. Senza, il progetto non compila (`'EntityTypeBuilder<Review>' non contiene una definizione di 'ToTable'`).

---

## (?) EnsureCreated vs Migrations: come nasce il database?

Il database fisico va creato da qualche parte. Ci sono due approcci:

- **Migrations** — Genero degli "script" versionati (`dotnet ef migrations add ...`) che descrivono l'evoluzione dello schema nel tempo. È l'approccio giusto per la produzione.
- **`EnsureCreated()`** — Al primo avvio EF guarda il modello e, se il database non esiste, lo crea di sana pianta con tutte le tabelle e i vincoli. Semplice e perfetto per un esercizio in locale.

In questo progetto uso `EnsureCreated()` nel `Program.cs`. Il rovescio della medaglia: `EnsureCreated` **non** aggiorna uno schema già esistente. Quindi se in futuro modifico un'entità, per rivedere la modifica devo eliminare il database `MovieManagerDb` e riavviare (verrà ricreato), oppure passare alle migrations.

Subito dopo `EnsureCreated()` gira `MovieDbSeeder` (`Data/MovieDbSeeder.cs`), che riempie il catalogo di dati di esempio. Lo schema e i dati nascono così in due passaggi distinti: struttura prima, contenuto poi. Il dettaglio di come lo fa senza mai duplicare niente è nel [capitolo 10](10-database-sql-server.md).

> Per vedere **cosa** ha generato davvero questo `OnModelCreating` — tipi delle colonne, vincoli, chiavi esterne letti dal database vero — c'è il [capitolo 10](10-database-sql-server.md).

[➡ Prossima parte: DAL — Generic Repository e Unit of Work](04-dal-repository-unitofwork.md)
