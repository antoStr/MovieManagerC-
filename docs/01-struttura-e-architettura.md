# 1) Struttura della solution e architettura a strati

[⬅ Torna all'indice](../README.md)

In questa prima parte metto in piedi lo "scheletro" del progetto: una solution con tre progetti separati. Prima di scrivere una sola entità, è importante capire **perché** si divide un progetto in livelli invece di buttare tutto in un unico progetto.

---

## (?) Che cosa è un'architettura a strati (layered architecture)?

È un modo di organizzare il codice separando le responsabilità in "livelli" sovrapposti, dove ogni livello parla **solo** con quello immediatamente sotto di lui. Nel nostro caso:

- **DAL** (Data Access Layer) → sa parlare col database. Non sa niente di HTTP.
- **BLL** (Business Logic Layer) → contiene le regole applicative. Non sa niente di database (usa il DAL) né di HTTP.
- **PL** (Presentation Layer) → espone le API HTTP. Non sa niente di come i dati sono salvati (usa il BLL).

**Perché conviene?**

1. Se un domani cambio database (da SQL Server a PostgreSQL) tocco solo il DAL.
2. Se cambio il modo di esporre i dati (da API REST a un'app desktop) tocco solo il PL.
3. Il codice è più facile da leggere e testare, perché ogni pezzo fa una cosa sola.

È lo stesso ragionamento dei package `model` / `dao` / `controller` del vecchio gestionale Java, solo più formalizzato: qui ogni livello è un **progetto** a sé.

---

## (?) Che cosa è una solution?

Una **solution** (`.sln` / `.slnx`) è un contenitore che raggruppa più progetti che lavorano insieme. È l'equivalente concettuale del progetto Maven che raggruppava tutto: qui però ogni "modulo" è un progetto `.csproj` indipendente, e la solution li tiene uniti.

Ho usato il formato nuovo **`.slnx`** (XML, molto più leggibile del vecchio `.sln`):

```xml
<Solution>
  <Project Path="MovieManager.DAL/MovieManager.DAL.csproj" />
  <Project Path="MovieManager.BLL/MovieManager.BLL.csproj" />
  <Project Path="MovieManager.PL.API/MovieManager.PL.API.csproj" />
</Solution>
```

---

## 1.1 Creazione dei tre progetti

I due livelli DAL e BLL sono **class library** (librerie, non si avviano da sole). Il PL invece è un progetto web vero e proprio, e su come crearlo c'è una storia che vale la pena raccontare per intero, perché spiega metà della struttura delle cartelle.

### DAL e BLL: due librerie

```bash
dotnet new classlib -o MovieManager.DAL -f net10.0
dotnet new classlib -o MovieManager.BLL -f net10.0
```

Il flag `-f net10.0` fissa il **target framework**: tutti i progetti girano su .NET 10. Dopo la creazione ho eliminato il `Class1.cs` che entrambe generano: è solo un segnaposto.

### PL: il template indicato dal docente

Il Presentation Layer l'ho creato **da Visual Studio**, con il template che ci è stato indicato:

> **App Web ASP.NET Core (Model-View-Controller)**

Da riga di comando è lo stesso identico template, e si chiama `mvc`:

```bash
dotnet new mvc -o MovieManager.PL.API -f net10.0
```

Che sia proprio quello lo dice `dotnet new list`, che stampa il nome per esteso accanto al nome breve:

```
Nome modello                                  Nome breve   Lingua   Tag
--------------------------------------------  -----------  -------  --------
App Web ASP.NET Core (Model-View-Controller)  mvc          [C#],F#  Web/MVC
```

### Cosa genera quel template, e cosa ne ho tenuto

Qui arriva il punto. Il template MVC nasce **attrezzato per produrre pagine HTML**, e infatti tira dentro un sacco di roba che a questo progetto non serve:

| Cosa genera | Cosa me ne faccio |
|-------------|-------------------|
| `Controllers/HomeController.cs` | **eliminato** — il mio primo controller sarà `MoviesController` |
| `Views/` — 7 file `.cshtml` (`Home/Index`, `Home/Privacy`, `Shared/_Layout`…) | **eliminata** — non genero HTML |
| `Models/ErrorViewModel.cs` | **eliminato** — serviva alla pagina d'errore Razor |
| `wwwroot/` — **~70 file**: Bootstrap, jQuery, `site.css`, favicon | **eliminata** — non servo file statici |
| `Program.cs` | **riscritto** (vedi sotto e [capitolo 9](09-plapi-program-di-scalar.md)) |
| `appsettings.json`, `launchSettings.json`, il `.csproj` | tenuti e adattati |

Alla fine del giro, del template MVC è rimasta **la struttura** (un progetto `Microsoft.NET.Sdk.Web` con i controller e il routing) e nient'altro. Il perché è tutto nella prossima sezione.

### Cosa cambia in `Program.cs`

Il template MVC parte così:

```csharp
builder.Services.AddControllersWithViews();   // controller + motore Razor
...
app.UseRouting();
app.MapStaticAssets();                        // serve i file di wwwroot
app.MapControllerRoute(                       // rotte "a segmenti": /Movies/Edit/3
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

Il progetto invece usa:

```csharp
builder.Services.AddControllers();            // solo controller, niente Razor
...
app.MapControllers();                         // rotte dagli attributi: [Route("api/[controller]")]
```

Sparisce `MapStaticAssets()` (non c'è più `wwwroot`), sparisce `UseRouting()` (con `MapControllers()` è implicito), e le due righe che restano cambiano entrambe. **Sono queste due a decidere se un'app risponde HTML o JSON** — il dettaglio sta nella sezione qui sotto e nel [capitolo 9](09-plapi-program-di-scalar.md).

> ⚠️ **Un pacchetto che il template MVC non ti dà.** `Microsoft.AspNetCore.OpenApi` è incluso dal template *Web API*, ma **non** da quello MVC — è normale, un'app che produce HTML non ha nessun documento OpenAPI da generare. Partendo da MVC va quindi aggiunto a mano (sezione 1.3), altrimenti `builder.Services.AddOpenApi()` non compila nemmeno.

---

## (?) MVC o Web API? Perché il progetto ha un template e ne usa un altro

Domanda legittima: se il progetto è un'API, perché partire da un template MVC?

**Perché il template è solo il punto di partenza, e MVC è il sovrainsieme.** `AddControllersWithViews()` fa tutto quello che fa `AddControllers()`, più le Razor view. Quindi da un progetto MVC si può benissimo costruire un'API pura: basta non usare le View e togliere quello che non serve. Il contrario invece non vale — da un progetto Web API non si ottengono le View senza aggiungere pezzi.

La differenza vera tra i due mondi è **chi disegna l'interfaccia**:

| | **MVC** (il template) | **Web API** (quello che ho costruito) |
|---|---|---|
| Il controller restituisce | `View(viewModel)` → **HTML** | `Ok(model)` → **JSON** |
| Chi costruisce la pagina | **il server**, con Razor | **il client** (browser, app mobile, Scalar) |
| Le rotte | a segmenti: `/Movies/Edit/3` | dagli attributi: `/api/movies/3` |
| L'interfaccia utente | i file `.cshtml` | non esiste: c'è Scalar ([capitolo 11](11-scalar-e-prova-api.md)) |
| Serve `wwwroot`? | sì (CSS, JS, immagini) | no |

L'esercizio chiede **API REST** e le guide parlano solo di Scalar, OpenAPI e JSON: nessuna pagina da disegnare. Quindi la parte "View" del Model-View-Controller resta inutilizzata, e tenersi in giro 70 file di Bootstrap e jQuery che nessuno serve sarebbe solo rumore. Da qui la potatura.

> **La "V" di MVC è il posto dove stava la JSP.** Nel gestionale dipendenti in Java il servlet preparava i dati e la **JSP** li trasformava in HTML. Una View Razor (`.cshtml`) fa esattamente quel mestiere, con un'altra sintassi. Qui quel passaggio non c'è: il controller restituisce l'oggetto e ci pensa ASP.NET a serializzarlo in JSON. **L'HTML lo farà, semmai, chi consuma l'API.**
>
> E il `ViewModel`? È l'oggetto sagomato su **una schermata**, che in un progetto MVC vive nel PL accanto alle View. Il template ne genera uno solo, `ErrorViewModel`, per la pagina d'errore — e l'ho eliminato con lei. Perché in questo progetto non serve, e che differenza c'è tra Entity, Model, DTO e ViewModel, è spiegato per bene nel [capitolo 5](05-bll-models.md).

---

## 1.2 I riferimenti tra progetti (la direzione delle dipendenze)

Questa è la parte concettualmente più importante. Le dipendenze devono andare **in una sola direzione**: PL → BLL → DAL. Il DAL non deve mai "vedere" il BLL o il PL.

```bash
# Il BLL usa le entità e i repository del DAL
dotnet add MovieManager.BLL reference MovieManager.DAL

# Il PL usa i servizi/model del BLL (e il DbContext del DAL per registrarlo in DI)
dotnet add MovieManager.PL.API reference MovieManager.DAL MovieManager.BLL
```

Se per sbaglio provassi ad aggiungere un riferimento nella direzione opposta (per esempio DAL → BLL), otterrei un errore di **dipendenza circolare**: è il compilatore stesso che mi impedisce di rompere l'architettura.

---

## 1.3 I pacchetti NuGet, progetto per progetto

I **pacchetti NuGet** sono le librerie esterne (l'equivalente delle dipendenze nel `pom.xml` di Maven). Li ho installati con `dotnet add package`, mettendo ogni pacchetto **solo** nel progetto che ne ha davvero bisogno:

```bash
# DAL: Entity Framework Core (ORM) + Relational (per i vincoli tabellari)
dotnet add MovieManager.DAL package Microsoft.EntityFrameworkCore --version 10.0.9
dotnet add MovieManager.DAL package Microsoft.EntityFrameworkCore.Relational --version 10.0.9

# BLL: AutoMapper per convertire entità <-> model
dotnet add MovieManager.BLL package AutoMapper --version 14.0.0

# PL: provider SQL Server, AutoMapper, OpenAPI e Scalar
dotnet add MovieManager.PL.API package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.9
dotnet add MovieManager.PL.API package AutoMapper --version 14.0.0
dotnet add MovieManager.PL.API package Microsoft.AspNetCore.OpenApi --version 10.0.9
dotnet add MovieManager.PL.API package Scalar.AspNetCore
```

> ⚠️ **`Microsoft.AspNetCore.OpenApi` va aggiunto a mano**, ed è una conseguenza diretta del template scelto (sezione 1.1). Il template *Web API* se lo porta dietro da solo; quello **MVC no**, perché un'app che genera HTML non ha nessun documento OpenAPI da produrre. Confrontando i due `.csproj` appena creati la differenza è tutta lì:
>
> ```xml
> <!-- .csproj generato da: dotnet new webapi -->
> <ItemGroup>
>   <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.9" />
> </ItemGroup>
>
> <!-- .csproj generato da: dotnet new mvc  ->  nessun ItemGroup -->
> ```
>
> Senza quel pacchetto, `builder.Services.AddOpenApi()` del [capitolo 9](09-plapi-program-di-scalar.md) non compila, e senza OpenAPI non c'è Scalar ([capitolo 11](11-scalar-e-prova-api.md)).

**Perché `Relational` nel DAL?** La guida chiede di configurare nel `DbContext` un vincolo sul punteggio delle recensioni (da 1 a 10). Per esprimere un vero **check constraint** a livello di tabella serve l'estensione relazionale di EF Core, che non è inclusa nel pacchetto base `Microsoft.EntityFrameworkCore`. Ne parlo in dettaglio nel [capitolo 3](03-dal-dbcontext.md).

---

## 1.4 Struttura finale delle cartelle

Alla fine di tutto il progetto avrà questa forma (le cartelle `bin/` e `obj/` sono generate dalla build e non vanno versionate):

```
MovieManager/
├── MovieManager.slnx
├── README.md
├── COMANDI.txt                   <-- comandi pronti (Scalar, PowerShell, sqlcmd)
├── docs/                         <-- questa documentazione
├── MovieManager.DAL/
│   ├── Entities/                 (Genre, Director, Actor, Movie, MovieActor, Review)
│   ├── Data/                     (MovieDbContext, MovieDbSeeder)
│   └── Repositories/             (GenericRepository, UnitOfWork, MovieActorRepository + Interfaces)
├── MovieManager.BLL/
│   ├── Models/                   (*Model + IModelWithId)
│   └── Services/                 (GenericService, MovieActorService + Interfaces)
└── MovieManager.PL.API/
    ├── Controllers/              (Movies, Genres, Directors, Actors, Reviews, MovieActors)
    ├── Configurations/           (MappingProfile, DatabaseExceptionHandler)
    ├── Program.cs
    └── appsettings.json
```

Vale la pena notare **cosa non c'è**, visto da dove siamo partiti (sezione 1.1):

```
MovieManager.PL.API/
    ├── Views/            <-- c'era, eliminata: non genero HTML
    ├── wwwroot/          <-- c'era, eliminata: ~70 file tra Bootstrap e jQuery
    └── Models/           <-- c'era il solo ErrorViewModel, eliminato
```

Le tre cartelle che il template MVC porta in dote e che qui non hanno senso. Il PL contiene **solo** i controller e la configurazione: tutto ciò che era Razor è sparito.

> I file `MovieDbSeeder` e `DatabaseExceptionHandler` non fanno parte della traccia originale: li ho aggiunti dopo, il primo per avere dati di esempio a ogni avvio ([capitolo 10](10-database-sql-server.md)), il secondo per non far uscire come `500` gli errori di richiesta ([capitolo 9](09-plapi-program-di-scalar.md)).
>
> Nota sui nomi: la cartella `Models` del PL è stata eliminata, ma i **model esistono** — stanno in `MovieManager.BLL/Models`, che è un'altra cosa. Non è un dettaglio da poco, ed è il tema del [capitolo 5](05-bll-models.md).

---

## Verifica finale

Alla fine di questa fase la solution deve **compilare a vuoto** (nessuna entità ancora, ma la struttura c'è):

```bash
dotnet build MovieManager.slnx
```

Se la build passa, lo scheletro è pronto e posso iniziare a riempire il DAL.

[➡ Prossima parte: DAL — Le entità](02-dal-entita.md)
