# 2) DAL — Le entità

[⬅ Torna all'indice](../README.md)

Il primo contenuto vero del progetto sono le **entità**: le classi che descrivono i dati che voglio gestire. Vanno tutte nella cartella `MovieManager.DAL/Entities` con namespace `MovieManager.DAL.Entities`.

---

## (?) Che cosa è un'entità?

Un'entità è una semplice classe C# che rappresenta una **tabella** del database. Ogni proprietà della classe diventa una **colonna**, e ogni oggetto di quella classe è una **riga**.

È lo stesso ruolo che nel gestionale Java aveva la classe `Dipendente` nel package `model`. La grande differenza è che qui non scrivo io il codice SQL per leggere/scrivere: ci pensa **Entity Framework Core** (l'ORM) a tradurre le classi in tabelle e le query LINQ in SQL. Ne parlo nel [capitolo 3](03-dal-dbcontext.md).

---

## (?) Concetti C# che uso nelle entità

Prima del codice, tre cose che ritornano in ogni entità:

- **Nullable reference types** — In C# moderno una `string` è considerata "non può essere null". Se un dato è opzionale lo dichiaro con il punto interrogativo: `string?`, `DateOnly?`, `decimal?`. Questo mi costringe a ragionare su cosa è obbligatorio e cosa no.
- **`= string.Empty` e `= null!`** — Per evitare warning del compilatore, inizializzo le stringhe obbligatorie con `string.Empty`, e le proprietà di navigazione obbligatorie con `null!` (che significa: "lo so che ora è null, ma garantisco io che EF lo riempirà").
- **Proprietà di navigazione** — Una proprietà che punta a un'altra entità (es. un `Movie` ha un `Genre`). Servono a EF per capire le **relazioni** e per potermi far navigare gli oggetti collegati. Le collezioni (`ICollection<T>`) le inizializzo con `new List<T>()` così non sono mai null.

---

## 2.1 Le entità semplici: Genre, Director, Actor

### Genre

```csharp
namespace MovieManager.DAL.Entities
{
    public class Genre
    {
        public int Id                       { get; set; }
        public string Name                  { get; set; } = string.Empty;
        public string? Description          { get; set; }
        public ICollection<Movie> Movies    { get; set; } = new List<Movie>();
    }
}
```

`Id` è la **chiave primaria**: EF Core, per convenzione, riconosce automaticamente come chiave una proprietà chiamata `Id` (o `<NomeClasse>Id`) di tipo `int`, e la configura come identity auto-incrementante. Non devo dichiarare niente a mano, esattamente come l'`auto_increment` del vecchio database MySQL.

`Movies` è la navigazione "uno-a-molti": un genere ha molti film.

### Director e Actor

Sono quasi identiche tra loro (nome, cognome, data di nascita opzionale, paese, biografia):

```csharp
namespace MovieManager.DAL.Entities
{
    public class Director
    {
        public int Id                       { get; set; }
        public string FirstName             { get; set; } = string.Empty;
        public string LastName              { get; set; } = string.Empty;
        public DateOnly? BirthDate          { get; set; }
        public string? Country              { get; set; }
        public string? Biography            { get; set; }
        public ICollection<Movie> Movies    { get; set; } = new List<Movie>();
    }
}
```

`Actor` è uguale, ma la sua collezione punta a `MovieActor` invece che a `Movie` (spiego il perché tra poco):

```csharp
public ICollection<MovieActor> MovieActors { get; set; } = new List<MovieActor>();
```

> Uso `DateOnly?` (e non `DateTime`) perché una data di nascita è **solo una data**, senza ora. È il tipo giusto per rappresentarla.

---

## 2.2 L'entità centrale: Movie

`Movie` è il cuore del dominio e ha più campi, comprese due **chiavi esterne** (`GenreId`, `DirectorId`) con le rispettive navigazioni:

```csharp
namespace MovieManager.DAL.Entities
{
    public class Movie
    {
        public int Id                               { get; set; }
        public string Title                         { get; set; } = string.Empty;
        public string? OriginalTitle                { get; set; }
        public DateOnly ReleaseDate                 { get; set; }
        public int DurationMinutes                  { get; set; }
        public string? Synopsis                     { get; set; }
        public string Language                      { get; set; } = string.Empty;
        public string? Country                      { get; set; }
        public decimal? Budget                      { get; set; }
        public decimal? Revenue                     { get; set; }
        public string? PosterUrl                    { get; set; }
        public string? AgeRating                    { get; set; }
        public int GenreId                          { get; set; }
        public Genre Genre                          { get; set; } = null!;
        public int DirectorId                       { get; set; }
        public Director Director                    { get; set; } = null!;
        public ICollection<MovieActor> MovieActors  { get; set; } = new List<MovieActor>();
        public ICollection<Review> Reviews          { get; set; } = new List<Review>();
    }
}
```

### (?) Che cosa è una chiave esterna (foreign key)?

È il modo con cui una tabella "punta" a un'altra. `GenreId` contiene l'`Id` del genere a cui il film appartiene: è il collegamento vero e proprio, quello che finisce nel database. La proprietà `Genre` (di tipo `Genre`) è invece la **navigazione**: mi permette, dato un film, di raggiungere direttamente l'oggetto genere collegato.

La coppia "chiave esterna + navigazione" (`GenreId` + `Genre`) è il pattern consigliato in EF Core: ho sia il valore semplice da salvare/inviare, sia l'oggetto comodo da navigare.

`decimal?` per `Budget` e `Revenue` perché sono importi di denaro: `decimal` è il tipo giusto per i valori monetari (niente errori di arrotondamento come coi `double`).

---

## 2.3 La tabella ponte: MovieActor

Un film ha **molti** attori e un attore recita in **molti** film: è una relazione **molti-a-molti**. In un database relazionale una relazione molti-a-molti si realizza con una **tabella ponte** (o "di associazione") che sta in mezzo. Qui è `MovieActor`:

```csharp
namespace MovieManager.DAL.Entities
{
    public class MovieActor
    {
        public int MovieId              { get; set; }
        public Movie Movie              { get; set; } = null!;
        public int ActorId              { get; set; }
        public Actor Actor              { get; set; } = null!;
        public string? CharacterName    { get; set; }
        public bool IsLeadRole          { get; set; }
        public int DisplayOrder         { get; set; }
    }
}
```

### (?) Perché una tabella ponte e non un semplice `Id`?

Perché la sua identità non è un singolo numero, ma la **coppia** `(MovieId, ActorId)`: è quella coppia a identificare univocamente "questo attore in questo film". Si chiama **chiave composta**. Inoltre la tabella ponte può portare dati propri della relazione: qui `CharacterName` (il nome del personaggio interpretato), `IsLeadRole` (è protagonista?) e `DisplayOrder` (ordine nei crediti).

Questa chiave composta avrà conseguenze importanti più avanti: il repository e il service generici (pensati per chiavi singole) non bastano, e servirà un `MovieActorRepository` / `MovieActorService` dedicati. Lo vedremo nei capitoli [4](04-dal-repository-unitofwork.md) e [6](06-bll-services.md).

> Nota: ho incluso `CharacterName` perché è previsto dalle istruzioni delle entità. La chiave composta e le due relazioni verso `Movie` e `Actor` le configuro nel `DbContext` (capitolo 3).

---

## 2.4 Le recensioni: Review

```csharp
namespace MovieManager.DAL.Entities
{
    public class Review
    {
        public int Id                   { get; set; }
        public int MovieId              { get; set; }
        public Movie Movie              { get; set; } = null!;
        public string ReviewerName      { get; set; } = string.Empty;
        public int Score                { get; set; }
        public string? Comment          { get; set; }
        public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    }
}
```

Due dettagli:

- `CreatedAt` è inizializzato a `DateTime.UtcNow`: se non specifico nulla al momento della creazione, la data/ora viene messa in automatico (in UTC, per non dipendere dal fuso orario del server).
- `Score` sarà vincolato tra 1 e 10, ma questo **non** lo esprimo nell'entità: è una regola del database che configuro nel `DbContext` con un check constraint (capitolo 3).

---

## Verifica finale

Con tutte e sei le entità create, il progetto DAL deve compilare senza errori:

```bash
dotnet build MovieManager.DAL
```

[➡ Prossima parte: DAL — Il DbContext](03-dal-dbcontext.md)
