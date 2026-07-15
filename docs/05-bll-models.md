# 5) BLL — I Model, IModelWithId e la validazione

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
using System.ComponentModel.DataAnnotations;

namespace MovieManager.BLL.Models
{
    public class GenreModel : IModelWithId
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Il nome del genere è obbligatorio.")]
        [StringLength(100, ErrorMessage = "Il nome non può superare i 100 caratteri.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
```

(Gli attributi `[Required]` e `[StringLength]` sono la validazione: ne parlo nella sezione 5.4.)

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

## 5.4 La validazione: le DataAnnotations

Le **DataAnnotations** sono attributi che dichiarano le regole di un campo. Le ho aggiunte dopo, e per un motivo concreto: senza, una `POST /api/Actors` con body **`{}`** rispondeva `201` e creava davvero un attore **senza nome**. Nome e cognome sono `NOT NULL` sul database, ma la stringa vuota soddisfa `NOT NULL` benissimo: il database era contento e il dato era spazzatura.

| Attributo | Cosa impone | Dove l'ho messo |
|-----------|-------------|-----------------|
| `[Required]` | non nullo **e non stringa vuota** | `FirstName`, `LastName`, `Name`, `Title`, `Language`, `ReviewerName` |
| `[StringLength(n)]` | lunghezza massima | gli stessi campi, con lo stesso `n` del `HasMaxLength` |
| `[Range(min, max)]` | intervallo numerico | `Score` (1–10), e le chiavi esterne (`>= 1`) |

```csharp
[Required(ErrorMessage = "Il nome è obbligatorio.")]
[StringLength(100, ErrorMessage = "Il nome non può superare i 100 caratteri.")]
public string FirstName { get; set; } = string.Empty;
```

### (?) Chi esegue questi controlli? Nel controller non c'è niente

È l'attributo **`[ApiController]`** sui controller ([capitolo 8](08-plapi-controllers.md)). Prima ancora di entrare nel metodo, ASP.NET Core valida il model; se qualcosa non torna, l'azione **non viene mai eseguita** e parte in automatico un `400` con l'elenco degli errori:

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

Nessuna riga di codice mia: gli attributi sono dichiarativi, il resto lo fa il framework.

### Il criterio che ho seguito: rispecchiare il database

Le regole dei model **combaciano con i vincoli veri** delle tabelle: `[StringLength(100)]` dove il `DbContext` dice `HasMaxLength(100)`, `[Range(1, 10)]` dove c'è il check constraint `CK_Review_Score`. Non ho inventato regole nuove né messo limiti dove il database non ne ha (`Country` è `nvarchar(max)`, quindi nessun `[StringLength]`).

Sembra una duplicazione, ma i due livelli fanno lavori diversi:

- **il model** rifiuta subito e **spiega** cosa c'è di sbagliato, in italiano, campo per campo;
- **il database** garantisce che il dato resti valido **comunque**, anche per chi non passa dall'API (un `INSERT` da `sqlcmd`, un altro programma domani).

Il primo è cortesia verso il client, il secondo è integrità dei dati. Servono entrambi.

> ⚠️ **Quello che le DataAnnotations non possono fare.** `[Range(1, int.MaxValue)]` su `GenreId` blocca `0` e i negativi, ma **non** può sapere se il genere `999` esiste davvero: quella risposta ce l'ha solo il database. Per questo un `genreId` inesistente viene intercettato più avanti, dal `DatabaseExceptionHandler` ([capitolo 11](11-scalar-e-prova-api.md)), che traduce la violazione di chiave esterna in un `400` invece di lasciarla uscire come `500`.

### Un effetto collaterale gradito

Gli stessi attributi finiscono nel documento **OpenAPI** e quindi in **Scalar**: `[Required]` diventa `"required": ["firstName"]`, `[StringLength(100)]` diventa `"maxLength": 100`, `[Range(1, 10)]` diventa `"minimum": 1, "maximum": 10`. Un attributo, tre risultati: validazione, documentazione e aiuto nell'interfaccia. Il dettaglio nel [capitolo 11](11-scalar-e-prova-api.md).

---

## Verifica finale

- Ogni model ha proprietà coerenti con la sua entità.
- I model a chiave singola implementano `IModelWithId`.
- `MovieActorModel` **non** implementa `IModelWithId`.
- I campi obbligatori hanno `[Required]`, con limiti allineati a quelli del `DbContext`.
- Una `POST` con body `{}` risponde `400` e non crea niente.

[➡ Prossima parte: BLL — Generic Service, MovieActorService, async/await](06-bll-services.md)
