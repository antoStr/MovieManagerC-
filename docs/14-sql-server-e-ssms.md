# 14) SQL Server e SSMS — usare il database come al lavoro

[⬅ Torna all'indice](../README.md)

Il [capitolo 10](10-database-sql-server.md) spiega **l'SQL**; questo spiega **il prodotto e lo strumento**. Sono due cose diverse, e la seconda è quella che si usa tutti i giorni in un lavoro vero.

Finora ho guardato il database con `sqlcmd`, e l'esercizio gira su **LocalDB**: un motore che si avvia da solo, senza utenti e senza configurazione. Al lavoro non sarà così. Ci sarà un **SQL Server vero**, probabilmente su una macchina che non è la mia, con utenti, permessi, backup e altre persone collegate insieme a me. E lo strumento non sarà `sqlcmd`: sarà **SSMS**.

> **La cosa più utile di tutto il capitolo, subito: SSMS funziona già ora con LocalDB.** Non serve installare SQL Server per imparare a usarlo. Si installa SSMS, si mette `(localdb)\MSSQLLocalDB` come server, e si ha davanti `MovieManagerDb` con tutte le sue tabelle — le stesse schermate, gli stessi menù, gli stessi piani di esecuzione che si vedranno al lavoro. Tutto quello che c'è qui sotto l'ho verificato così.

---

## (?) Cosa cambia davvero dall'esercizio al lavoro

| | Qui (esercizio) | Al lavoro |
|---|---|---|
| **Motore** | LocalDB, si avvia da solo | SQL Server come **servizio**, sempre acceso |
| **Dove** | la mia macchina | un server, raggiunto per rete |
| **Chi si collega** | solo io | l'app, i colleghi, i job notturni, i report |
| **Autenticazione** | il mio account Windows | login dedicati, con permessi limitati |
| **Se sbaglio una `DELETE`** | rilancio l'app e il seeder ripopola | **non c'è annulla**: si va di backup |
| **Strumento** | `sqlcmd` | **SSMS** |
| **Chi crea le tabelle** | `EnsureCreated()` all'avvio | migration applicate **prima** del deploy ([cap. 13](13-migrations.md)) |

La riga che cambia la vita è la quinta. In locale il database è usa e getta: qualunque disastro si ripara con un `DROP DATABASE` e un riavvio. Al lavoro il database **è** il valore dell'azienda — il codice si riscrive, i dati dei clienti no. Da qui discende tutto il resto: i permessi ristretti, i backup, l'abitudine di provare le cose dentro una transazione.

---

## 14.1 Installare SQL Server e SSMS

Sono due prodotti separati: il **motore** (SQL Server) e lo **strumento** (SSMS). Al lavoro il motore è già su un server e si installa solo SSMS.

```powershell
# SSMS: lo strumento. E' questo che serve davvero.
winget install Microsoft.SQLServerManagementStudio

# Il motore, solo se serve un SQL Server anche in locale:
winget install Microsoft.SQLServer.2022.Express      # gratuita, ok anche in produzione
winget install Microsoft.SQLServer.2022.Developer    # completa, ma licenza solo sviluppo/test
```

> ⚠️ **Serve un terminale come amministratore.** Entrambi installano roba di sistema (SSMS un'applicazione, SQL Server anche un servizio di Windows) e senza elevazione l'installer si ferma subito. È il motivo per cui questo passaggio va fatto a mano.

Express o Developer? Per imparare, **Developer**: è identica a Enterprise e gratuita. Per far girare qualcosa davvero, **Express**: ha dei limiti (10 GB per database) ma la licenza permette la produzione. Il confronto completo delle edizioni è nel [capitolo 10](10-database-sql-server.md).

Verifica che il motore sia in piedi:

```powershell
Get-Service | Where-Object Name -like "MSSQL*" | Select-Object Name, Status
sqlcmd -S ".\SQLEXPRESS" -Q "SELECT @@VERSION;"
```

---

## 14.2 Collegarsi: la finestra che si vede per prima

All'avvio SSMS chiede a cosa collegarsi. È la schermata su cui ci si blocca il primo giorno, e ha solo quattro campi che contano.

| Campo | Cosa mettere |
|-------|--------------|
| **Server type** | `Database Engine` (gli altri servono ad altri prodotti) |
| **Server name** | **il campo che conta**, vedi sotto |
| **Authentication** | `Windows Authentication` oppure `SQL Server Authentication` |
| **Trust server certificate** | da spuntare in locale, vedi sotto |

Il **Server name** segue la stessa logica della stringa di connessione del [capitolo 10](10-database-sql-server.md) — è la stessa cosa scritta in un campo invece che in un `appsettings.json`:

| Cosa voglio raggiungere | Server name |
|---|---|
| **LocalDB, cioè `MovieManagerDb` di questo progetto** | `(localdb)\MSSQLLocalDB` |
| SQL Server Express sulla mia macchina | `.\SQLEXPRESS` (il `.` è "questa macchina") |
| Istanza predefinita, in locale | `localhost` |
| Un server in rete | `SRV-DATI` o `192.168.1.50,1433` |

**Le due autenticazioni**, che è la distinzione da avere chiara:

- **Windows Authentication** — SSMS usa l'account con cui ho fatto login a Windows. Nessuna password da digitare. È l'equivalente di `Trusted_Connection=True`, e al lavoro è quasi sempre questa: l'amministratore dà i permessi al mio utente di dominio e io non gestisco nessuna password.
- **SQL Server Authentication** — utente e password gestiti da SQL Server, indipendenti da Windows. Serve quando chi si collega non è un utente Windows del dominio — tipicamente **l'applicazione**.

> ⚠️ **"Trust server certificate": perché va spuntato.** SQL Server cifra la connessione con un certificato; in locale quel certificato è **autofirmato** e il client, giustamente, non si fida. Spuntando la casella dico "lo so, fidati lo stesso". In locale va bene. Su un server di produzione **no**: lì il certificato è vero, e spuntare quella casella significa rinunciare a verificare di essere collegati al server giusto. È esattamente lo stesso `TrustServerCertificate=True` che sta in `appsettings.json`, con lo stesso identico distinguo.

---

## 14.3 Il giro di SSMS in tre pezzi

Finita la connessione, la finestra ha tre zone. Sono queste, e non cambiano mai.

```
┌──────────────────┬────────────────────────────────────┐
│ Object Explorer  │  Query Editor                      │
│ (a sinistra)     │  (dove scrivo l'SQL)               │
│                  │                                    │
│ Databases        │  SELECT * FROM Movies;             │
│  └ MovieManagerDb├────────────────────────────────────┤
│     ├ Tables     │  Results / Messages                │
│     │  ├ Movies  │  (la griglia dei risultati)        │
│     │  ├ Genres  │                                    │
│     │  └ ...     │                                    │
│     ├ Views      │                                    │
│     ├ Programm.  │                                    │
│     └ Security   │                                    │
└──────────────────┴────────────────────────────────────┘
```

**1. Object Explorer** — l'albero del server. Espandendo `Databases` → `MovieManagerDb` → `Tables` si trovano le sei tabelle del progetto. Espandendo una tabella si vedono `Columns`, `Keys`, `Constraints`, `Indexes`: è la stessa roba che nel [capitolo 10](10-database-sql-server.md) ho tirato fuori interrogando `INFORMATION_SCHEMA` e `sys.foreign_keys`, ma già formattata.

**2. Query Editor** — si apre con **`Ctrl+N`** (o tasto destro sul database → `New Query`). ⚠️ **Attenzione**: la query gira sul database selezionato nella tendina in alto, **non** su quello che ho evidenziato nell'Object Explorer. È l'errore del primo giorno: si lancia una `DROP TABLE` credendo di essere su `MovieManagerDb` e si è su `master`. Il modo sicuro è dirlo nella query stessa:

```sql
USE MovieManagerDb;
GO
SELECT * FROM Movies;
```

**3. Results** — la griglia. Si copia con `Ctrl+C`, si esporta con tasto destro → `Save Results As`. La linguetta `Messages` accanto è quella che si guarda quando qualcosa va storto: lì compaiono gli errori con il **numero** (544, 547, 2627, 2714 — [cap. 13](13-migrations.md)) e le righe interessate.

### Le scorciatoie che si usano tutto il giorno

| Tasti | Cosa fa |
|-------|---------|
| **`F5`** | esegue. Se ho **selezionato** del testo, esegue **solo quello** ← la più utile in assoluto |
| `Ctrl+N` | nuova finestra query |
| **`Ctrl+M`** | attiva il piano di esecuzione (sezione 14.5) |
| `Ctrl+R` | mostra/nasconde la griglia dei risultati |
| `Ctrl+Shift+Q` | Query Designer, per costruire una query a mouse |
| `Alt+F1` | su un nome di tabella selezionato: ne mostra la struttura (`sp_help`) |

**`F5` sulla selezione è la cosa più importante di questa tabella.** In un file con venti query, selezionare le tre righe che servono ed eseguire solo quelle è il modo normale di lavorare — ed è anche la rete di sicurezza: si seleziona la `WHERE` insieme alla `DELETE`, mai la `DELETE` da sola.

### Guardare e modificare i dati senza scrivere SQL

Tasto destro su una tabella:

| Voce | Cosa fa | Attenzione |
|------|---------|------------|
| `Select Top 1000 Rows` | genera ed esegue la `SELECT` | il modo veloce di sbirciare |
| `Edit Top 200 Rows` | griglia **modificabile**: cambio una cella e viene salvata | ⚠️ scrive davvero, senza `WHERE` e senza conferma |
| `Script Table as` → `CREATE To` | genera la `CREATE TABLE` completa | vedi sotto |
| `Design` | modifica la struttura a mouse | ⚠️ **mai** su un progetto con EF |
| `Properties` → `Storage` | quanto spazio occupa | |

> ⚠️ **`Design` e `Edit Rows` sono due trappole di natura diversa.**
>
> `Edit Top 200 Rows` scrive nel database appena esco dalla cella. Nessun `Ctrl+S`, nessuna conferma, nessun `ROLLBACK` possibile. Comodissimo per correggere un dato, pericoloso perché non sembra un'operazione di scrittura.
>
> `Design` è peggio, in un progetto come questo: modificare una colonna a mouse cambia il database **senza cambiare le entità C#**. Il modello e lo schema divergono, e alla prima query esce un `Invalid column name` (errore 207). In un progetto con EF Core lo schema si cambia **da C#** — modificando l'entità e generando una migration ([cap. 13](13-migrations.md)). Il database non è la fonte della verità: lo sono le classi.

**`Script Table as` → `CREATE To`** merita una riga a parte: fa scrivere a SSMS la `CREATE TABLE` completa di una tabella esistente, vincoli e indici compresi. È il modo per rispondere a "com'è fatta davvero questa tabella?" senza fidarsi di nessuno — ed è anche il modo per vedere, in T-SQL leggibile, cosa ha prodotto `EnsureCreated()` a partire dal mio `OnModelCreating`.

---

## 14.4 Le domande di ogni giorno, e la risposta in T-SQL

Ogni cosa che SSMS fa col mouse, sotto è una query. Conoscere entrambe le strade serve: il mouse è più veloce, il T-SQL funziona anche via `sqlcmd`, negli script e in una pipeline.

| Domanda | Con il mouse | In T-SQL |
|---------|--------------|----------|
| Com'è fatta questa tabella? | espandi `Columns` / `Alt+F1` | `EXEC sp_help 'Movies';` |
| Quanto occupa il database? | tasto destro → `Properties` | `EXEC sp_spaceused;` |
| Quali tabelle ci sono? | espandi `Tables` | `SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES;` |
| Che indici ha? | espandi `Indexes` | `SELECT name, type_desc FROM sys.indexes WHERE object_id = OBJECT_ID('Movies');` |
| Chi è collegato adesso? | `Activity Monitor` | `SELECT session_id, login_name, status FROM sys.dm_exec_sessions WHERE is_user_process = 1;` |
| Che versione è? | in cima all'Object Explorer | `SELECT @@VERSION;` |

Misurato sul database del progetto:

```sql
EXEC sp_spaceused;
```
```
database_name   database_size  unallocated space
MovieManagerDb  16.00 MB       4.23 MB

reserved  data     index_size  unused
3856 KB   1224 KB  1536 KB     1096 KB
```

Un dettaglio che dice più di quanto sembri: **gli indici (1536 KB) occupano più dei dati (1224 KB)**. Sono i tre indici sulle chiavi esterne che EF crea da solo ([cap. 13](13-migrations.md)) più le chiavi primarie. È normale, ed è il prezzo che si paga per avere le JOIN veloci: un indice è dati duplicati e riordinati.

---

## 14.5 Il piano di esecuzione: la funzione per cui esiste SSMS

Questa è **la** cosa che `sqlcmd` non sa fare e che al lavoro si usa davvero. `Ctrl+M`, poi `F5`: accanto ai risultati compare la linguetta **Execution plan**, con il disegno di *come* SQL Server ha eseguito la query.

Non serve saperlo leggere tutto. Serve saper leggere **una cosa sola**: `Seek` o `Scan`.

| | Cosa fa | Quando è giusto |
|---|---|---|
| **Index Seek** | va **dritto** alle righe che servono, usando un indice | quasi sempre ciò che voglio |
| **Index Scan** | **legge tutta** la tabella e scarta | ok su tabelle piccole, o se davvero servono tutte le righe |

Su 6 film non cambia niente. Su 6 milioni, la differenza tra `Seek` e `Scan` è la differenza tra 3 millisecondi e 30 secondi.

I piani veri del database del progetto (ottenuti con `SET SHOWPLAN_TEXT ON`, cioè quello che SSMS disegna):

**1. Cerca per chiave primaria → `Seek`, il caso buono:**

```sql
SELECT Title FROM Movies WHERE Id = 3;
```
```
|--Clustered Index Seek(OBJECT:([MovieManagerDb].[dbo].[Movies].[PK_Movies]),
                        SEEK:([Movies].[Id]=CONVERT_IMPLICIT(int,[@1],0)) ORDERED FORWARD)
```

Usa `PK_Movies` e va dritto alla riga. È la query che fa `FindAsync(3)` del repository ([cap. 12](12-dal-controller-all-sql.md)).

**2. Cerca per una colonna senza indice → `Scan`:**

```sql
SELECT Title FROM Movies WHERE Language = 'Inglese';
```
```
|--Clustered Index Scan(OBJECT:([MovieManagerDb].[dbo].[Movies].[PK_Movies]))
```

**`Scan`, non `Seek`.** Su `Language` non c'è nessun indice — né io né EF l'abbiamo creato — quindi SQL Server non ha scorciatoie: legge tutti i film e tiene quelli in inglese. Su questa tabella è la scelta giusta (con 6 righe, leggerle tutte costa meno che consultare un indice). Su una tabella grande, invece, questo `Scan` è il segnale che manca un indice.

**3. Una JOIN → `Scan` + `Seek`, e si vede l'indice di EF al lavoro:**

```sql
SELECT m.Title, g.Name FROM Movies m JOIN Genres g ON g.Id = m.GenreId;
```
```
|--Nested Loops(Inner Join, OUTER REFERENCES:([m].[GenreId]))
     |--Clustered Index Scan(OBJECT:([Movies].[PK_Movies] AS [m]))
     |--Clustered Index Seek(OBJECT:([Genres].[PK_Genres] AS [g]),
                             SEEK:([g].[Id]=[Movies].[GenreId]) ORDERED FORWARD)
```

Si legge così: SQL Server **scandisce** i film (li vuole tutti, quindi giusto così) e per ognuno fa un **`Seek`** su `Genres` usando la chiave primaria. È il `Nested Loops`: per ogni riga di sinistra, una ricerca puntuale a destra. Il `Seek` funziona perché `Genres.Id` è indicizzato — ed è **il motivo per cui le foreign key vanno indicizzate**, cosa che EF ha fatto per me senza dirmelo.

### `STATISTICS IO`: il numero che non mente

Il piano dice *come*; `SET STATISTICS IO ON` dice *quanto*.

```sql
SET STATISTICS IO ON;
SELECT m.Title, g.Name FROM Movies m JOIN Genres g ON g.Id = m.GenreId;
```
```
Table 'Genres'. Scan count 0, logical reads 12, physical reads 1, ...
Table 'Movies'. Scan count 1, logical reads 3,  physical reads 1, ...
```

**`logical reads`** è il numero da guardare: quante pagine da 8 KB SQL Server ha dovuto leggere. È la misura onesta del costo di una query, molto più del tempo — perché il tempo dipende dalla cache, dal carico, dalla macchina, mentre le letture logiche no. Quando si ottimizza una query, il metro è questo: si cambia qualcosa e si guarda se `logical reads` scende.

Curiosità che si legge in questi numeri: `Genres` ha **12** letture logiche contro le 3 di `Movies`, pur essendo più piccola. È il `Nested Loops`: la tabella dei generi viene consultata **una volta per ogni film**.

---

## 14.6 Backup e restore: la cosa che al lavoro conta più di tutte

In locale il database è usa e getta. Al lavoro no, e il backup è l'unica cosa che sta tra un errore e un disastro.

Da SSMS: tasto destro sul database → `Tasks` → `Back Up...` / `Restore` → `Database...`. Sotto, in T-SQL — verificato su `MovieManagerDb`:

```sql
-- BACKUP completo
BACKUP DATABASE MovieManagerDb
TO DISK = N'C:\temp\MovieManagerDb.bak'
WITH INIT, FORMAT;
```
```
Processed 512 pages for database 'MovieManagerDb', file 'MovieManagerDb' on file 1.
Processed 2 pages for database 'MovieManagerDb', file 'MovieManagerDb_log' on file 1.
BACKUP DATABASE successfully processed 514 pages in 0.012 seconds (334.309 MB/sec).
```

Ne è uscito un file da **4,1 MB**. `WITH INIT` sovrascrive il file invece di accodarsi (senza, un `.bak` cresce all'infinito accumulando backup, e chi lo ripristina prende per sbaglio il primo).

```sql
-- Cosa c'è dentro un .bak, prima di ripristinarlo
RESTORE FILELISTONLY FROM DISK = N'C:\temp\MovieManagerDb.bak';

-- RESTORE con un nome NUOVO: non tocca l'originale
RESTORE DATABASE MovieManagerDb_Restored
FROM DISK = N'C:\temp\MovieManagerDb.bak'
WITH MOVE 'MovieManagerDb'     TO N'C:\temp\r.mdf',
     MOVE 'MovieManagerDb_log' TO N'C:\temp\r.ldf';
```
```
RESTORE DATABASE successfully processed 514 pages in 0.007 seconds.
-- e nel database ripristinato: 6 film, tutti al loro posto
```

**`WITH MOVE` è il pezzo che si dimentica sempre.** Un `.bak` si porta dietro i percorsi dei file originali; ripristinandolo con un nome nuovo sulla stessa istanza, senza `MOVE`, SQL Server prova a scrivere sui file dell'originale e fallisce. `MOVE` dice dove mettere i file nuovi, e i nomi logici da usare (`MovieManagerDb`, `MovieManagerDb_log`) sono quelli che restituisce `RESTORE FILELISTONLY`.

> **Ripristinare con un nome diverso è anche la tecnica giusta per recuperare un solo dato.** Se qualcuno cancella una riga in produzione, non si ripristina *sopra* il database vivo — si ripristina **accanto**, con un altro nome, si tira fuori la riga con una `SELECT` e la si reinserisce. Ripristinare sopra significherebbe buttare via tutto quello che è successo dopo il backup.

Un backup vale zero finché non è stato ripristinato almeno una volta. È una frase da manuale, ed è vera: un `.bak` corrotto sembra identico a uno buono finché non serve.

---

## 14.7 Chi sta facendo cosa sul server

Al lavoro non si è mai soli sul database, e quando "l'applicazione è lenta" la domanda è chi la sta bloccando. In SSMS: tasto destro sul server → `Activity Monitor`. In T-SQL:

```sql
-- chi è collegato
SELECT session_id, login_name, DB_NAME(database_id) AS DbName, status
FROM sys.dm_exec_sessions
WHERE is_user_process = 1;
```
```
session_id  login_name  DbName  status
85          ANTO\prodo  master  running
```

```sql
-- chi sta bloccando chi  <- la query che risolve il 90% dei "va lento"
SELECT r.session_id, r.status, r.wait_type, r.blocking_session_id, t.text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id <> @@SPID;

-- il vecchio classico, più leggibile
EXEC sp_who2;
```

`blocking_session_id` diverso da 0 significa che quella sessione è **ferma ad aspettarne un'altra**: qualcuno ha aperto una transazione e non l'ha chiusa. È la causa più comune di un'applicazione che "si è piantata" senza errori — e si aggancia direttamente al discorso delle transazioni del [capitolo 10](10-database-sql-server.md): una `BEGIN TRANSACTION` senza `COMMIT` né `ROLLBACK` tiene i lock finché la sessione non muore.

> ⚠️ In SSMS è facilissimo lasciare una transazione aperta: si esegue `BEGIN TRANSACTION` per fare una prova, ci si distrae, e intanto si stanno bloccando i colleghi. `SELECT @@TRANCOUNT;` dice se ce n'è una aperta nella mia sessione: se risponde un numero diverso da `0`, c'è qualcosa da chiudere.

---

## 14.8 Login, utenti e permessi

L'altra cosa che in locale non esiste e al lavoro è quotidiana. In SSMS stanno in `Security` → `Logins` (a livello di server) e in `MovieManagerDb` → `Security` → `Users` (dentro il database).

**Login e utente sono due cose diverse**, ed è la confusione classica:

- Il **login** è a livello di **server**: è chi può *entrare*.
- L'**utente** è a livello di **database**: è cosa può *fare* lì dentro.

Un login senza utente entra ma non vede niente. Servono entrambi:

```sql
-- 1. il login: può entrare nel server
CREATE LOGIN movieapp WITH PASSWORD = 'UnaPasswordSolida!123';

-- 2. l'utente: esiste dentro questo database
USE MovieManagerDb;
CREATE USER movieapp FOR LOGIN movieapp;

-- 3. i permessi: cosa può fare
ALTER ROLE db_datareader ADD MEMBER movieapp;   -- SELECT su tutto
ALTER ROLE db_datawriter ADD MEMBER movieapp;   -- INSERT/UPDATE/DELETE su tutto
```

I ruoli che si incontrano sempre:

| Ruolo | Cosa dà |
|-------|---------|
| `db_datareader` | leggere tutte le tabelle |
| `db_datawriter` | scrivere tutte le tabelle |
| `db_ddladmin` | creare/modificare tabelle (`ALTER TABLE`) |
| `db_owner` | tutto, dentro questo database |
| `sysadmin` (server) | **tutto tutto**. Fuori discussione per un'applicazione |

> ⚠️ **`db_datareader` + `db_datawriter` non bastano per `EnsureCreated()` né per le migration**, che devono creare tabelle e vogliono `db_ddladmin`. La tentazione è dare `db_owner` all'applicazione e chiudere la questione: è esattamente la scelta sbagliata. Un'app che serve richieste HTTP ha bisogno di leggere e scrivere righe, non di poter cancellare tabelle — e se qualcuno trova una SQL injection, la differenza tra i due permessi è la differenza tra dei dati rubati e il database azzerato. È il motivo per cui in produzione **lo schema si applica prima del deploy, con un account diverso** ([cap. 13](13-migrations.md)).

Per vedere chi può fare cosa:

```sql
SELECT dp.name AS Utente, dp.type_desc AS Tipo, r.name AS Ruolo
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members rm ON rm.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals r     ON r.principal_id = rm.role_principal_id
WHERE dp.type IN ('S', 'U');
```

---

## 14.9 Collegare MovieManager a SQL Server

È **una riga** di `appsettings.json`. Il codice C# non cambia: `Program.cs` legge la stringa dalla configurazione e `UseSqlServer` parla con qualunque edizione ([cap. 9](09-plapi-program-di-scalar.md)). È il vantaggio concreto dell'architettura a strati.

```json
// prima (LocalDB)
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=MovieManagerDb;Trusted_Connection=True;TrustServerCertificate=True"

// dopo (SQL Server Express in locale)
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=MovieManagerDb;Trusted_Connection=True;TrustServerCertificate=True"
```

Poi `dotnet run`: `EnsureCreated()` crea il database sulla nuova istanza e il seeder lo ripopola. Tutte le stringhe per ogni scenario sono nel [capitolo 10](10-database-sql-server.md) e in [`COMANDI.txt`](../COMANDI.txt).

> ⚠️ **La password non va in `appsettings.json`**, che finisce su Git. In sviluppo si usano gli **User Secrets**, che salvano fuori dalla cartella del progetto:
>
> ```powershell
> dotnet user-secrets init --project MovieManager.PL.API
> dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Password=..." --project MovieManager.PL.API
> ```
>
> In produzione: variabili d'ambiente o un gestore di segreti. Con `Trusted_Connection=True` il problema non si pone — nella stringa non c'è nessun segreto — ed è un motivo in più per preferire l'autenticazione Windows quando si può.

### Le prime tre cose da guardare quando l'app non si collega

| Sintomo | Causa quasi sempre |
|---------|--------------------|
| `A network-related or instance-specific error...` | nome dell'istanza sbagliato, o servizio fermo: `Get-Service *MSSQL*` |
| `Login failed for user '...'` | il login esiste ma manca l'**utente** nel database (sezione 14.8) |
| `The certificate chain was issued by an authority that is not trusted` | manca `TrustServerCertificate=True` |
| Funziona in locale, non da un'altra macchina | TCP/IP disabilitato, o la porta 1433 chiusa nel firewall |

L'ultimo è quello che fa perdere il pomeriggio: su SQL Server Express il protocollo **TCP/IP è disattivato** di default. Si abilita da `SQL Server Configuration Manager` → `SQL Server Network Configuration` → `Protocols` → `TCP/IP` → `Enabled`, e poi **va riavviato il servizio**. In locale non serve (si passa da memoria condivisa), quindi il problema si manifesta solo quando qualcun altro prova a collegarsi — cioè sempre troppo tardi.

---

## 14.10 Quale strumento, quando

| Strumento | Quando conviene |
|-----------|-----------------|
| **SSMS** | il lavoro vero: piani di esecuzione, backup, permessi, Activity Monitor. Solo Windows |
| **sqlcmd** | script, automazioni, pipeline, e quando c'è solo un terminale. È quello del [cap. 10](10-database-sql-server.md) |
| **SQL Server Object Explorer** (Visual Studio) | una sbirciata veloce senza uscire dall'IDE. Molto più limitato di SSMS |
| **Azure Data Studio / VS Code + estensione mssql** | multipiattaforma, leggero. Bene per le query, non per l'amministrazione |
| **Scalar / l'API** | quando voglio provare **l'applicazione**, non il database ([cap. 11](11-scalar-e-prova-api.md)) |

L'ultima riga è la distinzione da tenere ferma. Quando qualcosa non funziona, la domanda è sempre: **è l'API o è il database?** Si prova la stessa cosa da Scalar e da SSMS, e la risposta arriva subito. Se la `SELECT` in SSMS torna il dato giusto e l'API no, il problema è nel mio codice; se anche SSMS non lo trova, il dato non c'è e ho sbagliato altrove.

---

## Verifica finale

1. **Devo installare SQL Server per usare SSMS?** No. `(localdb)\MSSQLLocalDB` come server name e si lavora subito su `MovieManagerDb`.
2. **Login e utente sono la stessa cosa?** No: il login entra nel *server*, l'utente esiste nel *database*. Servono entrambi.
3. **Cosa guardo per primo in un piano di esecuzione?** `Seek` o `Scan`.
4. **Qual è la misura onesta del costo di una query?** `logical reads`, non il tempo.
5. **Posso cambiare una colonna da `Design`?** In un progetto con EF, **no**: lo schema si cambia dalle entità ([cap. 13](13-migrations.md)).
6. **Perché `WITH MOVE` nel restore?** Perché il `.bak` contiene i percorsi originali e senza `MOVE` va a sbattere contro i file del database vivo.
7. **Che permessi do all'applicazione?** `db_datareader` + `db_datawriter`. Mai `db_owner`, mai `sysadmin`.

[⬅ Torna all'indice](../README.md)
