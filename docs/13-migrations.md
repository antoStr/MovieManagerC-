# 13) Le migration — versionare lo schema del database

[⬅ Torna all'indice](../README.md)

Il [capitolo 3](03-dal-dbcontext.md) chiude con una promessa non mantenuta: *"o passare alle migrations"*. Questo capitolo la mantiene.

Il progetto crea il database con `EnsureCreated()`, e finché lo schema non cambia va benissimo. Il problema arriva il giorno in cui **modifico un'entità**: `EnsureCreated()` non aggiorna uno schema che esiste già, quindi l'unica strada è `DROP DATABASE` e ricominciare — **perdendo tutti i dati**. Le migration servono esattamente a questo: far evolvere lo schema **senza buttare via niente**.

> **Nota sullo stato del progetto.** Il codice qui non cambia: `Program.cs` usa ancora `EnsureCreated()` e la cartella `Migrations/` non esiste. Tutti i comandi e gli output di questo capitolo li ho però eseguiti **davvero**, su una copia di lavoro del progetto e su un database separato (`MovieManagerDb_MigTest`), poi eliminato. Quello che si legge qui è successo; non è la documentazione ufficiale ricopiata. La sezione 13.9 spiega cosa servirebbe per convertire il progetto per davvero.

---

## (?) Che cosa è una migration?

Una **migration** è un pezzo di codice C# **versionato** che descrive **una modifica** allo schema del database, in due direzioni: come applicarla (`Up`) e come annullarla (`Down`).

Il paragone che chiarisce tutto: **le migration stanno al database come Git sta al codice**. Ogni migration è un commit dello schema. C'è una storia ordinata, si può andare avanti, si può tornare indietro, e la storia si versiona insieme al codice sorgente.

La differenza con `EnsureCreated()` in una riga:

| | `EnsureCreated()` | Migrations |
|---|---|---|
| Cosa fa | crea il database **se non esiste** | applica le **modifiche mancanti** |
| Se il database esiste già | **non fa niente** | applica solo le migration nuove |
| Se cambio un'entità | serve `DROP DATABASE` → **dati persi** | `ALTER TABLE` → **dati salvi** |
| Storico | nessuno | tabella `__EFMigrationsHistory` |
| Tornare indietro | impossibile | `database update <nome-precedente>` |
| Adatto a | esercizi, prototipi, test | **tutto il resto**, produzione compresa |

Non si mescolano: un database nato da `EnsureCreated()` **non** può passare alle migration senza un passaggio in più (sezione 13.8).

---

## 13.1 Cosa serve prima di cominciare

Due cose, e nessuna delle due è ovvia in questo progetto.

### Lo strumento `dotnet ef`

Non è incluso nell'SDK: è un **tool globale** da installare una volta sola.

```powershell
dotnet tool install --global dotnet-ef
dotnet ef --version        # -> Entity Framework Core .NET Command-line Tools 10.0.10
```

Se `dotnet ef` non viene trovato dopo l'installazione, manca la cartella dei tool nel `PATH` (`%USERPROFILE%\.dotnet\tools`): basta riaprire il terminale. Per aggiornarlo: `dotnet tool update --global dotnet-ef`.

### Il pacchetto `Design`, nel progetto di avvio

```powershell
dotnet add MovieManager.PL.API package Microsoft.EntityFrameworkCore.Design --version 10.0.9
```

Va nel progetto **di avvio** (`MovieManager.PL.API`), quello che ha `Program.cs` e la stringa di connessione. Serve solo agli strumenti a riga di comando, non a runtime.

### ⚠️ E qui il progetto presenta il conto: serve anche `SqlServer` nel DAL

Questo è **il** punto in cui l'architettura a strati si fa sentire, e non lo dice nessuna guida. Il `DbContext` sta in `MovieManager.DAL`, ma il provider `Microsoft.EntityFrameworkCore.SqlServer` sta in `MovieManager.PL.API` — è la scelta deliberata del [capitolo 9](09-plapi-program-di-scalar.md), che tiene il DAL indipendente dal database.

Le migration rompono questo equilibrio. Generando la prima con il DAL come destinazione, il comando **riesce**, ma poi la solution **non compila più**:

```
error CS0103: Il nome 'SqlServerModelBuilderExtensions' non esiste nel contesto corrente
error CS0103: Il nome 'SqlServerPropertyBuilderExtensions' non esiste nel contesto corrente
```

Il motivo, una volta visto, è ovvio: EF genera dentro il DAL i file `*.Designer.cs` e `MovieDbContextModelSnapshot.cs`, che descrivono il modello **con i dettagli specifici di SQL Server** (`IDENTITY`, i tipi di colonna). Quel codice usa tipi che vivono nel pacchetto del provider — e il DAL non ce l'ha.

Due soluzioni, entrambe legittime:

| | Come | Prezzo |
|---|------|--------|
| **A. Provider anche nel DAL** | `dotnet add MovieManager.DAL package Microsoft.EntityFrameworkCore.SqlServer` | Il DAL non è più agnostico rispetto al database: si perde parte di quello che il capitolo 9 aveva costruito |
| **B. Migration nel PL.API** | `--project MovieManager.PL.API` + `options.UseSqlServer(cs, b => b.MigrationsAssembly("MovieManager.PL.API"))` | Il DAL resta pulito, ma le migration finiscono lontano dal `DbContext` che descrivono |

Ho verificato la **A**: aggiunto il pacchetto al DAL, la solution torna a compilare (`Errori: 0`). È la strada più semplice ed è quella che seguo nel resto del capitolo. Ma vale la pena essere consapevoli del prezzo: **le migration hanno un provider dentro**, e un `DbContext` con migration non è mai davvero portabile tra database diversi. È una delle poche promesse che gli ORM non riescono a mantenere fino in fondo.

---

## 13.2 `migrations add` — creare la prima migration

```powershell
dotnet ef migrations add InitialCreate --project MovieManager.DAL --startup-project MovieManager.PL.API
```

| Parte | Significato |
|-------|-------------|
| `InitialCreate` | il nome che scelgo io; diventa parte del nome del file |
| `--project` | dove **finiscono** le migration (dov'è il `DbContext`) |
| `--startup-project` | dove **si avvia** l'app (dov'è la stringa di connessione) |

I due parametri esistono proprio per progetti come questo, con `DbContext` e `Program.cs` separati. In un progetto singolo si omettono entrambi.

Output reale, la prima volta che l'ho lanciato:

```
Build started...
Build succeeded.
warn: Microsoft.EntityFrameworkCore.Model.Validation[30000]
      No store type was specified for the decimal property 'Budget' on entity type 'Movie'.
      This will cause values to be silently truncated if they do not fit in the default
      precision and scale...
warn: Microsoft.EntityFrameworkCore.Model.Validation[30000]
      No store type was specified for the decimal property 'Revenue' on entity type 'Movie'...
Done. To undo this action, use 'ef migrations remove'
```

### Quei due warning hanno scovato un bug vero

Non li avevo mai visti, perché `EnsureCreated()` fa **esattamente la stessa scelta** ma non avvisa. Dicevano che `Budget` e `Revenue` finivano in `decimal(18,2)` — la precisione predefinita di EF, che non avevo scelto io — e che i valori fuori scala sarebbero stati **troncati in silenzio**.

Ho controllato se il troncamento fosse reale o teorico:

```sql
BEGIN TRANSACTION;
UPDATE Movies SET Budget = 123.456789 WHERE Id = 1;
SELECT Budget FROM Movies WHERE Id = 1;   -- 123.46   <- arrotondato, nessun errore
ROLLBACK;
```

Reale. Scrivendo `123.456789` il database salva `123.46` e **non dice niente**. (Con un valore fuori dalle 18 cifre di precisione, invece, esce l'errore 8115: quello è rumoroso, ed è il caso meno pericoloso dei due.)

La correzione, in `MovieDbContext.OnModelCreating`:

```csharp
// Precisione esplicita sugli importi: senza, EF sceglie decimal(18,2) da sé
// e avvisa che i valori fuori scala verrebbero troncati in silenzio.
// Lo schema non cambia: stessa precisione, ma dichiarata.
modelBuilder.Entity<Movie>()
    .Property(m => m.Budget).HasPrecision(18, 2);
modelBuilder.Entity<Movie>()
    .Property(m => m.Revenue).HasPrecision(18, 2);
```

Rigenerando la migration, i warning spariscono e la colonna esce così:

```csharp
Budget  = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
Revenue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
```

**Il tipo SQL è identico a prima** — `decimal(18,2)` — quindi il database esistente non va toccato: nessuna migration da applicare, nessun `DROP`. È cambiato solo chi ha preso la decisione: prima EF di nascosto, ora il codice, per iscritto.

> È il primo vantaggio concreto delle migration, **ancora prima di adottarle**: rendono visibile quello che l'ORM decide al posto mio. Il bug era lì da sempre; è bastato chiedere a EF di scrivere lo schema in un file perché saltasse fuori.
>
> Attenzione però: `HasPrecision` **non** risolve il problema fino in fondo. Il database continua ad arrotondare `123.456789` a `123.46` — è quello che deve fare una colonna con 2 decimali. Quello che cambia è che ora la scelta è dichiarata e leggibile. C'è anzi una conseguenza più sottile, sul valore che l'API restituisce dopo una POST: è la trappola in fondo al [capitolo 12](12-dal-controller-all-sql.md).

### I tre file generati

```
MovieManager.DAL/Migrations/
├── 20260715183413_InitialCreate.cs             176 righe   <- la migration
├── 20260715183413_InitialCreate.Designer.cs    225 righe   <- il modello a quel momento
└── MovieDbContextModelSnapshot.cs              222 righe   <- il modello ADESSO
```

Il prefisso `20260715183413` è un **timestamp** (`AAAAMMGGHHMMSS`): è quello che dà l'ordine alle migration, non il nome. Due migration create da persone diverse si ordinano da sole.

| File | A cosa serve | Si tocca? |
|------|--------------|-----------|
| `_InitialCreate.cs` | `Up()` e `Down()`: **le modifiche** | Sì, si può modificare a mano |
| `_InitialCreate.Designer.cs` | fotografia del modello **dopo** questa migration | Mai |
| `MovieDbContextModelSnapshot.cs` | fotografia del modello **corrente** | Mai a mano |

Lo **snapshot** è il pezzo che spiega tutto il meccanismo, e merita di essere capito: quando lancio `migrations add`, EF **non guarda il database**. Confronta il mio modello C# di adesso con lo snapshot (com'era l'ultima volta) e genera la **differenza**. Il database non è coinvolto — è la ragione per cui `migrations add` funziona anche con SQL Server spento, e per cui rifiuta il parametro `--connection`:

```
Unrecognized option '--connection'
```

Non è una svista: **generare una migration è un'operazione sul codice, non sul database**.

### Cosa c'è dentro

```csharp
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Actors",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName  = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                Country   = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Biography = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Actors", x => x.Id);
            });
        // ... le altre cinque tabelle ...
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MovieActors");
        migrationBuilder.DropTable(name: "Reviews");
        migrationBuilder.DropTable(name: "Actors");
        migrationBuilder.DropTable(name: "Movies");
        migrationBuilder.DropTable(name: "Directors");
        migrationBuilder.DropTable(name: "Genres");
    }
}
```

**Questa migration è la conferma scritta di tutto il [capitolo 10](10-database-sql-server.md)**, ma stavolta in codice generato invece che letta dal database:

- `.Annotation("SqlServer:Identity", "1, 1")` — la colonna `IDENTITY`, parte da 1 e cresce di 1. È l'origine dell'errore 544.
- `nvarchar(100)` con `maxLength: 100` ← il mio `HasMaxLength(100)`; `nvarchar(max)` dove non l'ho messo.
- `DateOnly` → `date`, `bool` → `bit`, `decimal` → `decimal(18,2)`, `DateTime` → `datetime2`.
- Nel `Down()`, **l'ordine è invertito**: prima le tabelle che dipendono dalle altre. Non si può cancellare `Movies` finché `Reviews` la referenzia.

E ci sono le due cose che il capitolo 10 aveva scoperto interrogando `sys.foreign_keys`:

```csharp
table.ForeignKey(
    name: "FK_Movies_Genres_GenreId",
    column: x => x.GenreId,
    principalTable: "Genres",
    principalColumn: "Id",
    onDelete: ReferentialAction.Cascade);       // <- la cascata, scritta nero su bianco

table.PrimaryKey("PK_MovieActors", x => new { x.MovieId, x.ActorId });   // <- la chiave composta
table.CheckConstraint("CK_Review_Score", "[Score] >= 1 AND [Score] <= 10");
```

`ReferentialAction.Cascade` non l'ho mai scritto io: è la **convenzione** di EF per le relazioni obbligatorie ([capitolo 3](03-dal-dbcontext.md)). Vederlo in un file che posso aprire e modificare è tutta un'altra cosa rispetto a scoprirlo cancellando un genere e ritrovandosi senza film. **Con le migration le convenzioni dell'ORM diventano codice leggibile e discutibile in una code review.**

EF ha anche creato tre indici che non avevo chiesto:

```csharp
migrationBuilder.CreateIndex(name: "IX_Movies_GenreId",       table: "Movies",      column: "GenreId");
migrationBuilder.CreateIndex(name: "IX_Movies_DirectorId",    table: "Movies",      column: "DirectorId");
migrationBuilder.CreateIndex(name: "IX_MovieActors_ActorId",  table: "MovieActors", column: "ActorId");
```

Un indice su ogni chiave esterna: è ciò che rende veloci le JOIN della sezione 10.6.

---

## 13.3 `database update` — applicare le migration

```powershell
dotnet ef database update --project MovieManager.DAL --startup-project MovieManager.PL.API
```

Questo comando, a differenza di `add`, **parla col database**. Output reale:

```
Executed DbCommand (145ms) [Parameters=[], CommandType='Text', CommandTimeout='60']
      CREATE DATABASE [MovieManagerDb_MigTest];
Applying migration '20260715183413_InitialCreate'.
Done.
```

Per applicarle a un database diverso da quello di `appsettings.json` — come ho fatto io per non toccare `MovieManagerDb` — c'è `--connection`:

```powershell
dotnet ef database update --project MovieManager.DAL --startup-project MovieManager.PL.API `
  --connection "Server=(localdb)\MSSQLLocalDB;Database=MovieManagerDb_MigTest;Trusted_Connection=True;TrustServerCertificate=True"
```

### La tabella `__EFMigrationsHistory`

Qui sta il cuore del meccanismo. Dopo l'update:

```sql
SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory;
```
```
MigrationId                   ProductVersion
----------------------------  --------------
20260715183413_InitialCreate  10.0.9
```

EF crea nel database una tabella che **elenca le migration già applicate**. È tutta la magia: a ogni `database update`, EF legge questa tabella, la confronta con i file presenti nel progetto, e applica **solo quelle che mancano**. Nient'altro. Niente di intelligente, niente di magico — un elenco di ricevute.

Il confronto misurato fra i due database dice tutto:

```sql
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
```

| `MovieManagerDb` (`EnsureCreated`) | `MovieManagerDb_MigTest` (migrations) |
|---|---|
| Actors, Directors, Genres, MovieActors, Movies, Reviews | **`__EFMigrationsHistory`**, Actors, Directors, Genres, MovieActors, Movies, Reviews |
| **6 tabelle** | **7 tabelle** |

Le sei tabelle del dominio sono **identiche**. L'unica differenza è la settima: il database con le migration **sa cosa gli è stato fatto**, l'altro no. Da questa tabella in più discende tutto — gli aggiornamenti incrementali, il rollback, e il problema della sezione 13.8.

---

## 13.4 La seconda migration: qui si vede il punto di tutto

Fin qui le migration sembrano un `EnsureCreated()` più complicato. Il senso si vede **al primo cambiamento di schema**. Aggiungo un campo a un'entità:

```csharp
// MovieManager.DAL/Entities/Genre.cs
public class Genre
{
    public int Id                       { get; set; }
    public string Name                  { get; set; } = string.Empty;
    public string? Description          { get; set; }
    public bool IsActive                { get; set; }        // <-- nuovo
    public ICollection<Movie> Movies    { get; set; } = new List<Movie>();
}
```

```powershell
dotnet ef migrations add AddIsActiveToGenre --project MovieManager.DAL --startup-project MovieManager.PL.API
```

La migration generata **non ricrea niente**: contiene solo la differenza.

```csharp
public partial class AddIsActiveToGenre : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            table: "Genres",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsActive", table: "Genres");
    }
}
```

Cinque righe. EF ha confrontato il modello con lo snapshot, ha visto **una** proprietà in più, e ha generato **una** `AddColumn`. Il `defaultValue: false` è necessario: la colonna è `NOT NULL` e le righe che già esistono devono pur avere un valore.

### La prova: i dati sopravvivono

Questa è la dimostrazione che vale il capitolo intero. Prima inserisco un dato:

```sql
INSERT INTO Genres (Name, Description) VALUES ('Western', 'Film di frontiera.');
```
```
Id Name
-- ----
1  Western
```

Poi applico la migration:

```powershell
dotnet ef database update --project MovieManager.DAL --startup-project MovieManager.PL.API --connection "..."
```
```
Applying migration '20260715183757_AddIsActiveToGenre'.
      ALTER TABLE [Genres] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
