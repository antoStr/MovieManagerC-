# 8) PL — I Controller API

[⬅ Torna all'indice](../README.md)

Siamo al livello che parla col mondo esterno. I **controller** ricevono le richieste HTTP, chiamano il service giusto e restituiscono una risposta HTTP. Vanno in `MovieManager.PL.API/Controllers`.

---

## (?) Che cosa è un controller API?

Un controller è una classe che raggruppa gli **endpoint** (gli indirizzi) di una risorsa. `MoviesController` gestisce tutto ciò che riguarda `/api/movies`. È il corrispondente della **servlet** del gestionale Java, ma molto più ordinato: invece di uno `switch` su un parametro `action` dentro `doGet`/`doPost`, qui ogni operazione è un **metodo separato** con la sua rotta e il suo verbo HTTP.

La regola d'oro: i controller devono restare **sottili**. Nessuna logica di business qui dentro; tutto delegato al service. Il controller si occupa solo di HTTP.

---

## 8.1 Gli attributi in cima al controller

```csharp
using Microsoft.AspNetCore.Mvc;
using MovieManager.BLL.Models;
using MovieManager.BLL.Services.Interfaces;

namespace MovieManager.PL.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MoviesController : ControllerBase
    {
        private readonly IGenericService<MovieModel> _service;

        public MoviesController(IGenericService<MovieModel> service)
        {
            _service = service;
        }
        // ... endpoint ...
    }
}
```

- **`[ApiController]`** — attiva i comportamenti tipici delle API: validazione automatica del model, risposte d'errore standard, binding dei parametri più intelligente.
- **`[Route("api/[controller]")]`** — definisce la rotta base. Il segnaposto `[controller]` viene sostituito col nome del controller senza il suffisso "Controller": `MoviesController` → `api/movies`. Comodo, perché la rotta segue automaticamente il nome della classe.
- **`[Produces("application/json")]`** — dichiara che la risposta è JSON.
- **Costruttore** — riceve `IGenericService<MovieModel>` via **Dependency Injection**. Il controller non sa (e non deve sapere) quale implementazione arriva: chiede l'interfaccia e basta.

---

## 8.2 Gli endpoint CRUD

Ogni operazione è un metodo asincrono con il suo verbo HTTP e i suoi status code documentati con `ProducesResponseType`.

```csharp
[HttpGet]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<ActionResult<IReadOnlyList<MovieModel>>> GetAll(CancellationToken cancellationToken)
    => Ok(await _service.GetAllAsync(cancellationToken));

[HttpGet("{id:int}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<MovieModel>> GetById(int id, CancellationToken cancellationToken)
{
    var model = await _service.GetByIdAsync(id, cancellationToken);
    return model is null ? NotFound() : Ok(model);
}

[HttpPost]
[ProducesResponseType(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<ActionResult<MovieModel>> Create(MovieModel model, CancellationToken cancellationToken)
{
    var created = await _service.CreateAsync(model, cancellationToken);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
}

[HttpPut("{id:int}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Update(int id, MovieModel model, CancellationToken cancellationToken)
{
    if (id != model.Id)
        return BadRequest();

    var updated = await _service.UpdateAsync(model, cancellationToken);
    return updated ? NoContent() : NotFound();
}

[HttpDelete("{id:int}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
{
    var deleted = await _service.DeleteAsync(id, cancellationToken);
    return deleted ? NoContent() : NotFound();
}
```

### (?) Cosa sono gli status code HTTP e `ProducesResponseType`?

Gli **status code** sono il modo standard con cui un'API dice com'è andata:

| Codice | Significato | Quando |
|--------|-------------|--------|
| `200 OK` | tutto ok, ecco i dati | GET riuscita |
| `201 Created` | risorsa creata | POST riuscita |
| `204 No Content` | ok, niente da restituire | PUT/DELETE riuscita |
| `400 Bad Request` | richiesta malformata | dati non validi |
| `404 Not Found` | risorsa inesistente | id non trovato |

Gli attributi `[ProducesResponseType(...)]` **non cambiano** il comportamento: servono a **documentare** quali risposte un endpoint può dare. Questa informazione finisce nel documento OpenAPI e quindi nella UI Scalar, che mostrerà all'utente tutti i possibili esiti.

### Due dettagli utili

- **`CreatedAtAction`** (nel POST) restituisce `201` **e** aggiunge nell'header `Location` l'URL dove ritrovare la risorsa appena creata (`GET /api/movies/{id}`). È la buona pratica REST per le creazioni.
- **Coerenza rotta/body** (nel PUT): controllo che l'`id` nell'URL coincida con `model.Id`. Se non coincidono restituisco `400`, perché la richiesta è ambigua (a quale record mi riferisco?). È l'equivalente pulito dei controlli che facevo a mano nella servlet.

Gli altri controller a chiave singola — `GenresController`, `DirectorsController`, `ActorsController`, `ReviewsController` — sono **identici** a questo: cambia solo il tipo di model iniettato (`IGenericService<GenreModel>`, ecc.). È il vantaggio del service generico: il controller diventa un guscio uniforme.

---

## 8.3 Il controller speciale: MovieActorsController

Per la tabella ponte a chiave composta serve un controller diverso, che usa `IMovieActorService` e ha rotte con **due** parametri:

```csharp
// Controller dedicato alla tabella ponte MovieActor: chiave composta (movieId, actorId),
// quindi non usa IGenericService ma il servizio custom IMovieActorService.
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MovieActorsController : ControllerBase
{
    private readonly IMovieActorService _service;

    public MovieActorsController(IMovieActorService service)
    {
        _service = service;
    }

    // GET api/movieactors/movie/5  -> tutte le associazioni (attori) di un film
    [HttpGet("movie/{movieId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MovieActorModel>>> GetByMovie(int movieId, CancellationToken cancellationToken)
        => Ok(await _service.GetByMovieIdAsync(movieId, cancellationToken));

    // GET api/movieactors/5/8  -> singola associazione film-attore
    [HttpGet("{movieId:int}/{actorId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieActorModel>> GetByIds(int movieId, int actorId, CancellationToken cancellationToken)
    {
        var model = await _service.GetByIdsAsync(movieId, actorId, cancellationToken);
        return model is null ? NotFound() : Ok(model);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MovieActorModel>> Create(MovieActorModel model, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(model, cancellationToken);
        return CreatedAtAction(nameof(GetByIds), new { movieId = created.MovieId, actorId = created.ActorId }, created);
    }

    [HttpPut("{movieId:int}/{actorId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int movieId, int actorId, MovieActorModel model, CancellationToken cancellationToken)
    {
        if (movieId != model.MovieId || actorId != model.ActorId)
            return BadRequest();

        var updated = await _service.UpdateAsync(model, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{movieId:int}/{actorId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int movieId, int actorId, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(movieId, actorId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
```

Le rotte riflettono la chiave composta: `api/movieactors/{movieId}/{actorId}` per la singola associazione, e `api/movieactors/movie/{movieId}` per elencare tutti gli attori di un film. Il controllo di coerenza nel PUT qui verifica **entrambe** le chiavi.

I `ProducesResponseType` ci sono come negli altri controller, e servono allo stesso scopo: far finire i codici di risposta nel documento OpenAPI, e da lì in Scalar ([capitolo 11](11-scalar-e-prova-api.md)). L'unica differenza è che qui ce ne sono **due** di rotte per la stessa risorsa, perché la chiave è doppia.

[➡ Prossima parte: PL — Program.cs, DI e Scalar](09-plapi-program-di-scalar.md)
