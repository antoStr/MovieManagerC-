# 10) Il database — SQL Server, schema e SQL dalle basi alle join

[⬅ Torna all'indice](../README.md)

Nei capitoli precedenti il database è sempre rimasto sullo sfondo: EF Core lo crea, i repository ci parlano, io scrivo C#. Questo capitolo lo mette in primo piano. Serve a due cose: **capire cosa EF ha davvero costruito** dentro SQL Server, e **saper interrogare quel database a mano**, senza passare dall'applicazione.

È un capitolo trasversale: non aggiunge codice al progetto, spiega il pezzo che sta sotto a tutto il resto.

---

## (?) Che cosa è un DBMS? Che cosa è SQL Server?

Un **DBMS** (Database Management System) è il programma che custodisce i dati e li gestisce per conto mio: li salva su disco, li indicizza, applica i vincoli, gestisce più utenti insieme e garantisce che una transazione o vada a buon fine tutta o non lasci traccia. Io non tocco mai i file: parlo col DBMS in **SQL**.

**SQL Server** è il DBMS relazionale di Microsoft. *Relazionale* significa che i dati stanno in **tabelle** (righe e colonne) legate tra loro da **chiavi**: è esattamente il modello che ho disegnato con le entità del [capitolo 2](02-dal-entita.md).

Il suo dialetto SQL si chiama **T-SQL** (Transact-SQL): è SQL standard più estensioni Microsoft. Quasi tutto quello che scrivo qui è SQL standard e funzionerebbe anche altrove; dove uso qualcosa di specifico di T-SQL (`TOP`, `GETDATE()`, `IDENTITY`) lo segnalo.

## (?) Che cosa è un'istanza? E un servizio?

Prima delle edizioni serve questo, perché è il concetto che regge tutta la sezione.

Un'**istanza** di SQL Server è **una copia in esecuzione del motore**, con i suoi database, i suoi utenti e la sua configurazione. Su una macchina possono convivere più istanze indipendenti (una per SQL Server 2019, una per il 2022…), e ognuna si raggiunge con un nome diverso. Per questo la stringa di connessione dice sempre *quale* istanza, non solo *quale* macchina.

Nelle edizioni "vere", un'istanza è un **servizio di Windows**: un processo che parte all'avvio del computer e resta acceso, in attesa di connessioni, anche quando nessuno lo sta usando. Si vede nel Gestione servizi con un nome come `MSSQL$SQLEXPRESS`, e si controlla come qualsiasi servizio:

```powershell
Get-Service | Where-Object Name -like "MSSQL*"     # c'è? sta girando?
Start-Service "MSSQL`$SQLEXPRESS"                  # avvia (backtick: $ va protetto in PowerShell)
Stop-Service  "MSSQL`$SQLEXPRESS"                  # ferma
```

**LocalDB rompe proprio questa regola**, ed è tutta la sua ragione d'essere.

## (?) Che cosa è LocalDB? Perché non "SQL Server" e basta?

| Edizione | Cos'è | Servizio sempre attivo? | Quando si usa |
|----------|-------|--------------------------|---------------|
| **LocalDB** | Motore minimo che gira **su richiesta**, come processo del mio utente | **No**: parte da solo alla prima connessione e si spegne dopo ~5 min di inattività | sviluppo locale, esercizi |
| **Express** | Gratuita, completa, come servizio di Windows. Limiti: 10 GB per database, 1 GB di RAM, 4 core | Sì | piccole applicazioni, anche in produzione |
| **Developer** | **Identica a Enterprise**, gratuita, ma la licenza vieta la produzione | Sì | sviluppo e test seri |
| **Standard / Enterprise** | Complete, a pagamento | Sì | produzione |

Le differenze che contano davvero, e che si sentono passando dall'una all'altra:

| | LocalDB | Express / Developer / Standard |
|---|---|---|
| Si collega da un'altra macchina? | **Mai** | Sì (aprendo la porta 1433) |
| Utenti | solo il proprietario | utenti e permessi veri |
| Login SQL (utente + password) | non ha senso | supportati |
| Backup automatici, SQL Agent | no | sì (tranne Express per l'Agent) |
| Va bene in produzione? | **No** | Sì |

**Questo progetto usa LocalDB**, ed è la scelta giusta per un esercizio: si installa con Visual Studio, non chiede configurazione, e la prima volta che qualcuno si collega l'istanza si avvia da sola. È inadatta alla produzione per un motivo solo ma insormontabile: **nessuno può collegarsi dall'esterno**. Non è un server, è un motore di database prestato a un'applicazione desktop.

L'istanza si chiama `MSSQLLocalDB` e la ritrovo nella stringa di connessione in `appsettings.json`:

```json
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=MovieManagerDb;Trusted_Connection=True;TrustServerCertificate=True"
```

Pezzo per pezzo:

| Pezzo | Significato |
|-------|-------------|
| `Server=(localdb)\MSSQLLocalDB` | l'istanza LocalDB a cui collegarmi |
| `Database=MovieManagerDb` | il database dentro quell'istanza |
| `Trusted_Connection=True` | autenticazione **Windows**: uso il mio account, niente utente e password nella stringa |
| `TrustServerCertificate=True` | accetta il certificato autofirmato di LocalDB (accettabile in locale, **da non** portare in produzione) |

Comandi utili, che valgono **solo** per LocalDB (`sqllocaldb` non esiste per le altre edizioni: lì si usano i servizi di Windows):

```powershell
sqllocaldb info                  # elenca le istanze presenti  -> MSSQLLocalDB
sqllocaldb info MSSQLLocalDB     # stato e versione dell'istanza
sqllocaldb start MSSQLLocalDB    # avvia l'istanza
sqllocaldb stop MSSQLLocalDB     # ferma l'istanza
```

### Le stringhe di connessione, scenario per scenario

Cambiare motore è **una riga** di `appsettings.json`, e il codice C# non cambia mai: `Program.cs` legge la stringa dalla configurazione e `UseSqlServer` parla con qualunque edizione ([capitolo 9](09-plapi-program-di-scalar.md)).

| Scenario | Stringa di connessione |
|----------|------------------------|
| **LocalDB** (quella attuale) | `Server=(localdb)\MSSQLLocalDB;Database=MovieManagerDb;Trusted_Connection=True;TrustServerCertificate=True` |
| **Express in locale**, auth Windows | `Server=.\SQLEXPRESS;Database=MovieManagerDb;Trusted_Connection=True;TrustServerCertificate=True` |
| **Istanza predefinita** in locale | `Server=localhost;Database=MovieManagerDb;Trusted_Connection=True;TrustServerCertificate=True` |
| **Login SQL** (utente + password) | `Server=localhost;Database=MovieManagerDb;User Id=movieapp;Password=***;TrustServerCertificate=True` |
| **Server remoto**, porta esplicita | `Server=192.168.1.50,1433;Database=MovieManagerDb;User Id=movieapp;Password=***;Encrypt=True` |

Il `.\` di `.\SQLEXPRESS` significa "questa macchina": `.` è l'abbreviazione di `localhost`, `SQLEXPRESS` è il nome dell'istanza. Un'**istanza predefinita** (senza nome) si indica con il solo `localhost`.

Cambiata la stringa basta riavviare: `EnsureCreated()` crea il database sulla nuova istanza e il seeder lo ripopola. Nessun'altra riga da toccare — è il vantaggio concreto dell'architettura a strati del [capitolo 1](01-struttura-e-architettura.md).

> 📖 **Installare SQL Server, usare SSMS, creare login e permessi, leggere un piano di esecuzione, fare backup**: tutto questo sta nel [capitolo 14](14-sql-server-e-ssms.md), dedicato a come si lavora su SQL Server fuori da un esercizio. Qui il tema è **l'SQL**; là è **lo strumento**.

---

## 10.1 Collegarsi al database

Tre strade, tutte valide.

**1. `sqlcmd` da terminale** — è quella che uso in questo capitolo. Un comando singolo:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MovieManagerDb -Q "SELECT * FROM Genres;" -W
```

- `-S` server/istanza, `-d` database, `-Q` esegue la query ed esce, `-W` toglie gli spazi di riempimento (output molto più leggibile).

Oppure una sessione interattiva, dove scrivo la query, poi `GO` su una riga a parte per eseguirla, e `QUIT` per uscire:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d MovieManagerDb
```

**2. Visual Studio** — `Visualizza` → `SQL Server Object Explorer`, espando `(localdb)\MSSQLLocalDB` → `Databases` → `MovieManagerDb`. Da lì vedo tabelle e dati con l'interfaccia grafica, e posso aprire una finestra query.

**3. L'applicazione stessa** — via Scalar o PowerShell, ma lì passo dall'API. Vedi il [capitolo 11](11-scalar-e-prova-api.md) e il file [`COMANDI.txt`](../COMANDI.txt).

> ⚠️ **`sqlcmd` non esiste?** Arriva con SQL Server o con gli strumenti a riga di comando. Verifico con `(Get-Command sqlcmd).Source`. Se manca, uso Visual Studio (strada 2).

---

## 10.2 Che cosa ha costruito EF Core

Questo è il punto in cui i due mondi si toccano. Io ho scritto classi C#; `EnsureCreated()` le ha tradotte in tabelle. Ecco la corrispondenza reale, letta dal database vero (`INFORMATION_SCHEMA.COLUMNS`):

| In C# (entità) | In SQL Server | Perché |
|----------------|---------------|--------|
| `int Id` | `int NOT NULL`, **IDENTITY**, chiave primaria | convenzione: la proprietà si chiama `Id` → chiave primaria auto-incrementante |
| `string FirstName` (non nullable) | `nvarchar(100) NOT NULL` | `NOT NULL` per il tipo non nullable, `100` da `HasMaxLength(100)` |
| `string? Country` (nullable) | `nvarchar(max) NULL` | il `?` la rende nullable; senza `HasMaxLength` diventa `max` |
| `DateOnly? BirthDate` | `date NULL` | `DateOnly` → `date` (solo giorno, niente ora) |
| `DateTime CreatedAt` | `datetime2 NOT NULL` | `DateTime` → `datetime2` |
| `decimal? Budget` | `decimal(18,2) NULL` | dal mio `HasPrecision(18, 2)` — prima era la precisione **predefinita** di EF, con il rischio di troncamenti silenziosi ([cap. 3](03-dal-dbcontext.md)) |
| `bool IsLeadRole` | `bit NOT NULL` | in SQL Server il booleano è `bit` (0/1) |
| `int GenreId` + `Genre Genre` | `int NOT NULL` + **FOREIGN KEY** | la coppia "id + proprietà di navigazione" diventa una chiave esterna |
| `ICollection<MovieActor> MovieActors` | **niente colonna** | il lato "molti" vive nell'altra tabella, non qui |

Le sei tabelle:

```
Genres(Id, Name, Description)
Directors(Id, FirstName, LastName, BirthDate, Country, Biography)
Actors(Id, FirstName, LastName, BirthDate, Country, Biography)
Movies(Id, Title, OriginalTitle, ReleaseDate, DurationMinutes, Synopsis,
       Language, Country, Budget, Revenue, PosterUrl, AgeRating,
       GenreId ->Genres, DirectorId ->Directors)
MovieActors(MovieId ->Movies, ActorId ->Actors, CharacterName, IsLeadRole, DisplayOrder)
       chiave primaria = (MovieId, ActorId)
Reviews(Id, MovieId ->Movies, ReviewerName, Score, Comment, CreatedAt)
```

Per farmi dire dal database com'è fatta davvero una tabella, senza fidarmi del codice:

```sql
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Movies'
ORDER BY ORDINAL_POSITION;
```

> **Nota su `nvarchar`**: la `n` sta per *Unicode*. `nvarchar` tiene qualsiasi alfabeto (accenti italiani, coreano, giapponese) mentre `varchar` no. EF usa `nvarchar` di default, ed è il motivo per cui posso salvare "La città incantata" o "Gisaengchung" senza problemi. `nvarchar(max)` non ha limite pratico; `nvarchar(100)` ne ha uno, e viene dal mio `HasMaxLength(100)`.

---

## (?) Che cosa è una colonna IDENTITY?

È il meccanismo che dà l'`Id` alle righe nuove. Dichiarando una colonna `IDENTITY`, SQL Server ci mette lui un numero progressivo a ogni `INSERT`, e **rifiuta** i tentativi di scriverlo a mano:

```
Cannot insert explicit value for identity column in table 'Actors' when IDENTITY_INSERT is set to OFF.
```

Questo errore (numero **544**) è esattamente quello in cui sono incappato mandando una POST con un `id` diverso da zero nel body. La regola: **l'Id non lo decido io, lo decide il database**. Il codice ora se ne occupa da solo (`GenericService.CreateAsync` azzera l'Id in arrivo, [capitolo 6](06-bll-services.md)).

Nell'`INSERT` a mano, la colonna `Id` semplicemente non si nomina:

```sql
-- giusto: l'Id lo mette SQL Server
INSERT INTO Actors (FirstName, LastName, Country) VALUES ('Mia', 'Goth', 'Regno Unito');

-- sbagliato: errore 544
INSERT INTO Actors (Id, FirstName, LastName) VALUES (99, 'Mia', 'Goth');
```

Forzare la mano si può, ma è un'eccezione (serve per esempio a migrare dati preservando gli id):

```sql
SET IDENTITY_INSERT Actors ON;
INSERT INTO Actors (Id, FirstName, LastName) VALUES (500, 'Test', 'Test');
SET IDENTITY_INSERT Actors OFF;
```

> ⚠️ Gli id **non vengono riciclati**. Se creo l'attore 11 e lo cancello, il prossimo sarà 12, non 11. È normale: l'identity è un contatore, non una lista di posti liberi.

---

## 10.3 SELECT: leggere i dati

La forma base è sempre la stessa: **cosa** voglio (`SELECT`), **da dove** (`FROM`), **quali righe** (`WHERE`), **in che ordine** (`ORDER BY`).

```sql
-- tutte le colonne di tutte le righe
SELECT * FROM Movies;

-- solo le colonne che mi servono (da preferire: meno dati, più chiarezza)
SELECT Title, ReleaseDate, DurationMinutes
FROM Movies
ORDER BY ReleaseDate DESC;
```

Risultato reale sul database del progetto:

```
Title               ReleaseDate  DurationMinutes
------------------  -----------  ---------------
Oppenheimer         2023-07-21   180
Barbie              2023-07-20   114
Dune                2021-10-22   155
Parasite            2019-05-30   132
Blade Runner 2049   2017-10-05   164
La citta incantata  2001-07-20   125
```

`ORDER BY` accetta `ASC` (crescente, predefinito) o `DESC` (decrescente).

**`TOP`** limita il numero di righe (è la versione T-SQL di `LIMIT`), e **`AS`** rinomina una colonna nel risultato:

```sql
SELECT TOP 3 Title AS Film, DurationMinutes AS Minuti
FROM Movies
ORDER BY DurationMinutes DESC;
```

```
Film               Minuti
-----------------  ------
Oppenheimer        180
Blade Runner 2049  164
Dune               155
```

**`DISTINCT`** elimina i duplicati:

```sql
SELECT DISTINCT Country FROM Actors ORDER BY Country;
-- Australia, Canada, Corea del Sud, Irlanda, Regno Unito, Stati Uniti, Svezia
```

---

## 10.4 WHERE: filtrare le righe

| Operatore | Significato | Esempio |
|-----------|-------------|---------|
| `=` `<>` `<` `>` `<=` `>=` | confronti | `DurationMinutes > 150` |
| `AND` `OR` `NOT` | combinazioni | `Language = 'Inglese' AND DurationMinutes > 150` |
| `LIKE` | testo con jolly: `%` (molti caratteri), `_` (uno) | `Title LIKE 'B%'` |
| `IN` | uno tra questi valori | `Country IN ('Stati Uniti', 'Canada')` |
| `BETWEEN` | intervallo, **estremi inclusi** | `YEAR(ReleaseDate) BETWEEN 2017 AND 2021` |
| `IS NULL` / `IS NOT NULL` | valore assente | `Budget IS NULL` |

```sql
SELECT Title, Language, Country
FROM Movies
WHERE Language = 'Inglese' AND DurationMinutes > 150;
-- Blade Runner 2049, Oppenheimer

SELECT Title FROM Movies WHERE Title LIKE 'B%';
-- Blade Runner 2049, Barbie

SELECT FirstName, LastName, Country
FROM Actors
WHERE Country IN ('Stati Uniti', 'Canada');
```

> ⚠️ **Il tranello di NULL.** `NULL` non è un valore, è l'**assenza** di valore, e non si confronta con `=`. `WHERE Budget = NULL` non restituisce **mai** niente, nemmeno per le righe che hanno il budget vuoto. Va scritto `WHERE Budget IS NULL`. Questa è una delle prime cose che confonde arrivando dal C#, dove `x == null` funziona benissimo.

Le stringhe vanno tra apici singoli (`'Inglese'`). Un apostrofo dentro una stringa si raddoppia: `'fantascienza d''autore'`.

---

## 10.5 Aggregare: contare e fare medie

Le **funzioni di aggregazione** riducono tante righe a un valore solo.

| Funzione | Cosa fa |
|----------|---------|
| `COUNT(*)` | conta le righe |
| `COUNT(colonna)` | conta i valori **non NULL** di quella colonna |
| `SUM` / `AVG` | somma / media |
| `MIN` / `MAX` | minimo / massimo |

```sql
SELECT COUNT(*) AS NumeroFilm,
       AVG(DurationMinutes) AS DurataMedia,
       MIN(DurationMinutes) AS PiuCorto,
       MAX(DurationMinutes) AS PiuLungo
FROM Movies;
```

```
NumeroFilm  DurataMedia  PiuCorto  PiuLungo
----------  -----------  --------  --------
6           145          114       180
```

### ⚠️ Il tranello più insidioso: AVG su una colonna intera

`Score` è una colonna `int`. Quando faccio `AVG` di interi, SQL Server calcola **in interi** e tronca il risultato — senza avvisare:

```sql
SELECT AVG(Score) AS MediaSbagliata,
       AVG(CAST(Score AS decimal(4,2))) AS MediaGiusta
FROM Reviews;
```

```
MediaSbagliata  MediaGiusta
--------------  -----------
8               8.750000
```

La media vera è **8.75**, ma la prima colonna dice **8**. Non è un arrotondamento, è un troncamento della divisione intera. La regola: **prima di fare la media di una colonna intera, converto con `CAST(... AS decimal)`**. È un errore che passa inosservato perché il risultato *sembra* plausibile.

### GROUP BY: aggregare per gruppi

`GROUP BY` fa la stessa cosa, ma per ogni gruppo invece che su tutto:

```sql
SELECT Language AS Lingua, COUNT(*) AS Film
FROM Movies
GROUP BY Language
ORDER BY Film DESC;
```

```
Lingua      Film
----------  ----
Inglese     4
Coreano     1
Giapponese  1
```

**Regola d'oro:** ogni colonna nella `SELECT` che non è dentro una funzione di aggregazione **deve** stare nel `GROUP BY`. Altrimenti SQL Server rifiuta la query — giustamente: non saprebbe quale valore mostrare per un gruppo di righe diverse.

### HAVING: filtrare i gruppi

`WHERE` filtra le **righe** *prima* del raggruppamento; `HAVING` filtra i **gruppi** *dopo*. È la distinzione che confonde di più:

```sql
-- attori che compaiono in più di un film
SELECT ActorId, COUNT(*) AS NumeroFilm
FROM MovieActors
GROUP BY ActorId
HAVING COUNT(*) > 1;
-- ActorId 8 (Ryan Gosling), 2 film
```

`WHERE COUNT(*) > 1` qui non funzionerebbe: al momento del `WHERE` i gruppi non esistono ancora.

L'ordine in cui SQL Server esegue davvero i pezzi (utile per capire tutto il resto):

```
FROM → WHERE → GROUP BY → HAVING → SELECT → ORDER BY
```

Ecco perché un alias definito nella `SELECT` non è utilizzabile nella `WHERE` (non esiste ancora) ma lo è nella `ORDER BY` (a quel punto esiste).

---

## 10.6 Le JOIN: mettere insieme le tabelle

Qui sta il cuore del modello relazionale. I dati sono spezzati su più tabelle per non ripeterli (il film non contiene il nome del regista, contiene il suo `DirectorId`): la **JOIN** li ricompone.

### INNER JOIN

Tiene **solo** le righe che hanno corrispondenza da entrambe le parti.

```sql
SELECT m.Title AS Film,
       g.Name  AS Genere,
       d.FirstName + ' ' + d.LastName AS Regista
FROM Movies m
INNER JOIN Genres    g ON g.Id = m.GenreId
INNER JOIN Directors d ON d.Id = m.DirectorId
ORDER BY m.Title;
```

```
Film                Genere        Regista
------------------  ------------  -----------------
Barbie              Commedia      Greta Gerwig
Blade Runner 2049   Fantascienza  Denis Villeneuve
Dune                Fantascienza  Denis Villeneuve
La citta incantata  Animazione    Hayao Miyazaki
Oppenheimer         Dramma        Christopher Nolan
Parasite            Thriller      Bong Joon-ho
```

Da notare:

- **Gli alias di tabella** (`Movies m`, `Genres g`) accorciano tutto e diventano obbligatori quando due tabelle hanno colonne con lo stesso nome (`Id` c'è dappertutto): `m.Id` e `g.Id` sono cose diverse.
- **`ON`** dice *come* si agganciano le tabelle: quasi sempre "chiave esterna di una = chiave primaria dell'altra".
- Si concatenano quante JOIN servono, una per relazione da seguire.
- `+` concatena stringhe in T-SQL (in altri dialetti è `||` o `CONCAT`).

### LEFT JOIN

Tiene **tutte** le righe della tabella di sinistra, anche quelle senza corrispondenza a destra: dove manca, mette `NULL`. È lo strumento per rispondere alle domande del tipo "chi **non** ha...":

```sql
-- film senza nemmeno un attore registrato
SELECT m.Title
FROM Movies m
LEFT JOIN MovieActors ma ON ma.MovieId = m.Id
WHERE ma.MovieId IS NULL;
-- La citta incantata
```

Il trucco da ricordare: `LEFT JOIN` + `WHERE colonna_destra IS NULL` = "tutti quelli **senza**". Con `INNER JOIN` quelle righe sparirebbero e la domanda resterebbe senza risposta.

| Tipo | Cosa tiene |
|------|-----------|
| `INNER JOIN` | solo le righe abbinate da entrambe le parti |
| `LEFT JOIN` | tutte quelle di sinistra + le abbinate di destra |
| `RIGHT JOIN` | il contrario (raro: basta invertire le tabelle e usare LEFT) |
| `FULL JOIN` | tutte da entrambe le parti |
| `CROSS JOIN` | tutte le combinazioni possibili (attenzione: esplode) |

### JOIN + GROUP BY insieme

```sql
-- film con media voti almeno 9
SELECT m.Title AS Film,
       COUNT(r.Id) AS Recensioni,
       CAST(AVG(CAST(r.Score AS decimal(4,2))) AS decimal(4,2)) AS Media
FROM Movies m
INNER JOIN Reviews r ON r.MovieId = m.Id
GROUP BY m.Title
HAVING AVG(CAST(r.Score AS decimal(4,2))) >= 9
ORDER BY Media DESC;
```

```
Film                Recensioni  Media
------------------  ----------  -----
La citta incantata  1           10.00
Parasite            1           10.00
Blade Runner 2049   1           9.00
```

(Di nuovo il `CAST`: senza, la media sarebbe intera e il filtro `>= 9` darebbe risultati diversi.)

---

## 10.7 Il molti-a-molti: la tabella ponte all'opera

`MovieActors` è il motivo per cui esiste tutta la complicazione della chiave composta ([capitolo 3](03-dal-dbcontext.md)). Nella pratica significa una JOIN che **attraversa** la tabella ponte per collegare film e attori:

```sql
SELECT m.Title AS Film,
       a.FirstName + ' ' + a.LastName AS Attore,
       ma.CharacterName AS Personaggio
FROM MovieActors ma
INNER JOIN Movies m ON m.Id = ma.MovieId
INNER JOIN Actors a ON a.Id = ma.ActorId
WHERE ma.IsLeadRole = 1
ORDER BY m.Title;
```

```
Film               Attore             Personaggio
-----------------  -----------------  ---------------------
Barbie             Margot Robbie      Barbie
Blade Runner 2049  Ryan Gosling       K
Dune               Timothee Chalamet  Paul Atreides
Oppenheimer        Cillian Murphy     J. Robert Oppenheimer
Parasite           Song Kang-ho       Kim Ki-taek
```

La stessa struttura, letta dall'altro verso, risponde alla domanda opposta:

```sql
-- in quali film recita Ryan Gosling
SELECT m.Title, ma.CharacterName
FROM Actors a
INNER JOIN MovieActors ma ON ma.ActorId = a.Id
INNER JOIN Movies m ON m.Id = ma.MovieId
WHERE a.LastName = 'Gosling';
-- Blade Runner 2049 / K   +   Barbie / Ken
```

È **la** cosa che una tabella ponte permette e che una semplice chiave esterna no: un attore in molti film **e** un film con molti attori, con in più gli attributi della relazione (`CharacterName`, `IsLeadRole`, `DisplayOrder`) che non appartengono né al film né all'attore, ma al **legame** tra i due.

> `WHERE ma.IsLeadRole = 1`: in SQL Server il `bool` è un `bit`, quindi si confronta con `1`/`0`, non con `true`/`false`.

---

## 10.8 Subquery ed EXISTS

Una **subquery** è una query dentro un'altra.

```sql
-- con IN: i film di fantascienza, senza scrivere l'id del genere a mano
SELECT Title FROM Movies
WHERE GenreId IN (SELECT Id FROM Genres WHERE Name = 'Fantascienza');
-- Dune, Blade Runner 2049
```

**Subquery correlata** — dipende dalla riga esterna e viene valutata per ognuna:

```sql
SELECT m.Title,
       (SELECT COUNT(*) FROM MovieActors ma WHERE ma.MovieId = m.Id) AS Attori
FROM Movies m
ORDER BY Attori DESC;
```

```
Title               Attori
------------------  ------
Oppenheimer         3
Dune                3
Blade Runner 2049   2
Barbie              2
Parasite            1
La citta incantata  0
```

**`EXISTS` / `NOT EXISTS`** — chiedono solo *"esiste almeno una riga?"*, senza portarsi dietro dati:

```sql
-- attori che non hanno mai avuto un ruolo da protagonista
SELECT a.FirstName + ' ' + a.LastName AS MaiProtagonista
FROM Actors a
WHERE NOT EXISTS (
    SELECT 1 FROM MovieActors ma
    WHERE ma.ActorId = a.Id AND ma.IsLeadRole = 1
)
ORDER BY a.LastName;
```

```
MaiProtagonista
-----------------
Emily Blunt
Zendaya Coleman
Robert Downey Jr
Rebecca Ferguson
Harrison Ford
```

Il `SELECT 1` non è un errore: a `EXISTS` interessa solo *se* la riga c'è, non cosa contiene. `NOT EXISTS` è spesso più leggibile del giro `LEFT JOIN ... IS NULL`, e gestisce meglio i `NULL` rispetto a `NOT IN`.

---

## 10.9 Window function: un assaggio

Le **window function** calcolano un valore aggregato **senza** collassare le righe: ogni riga resta, e accanto compare il calcolo fatto su un "gruppo" (la *finestra*).

```sql
-- classifica per durata dentro ogni genere
SELECT g.Name AS Genere, m.Title AS Film, m.DurationMinutes AS Minuti,
       ROW_NUMBER() OVER (PARTITION BY g.Name ORDER BY m.DurationMinutes DESC) AS Posizione
FROM Movies m
JOIN Genres g ON g.Id = m.GenreId
ORDER BY g.Name, Posizione;
```

```
Genere        Film                Minuti  Posizione
------------  ------------------  ------  ---------
Animazione    La citta incantata  125     1
Commedia      Barbie              114     1
Dramma        Oppenheimer         180     1
Fantascienza  Blade Runner 2049   164     1
Fantascienza  Dune                155     2
Thriller      Parasite            132     1
```

`PARTITION BY` è "il `GROUP BY` della finestra": la numerazione riparte da 1 a ogni genere. Con `GROUP BY` avrei ottenuto una riga per genere; qui li vedo tutti.

```sql
-- ogni film confrontato con la durata media generale
SELECT Title, DurationMinutes AS Minuti,
       AVG(DurationMinutes) OVER () AS MediaGenerale,
       DurationMinutes - AVG(DurationMinutes) OVER () AS Scarto
FROM Movies
ORDER BY Scarto DESC;
```

```
Title               Minuti  MediaGenerale  Scarto
------------------  ------  -------------  ------
Oppenheimer         180     145            35
Blade Runner 2049   164     145            19
Dune                155     145            10
Parasite            132     145            -13
La citta incantata  125     145            -20
Barbie              114     145            -31
```

`OVER ()` vuoto = la finestra è **tutta** la tabella. Le più usate: `ROW_NUMBER()` (progressivo), `RANK()` (con salti sui pari merito), `DENSE_RANK()` (senza salti), più le solite `SUM`/`AVG`/`COUNT` con `OVER`.

---

## 10.10 Modificare i dati: INSERT, UPDATE, DELETE

```sql
-- INSERT: mai la colonna Id (è IDENTITY)
INSERT INTO Genres (Name, Description) VALUES ('Documentario', 'Racconto della realta.');

-- INSERT multiplo
INSERT INTO Actors (FirstName, LastName, Country) VALUES
    ('Mia', 'Goth', 'Regno Unito'),
    ('Barry', 'Keoghan', 'Irlanda');

-- UPDATE
UPDATE Movies SET Synopsis = 'Nuova sinossi.' WHERE Id = 4;

-- DELETE
DELETE FROM Genres WHERE Name = 'Documentario';
```

> ⚠️ **La `WHERE` dimenticata è il classico disastro.** `UPDATE Movies SET Synopsis = 'x';` (senza `WHERE`) riscrive **tutte** le righe, `DELETE FROM Movies;` le cancella **tutte**. Non c'è annulla. L'abitudine che salva: scrivo prima la query come `SELECT` con la stessa `WHERE`, guardo quali righe escono, e solo dopo la trasformo in `UPDATE`/`DELETE`.

---

## 10.11 Le query con `INTO`: copiare righe e creare tabelle al volo

Ci sono due istruzioni con `INTO` e si somigliano nel nome, ma fanno cose opposte. La differenza sta tutta in **chi crea la tabella di destinazione**.

| | `SELECT ... INTO nuova` | `INSERT INTO esistente ... SELECT` |
|---|---|---|
| La tabella di destinazione | la **crea** l'istruzione | deve **già esistere** |
| Se esiste già | **errore** | ci aggiunge le righe |
| Le colonne | le deduce dalla `SELECT` | le indico io |
| Serve per | fotografie, tabelle di lavoro | copiare righe tra tabelle vere |

### `SELECT ... INTO` — crea la tabella e la riempie

```sql
SELECT Title, DurationMinutes, ReleaseDate
INTO MoviesLunghi
FROM Movies
WHERE DurationMinutes > 150;
```

In un colpo solo: `MoviesLunghi` **non esisteva**, ora esiste e contiene 3 righe.

```
Title              DurationMinutes  ReleaseDate
-----------------  ---------------  -----------
Dune               155              2021-10-22
Blade Runner 2049  164              2017-10-05
Oppenheimer        180              2023-07-21
```

I **tipi delle colonne** vengono ereditati dalla `SELECT`, senza doverli dichiarare:

```sql
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MoviesLunghi';
```
```
COLUMN_NAME      DATA_TYPE  IS_NULLABLE
---------------  ---------  -----------
Title            nvarchar   NO
DurationMinutes  int        NO
ReleaseDate      date       NO
```

È comodissimo per **farsi una fotografia prima di combinare qualcosa**:

```sql
SELECT * INTO Movies_Backup_20260715 FROM Movies;   -- copia di sicurezza in una riga
-- ... esperimenti ...
DROP TABLE Movies_Backup_20260715;                  -- fatto, la butto
```

Nota che il nome della tabella **non può essere una variabile**: `SELECT ... INTO @nome` non esiste. Il nome si scrive lì, letterale.

### ⚠️ Cosa `SELECT INTO` **non** copia (la parte che si sbaglia)

Copia **struttura e dati**, e basta. Ho verificato ogni voce sul database del progetto:

| Cosa | Copiata? |
|------|----------|
| Colonne, tipi, nullabilità | **Sì** |
| Righe | **Sì** |
| `IDENTITY` | **Sì** ← l'unica sorpresa |
| Chiave primaria | No |
| Foreign key | No |
| **Check constraint** | **No** |
| Indici | No |

La riga da tenere a mente è il **check constraint**. La copia di `Reviews` accetta felicemente un punteggio che la tabella vera rifiuta:

```sql
SELECT * INTO ReviewsCopia FROM Reviews;

-- sulla COPIA: passa
INSERT INTO ReviewsCopia (MovieId, ReviewerName, Score, Comment, CreatedAt)
VALUES (1, 'Test', 99, 'fuori range 1-10', GETDATE());
-- -> inserito, Score = 99

-- sulla tabella VERA: respinto
INSERT INTO Reviews (MovieId, ReviewerName, Score, Comment, CreatedAt)
VALUES (1, 'Test', 99, 'fuori range', GETDATE());
-- -> Msg 547: The INSERT statement conflicted with the CHECK constraint "CK_Review_Score".
```

Il `CK_Review_Score` del [capitolo 3](03-dal-dbcontext.md) **non c'è** nella copia. Quindi: una tabella nata da `SELECT INTO` è un contenitore di dati, **non una tabella equivalente**. Va benissimo per una fotografia temporanea; usarla per rimpiazzare l'originale significherebbe perdere tutte le difese in silenzio.

L'`IDENTITY` invece **viene copiata**, e me ne sono accorto sbattendoci contro: provando a inserire nella copia con un `Id` esplicito è ricomparso il vecchio amico del [capitolo 6](06-bll-services.md):

```
Msg 544: Cannot insert explicit value for identity column in table 'ReviewsCopia'
when IDENTITY_INSERT is set to OFF.
```

### `INSERT INTO ... SELECT` — la tabella esiste già

Quando la destinazione c'è, si usa la forma normale dell'`INSERT` con una `SELECT` al posto dei `VALUES`:

```sql
INSERT INTO MoviesArchivio (Title, DurationMinutes, ReleaseDate)
SELECT Title, DurationMinutes, ReleaseDate
FROM Movies
WHERE ReleaseDate < '2010-01-01';
```

Le colonne devono corrispondere per **numero e ordine** (i nomi nella `SELECT` non contano, conta la posizione). È la forma da usare quasi sempre: la destinazione l'ho creata io, con i vincoli che volevo, e ci sto solo mettendo dentro delle righe.

### Provarle senza rischi

Tutti gli esempi qui sopra li ho eseguiti **dentro una transazione poi annullata**, che è il modo sicuro di sperimentare (sezione 10.10):

```sql
BEGIN TRANSACTION;
    SELECT Title INTO Prova FROM Movies;
    SELECT COUNT(*) FROM Prova;      -- 6
ROLLBACK;

SELECT CASE WHEN OBJECT_ID('Prova') IS NULL THEN 'NO' ELSE 'SI' END AS EsisteAncora;
-- NO
```

Dettaglio non ovvio e molto utile: **in SQL Server anche il DDL è transazionale**. Il `ROLLBACK` non annulla solo le righe, si porta via la **tabella intera**. Non è così dappertutto — in MySQL o Oracle un `CREATE TABLE` fa commit implicito e non si può disfare. Qui posso creare tabelle di prova senza lasciare traccia.

### E in EF Core?

Nessun equivalente: `SELECT INTO` crea tabelle, e creare tabelle non è mestiere di un ORM — è mestiere delle migration ([capitolo 13](13-migrations.md)). Il parente più vicino è la proiezione `.Select(m => new { ... })`, che però costruisce oggetti in C#, non tabelle in SQL ([capitolo 12](12-dal-controller-all-sql.md)). Per eseguirle da EF servirebbe `db.Database.ExecuteSqlRaw("SELECT ... INTO ...")`, cioè scavalcare l'ORM di proposito.

---

## (?) Che cosa è una transazione? (e cosa c'entra la Unit of Work)

Una **transazione** è un blocco di operazioni che vale come una sola: o vanno a buon fine **tutte**, o è come se non fosse successo niente (`ROLLBACK`). È la proprietà che impedisce di restare a metà — il classico bonifico che esce da un conto ma non entra nell'altro.

```sql
BEGIN TRANSACTION;
    UPDATE Movies SET DurationMinutes = 999 WHERE Id = 1;
    SELECT Title, DurationMinutes FROM Movies WHERE Id = 1;   -- vedo 999
ROLLBACK;
SELECT Title, DurationMinutes FROM Movies WHERE Id = 1;       -- torna 155
```

`COMMIT` conferma, `ROLLBACK` annulla. Questo è anche il modo **sicuro di provare una query distruttiva**: la eseguo dentro una transazione, guardo l'effetto, e annullo. L'ho usato per misurare davvero cosa si porta via un `DELETE` a cascata (sezione 10.13).

E qui si chiude il cerchio con il [capitolo 4](04-dal-repository-unitofwork.md): **`SaveChangesAsync()` di EF Core è già una transazione**. Tutte le modifiche accumulate nel change tracker vengono inviate in blocco e, se una fallisce, viene annullato tutto. È esattamente il motivo per cui la Unit of Work esiste e per cui il `SaveChanges` sta nel service e non nel repository: è il punto in cui decido *dove finisce la transazione*.

---

## 10.12 Come EF Core traduce: da LINQ a SQL

Il vantaggio di sapere l'SQL è capire cosa combina l'ORM. Con la configurazione di log di questo progetto, **EF stampa in console ogni query che esegue**. Avviando l'app si vede scorrere l'SQL vero.

> Questa sezione è l'assaggio; il discorso completo — quando parte davvero una query, quale metodo la fa partire, il catalogo delle traduzioni LINQ → SQL — è nel [capitolo 12](12-dal-controller-all-sql.md).

Esempi presi dal log reale:

**`context.Genres.ToDictionaryAsync(g => g.Name)`** (nel seeder) diventa:

```sql
SELECT [g].[Id], [g].[Description], [g].[Name] FROM [Genres] AS [g]
```

**`_dbSet.FindAsync(id)`** (`GenericRepository`) diventa:

```sql
SELECT TOP(1) [a].[Id], [a].[Biography], [a].[BirthDate], [a].[Country], [a].[FirstName], [a].[LastName]
FROM [Actors] AS [a]
WHERE [a].[Id] = @p
```

**`_dbSet.AddAsync(entity)` + `SaveChangesAsync()`** diventa:

```sql
INSERT INTO [Actors] ([Biography], [BirthDate], [Country], [FirstName], [LastName])
OUTPUT INSERTED.[Id]
VALUES (@p0, @p1, @p2, @p3, @p4);
```

Quest'ultimo merita attenzione, perché racconta due cose in tre righe:

1. **La colonna `[Id]` non c'è.** EF sa che è IDENTITY e la lascia decidere a SQL Server. Era esattamente qui il bug iniziale: con un `id` diverso da zero nel body, EF pensava che volessi imporlo e generava `INSERT INTO [Actors] ([Id], [Biography], ...)` → errore 544. Confrontare i due `INSERT` è il modo più diretto per vedere la differenza.
2. **`OUTPUT INSERTED.[Id]`** è il modo con cui EF si fa restituire l'Id appena generato nella stessa andata e ritorno. È grazie a questo che `CreateAsync` può rimappare l'entità e restituire al client il model con l'Id valorizzato, senza una `SELECT` in più.

Le parentesi quadre `[...]` sono la delimitazione dei nomi in T-SQL: servono quando un nome coincide con una parola riservata. `@p`, `@p0`... sono **parametri**: EF non incolla mai i valori dentro la stringa SQL, li passa a parte. È ciò che rende impossibile la **SQL injection** — lo stesso motivo per cui in Java usavo `PreparedStatement` invece di concatenare stringhe.

> **E da EF?** Verrebbe da pensare che `_dbSet.Average(r => r.Score)` soffra dello stesso troncamento della sezione 10.5. **Non è così**: EF traduce in `AVG(CAST([r].[Score] AS float))`, mettendo il `CAST` da solo, e restituisce **8,75**. Il tranello dell'`AVG` intero è dell'SQL scritto a mano, non dell'ORM — il confronto affiancato è nel [capitolo 12](12-dal-controller-all-sql.md).

---

## 10.13 I vincoli: cosa il database rifiuta (e cosa cancella)

Il database non è un sacco dove buttare dati: fa rispettare le regole a prescindere da chi scrive (l'API, `sqlcmd`, chiunque). Per vederli tutti:

```sql
SELECT OBJECT_NAME(parent_object_id) AS Tabella, name AS Vincolo, delete_referential_action_desc AS OnDelete
FROM sys.foreign_keys;
```

### Le foreign key sono tutte in CASCADE ⚠️

Questa è la cosa più importante del capitolo. Le relazioni sono obbligatorie (`int GenreId` non nullable), e per convenzione EF le configura con **`ON DELETE CASCADE`**:

```
Genres    -> Movies -> MovieActors + Reviews
Directors -> Movies -> MovieActors + Reviews
Actors    -> MovieActors
Movies    -> MovieActors + Reviews
```

La cascata è **a più livelli**: cancellando un genere spariscono i suoi film, e con loro le recensioni e il cast di quei film. Misurato davvero, dentro una transazione poi annullata:

```sql
BEGIN TRANSACTION;
SELECT COUNT(*) FROM Movies;   -- 6
DELETE FROM Genres WHERE Name = 'Fantascienza';
SELECT COUNT(*) FROM Movies;   -- 4   (-2 film)
SELECT COUNT(*) FROM Reviews;  -- 5   (-3 recensioni)
ROLLBACK;
```

**Un solo genere cancellato = 2 film, 3 recensioni e 5 righe di cast spariti.** Prima di cancellare qualcosa conviene sempre guardare cosa ci sta attaccato:

```sql
SELECT Title FROM Movies WHERE GenreId = 1;
```

### Il check constraint

```sql
SELECT name, definition FROM sys.check_constraints;
-- CK_Review_Score : ([Score]>=(1) AND [Score]<=(10))
```

Nasce dal `HasCheckConstraint` del [capitolo 3](03-dal-dbcontext.md) ed è applicato dal **database**: un `INSERT` diretto con `Score = 99` viene rifiutato anche bypassando l'API. Dall'API, invece, l'errore viene intercettato prima (`[Range(1, 10)]` sul model → `400`, vedi [capitolo 5](05-bll-models.md)). Due difese sullo stesso vincolo, a due livelli diversi: quella applicativa dà un messaggio chiaro, quella del database garantisce che il dato resti valido comunque.

---

## 10.14 Come nascono i dati: EnsureCreated e il seeder

All'avvio, `Program.cs` fa due cose in sequenza ([capitolo 9](09-plapi-program-di-scalar.md)):

```csharp
db.Database.EnsureCreated();      // schema: crea il database se non esiste
await MovieDbSeeder.SeedAsync(db); // dati: inserisce quello che manca
```

`MovieDbSeeder` (`MovieManager.DAL/Data/MovieDbSeeder.cs`) riempie il catalogo di esempio: 5 generi, 5 registi, 10 attori, 6 film, 11 collegamenti di cast e 7 recensioni.

La proprietà che lo rende usabile a ogni avvio è che è **idempotente**: confronta la **chiave naturale** (il nome del genere, il titolo del film) e **non** l'Id.

```csharp
var existing = await context.Genres.ToDictionaryAsync(g => g.Name, cancellationToken);
// ... aggiunge solo quelli il cui nome non è già presente
```

Perché la chiave naturale e non l'Id? Perché gli Id li assegna il database: al primo avvio non li conosco. Se il seeder ragionasse per Id dovrebbe imporli, e ricadrei dritto nell'errore 544 dell'`IDENTITY_INSERT`. Ragionando per nome, invece, il seeder **riconosce** una riga già inserita a mano e la lascia stare, agganciandoci semmai il resto (cast e recensioni).

Conseguenze pratiche:

- Riavviare l'app **non** duplica niente e non sovrascrive le righe esistenti.
- Se cancello un dato del seed, al riavvio **torna** (il seeder si accorge che manca).
- Se aggiungo dati miei, restano dove sono: il seeder aggiunge, non allinea.

Per ripartire davvero da zero serve eliminare il database:

```powershell
# 1. fermare l'app, poi:
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "ALTER DATABASE MovieManagerDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE MovieManagerDb;"
# 2. riavviare: EnsureCreated ricrea lo schema, il seeder ripopola
dotnet run --project MovieManager.PL.API
```

Il `SET SINGLE_USER WITH ROLLBACK IMMEDIATE` chiude le connessioni ancora aperte: senza, il `DROP DATABASE` fallisce dicendo che il database è in uso.

> **Perché serve il `DROP` e non basta riavviare?** Perché `EnsureCreated()` crea lo schema solo se il database **non esiste**: non aggiorna uno schema già presente. Se modifico un'entità, la modifica si vede solo dopo aver eliminato il database. È il limite già segnalato nel [capitolo 3](03-dal-dbcontext.md), ed è **esattamente il problema che risolvono le migration**: con quelle, la stessa modifica diventa un `ALTER TABLE` e i dati restano al loro posto. Il confronto completo, comandi compresi, è nel [capitolo 13](13-migrations.md).

---

## Verifica finale

Query da tenere a portata di mano per controllare lo stato del catalogo:

```sql
-- quante righe per tabella
SELECT 'Genres' AS Tabella, COUNT(*) AS Righe FROM Genres
UNION ALL SELECT 'Directors', COUNT(*) FROM Directors
UNION ALL SELECT 'Actors',    COUNT(*) FROM Actors
UNION ALL SELECT 'Movies',    COUNT(*) FROM Movies
UNION ALL SELECT 'MovieActors', COUNT(*) FROM MovieActors
UNION ALL SELECT 'Reviews',   COUNT(*) FROM Reviews;
```

Su un database appena creato dal seeder escono: **5 / 5 / 10 / 6 / 11 / 7**.

Se il numero di recensioni è più alto non c'è nulla di rotto: sono quelle aggiunte a mano, che il seeder non tocca. Sul mio database ce n'è **8**, perché una l'avevo inserita io provando l'API prima che il seeder esistesse.

```sql
-- panoramica del catalogo con tutto collegato
SELECT m.Id, m.Title, g.Name AS Genere, d.LastName AS Regista,
       (SELECT COUNT(*) FROM MovieActors ma WHERE ma.MovieId = m.Id) AS Cast,
       (SELECT COUNT(*) FROM Reviews r WHERE r.MovieId = m.Id) AS Recensioni
FROM Movies m
JOIN Genres g    ON g.Id = m.GenreId
JOIN Directors d ON d.Id = m.DirectorId
ORDER BY m.Id;
```

`UNION ALL` impila i risultati di più query con le stesse colonne (`UNION` da solo eliminerebbe i duplicati, e costa di più).

Tutti i comandi pronti all'uso, anche lato PowerShell e API, stanno in [`COMANDI.txt`](../COMANDI.txt).

[➡ Prossima parte: Scalar — provare le API dal browser](11-scalar-e-prova-api.md)