Done.
```

E controllo:

```sql
SELECT Id, Name, IsActive FROM Genres;
```
```
Id Name    IsActive
-- ------- --------
1  Western 0
```

**`ALTER TABLE`, non `CREATE TABLE`.** La riga "Western" è ancora lì, con la colonna nuova valorizzata a `0`. Con `EnsureCreated()` la stessa modifica avrebbe richiesto:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "ALTER DATABASE MovieManagerDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MovieManagerDb;"
```

cioè **buttare via tutto il database** e ripartire dal seeder. In locale è un fastidio. Su un database vero è impensabile — ed è per questo che nessuno usa `EnsureCreated()` in produzione.

E lo storico ora ha due righe:

```
MigrationId
--------------------------------
20260715183413_InitialCreate
20260715183757_AddIsActiveToGenre
```

---

## 13.5 Tornare indietro: il rollback

Ogni migration ha il suo `Down()`, quindi si può disfare. Si passa il nome della migration **a cui si vuole tornare**:

```powershell
dotnet ef database update InitialCreate --project MovieManager.DAL --startup-project MovieManager.PL.API --connection "..."
```
```
Reverting migration '20260715183757_AddIsActiveToGenre'.
      ALTER TABLE [Genres] DROP COLUMN [IsActive];
Done.
```

