# 6) BLL — Generic Service, MovieActorService, async/await

[⬅ Torna all'indice](../README.md)

I model sono pronti; ora serve chi ci lavora sopra: i **service**. Sono il cuore del Business Logic Layer e vanno in `MovieManager.BLL/Services`.

---

## (?) Che cosa fa un service nel BLL?

Il service è l'intermediario tra i controller (che parlano HTTP) e i repository (che parlano col database). Le sue responsabilità:

1. esporre le operazioni applicative (qui il CRUD);
2. usare repository e unit of work senza esporli ai controller;
3. lavorare con i **model** del BLL, non con le entità del DAL;
4. convertire model ↔ entità tramite AutoMapper.

Come per i repository, invece di scrivere un service per entità, ne scrivo **uno generico** che vale per tutte.

---

## 6.1 L'interfaccia IGenericService

File: `Services/Interfaces/IGenericService.cs`:

```csharp
namespace MovieManager.BLL.Services.Interfaces
{
    public interface IGenericService<TModel> where TModel : class
    {
        Task<TModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TModel>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(TModel model, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
```

Il tipo di ritorno **`bool`** di `UpdateAsync`/`DeleteAsync` ha un significato applicativo preciso:

- `true` → operazione eseguita;
- `false` → record non trovato.

Sarà il controller a tradurre questo `bool` in uno status HTTP (`204` oppure `404`).

---

## 6.2 L'implementazione GenericService

File: `Services/GenericService.cs`. Qui i generics diventano **due**: uno per l'entità del DAL, uno per il model del BLL.

```csharp
using AutoMapper;
using MovieManager.BLL.Models;
using MovieManager.BLL.Services.Interfaces;
using MovieManager.DAL.Repositories.Interfaces;

namespace MovieManager.BLL.Services
{
    public class GenericService<TEntity, TModel> : IGenericService<TModel>
        where TEntity : class, new()
        where TModel : class, IModelWithId, new()
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGenericRepository<TEntity> _repository;
        private readonly IMapper _mapper;

        public GenericService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _repository = unitOfWork.Repository<TEntity>();
            _mapper = mapper;
        }
        // ... metodi ...
    }
}
```

### (?) Cosa significano i vincoli generici?

- `where TEntity : class, new()` → `TEntity` è una classe (reference type) e ha un **costruttore vuoto** (`new()`), così AutoMapper può istanziarla.
- `where TModel : class, IModelWithId, new()` → `TModel` è una classe, ha il costruttore vuoto **e** implementa `IModelWithId`. Grazie a quest'ultimo posso scrivere `model.Id` in sicurezza (vedi [capitolo 5](05-bll-models.md)).

### Le dipendenze del costruttore

Il service riceve via DI la **Unit of Work** e il **mapper**. Il repository non lo riceve direttamente: lo chiede alla UoW con `unitOfWork.Repository<TEntity>()`, così tutto passa dallo stesso `DbContext` e il salvataggio resta centralizzato.

### I metodi, uno per uno

```csharp
public async Task<TModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
{
    var entity = await _repository.GetByIdAsync(id, cancellationToken);
    return entity is null ? null : _mapper.Map<TModel>(entity);   // entità -> model
}

public async Task<IReadOnlyList<TModel>> GetAllAsync(CancellationToken cancellationToken = default)
{
    var entities = await _repository.GetAllAsync(cancellationToken);
    return _mapper.Map<IReadOnlyList<TModel>>(entities);
}

public async Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default)
{
    var entity = _mapper.Map<TEntity>(model);        // model -> entità
    await _repository.AddAsync(entity, cancellationToken);
    await _unitOfWork.SaveChangesAsync(cancellationToken);   // il salvataggio è QUI
    return _mapper.Map<TModel>(entity);              // rileggo l'Id generato dal DB
}

public async Task<bool> UpdateAsync(TModel model, CancellationToken cancellationToken = default)
{
    var existing = await _repository.GetByIdAsync(model.Id, cancellationToken);
    if (existing is null) return false;              // non trovato

    _mapper.Map(model, existing);                    // aggiorno l'entità GIÀ tracciata
    _repository.Update(existing);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    return true;
}

public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
{
    var entity = await _repository.GetByIdAsync(id, cancellationToken);
    if (entity is null) return false;

    _repository.Remove(entity);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    return true;
}
```

Il punto architetturale chiave: **il `SaveChangesAsync` è sempre nel service**, mai nel repository. Il repository prepara la modifica, il service decide l'esito (`true`/`false`) e la Unit of Work conferma con una transazione.

> **Nota sul disallineamento delle firme.** Nel repository `Update`/`Remove` sono `void`; nel service `UpdateAsync`/`DeleteAsync` ritornano `bool`. Non è un errore: nel repository sono comandi sul change tracker di EF (non sanno se il record "esisteva"), mentre nel service il `bool` è una **regola applicativa** (prima controllo se il record c'è, poi agisco). Sono due livelli con responsabilità diverse.

---

## 6.3 Il servizio dedicato: MovieActorService

Il `GenericService` funziona solo con model `IModelWithId` a chiave singola. `MovieActorModel` non lo è, quindi ha il suo servizio dedicato, gemello del suo repository dedicato.

`Services/Interfaces/IMovieActorService.cs`:

```csharp
using MovieManager.BLL.Models;

namespace MovieManager.BLL.Services.Interfaces
{
    public interface IMovieActorService
    {
        Task<MovieActorModel?> GetByIdsAsync(int movieId, int actorId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MovieActorModel>> GetByMovieIdAsync(int movieId, CancellationToken cancellationToken = default);
        Task<MovieActorModel> CreateAsync(MovieActorModel model, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(MovieActorModel model, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int movieId, int actorId, CancellationToken cancellationToken = default);
    }
}
```

L'implementazione `Services/MovieActorService.cs` è concettualmente identica al generic service, ma usa `IMovieActorRepository` e lavora sulla coppia di chiavi:

```csharp
public MovieActorService(IMovieActorRepository repository, IUnitOfWork unitOfWork, IMapper mapper)
{
    _repository = repository;
    _unitOfWork = unitOfWork;
    _mapper = mapper;
}

public async Task<bool> DeleteAsync(int movieId, int actorId, CancellationToken cancellationToken = default)
{
    var entity = await _repository.GetByIdsAsync(movieId, actorId, cancellationToken);
    if (entity is null) return false;

    _repository.Remove(entity);
    await _unitOfWork.SaveChangesAsync(cancellationToken);
    return true;
}
```

Nota che usa comunque la **stessa** `IUnitOfWork` per il commit: repository dedicato per la ricerca a doppia chiave, unit of work condivisa per il salvataggio.

---

## (?) Mini-guida: Task, async e await

Praticamente ogni metodo di service e repository è **asincrono**. Vale la pena capire perché.

- **`Task` / `Task<T>`** — rappresentano un'operazione che finirà *in futuro*. `Task` non restituisce un valore, `Task<T>` restituirà un `T`. L'accesso al database è I/O e può richiedere tempo: modellarlo come `Task` è naturale.
- **`async`** — marca un metodo che può usare `await` e il cui risultato viene impacchettato in un `Task`.
- **`await`** — aspetta il completamento di un `Task` **senza bloccare** il thread.

### Sincrono vs asincrono, in due righe

- **Sincrono:** `var movies = repository.GetAll();` → il thread resta bloccato fino al ritorno dei dati. Con tante richieste insieme, i thread si saturano.
- **Asincrono:** `var movies = await repository.GetAllAsync();` → durante l'attesa I/O il thread è libero di servire altre richieste.

In un'applicazione web, dove arrivano molte richieste contemporanee, l'approccio asincrono migliora nettamente la **scalabilità**. Per questo tutto il progetto usa `...Async` + `await`. Il `CancellationToken` che passo ovunque completa il quadro: se il client annulla la richiesta, l'operazione può fermarsi invece di sprecare risorse.

[➡ Prossima parte: PL — AutoMapper e il MappingProfile](07-plapi-automapper-mapping.md)
