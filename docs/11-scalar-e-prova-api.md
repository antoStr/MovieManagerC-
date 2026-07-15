# 11) Scalar — provare le API dal browser

[⬅ Torna all'indice](../README.md)

Questo progetto non ha pagine HTML: niente JSP, niente form. L'unica interfaccia è **Scalar**, e non è un ripiego — è il modo normale di lavorare con una Web API. Il [capitolo 9](09-plapi-program-di-scalar.md) ha spiegato **come si configura** (`AddOpenApi` + `MapScalarApiReference`); questo capitolo spiega **come si usa** e, soprattutto, come leggere quello che mostra.

---

## (?) Ripasso in due righe: OpenAPI e Scalar

- **OpenAPI** è il *documento*: un JSON che descrive l'API (endpoint, parametri, schemi, risposte). Lo genera .NET 10 da solo, leggendo i controller.
- **Scalar** è la *interfaccia*: legge quel JSON e ne fa una pagina navigabile dove posso lanciare richieste vere.

Il punto che spiega tutto il resto: **Scalar non legge il mio codice, legge il documento OpenAPI**. Tutto quello che vedo nella pagina è arrivato lì attraverso quel JSON. Se una cosa non compare in Scalar, il problema non è Scalar: è che non è finita nell'OpenAPI.

---

## 11.1 Aprire Scalar

Avvio l'applicazione:

```powershell
cd "C:\Workspace\EsercizioC#Avanzato\MovieManager"
dotnet run --project MovieManager.PL.API
```

e apro **`http://localhost:5140/scalar`**.

> ⚠️ **Attenzione alla porta.** In `launchSettings.json` ci sono due profili: `http` (porta **5140**) e `https` (porta **7109** + 5140). `dotnet run` senza argomenti usa il **primo profilo**, cioè `http`, e apre `http://localhost:5140/scalar`. Per usare l'altro serve `dotnet run --launch-profile https`. Se cerco la 7109 dopo un `dotnet run` semplice, non trovo niente — e nel log compare `Failed to determine the https port for redirect`, che è innocuo e ha esattamente questa spiegazione.

Scalar e OpenAPI sono esposti **solo in sviluppo** (`if (app.Environment.IsDevelopment())`): in produzione la documentazione interattiva non deve essere pubblica.

---

## 11.2 Come è fatta la pagina

A sinistra gli endpoint, raggruppati per **tag**. I tag arrivano dal nome del controller: `ActorsController` → tag `Actors`. Sono i 13 percorsi del progetto, per un totale di **30 operazioni**:

```
/api/Actors                            GET  POST
/api/Actors/{id}                       GET  PUT  DELETE
/api/Directors                         GET  POST
/api/Directors/{id}                    GET  PUT  DELETE
/api/Genres                            GET  POST
/api/Genres/{id}                       GET  PUT  DELETE
/api/Movies                            GET  POST
/api/Movies/{id}                       GET  PUT  DELETE
/api/Reviews                           GET  POST
/api/Reviews/{id}                      GET  PUT  DELETE
/api/MovieActors                       POST
/api/MovieActors/movie/{movieId}       GET
/api/MovieActors/{movieId}/{actorId}   GET  PUT  DELETE
```

Cinque entità con lo stesso identico schema CRUD (è il `GenericService` che si vede da fuori), più `MovieActors` che fa storia a sé: rotte diverse perché la chiave è la **coppia** `(movieId, actorId)` e non un `Id` singolo. L'architettura del progetto è leggibile direttamente dalla barra laterale.

Selezionando un endpoint vedo: il verbo e il percorso, i parametri, lo schema del body, e i codici di risposta possibili — quelli dichiarati con `ProducesResponseType` nei controller ([capitolo 8](08-plapi-controllers.md)).

---

## 11.3 Lanciare una richiesta

1. Scelgo l'endpoint nella barra a sinistra.
2. Premo **"Test Request"**.
3. Compilo i parametri di percorso (es. `id`) e/o il body JSON.
4. Premo **"Send"**.
5. Leggo sotto: **status**, body della risposta, header.

Provo `GET /api/Movies`: risponde `200` con i 6 film del seeder. Se il database fosse vuoto risponderebbe `200 []` — array vuoto, non un errore: "nessun film" è una risposta legittima, non un `404`.

---

## 11.4 La trappola del campo `id` nel body

Questa merita un paragrafo perché mi è costata un errore vero.

Scalar **precompila il body di esempio** partendo dallo schema OpenAPI, e per un `integer` ci mette un numero. Quindi il body suggerito per una POST contiene anche `"id"`. Mandandolo così com'è, con un `id` diverso da zero, ottenevo:

```
Cannot insert explicit value for identity column in table 'Actors'
when IDENTITY_INSERT is set to OFF.
```

Il motivo sta nel database, non in Scalar: `Id` è una colonna **IDENTITY** e il valore lo assegna SQL Server ([capitolo 10](10-database-sql-server.md)). Ora `GenericService.CreateAsync` azzera l'`id` in arrivo dal client, quindi il campo viene **ignorato**: posso lasciarlo a 0, cancellarlo o scriverci qualsiasi numero, e la riga viene creata comunque con l'Id deciso dal database.

L'unica eccezione è `MovieActors`: lì `movieId` e `actorId` **non** sono generati, sono la chiave che scelgo io e devono puntare a righe esistenti.

> **La lezione generale:** il body precompilato da Scalar è un *segnaposto*, non un esempio valido. Serve a mostrare la forma del JSON, non a essere spedito così com'è.

---

## 11.5 Scalar conosce i vincoli (perché stanno nell'OpenAPI)

Questa è la parte che lega tutto. Le **DataAnnotations** messe sui model del BLL ([capitolo 5](05-bll-models.md)) non servono solo a validare: finiscono **dentro il documento OpenAPI** e quindi Scalar le mostra.

`[Required]` e `[StringLength(100)]` su `ActorModel` diventano, nel JSON generato:

```json
"ActorModel": {
  "required": [ "firstName", "lastName" ],
  "properties": {
    "firstName": { "type": "string", "maxLength": 100 },
    "lastName":  { "type": "string", "maxLength": 100 },
    "birthDate": { "type": ["null", "string"], "format": "date" }
  }
}
```

E `[Range(1, 10)]` su `ReviewModel.Score` diventa:

```json
"score": { "type": "integer", "format": "int32", "minimum": 1, "maximum": 10 }
```

Risultato: in Scalar i campi obbligatori sono segnalati come tali e i limiti sono visibili **prima** di inviare. Un solo attributo in C# ottiene tre cose insieme: la validazione a runtime, la documentazione dell'API e l'aiuto nell'interfaccia. È il motivo per cui vale la pena metterli anche in un progetto piccolo.

Nota che `birthDate` è dichiarato `"format": "date"`: è la traduzione di `DateOnly?`. Per questo la data va scritta `"1995-12-27"` e non con l'ora.

---

## 11.6 Leggere i codici di risposta

| Codice | Quando | Da dove arriva |
|--------|--------|----------------|
| `200 OK` | GET riuscita | `Ok(...)` nel controller |
| `201 Created` | POST riuscita; il body contiene la riga creata con il suo Id | `CreatedAtAction(...)` |
| `204 No Content` | PUT o DELETE riuscite. **Nessun body: è normale** | `NoContent()` |
| `400 Bad Request` | richiesta non valida | validazione dei model, oppure id discordante nella PUT |
| `404 Not Found` | l'id richiesto non esiste | `NotFound()` |
| `409 Conflict` | riga già esistente (coppia MovieActors duplicata) | `DatabaseExceptionHandler` |
| `500` | errore vero del server | ormai raro: le violazioni di vincolo sono tradotte in 400/409 |

Un `400` da validazione arriva con il dettaglio campo per campo. Mandando `{}` su `POST /api/Actors`:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "FirstName": [ "Il nome è obbligatorio." ],
    "LastName":  [ "Il cognome è obbligatorio." ]
  }
}
```

Questo formato lo genera automaticamente `[ApiController]` quando la validazione fallisce: nel controller non c'è una riga di codice che lo faccia.

Un `400` dal database, invece, arriva dal `DatabaseExceptionHandler` (`Configurations/DatabaseExceptionHandler.cs`). Serve perché alcuni errori si possono scoprire **solo** provando a scrivere: che `genreId = 999` non esista lo sa il database, non il model. Senza quel gestore, una richiesta sbagliata tornerebbe come `500`, facendo sembrare un bug del server quello che è un errore del client.

---

## 11.7 Il documento OpenAPI sotto la superficie

Vale la pena aprirlo almeno una volta: **`http://localhost:5140/openapi/v1.json`**.