Verificato subito dopo:

```sql
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Genres';
-- Id, Name, Description        <- IsActive sparita

SELECT MigrationId FROM __EFMigrationsHistory;
-- 20260715183413_InitialCreate  <- una sola riga: lo storico si aggiorna

SELECT Id, Name FROM Genres;
-- 1  Western                    <- il dato c'e' ancora
```

EF ha eseguito il `Down()`, ha tolto la riga dallo storico, e il resto dei dati non l'ha toccato.

> ⚠️ **Il rollback non è gratis.** Qui la colonna era nuova e vuota, quindi buttarla via non costa niente. Ma il `Down()` di una `DropColumn` su una colonna **piena** cancella quei dati per sempre: il `Down()` sa ricostruire la *struttura*, non il *contenuto*. Il rollback è uno strumento da sviluppo; in produzione, quasi sempre, si va avanti con una migration correttiva invece di tornare indietro.

Per disfare **tutto**, fino al database vuoto: `dotnet ef database update 0`. Lo `0` è il "prima di tutte".

---

## 13.6 `migrations remove` — cancellare una migration sbagliata

Serve quando ho creato una migration, mi accorgo di aver sbagliato, e **non l'ho ancora applicata**: a quel punto non c'è niente da disfare nel database, basta buttare via il file e rifarlo.

