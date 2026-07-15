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

Ho creato i progetti da riga di comando con `dotnet new`. I due livelli DAL e BLL sono **class library** (librerie, non si avviano da sole), mentre il PL è una **Web API**.

```bash
# Data Access Layer e Business Logic Layer: librerie di classi
dotnet new classlib -o MovieManager.DAL -f net10.0
dotnet new classlib -o MovieManager.BLL -f net10.0

# Presentation Layer: Web API con i controller
dotnet new webapi --use-controllers -o MovieManager.PL.API -f net10.0
```

Il flag `-f net10.0` fissa il **target framework**: tutti i progetti girano su .NET 10.

> Dopo la creazione ho eliminato i file di esempio generati automaticamente (`Class1.cs` nelle librerie e il `WeatherForecast` nella Web API): servono solo come segnaposto.

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
dotnet add MovieManager.PL.API package Scalar.AspNetCore
# (Microsoft.AspNetCore.OpenApi è già incluso dal template webapi di .NET 10)
```

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

> I file `MovieDbSeeder` e `DatabaseExceptionHandler` non fanno parte della traccia originale: li ho aggiunti dopo, il primo per avere dati di esempio a ogni avvio ([capitolo 10](10-database-sql-server.md)), il secondo per non far uscire come `500` gli errori di richiesta ([capitolo 9](09-plapi-program-di-scalar.md)).

---

## Verifica finale

Alla fine di questa fase la solution deve **compilare a vuoto** (nessuna entità ancora, ma la struttura c'è):

```bash
dotnet build MovieManager.slnx
```

Se la build passa, lo scheletro è pronto e posso iniziare a riempire il DAL.

[➡ Prossima parte: DAL — Le entità](02-dal-entita.md)
