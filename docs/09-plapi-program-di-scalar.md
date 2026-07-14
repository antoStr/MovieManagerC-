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
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Controller + OpenAPI nativo .NET 10 ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

// In sviluppo crea il database/schema se non esiste ancora (LocalDB).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MovieDbContext>();
    db.Database.EnsureCreated();
}

// Pipeline HTTP
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

## 9.3 EnsureCreated: il database nasce all'avvio

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MovieDbContext>();
    db.Database.EnsureCreated();
}
```

Al primo avvio, se `MovieManagerDb` non esiste in LocalDB, viene creato con tutte le tabelle, le relazioni e il check constraint sul punteggio. Uso uno *scope* esplicito perché il `DbContext` è Scoped e qui, fuori da una richiesta HTTP, uno scope non esiste ancora: me lo creo a mano. (Limiti e alternative di `EnsureCreated` nel [capitolo 3](03-dal-dbcontext.md).)

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

Così `dotnet run` apre subito `https://localhost:7109/scalar`.

---

## 9.4 Prova sul campo (verifica end-to-end)

Ho avviato l'app e testato il flusso completo. Tutto risponde come previsto:

| Chiamata | Esito |
|----------|-------|
| `GET /openapi/v1.json` | `200` (documento OpenAPI generato) |
| `GET /scalar` | `302` → apre la UI Scalar |
| `GET /api/genres` (vuoto) | `200 []` |
| `POST /api/genres` | `201` con l'`id` generato |
| `POST /api/directors` | `201` |
| `POST /api/movies` (con FK a genere e regista) | `201` — integrità referenziale ok |
| `GET /api/movies/1` | `200` (rilettura) |
| `GET /api/movies/999` | `404` |
| `POST /api/movieactors` (chiave composta) | `201` |
| `GET /api/movieactors/movie/1` | `200` con l'associazione |
| `DELETE /api/movieactors/1/1` | `204`, poi `GET` → `404` |
| `POST /api/reviews` con `score = 8` | `201` |
| `POST /api/reviews` con `score = 50` | `500` — il check constraint 1–10 **rifiuta** l'inserimento |

Questo conferma che tutti i livelli collaborano correttamente: Controller → Service → AutoMapper → Repository → Unit of Work → EF Core → LocalDB, comprese le relazioni, la chiave composta e i vincoli di database.

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
8. ✅ La solution compila e l'applicazione gira e risponde correttamente.

[⬅ Torna all'indice](../README.md)