```powershell
$o = Invoke-RestMethod "http://localhost:5140/openapi/v1.json"
$o.openapi                          # 3.1.1
$o.info.title                       # MovieManager.PL.API | v1
$o.paths.PSObject.Properties.Name   # tutti i percorsi
$o.components.schemas.ActorModel    # lo schema di un model
```

Il documento ha due parti che contano:

- **`paths`** — i percorsi e, per ognuno, i verbi con parametri e risposte. Nasce dai controller e dai loro attributi.
- **`components.schemas`** — gli schemi dei model (`ActorModel`, `MovieModel`, ...). Nascono dalle classi del BLL e dalle loro DataAnnotations.

Ed è qui che si chiude il cerchio dell'architettura a strati: la classe C# del BLL → lo schema OpenAPI → il form in Scalar. Se rinomino una proprietà del model, la modifica arriva fino alla pagina web **senza toccare niente altro**, perché nessuno dei tre passaggi è scritto a mano.

---

## 11.8 Un giro CRUD completo da Scalar

L'ordine giusto per creare un film da zero, tenendo conto delle dipendenze:

1. **`POST /api/Genres`** → `{ "name": "Documentario" }` → mi segno l'`id` dalla risposta.
2. **`POST /api/Directors`** → `{ "firstName": "Sofia", "lastName": "Coppola" }` → `id`.
3. **`POST /api/Movies`** → uso i due id appena ottenuti in `genreId` e `directorId`.
4. **`POST /api/MovieActors`** → collego un attore al film: `movieId` + `actorId` esistenti.
5. **`GET /api/MovieActors/movie/{movieId}`** → verifico il cast.
6. **`PUT /api/Movies/{id}`** → modifico il film.
7. **`DELETE /api/Movies/{id}`** → lo elimino.

