# 7) PL — AutoMapper e il MappingProfile

[⬅ Torna all'indice](../README.md)

Nei service ho scritto tante volte `_mapper.Map<...>(...)` per convertire entità ↔ model. Ora vediamo chi è questo `_mapper` e come gli insegno le conversioni. Siamo nel Presentation Layer: la configurazione va in `MovieManager.PL.API/Configurations/MappingProfile.cs`.

---

## (?) Che cosa è AutoMapper? Che cos'è il "mapping"?

Entità e model contengono quasi gli stessi dati, con gli stessi nomi. Convertire a mano un `Movie` in un `MovieModel` significherebbe scrivere righe noiosissime tipo `model.Title = entity.Title; model.Language = entity.Language; ...` per ogni campo di ogni entità. Un lavoro ripetitivo e pieno di possibili sviste.

**AutoMapper** automatizza questo "mapping": data una coppia di tipi, copia da solo le proprietà che hanno lo **stesso nome**. Io devo solo dichiarare **quali** coppie di tipi possono essere convertite; al resto pensa lui.

---

## 7.1 Il MappingProfile

Un **Profile** è una classe dove raggruppo le regole di mapping. Eredita da `Profile` (di AutoMapper) e definisce le mappe nel costruttore:

```csharp
using AutoMapper;
using MovieManager.BLL.Models;
using MovieManager.DAL.Entities;

namespace MovieManager.PL.API.Configurations
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Actor, ActorModel>().ReverseMap();
            CreateMap<Director, DirectorModel>().ReverseMap();
            CreateMap<Genre, GenreModel>().ReverseMap();
            CreateMap<Movie, MovieModel>().ReverseMap();
            CreateMap<Review, ReviewModel>().ReverseMap();
            CreateMap<MovieActor, MovieActorModel>().ReverseMap();
        }
    }
}
```

### (?) Cosa fa `CreateMap` e cosa fa `ReverseMap`?

`CreateMap<Movie, MovieModel>()` dichiara: "so convertire un `Movie` in un `MovieModel`". Ma nel progetto mi serve la conversione in **entrambe** le direzioni:

- **entità → model** in lettura (`GetAllAsync`, `GetByIdAsync`);
- **model → entità** in scrittura (`CreateAsync`, `UpdateAsync`).

`ReverseMap()` crea automaticamente anche la mappa inversa (`MovieModel → Movie`), evitandomi di scrivere due `CreateMap` separati.

---

## 7.2 I tre modi in cui uso il mapping

Nei service compaiono tre usi diversi, vale la pena distinguerli:

**1. `Map<TDestination>(source)` — crea un oggetto nuovo.** Usato in lettura e in creazione:

```csharp
_mapper.Map<MovieModel>(entity);          // nuovo MovieModel dai dati dell'entità
_mapper.Map<Movie>(model);                // nuova entità Movie dai dati del model
```

**2. `Map(source, destination)` — aggiorna un oggetto esistente.** Usato nell'update, per riversare i valori del model su un'entità **già caricata e tracciata** da EF (così EF sa cosa è cambiato e genera l'`UPDATE`):

```csharp
_mapper.Map(model, existing);             // aggiorno 'existing', non ne creo uno nuovo
```

Questa distinzione è importante: se nell'update creassi una nuova entità invece di aggiornare quella tracciata, EF non capirebbe che si tratta di una modifica.

---

## (?) Cosa succede se dimentico una mappa?

Se chiamo `_mapper.Map<QualcosaModel>(qualcosa)` ma non ho dichiarato la relativa `CreateMap`, AutoMapper lancia un **errore a runtime** al primo utilizzo (non in compilazione). È l'errore più comune con questa libreria. Per questo la regola è: **ogni coppia entità ↔ model usata dai service deve avere la sua riga nel `MappingProfile`**. Nel nostro caso sono sei, una per entità.

---

## 7.3 Perché il Profile sta nel progetto API?

Il `MappingProfile` è nel PL (non nel BLL) perché è lì che verrà **registrato** in AutoMapper, all'avvio, con:

```csharp
builder.Services.AddAutoMapper(typeof(Program).Assembly);
```

`typeof(Program).Assembly` dice ad AutoMapper: "cerca tutti i `Profile` **nell'assembly della API**". Trovando `MappingProfile` lì dentro, carica automaticamente tutte le sue mappe. Ne riparlo nel [capitolo 9](09-plapi-program-di-scalar.md).

> **Sul pacchetto AutoMapper.** Le guide fissano la versione **14.0.0** e il progetto la rispetta (presente sia nel BLL, dove uso `IMapper`, sia nel PL, dove sta il Profile e la registrazione). Su quella versione la build segnala un advisory di sicurezza (`NU1903`): la questione è discussa nel [README](../README.md#nota-sui-pacchetti-e-sulla-sicurezza).

[➡ Prossima parte: PL — I Controller](08-plapi-controllers.md)
