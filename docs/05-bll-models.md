# 5) BLL — I Model e l'interfaccia IModelWithId

[⬅ Torna all'indice](../README.md)

Entro nel **Business Logic Layer**. La prima cosa da definire sono i **Model**: le classi che il BLL (e quindi le API) usano per scambiare dati, al posto delle entità del DAL. Vanno in `MovieManager.BLL/Models`.

---

## (?) Perché non uso direttamente le entità? A cosa servono i Model?

Verrebbe naturale esporre direttamente le entità (`Movie`, `Actor`...) dalle API. È però una cattiva pratica, per almeno tre motivi:

1. **Accoppiamento** — Le API resterebbero incollate alla struttura del database. Cambiare una colonna significherebbe cambiare il contratto pubblico dell'API.
2. **Cicli e dati di troppo** — Le entità hanno proprietà di navigazione (`Movie.Genre`, `Genre.Movies`, ...) che si puntano a vicenda. Serializzarle in JSON crea cicli infiniti o restituisce montagne di dati annidati che nessuno ha chiesto.
3. **Controllo** — Con i model decido io, campo per campo, cosa entra e cosa esce dall'API.

Il **Model** è quindi una versione "piatta" e pulita dell'entità, pensata per il mondo esterno. In questo progetto non uso DTO separati: il model del BLL è direttamente il contratto delle API.

---

## 5.1 I Model sono "piatti"

Regola che ho seguito: ogni model contiene i **campi semplici** e le **chiavi esterne** dell'entità corrispondente, ma **non** le proprietà di navigazione né le collezioni. Così evito i cicli del punto 2 e ottengo un JSON prevedibile.

`GenreModel`:

```csharp
namespace MovieManager.BLL.Models
{
    public class GenreModel : IModelWithId
    {
        public int Id               { get; set; }
        public string Name          { get; set; } = string.Empty;
        public string? Description  { get; set; }
    }
}
```

`MovieModel` — ha i campi del film e le due chiavi esterne `GenreId`/`DirectorId`, ma **non** gli oggetti `Genre`/`Director` né le collezioni:

```csharp
namespace MovieManager.BLL.Models
{
    public class MovieModel : IModelWithId
    {
        public int Id                   { get; set; }
        public string Title             { get; set; } = string.Empty;
        public string? OriginalTitle    { get; set; }
        public DateOnly ReleaseDate     { get; set; }
        public int DurationMinutes      { get; set; }
        public string? Synopsis         { get; set; }
        public string Language          { get; set; } = string.Empty;
        public string? Country          { get; set; }
        public decimal? Budget          { get; set; }
        public decimal? Revenue         { get; set; }
        public string? PosterUrl        { get; set; }
        public string? AgeRating        { get; set; }
        public int GenreId              { get; set; }
        public int DirectorId           { get; set; }
    }
}
```

Allo stesso modo esistono `ActorModel`, `DirectorModel` e `ReviewModel`, ciascuno "specchio piatto" della propria entità. Le proprietà stringa obbligatorie sono inizializzate con `string.Empty`, quelle opzionali sono `nullable`, esattamente come nelle entità.

---

## 5.2 L'interfaccia IModelWithId

Ecco il pezzo più interessante di questo livello:

```csharp
namespace MovieManager.BLL.Models
{
    public interface IModelWithId
    {
        int Id { get; set; }
    }
}
```

Un'interfaccia minuscola: dice solo "chi mi implementa ha una proprietà `Id` di tipo `int`". Tutti i model a chiave singola la implementano (`GenreModel : IModelWithId`, `MovieModel : IModelWithId`, ...).

### (?) Perché esiste IModelWithId? Non è ridondante?

No, ed è la chiave che fa funzionare il servizio generico in modo **sicuro**. Il `GenericService` (prossimo capitolo) a un certo punto deve leggere l'`Id` del model per aggiornarlo. Senza un vincolo, avrebbe due strade:

- usare la **reflection** per cercare a runtime una proprietà chiamata "Id" → lento e fragile (esplode a runtime se manca);
- pretendere che il model implementi `IModelWithId` → il **compilatore** garantisce che `Id` esista.

Con `where TModel : IModelWithId`, dentro il service posso scrivere `model.Id` in totale sicurezza: se qualcuno prova a usare il generic service con un model senza `Id`, il codice **non compila** nemmeno. Meglio un errore in compilazione oggi che un crash in produzione domani.

---

## 5.3 L'eccezione: MovieActorModel

`MovieActorModel` è l'unico model che **non** implementa `IModelWithId`, perché la sua identità è la coppia `(MovieId, ActorId)`, non un `Id` singolo:

```csharp
namespace MovieManager.BLL.Models
{
    public class MovieActorModel   // niente IModelWithId!
    {
        public int MovieId              { get; set; }
        public int ActorId              { get; set; }
        public string? CharacterName    { get; set; }
        public bool IsLeadRole          { get; set; }
        public int DisplayOrder         { get; set; }
    }
}
```

Proprio perché non ha un `Id` singolo, non può essere gestito dal `GenericService`: userà il servizio dedicato `MovieActorService`. È l'ennesima conseguenza a cascata della scelta di modellare la relazione molti-a-molti con una chiave composta.

---

## Verifica finale

- Ogni model ha proprietà coerenti con la sua entità.
- I model a chiave singola implementano `IModelWithId`.
- `MovieActorModel` **non** implementa `IModelWithId`.

[➡ Prossima parte: BLL — I Service](06-bll-services.md)