Due cose da sapere in questo giro:

- **L'ordine non è negoziabile.** Un film ha bisogno di un genere e di un regista che esistano già: `genreId` inventato → `400` dal gestore delle eccezioni. Le chiavi esterne impongono la sequenza.
- **La PUT sostituisce tutto.** I campi che ometto nel body diventano `null`/0, non restano al valore precedente. È la semantica di PUT (sostituzione della risorsa), non un bug. Metodo sicuro: prima una `GET`, copio il JSON della risposta, cambio quello che mi serve e lo rimando indietro intero.
- **La DELETE cancella a cascata.** Cancellare un film si porta via le sue recensioni e il suo cast; cancellare un *genere* si porta via i film di quel genere e, con loro, recensioni e cast. Vale la pena leggere la sezione 10.12 del [capitolo 10](10-database-sql-server.md) prima di premere Send.

---

## 11.9 Scalar, PowerShell o sqlcmd?

Tre strumenti, tre lavori diversi. Mi è utile tenerli distinti:

| Strumento | Passa da | Serve per |
|-----------|----------|-----------|
| **Scalar** | tutta la pipeline HTTP | esplorare l'API, provare a mano, vedere gli schemi. Ottimo per capire |
| **PowerShell** (`Invoke-RestMethod`) | tutta la pipeline HTTP | ripetere, automatizzare, concatenare più chiamate in uno script |
| **sqlcmd** | **niente**: parla col database | controllare com'è davvero il dato, indagare quando l'API si comporta in modo strano |

La distinzione importante è l'ultima riga: `sqlcmd` **salta l'applicazione**. Se una POST fallisce, guardare con `sqlcmd` cosa c'è davvero nella tabella è il modo più rapido per capire se il problema è nell'API o nei dati. Ed è così che ho scoperto che l'errore iniziale non era nei controller ma in una colonna IDENTITY.

Tutti i comandi pronti per Scalar, PowerShell e sqlcmd stanno in [`COMANDI.txt`](../COMANDI.txt).

---

## Verifica finale

- `http://localhost:5140/scalar` mostra i 6 gruppi (Actors, Directors, Genres, Movies, MovieActors, Reviews).
- `http://localhost:5140/openapi/v1.json` restituisce il documento (13 percorsi, 30 operazioni).
- Una POST con body `{}` risponde `400` con i messaggi per campo.
- Una POST valida risponde `201` con l'`id` assegnato dal database.

[⬅ Torna all'indice](../README.md)