È la differenza con il rollback della sezione precedente, e conviene fissarla subito perché i due comandi si confondono:

| | Cosa tocca | Cosa lascia intatto |
|---|---|---|
| **`database update <precedente>`** (rollback) | il **database**: esegue il `Down()` | il **file** della migration, che resta lì |
| **`migrations remove`** | il **file**: lo cancella dal progetto | il **database**, che non viene toccato |

Detto in una riga: il rollback disfa l'**effetto**, `remove` cancella l'**istruzione**.

```powershell
dotnet ef migrations remove --project MovieManager.DAL --startup-project MovieManager.PL.API
```
```
Removing migration '20260715183757_AddIsActiveToGenre'.
Reverting the model snapshot.
Done.
```

Cancella i due file e — importante — **riporta indietro anche lo snapshot**. Modificare a mano una migration già creata quasi sempre è sbagliato: si fa `remove`, si sistema l'entità, si rifà `add`.

Messo tutto insieme, ecco quale dei due usare a seconda di quanto è andata lontano la migration:

| Situazione | Comando |
|---|---|
| Migration creata ma **non** applicata | `migrations remove` |
| Migration **già applicata** al database | `database update <precedente>`, poi `migrations remove` |
| Migration già applicata **e condivisa** con altri (push fatto) | **niente**: nuova migration correttiva |

