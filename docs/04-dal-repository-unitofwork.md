# 4) DAL — Generic Repository e Unit of Work

[⬅ Torna all'indice](../README.md)

Potrei usare il `DbContext` direttamente dappertutto, ma è una cattiva idea: legherei tutto il progetto ai dettagli di EF Core. Meglio nascondere il `DbContext` dietro due pattern classici: il **Repository** e la **Unit of Work**. Vanno in `MovieManager.DAL/Repositories`.

---

## (?) Che cosa è il Repository pattern?

Un **repository** è una classe che fa da "magazziniere" dei dati: gli chiedo le operazioni (dammi tutti i film, aggiungi questo, elimina quello) e lui sa come farle, senza che io sappia com'è fatto il magazzino dietro.

Nel gestionale Java questo ruolo lo aveva il DAO (`DipendenteDAOImpl`). Qui il concetto è lo stesso, ma con una marcia in più: uso un repository **generico**, che funziona per **qualsiasi** entità, invece di scriverne uno per tabella.

## (?) Che cosa sono i generics (`<T>`)?

I **generics** permettono di scrivere una classe/metodo che lavora con un **tipo qualsiasi**, deciso al momento dell'uso. `GenericRepository<T>` è "un repository di T": se lo uso come `GenericRepository<Movie>` diventa un repository di film, come `GenericRepository<Actor>` diventa un repository di attori. Scrivo il codice **una volta sola** e vale per tutte le entità. È il motivo per cui non devo copiare-incollare un DAO per ogni tabella come facevo in Java.

---

## 4.1 L'interfaccia IGenericRepository

File: `Repositories/Interfaces/IGenericRepository.cs`. L'interfaccia elenca **cosa** so fare, senza dire **come** (esattamente come l'interfaccia `DipendenteDAO` del progetto Java):

```csharp
using System.Linq.Expressions;

namespace MovieManager.DAL.Repositories.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
        Task AddAsync(T entity, CancellationToken cancellationToken = default);
        void Update(T entity);
        void Remove(T entity);
    }
}
```

Un paio di cose da notare:

- `where T : class` è un **vincolo generico**: `T` deve essere un tipo riferimento (una classe), coerente col fatto che le entità EF sono classi.
- `FindAsync(Expression<Func<T, bool>> predicate)` accetta una **condizione** come parametro, per esempio `film => film.GenreId == 3`. È il modo generico di dire "trovami le entità che soddisfano questa regola".
- **Nessun metodo salva davvero.** `Add/Update/Remove` preparano soltanto la modifica. Il salvataggio (`SaveChanges`) è responsabilità della Unit of Work — spiego il perché più sotto.

---

## 4.2 L'implementazione GenericRepository

File: `Repositories/GenericRepository.cs`.

```csharp
using Microsoft.EntityFrameworkCore;
using MovieManager.DAL.Data;
using MovieManager.DAL.Repositories.Interfaces;
using System.Linq.Expressions;

namespace MovieManager.DAL.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly MovieDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(MovieDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await _dbSet.FindAsync(new object[] { id }, cancellationToken);

        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => await _dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

        public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
            => await _dbSet.AddAsync(entity, cancellationToken);

        public void Update(T entity)
            => _context.Entry(entity).State = EntityState.Modified;

        public void Remove(T entity)
            => _dbSet.Remove(entity);
    }
}
```

`context.Set<T>()` mi dà il `DbSet` giusto per il tipo `T` in modo dinamico: è la chiave che rende il repository davvero generico.

### (?) Che cosa è `AsNoTracking()`?

Di default il `DbContext` **tiene traccia** di ogni oggetto che legge (il *change tracking*): ne conserva una copia, per poter capire più tardi se l'ho modificato. È il meccanismo che fa funzionare `SaveChanges()` senza che io debba dirgli cosa è cambiato.

In **sola lettura** però è lavoro sprecato: tenere una copia di oggetti che nessuno modificherà costa memoria e basta. `AsNoTracking()` dice a EF "questi li leggo e via, non seguirli".

La regola che seguo nel repository è: **traccio solo quello che ho intenzione di modificare.**

| Metodo del repository | Traccia? | Perché |
|---|---|---|
| `GetAllAsync()` | **no** — `AsNoTracking()` | i dati escono verso il controller, nessuno li modificherà |
| `FindAsync(predicate)` | **no** — `AsNoTracking()` | idem: è una ricerca in sola lettura |
| `GetByIdAsync(id)` | **sì** | lo chiamano `UpdateAsync` e `DeleteAsync` proprio per modificare l'entità ([capitolo 6](06-bll-services.md)) |

> ⚠️ **Occhio all'omonimia: in questo file ci sono due `FindAsync` diversi, con comportamento opposto.**
>
> ```csharp
> public async Task<T?> GetByIdAsync(int id, ...)
>     => await _dbSet.FindAsync(new object[] { id }, ...);              // FindAsync di EF Core -> TRACCIA
>
> public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, ...)
>     => await _dbSet.AsNoTracking().Where(predicate).ToListAsync(...); // FindAsync mio -> NON traccia
> ```
>
> - **`FindAsync(predicate)`** è **mio**, dichiarato in `IGenericRepository`: cerca per **condizione** e non traccia.
> - **`_dbSet.FindAsync(id)`** è di **EF Core**: cerca per **chiave primaria** e traccia.
>
> Non è un errore di battitura né una svista: sono due metodi di due librerie diverse che si chiamano uguale. Quando leggo "FindAsync" — qui o negli altri capitoli — conta sempre **su cosa** è chiamato: sul repository è il mio, su `_dbSet` è quello di EF.

