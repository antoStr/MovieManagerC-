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

## (?) Entity, Model, DTO, ViewModel: quattro parole per lo stesso film

Questa è la sezione che avrei voluto leggere all'inizio. Sono quattro termini che si sentono usare quasi come sinonimi, e non lo sono: **ognuno descrive la stessa cosa sagomata per un posto diverso**. La confusione peggiora guardando il progetto di qualcun altro e trovandoci cartelle che qui non ci sono.

| Termine | È sagomato su… | Dove vive | Nel mio progetto |
|---------|----------------|-----------|------------------|
| **Entity** | il **database** | DAL | `Movie` — **18** proprietà |
| **Model** / **DTO** | il **trasferimento** oltre un confine | BLL | `MovieModel` — **14** proprietà, piatto |
| **ViewModel** | una **schermata** | PL | **non esiste**, e sotto spiego perché |
| **View** | non è un oggetto: è un **template HTML** | PL | **non esiste** |

La regola in una riga:

> **L'Entity descrive come il dato è *salvato*. Il DTO descrive come *viaggia*. Il ViewModel descrive come viene *mostrato*.**

Quei due numeri raccontano da soli la differenza. Contati sul codice vero:

| | Proprietà |
|---|---|
| `Movie` (entity) | **18** = 14 dati + **4 navigazioni** (`Genre`, `Director`, `MovieActors`, `Reviews`) |
| `MovieModel` | **14** |
| Tabella `Movies` nel database | **14 colonne** |

`MovieModel` ha esattamente le colonne della tabella, **e la differenza con l'entità sono precisamente le quattro navigazioni** — cioè i riferimenti ad altri oggetti che, serializzati in JSON, produrrebbero i cicli infiniti del punto 2 qui sopra. La "piattezza" del model, detta in numeri, è tutta lì.

### Model o DTO? (spoiler: sono la stessa cosa, qui)

**DTO** sta per *Data Transfer Object*: un oggetto il cui unico scopo è **portare dati oltre un confine** — tra due livelli, tra server e client — senza logica dentro. È la definizione esatta di `MovieModel`: proprietà e niente altro.

In progetti più grandi i due si separano:

- il **Model** del BLL rappresenta il **dominio** (un film, con le sue regole);
- il **DTO** è il **contratto pubblico** dell'API (cosa esce da `GET /api/movies`).

Separarli serve quando le due cose divergono: se domani volessi esporre un `MovieListDto` con tre soli campi per una griglia, senza toccare `MovieModel`. Qui coincidono, e la guida dell'esercizio lo dice esplicitamente: *"non si usano DTO separati: il model BLL viene usato direttamente come contratto del servizio"*. Quindi `MovieModel` è **un model che fa anche da DTO**, ed è una scelta consapevole, non un'omissione: con sei entità e un CRUD, un terzo strato di classi sarebbe cerimonia senza guadagno.

> Se un giorno servisse davvero un DTO più magro del model, il posto dove userlo è la proiezione: `ProjectTo<MovieListDto>()` fa arrivare dal database **solo le colonne di quel DTO** invece di caricare tutto e buttare via. È misurato nel [capitolo 12](12-dal-controller-all-sql.md): 17 colonne contro 3.

### E il ViewModel? Il salto è più grande di quanto sembri

Il ViewModel **non è un DTO con un altro nome**. È sagomato su **una schermata**, e la differenza si vede solo con un esempio concreto.

Immagina la pagina **"modifica film"** di un'app che genera HTML. Per disegnarla servono:

- i campi del film → ce li ha già `MovieModel`
- **la tendina con tutti i generi** tra cui scegliere
- **la tendina con tutti i registi**

Quelle due liste **non sono dati del film**. Un film non contiene l'elenco di tutti i generi esistenti. Esistono solo perché *quella pagina* ha due `<select>` da riempire. Metterle dentro `MovieModel` significherebbe sporcare il dominio con un dettaglio di grafica: nasce quindi un terzo oggetto, che vive **solo** nel PL e muore con quella schermata.