L'ultima riga è la stessa regola del `git push --force`: finché la storia è mia la riscrivo, quando è di tutti no.

---

## 13.7 `migrations script` — l'SQL per chi non lancia `dotnet ef`

In produzione spesso non si esegue `database update`: si consegna al DBA uno **script SQL** da rivedere e applicare.

```powershell
dotnet ef migrations script --idempotent --project MovieManager.DAL --startup-project MovieManager.PL.API
```

Output reale (l'inizio):

```sql
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715183413_InitialCreate'
)
BEGIN
    CREATE TABLE [Actors] (
        [Id] int NOT NULL IDENTITY,
        [FirstName] nvarchar(100) NOT NULL,
        ...
        CONSTRAINT [PK_Actors] PRIMARY KEY ([Id])
    );
END;
```

Due cose da leggere qui:

- **`--idempotent`** avvolge ogni migration in un `IF NOT EXISTS` sullo storico. Lo script si può eseguire **due volte senza danni**, e va bene per qualsiasi database, in qualunque stato sia. Senza il flag, lo script assume di partire da zero. È lo stesso concetto di idempotenza del `MovieDbSeeder` ([capitolo 10](10-database-sql-server.md)), applicato allo schema invece che ai dati.
- **`BEGIN TRANSACTION`** — ogni migration è una transazione: o passa tutta o non lascia traccia. Di nuovo il tema della sezione 10.10.

E finalmente si vede la definizione di `__EFMigrationsHistory`: due colonne, l'Id della migration e la versione di EF. Nient'altro.

| Variante | Cosa fa |
|----------|---------|
| `migrations script` | tutte, da zero |
| `migrations script --idempotent` | tutte, con i controlli. **La scelta normale** |
| `migrations script Da A` | solo l'intervallo tra due migration |
| `migrations script --output mig.sql` | scrive su file |

---

## 13.8 ⚠️ Il tranello: passare da `EnsureCreated()` alle migration

Questa è la cosa più importante del capitolo, ed è quella su cui ci si schianta.

`MovieManagerDb` esiste già, creato da `EnsureCreated()`: ha le sei tabelle ma **non** ha `__EFMigrationsHistory`. Cosa succede lanciandoci sopra `database update`?

L'ho provato (svuotando lo storico di un database di test, che è esattamente la stessa situazione):

```
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
Microsoft.Data.SqlClient.SqlException (0x80131904):
      There is already an object named 'Actors' in the database.
Error Number: 2714, State: 6, Class: 16
```

**Errore 2714.** Ed è perfettamente logico: senza storico, EF conclude che **nessuna** migration è mai stata applicata, quindi parte da `InitialCreate` e prova a fare `CREATE TABLE [Actors]`. Ma `Actors` c'è già. Un altro numero da aggiungere alla collezione del progetto — 544 (`IDENTITY_INSERT`), 547 (foreign key), 2627 (duplicato), e ora **2714** (oggetto già esistente).

Il malinteso da smontare: EF **non guarda mai lo schema del database** per decidere cosa applicare. Guarda solo `__EFMigrationsHistory`. Un database può avere tutte le tabelle giuste e, per EF, essere comunque "vuoto".

Le tre vie d'uscita:

**A. Ricreare da zero** — l'unica sensata in locale, dove i dati li rifà il seeder:

```powershell
# 1. fermare l'app
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "ALTER DATABASE MovieManagerDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MovieManagerDb;"
# 2. ricreare con le migration
dotnet ef database update --project MovieManager.DAL --startup-project MovieManager.PL.API
# 3. riavviare: il seeder ripopola
dotnet run --project MovieManager.PL.API
```

**B. Fingere che la prima migration sia già stata applicata** — quando i dati vanno tenuti. Si genera `InitialCreate`, non la si applica, e si scrive a mano la ricevuta nello storico:

```sql
CREATE TABLE [__EFMigrationsHistory] (
    [MigrationId] nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
);
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715183413_InitialCreate', N'10.0.9');
```

Da quel momento EF crede che `InitialCreate` sia stata applicata (e nei fatti è vero: le tabelle ci sono) e da lì in poi applica solo le migration nuove. Funziona **solo** se lo schema esistente corrisponde davvero alla migration — cosa vera qui, perché entrambi nascono dallo stesso `OnModelCreating`.

**C. `migrations script`** e applicarlo a mano, per chi vuole controllare ogni riga.

> **La lezione generale:** la scelta tra `EnsureCreated()` e migration va fatta **all'inizio**, perché tornare indietro costa. `EnsureCreated()` è comodo finché il progetto è un esercizio; nel momento in cui il database contiene qualcosa che non si può ricreare, le migration non sono più un'opzione, sono l'unica strada.

---

## 13.9 Cosa servirebbe per convertire questo progetto

Riassunto operativo, se un giorno volessi farlo davvero:

```powershell
# 1. Lo strumento (una volta per macchina)
dotnet tool install --global dotnet-ef

# 2. I pacchetti
dotnet add MovieManager.PL.API package Microsoft.EntityFrameworkCore.Design --version 10.0.9
dotnet add MovieManager.DAL     package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.9   # <- vedi 13.1

# 3. La prima migration
dotnet ef migrations add InitialCreate --project MovieManager.DAL --startup-project MovieManager.PL.API

# 4. Il database va ricreato (vedi 13.8)
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "ALTER DATABASE MovieManagerDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MovieManagerDb;"
```

E in `Program.cs`, una riga:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MovieDbContext>();

    // db.Database.EnsureCreated();          // prima
    await db.Database.MigrateAsync();        // dopo: applica le migration mancanti

    await MovieDbSeeder.SeedAsync(db);
}
```

`MigrateAsync()` fa esattamente quello che fa `dotnet ef database update`, ma all'avvio dell'app. Il seeder resta identico: schema e dati restano due passaggi distinti, com'erano prima.

> ⚠️ **`Migrate()` all'avvio non è una buona idea in produzione**, ed è bene saperlo anche se qui non cambia niente. Se l'app gira su più istanze, tutte proverebbero a migrare insieme all'avvio; e l'account dell'applicazione avrebbe bisogno dei permessi per fare `ALTER TABLE`, che è molto più di quanto serva per servire richieste. In produzione le migration si applicano **prima** del deploy, con `migrations script` o con un passo dedicato della pipeline. Per un esercizio in locale, `MigrateAsync()` all'avvio va benissimo.

---

## Riepilogo dei comandi

Tutti da lanciare dalla cartella della solution. Sono verificati su questo progetto.

| Comando | Cosa fa | Tocca il DB? |
|---------|---------|--------------|
| `dotnet ef migrations add <Nome>` | crea una migration dalle differenze del modello | **No** |
| `dotnet ef migrations list` | elenca le migration e quali sono `(Pending)` | Sì (legge lo storico) |
| `dotnet ef migrations remove` | cancella l'ultima migration non applicata | Sì (solo per controllare) |
| `dotnet ef database update` | applica tutte le migration mancanti | **Sì** |
| `dotnet ef database update <Nome>` | va a quella migration, anche **indietro** | **Sì** |
| `dotnet ef database update 0` | disfa tutto | **Sì** |
| `dotnet ef migrations script --idempotent` | genera l'SQL invece di applicarlo | No |
| `dotnet ef dbcontext info` | dice provider, database e data source in uso | Sì |
| `dotnet ef database drop` | elimina il database | **Sì** |

Con, per questo progetto, sempre in coda:

```
--project MovieManager.DAL --startup-project MovieManager.PL.API
```

Prova di `dbcontext info` sul progetto:

```
Type: MovieManager.DAL.Data.MovieDbContext
Provider name: Microsoft.EntityFrameworkCore.SqlServer
Database name: MovieManagerDb
Data source: (localdb)\MSSQLLocalDB
Options: EngineType=SqlServer
```

Utile più di quanto sembri: dice **su quale database si sta per lavorare** prima di lanciare un comando che lo modifica.

---

## Verifica finale

1. **`migrations add` guarda il database?** No. Confronta modello C# e snapshot.
2. **Cosa decide se una migration verrà applicata?** La tabella `__EFMigrationsHistory`, non lo schema.
3. **Cosa succede lanciando `database update` su un database nato da `EnsureCreated()`?** Errore 2714, "There is already an object named 'Actors'".
4. **I dati sopravvivono a una migration?** Sì: `ALTER TABLE`, non `DROP`. È tutto il punto.
5. **`remove` o rollback?** `remove` se non è applicata, `database update <precedente>` se lo è.
6. **Perché servirebbe `SqlServer` nel DAL?** Perché snapshot e designer contengono tipi del provider. Le migration non sono agnostiche.

[➡ Prossima parte: SQL Server e SSMS — usare il database come al lavoro](14-sql-server-e-ssms.md)