> 📖 Cosa cambia esattamente fra tracciare e non tracciare (nell'SQL: **niente**; in memoria: molto), e il bug silenzioso che nasce sbagliando questa scelta, sono nella [sezione 12.7](12-dal-controller-all-sql.md).

---

## (?) Che cosa è la Unit of Work?

La **Unit of Work** rappresenta "un'unità di lavoro" da salvare tutta insieme. Raccoglie tutte le modifiche fatte tramite i repository e le conferma con **un solo** `SaveChanges`, dentro **una sola** transazione. O va tutto a buon fine, o non cambia niente.

**Perché il `SaveChanges` non sta nel repository?** Perché il repository si occupa di *preparare* le modifiche sulle singole entità, mentre decidere *quando* renderle definitive è una responsabilità diversa e più ampia. Separarle significa che posso, per esempio, aggiungere un film **e** una recensione e salvarli in un colpo solo, in modo atomico. Se ogni repository facesse il suo `SaveChanges`, perderei questa atomicità.

---

## 4.3 IUnitOfWork e UnitOfWork

File: `Repositories/Interfaces/IUnitOfWork.cs`:

```csharp
namespace MovieManager.DAL.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<T> Repository<T>() where T : class;
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
```

`IDisposable` serve perché la Unit of Work possiede il `DbContext` e deve poterlo rilasciare (`Dispose`) a fine richiesta.

File: `Repositories/UnitOfWork.cs`:

```csharp
using MovieManager.DAL.Data;
using MovieManager.DAL.Repositories.Interfaces;

namespace MovieManager.DAL.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly MovieDbContext _context;
        private readonly Dictionary<Type, object> _repositories;

        public UnitOfWork(MovieDbContext context)
        {
            _context = context;
            _repositories = new Dictionary<Type, object>();
        }

        public IGenericRepository<T> Repository<T>() where T : class
        {
            if (!_repositories.ContainsKey(typeof(T)))
                _repositories[typeof(T)] = new GenericRepository<T>(_context);

            return (IGenericRepository<T>)_repositories[typeof(T)];
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => await _context.SaveChangesAsync(cancellationToken);

        public void Dispose()
            => _context.Dispose();
    }
}
```

Il metodo `Repository<T>()` fa da **cache**: la prima volta che chiedo il repository di un tipo lo crea, le volte successive restituisce sempre lo stesso (tutti sullo **stesso** `DbContext`). Così, qualunque repository io usi, le modifiche finiscono nello stesso contesto e le salvo tutte con un unico `SaveChangesAsync`.

> ⚠️ **Attenzione al costruttore:** la Unit of Work deve ricevere via Dependency Injection **solo** il `DbContext` e creare da sola il dizionario `_repositories`. Se per errore si mettesse anche il `Dictionary` tra i parametri del costruttore, la DI non saprebbe come fornirlo e l'app andrebbe in errore all'avvio. Il dizionario è uno stato interno, non una dipendenza.

---

## 4.4 Il caso speciale: MovieActorRepository (chiave composta)

`IGenericRepository<T>` ha un metodo `GetByIdAsync(int id)`: presuppone una **chiave singola**. Ma `MovieActor` ha una **chiave composta** `(MovieId, ActorId)`. Forzarlo nel repository generico sarebbe sbagliato. La soluzione pulita è un repository **dedicato**.

File: `Repositories/Interfaces/IMovieActorRepository.cs`:

```csharp
using MovieManager.DAL.Entities;

namespace MovieManager.DAL.Repositories.Interfaces
{
    public interface IMovieActorRepository
    {
        Task<MovieActor?> GetByIdsAsync(int movieId, int actorId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<MovieActor>> GetByMovieIdAsync(int movieId, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(int movieId, int actorId, CancellationToken cancellationToken = default);
        Task AddAsync(MovieActor entity, CancellationToken cancellationToken = default);
        void Update(MovieActor entity);
        void Remove(MovieActor entity);
    }
}
```

I metodi di ricerca lavorano sulla **coppia** di chiavi (`GetByIdsAsync`, `ExistsAsync`) o sul solo film (`GetByMovieIdAsync`, per elencare gli attori di un film). L'implementazione in `Repositories/MovieActorRepository.cs` usa il `DbContext` con query LINQ:

```csharp
public async Task<MovieActor?> GetByIdsAsync(int movieId, int actorId, CancellationToken cancellationToken = default)
    => await _context.MovieActors
        .FirstOrDefaultAsync(ma => ma.MovieId == movieId && ma.ActorId == actorId, cancellationToken);
```

Nota che `GetByIdsAsync` **non** usa `AsNoTracking`: l'entità va tracciata perché di solito la carico per poi aggiornarla o eliminarla.

> Questo repository dedicato **non** sostituisce quello generico: convivono. Il generico gestisce tutte le entità a chiave singola, il dedicato gestisce l'unico caso a chiave composta. È il principio di tenere separati i due tipi di CRUD.

---

## Verifica finale

Il DAL è completo. Controllo che compili e, soprattutto, che **nessun repository chiami `SaveChanges`** direttamente: quel compito è centralizzato nella Unit of Work.

```bash
dotnet build MovieManager.DAL
```

[➡ Prossima parte: BLL — I Model, IModelWithId e la validazione](05-bll-models.md)
