# 9) PL — Program.cs, Dependency Injection e Scalar

[⬅ Torna all'indice](../README.md)

Ultimo pezzo: il file che mette insieme tutto e avvia l'applicazione, `MovieManager.PL.API/Program.cs`. È qui che dico all'app **come costruire** ogni componente (Dependency Injection) e **come rispondere** alle richieste (pipeline HTTP + Scalar).

---

## (?) Che cosa è la Dependency Injection (DI)?

Nel gestionale Java, quando un metodo aveva bisogno del DAO, se lo creava da solo: `DipendenteDaoImpl dao = new DipendenteDaoImpl();`. Funziona, ma lega ogni classe alle sue dipendenze concrete e rende tutto difficile da cambiare e testare.

Con la **Dependency Injection** si ribalta il meccanismo: una classe **non crea** le sue dipendenze, se le fa **passare** dall'esterno (di solito nel costruttore). Un componente centrale, il **container DI**, sa come costruire ogni cosa e la fornisce a chi la chiede.

Nel progetto lo vediamo ovunque: il controller chiede `IGenericService<MovieModel>`, il service chiede `IUnitOfWork` e `IMapper`, la unit of work chiede `MovieDbContext`... e nessuno di loro fa mai `new`. Tutto parte dalle **registrazioni** in `Program.cs`.

---

## 9.1 Program.cs, dall'alto verso il basso

```csharp
using Microsoft.EntityFrameworkCore;
using MovieManager.BLL.Models;
using MovieManager.BLL.Services;
using MovieManager.BLL.Services.Interfaces;
using MovieManager.DAL.Data;
using MovieManager.DAL.Entities;
using MovieManager.DAL.Repositories;
using MovieManager.DAL.Repositories.Interfaces;
using MovieManager.PL.API.Configurations;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Controller + OpenAPI nativo .NET 10 ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// --- Violazioni dei vincoli di database -> 400/409 invece di 500 ---
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();

// --- DbContext su SQL Server / LocalDB ---
builder.Services.AddDbContext<MovieDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Repository generico + Unit of Work ---
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// --- Generic Service: una registrazione CHIUSA per ogni entità a chiave singola ---
builder.Services.AddScoped<IGenericService<ActorModel>, GenericService<Actor, ActorModel>>();
builder.Services.AddScoped<IGenericService<DirectorModel>, GenericService<Director, DirectorModel>>();
builder.Services.AddScoped<IGenericService<GenreModel>, GenericService<Genre, GenreModel>>();
builder.Services.AddScoped<IGenericService<MovieModel>, GenericService<Movie, MovieModel>>();
builder.Services.AddScoped<IGenericService<ReviewModel>, GenericService<Review, ReviewModel>>();

// --- Repository + Service dedicati alla chiave composta (MovieActor) ---
builder.Services.AddScoped<IMovieActorRepository, MovieActorRepository>();
builder.Services.AddScoped<IMovieActorService, MovieActorService>();

// --- AutoMapper: profili nell'assembly della API ---
builder.Services.AddAutoMapper(typeof(Program).Assembly);

var app = builder.Build();

// Crea il database/schema se non esiste ancora (LocalDB) e lo popola di dati di esempio.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MovieDbContext>();
    db.Database.EnsureCreated();
    await MovieDbSeeder.SeedAsync(db);
}

// Pipeline HTTP
app.UseExceptionHandler();          // vincoli DB violati -> 400/409

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## 9.2 Le registrazioni, spiegate

### Il DbContext e la stringa di connessione

```csharp
builder.Services.AddDbContext<MovieDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

È **qui** — e non nel DAL — che decido il provider (`UseSqlServer`) e leggo la stringa di connessione da `appsettings.json`. Così il DAL resta agnostico rispetto al database, e per passare a un altro motore basterebbe cambiare questa riga.

### Registrazione "aperta" vs "chiusa"

Due stili diversi, entrambi presenti:

- **Aperta (open generic):** `AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>))`. Registro il tipo generico *non specializzato*: vale per **qualunque** `T`. Quando qualcuno chiede `IGenericRepository<Movie>`, il container costruisce `GenericRepository<Movie>` al volo.
- **Chiusa (closed):** `AddScoped<IGenericService<MovieModel>, GenericService<Movie, MovieModel>>()`. Qui devo elencarle a mano, una per entità. Perché? Perché `IGenericService` ha **un** parametro generico (`TModel`), ma l'implementazione ne ha **due** (`TEntity, TModel`): il container non può indovinare da solo l'abbinamento `MovieModel → (Movie, MovieModel)`. Glielo dico io, esplicitamente, per tutte e cinque le entità a chiave singola.

### Il caso a chiave composta

```csharp
builder.Services.AddScoped<IMovieActorRepository, MovieActorRepository>();
builder.Services.AddScoped<IMovieActorService, MovieActorService>();
```

Repository e service dedicati registrati normalmente, accanto ai generici.

---

## (?) Scoped, Transient o Singleton? La questione del "ciclo di vita"

Registrando un servizio devo dire **quanto vive** ogni istanza. Ci sono tre scelte:

| Lifetime | Quante istanze | Adatto a |
|----------|----------------|----------|
| **Scoped** | una per **richiesta HTTP** | servizi che usano il `DbContext` |
| **Transient** | una **nuova ogni volta** | servizietti leggeri e stateless |
| **Singleton** | **una** per tutta l'app | configurazioni, cache globali |

Nel progetto uso **Scoped** dappertutto. Il motivo è il `DbContext`: **non** è thread-safe e va usato per una singola richiesta, poi buttato. Con `Scoped`, controller → service → unit of work → repository condividono **lo stesso** `DbContext` all'interno della stessa richiesta (indispensabile perché la Unit of Work funzioni), e a fine richiesta viene rilasciato. Un `Singleton` che dipende dal `DbContext` sarebbe un bug grave (lo terrebbe in vita per sempre, condiviso tra richieste diverse).

### `AddAutoMapper`

```csharp
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

Dice ad AutoMapper di scansionare l'assembly della API alla ricerca dei `Profile`: trova `MappingProfile` (capitolo 7) e registra tutte le sue mappe, rendendo `IMapper` iniettabile ovunque.

---

## 9.3 EnsureCreated e seeder: database e dati nascono all'avvio

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MovieDbContext>();
    db.Database.EnsureCreated();        // lo schema
    await MovieDbSeeder.SeedAsync(db);  // i dati
}
```

Al primo avvio, se `MovieManagerDb` non esiste in LocalDB, viene creato con tutte le tabelle, le relazioni e il check constraint sul punteggio. Uso uno *scope* esplicito perché il `DbContext` è Scoped e qui, fuori da una richiesta HTTP, uno scope non esiste ancora: me lo creo a mano. (Limiti e alternative di `EnsureCreated` nel [capitolo 3](03-dal-dbcontext.md).)

Subito dopo, `MovieDbSeeder` riempie il catalogo (5 generi, 5 registi, 10 attori, 6 film, 11 righe di cast, 7 recensioni), così l'app non parte mai su un database vuoto e ho subito dati veri su cui provare le query. Il seeder è **idempotente**: gira a ogni avvio senza duplicare niente, perché confronta la chiave naturale (nome, titolo) e non l'Id. Come e perché è spiegato nel [capitolo 10](10-database-sql-server.md).

L'`await` qui funziona perché `Program.cs` usa i *top-level statements*: il compilatore ci costruisce intorno un `Main` asincrono.

---

## 9.4 Il gestore delle eccezioni di database

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();
// ...
app.UseExceptionHandler();
```

Alcuni errori li può scoprire **solo** il database. Che `genreId = 999` non esista non lo sa nessun attributo di validazione: lo scopre SQL Server quando prova a scrivere e la chiave esterna non trova la riga. Senza un gestore, quell'eccezione risaliva tutta la pipeline e usciva come **`500 Internal Server Error`**: un errore *del client* travestito da bug *del server*.

`DatabaseExceptionHandler` (`Configurations/DatabaseExceptionHandler.cs`) implementa `IExceptionHandler` e guarda il **numero di errore** di SQL Server:

| Numero SQL | Significato | Risposta |
|-----------|-------------|----------|
| **547** | violazione di foreign key o di check constraint | `400 Bad Request` |
| **2627 / 2601** | chiave primaria o indice unico duplicati | `409 Conflict` |
| altro | non lo riconosco | lo lascio passare → resta un `500` vero |

L'ultima riga è la più importante: il gestore traduce **solo** ciò che riconosce. Un `NullReferenceException` deve continuare a essere un `500`, perché quello *è* un bug mio. Un gestore che ingoia tutto e risponde sempre `400` nasconderebbe i problemi veri.

### Dove va `UseExceptionHandler()` nella pipeline

Va **prima** degli endpoint. I middleware si annidano: l'eccezione lanciata in fondo (nel controller) risale verso l'esterno e viene catturata dal primo gestore che incontra. In sviluppo ASP.NET aggiunge da solo la *developer exception page* in cima alla pipeline — è quella che produceva le pagine di errore lunghissime che vedevo all'inizio. Mettendo `UseExceptionHandler()` più internamente, il mio gestore intercetta per primo le violazioni di vincolo e risponde `400`/`409`; la pagina di sviluppo resta a fare il suo lavoro per tutto il resto.

---

## (?) Che cosa sono OpenAPI e Scalar?

- **OpenAPI** è uno standard per **descrivere** un'API REST in un documento (JSON): quali endpoint esistono, che parametri accettano, cosa restituiscono. In .NET 10 questo documento è generato in modo **nativo** con `AddOpenApi()` + `MapOpenApi()`, senza librerie esterne come Swashbuckle.
- **Scalar** è una **UI interattiva** che legge quel documento OpenAPI e lo trasforma in una pagina dove posso vedere tutti gli endpoint e **provarli** direttamente dal browser ("Try it out"). È il sostituto moderno di Swagger UI.

Punto importante: **Scalar non legge i controller**, legge il documento OpenAPI. Quindi tutto ciò che finisce nell'OpenAPI (rotte, verbi, i `ProducesResponseType` del capitolo 8) appare automaticamente in Scalar, senza bisogno di attributi specifici per Scalar.

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // espone /openapi/v1.json
    app.MapScalarApiReference();    // espone la UI su /scalar
}
```

Espongo entrambi **solo in sviluppo**: in produzione non voglio la documentazione interattiva pubblica.

### Avvio diretto su Scalar

In `Properties/launchSettings.json`, nel profilo `https`, ho impostato l'apertura automatica del browser sulla pagina Scalar:

```json
"https": {
  "commandName": "Project",
  "launchBrowser": true,
  "launchUrl": "scalar",
  "applicationUrl": "https://localhost:7109;http://localhost:5140",
  "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
}
```

> ⚠️ **Quale porta esce davvero?** I profili sono due: `http` (porta **5140**) e `https` (**7109** + 5140). `dotnet run` senza argomenti usa il **primo profilo del file**, cioè `http`: apre `http://localhost:5140/scalar`, non la 7109. Per il profilo https serve dirlo esplicitamente:
>
> ```powershell
> dotnet run --launch-profile https     # https://localhost:7109/scalar
> ```
>
> Da qui viene anche l'avviso `Failed to determine the https port for redirect` che compare nel log con il profilo `http`: `UseHttpsRedirection()` vorrebbe rimandare su HTTPS ma non c'è nessuna porta HTTPS in ascolto. In locale è innocuo.

---

## 9.5 Prova sul campo (verifica end-to-end)

Ho avviato l'app e testato il flusso completo. Il percorso felice:

| Chiamata | Esito |
|----------|-------|
| `GET /openapi/v1.json` | `200` (documento OpenAPI, 13 percorsi / 30 operazioni) |
| `GET /scalar` | `302` → apre la UI Scalar |
| `GET /api/genres` | `200` con i generi del seeder |
| `POST /api/genres` | `201` con l'`id` generato |
| `POST /api/directors` | `201` |
| `POST /api/movies` (con FK a genere e regista) | `201` — integrità referenziale ok |
| `GET /api/movies/1` | `200` (rilettura) |
| `GET /api/movies/999` | `404` |
| `POST /api/movieactors` (chiave composta) | `201` |
| `GET /api/movieactors/movie/1` | `200` con il cast |
| `DELETE /api/movieactors/1/2` | `204`, poi `GET` → `404` |
| `POST /api/reviews` con `score = 8` | `201` |

E, più interessante, i percorsi di errore — quelli che dicono se le difese funzionano davvero:

| Chiamata sbagliata | Esito | Chi l'ha fermata |
|--------------------|-------|------------------|
| `POST /api/actors` con body `{}` | `400` + "Il nome è obbligatorio." | `[Required]` sul model |
| `POST /api/reviews` con `score = 99` | `400` + "Il punteggio deve essere compreso tra 1 e 10." | `[Range(1, 10)]` sul model |
| `POST /api/genres` con nome di 101 caratteri | `400` | `[StringLength(100)]` |
| `POST /api/movies` con `genreId = 999` | `400` "Vincolo di database non rispettato" | `DatabaseExceptionHandler` (SQL 547) |
| `POST /api/movieactors` con coppia già esistente | `409` "Risorsa già esistente" | `DatabaseExceptionHandler` (SQL 2627) |
| `PUT /api/actors/1` con `id = 2` nel body | `400` | il controllo `id != model.Id` nel controller |
| `DELETE /api/actors/999` | `404` | il `bool` di ritorno del service |

Le ultime due tabelle raccontano l'architettura meglio di qualsiasi diagramma: ogni errore viene fermato al livello **che ha l'informazione per farlo**. Il model sa che il nome è obbligatorio; il controller sa che gli id devono coincidere; il service sa che la riga non esiste; solo il database sa che il genere 999 non c'è. Nessuno di questi controlli poteva stare da un'altra parte.

Il flusso completo Controller → Service → AutoMapper → Repository → Unit of Work → EF Core → LocalDB funziona, relazioni, chiave composta e vincoli inclusi.

![UI Scalar del progetto](../res/scalar.png)

---

## Checklist finale del progetto

1. ✅ `MovieManager.BLL` referenzia `MovieManager.DAL`; `MovieManager.PL.API` referenzia entrambi.
2. ✅ Pacchetti a posto: EF Core (+Relational) nel DAL, AutoMapper nel BLL, SqlServer/AutoMapper/OpenAPI/Scalar nel PL.
3. ✅ Entità, `MovieDbContext`, repository generico + dedicato, Unit of Work.
4. ✅ Model + `IModelWithId`, `GenericService` + `MovieActorService`.
5. ✅ `MappingProfile` con le sei mappe `ReverseMap`.
6. ✅ Registrazioni DI con scope **Scoped**; AutoMapper registrato sull'assembly della API.
7. ✅ OpenAPI + Scalar attivi in sviluppo; avvio diretto su `/scalar`.
8. ✅ `EnsureCreated` + `MovieDbSeeder`: schema e dati di esempio all'avvio.
9. ✅ Validazione sui model e violazioni di vincolo tradotte in `400`/`409`.
10. ✅ La solution compila e l'applicazione gira e risponde correttamente.

[➡ Prossima parte: Il database — SQL Server, schema e SQL](10-database-sql-server.md)