```csharp
// Come sarebbe in un progetto che genera HTML — qui NON esiste
public class MovieEditViewModel
{
    public MovieModel Movie { get; set; }              // il dominio
    public List<SelectListItem> Generi { get; set; }   // roba della schermata
    public List<SelectListItem> Registi { get; set; }  // roba della schermata
}
```

`SelectListItem` dice già tutto: è un tipo di `Microsoft.AspNetCore.Mvc.Rendering`, cioè **una classe che esiste per disegnare un menù a tendina**. Un oggetto del genere non ha niente a che fare con un catalogo di film — ha a che fare con un `<select>`.

### Perché qui non c'è, e perché è giusto così

Il progetto è stato creato con il template **MVC** ([capitolo 1](01-struttura-e-architettura.md)), quindi la domanda è legittima: dov'è finita la "V" di Model-**View**-Controller?

Non c'è perché **non genero HTML**. Le due catene affiancate:

```
Questo progetto (API):   Entity (DAL) → Model (BLL) → JSON → il client si arrangia
Un progetto MVC vero:    Entity (DAL) → Model (BLL) → ViewModel (PL) → View (.cshtml) → HTML
```

Il mio lavoro **finisce al JSON**. Chi consuma l'API — un browser, un'app mobile, Scalar, un altro server — costruisce l'interfaccia che vuole, e non sono affari miei. Un'app MVC invece deve consegnare la pagina **già disegnata**, e per farlo le serve un oggetto sagomato su quella pagina.

**Niente HTML → niente View → niente ViewModel.** Non è un pezzo mancante: è un pezzo che non ha motivo di esistere. Il template MVC ne genera uno solo, `ErrorViewModel` (una classe con dentro un `RequestId`, per la pagina d'errore Razor), e l'ho eliminato insieme alla cartella `Views/`.

> **La "V" è dove stava la JSP.** Nel gestionale dipendenti in Java il servlet preparava i dati e la **JSP** li trasformava in HTML; quello che il servlet infilava con `request.setAttribute(...)` faceva, di fatto, il mestiere del ViewModel. Una View Razor (`.cshtml`) è la stessa identica idea con un'altra sintassi:
>
> ```cshtml
> <h1>@Model.Title</h1>
> @foreach (var a in Model.Cast) { <li>@a.Nome</li> }
> ```
>
> Qui quel passaggio non esiste: il controller restituisce l'oggetto e ci pensa ASP.NET a serializzarlo. Il [capitolo 11](11-scalar-e-prova-api.md) lo dice in una riga: *"niente JSP, niente form"*.

### ⚠️ Due trappole di vocabolario

**1. "ViewModel" usato al posto di "DTO".** Capita spessissimo di vedere una cartella `ViewModels` in progetti Web API che di View non ne hanno nemmeno una: è un'abitudine ereditata da MVC. Se un progetto ha `ViewModels/` ma nessun `.cshtml`, quelli **sono DTO col nome sbagliato**, e corrispondono ai `Model` di questo progetto. La domanda che scioglie il dubbio è sempre la stessa: *quel progetto genera HTML?* Se no, sono DTO.

**2. "ViewModel" in MVVM è un'altra cosa ancora.** In WPF, MAUI o Avalonia il ViewModel del pattern **MVVM** è un oggetto *vivo*, con data binding e `INotifyPropertyChanged`, che notifica la UI quando cambia. Stessa parola, mondo completamente diverso: lì il ViewModel ha comportamento, qui sarebbe stato solo un contenitore.

### La regola pratica da portarsi via

Quando incontro una classe e non so cosa sia, mi faccio tre domande in fila:

1. **La vede il database?** → è un'**Entity**.
2. **Attraversa un confine (livello, rete)?** → è un **DTO** (che nel BLL si chiama Model).
3. **Esiste solo perché una schermata ha bisogno di quella roba?** → è un **ViewModel**.

Se la risposta alla 3 è sì in un progetto che non ha View, c'è qualcosa che non torna.

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
