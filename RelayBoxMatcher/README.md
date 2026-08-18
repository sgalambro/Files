# Relay Box Matcher

Applicazione WinForms (.NET 8) per il controllo qualità delle scatole relè in linea di
assemblaggio: si definisce un "modello" a partire da una foto campione (posizione e colore
dei relè con etichetta colorata — tipicamente 2 blu, 4 rosa, 1 verde) e lo si confronta con
foto di test scattate sul pezzo montato, segnalando relè mancanti, di colore sbagliato o non
classificabili con certezza.

Sostituisce/riprende gli output già presenti nel repo (`git_sample_M29AT.zip`,
`git_test_*_M29AT.zip`, `git_result*.zip`): `templates_meta.json`, `expected.json`,
`test_report.json` e `summary.csv` usano lo stesso schema di quei file, con qualche campo
aggiuntivo (vedi sotto).

## Perché omografia + campionamento colore, e non feature matching

I risultati già presenti in `git_result*.zip` (probabilmente prodotti da un tentativo con
matching a feature, tipo ORB) sono chiaramente rotti: ogni template combacia sullo stesso
bbox minuscolo, indipendentemente dal template. È un problema strutturale, non un bug di
soglia: i relè sono oggetti piccoli, in plastica lucida, quasi privi di texture propria — un
pessimo bersaglio per il feature matching, che ha bisogno di keypoint distintivi.

Qui si usa invece:

1. **Omografia a 4+ punti**: l'operatore clicca, sul campione e su ogni foto di test, gli
   stessi punti fisici di riferimento (viti, angoli della scatola — non i relè). Da queste
   corrispondenze si calcola una trasformazione prospettica che proietta le posizioni note
   dei relè sul campione nelle coordinate della foto di test, qualunque siano risoluzione,
   rotazione o leggera prospettiva dello scatto (il campione nel repo è 2953×1314, i test
   1632×1224: coordinate pixel non sono riusabili direttamente).
2. **Campionamento colore robusto al riflesso flash**: nella regione proiettata si scartano i
   pixel di riflesso diretto (molto luminosi, poco saturi) e quelli della cavità nera dello
   zoccolo vuoto, poi si stima il colore dominante sui pixel restanti e lo si confronta con i
   prototipi di colore costruiti dagli stessi ritagli del campione (autocalibrazione sulla
   luce/fotocamera del giorno, niente soglie assolute hard-coded).

Contropartita: la calibrazione dei punti di riferimento è manuale (un clic per punto, per
ogni foto). Non c'è ancora rilevamento automatico dei punti — richiederebbe marker fiduciali
fisici sulla scatola (es. adesivi ArUco) o feature realmente distintive, assenti qui.

## Struttura

```
RelayBoxMatcher.sln
src/
  RelayBoxMatcher.Core/     libreria (net8.0): modelli, omografia, colore, servizi — nessuna dipendenza da WinForms
  RelayBoxMatcher.App/      applicazione WinForms (net8.0-windows)
```

## Build

Richiede .NET 8 SDK con i workload desktop di Windows (Visual Studio 2022 17.8+ va bene, o
`dotnet build` da un prompt con i target pack Windows Desktop installati). **Il progetto WinForms
compila solo su Windows.**

```
dotnet build RelayBoxMatcher.sln
```

oppure apri `RelayBoxMatcher.sln` in Visual Studio e premi F5.

> Nota: questo codice è stato scritto in un ambiente Linux sandbox senza accesso al .NET SDK
> (il proxy di rete blocca i domini di download Microsoft), quindi non ho potuto lanciare una
> build reale né i test. L'ho scritto e riletto con molta attenzione, ma la prima build va
> fatta sulla tua macchina Windows: se emergono errori, mandameli e li sistemo subito.

## Uso

### 1. Modello (campione)
1. "Carica foto campione..." e seleziona la foto di riferimento (Foto A).
2. Modalità "Disegna slot relè": trascina un rettangolo su ciascuno dei relè con etichetta
   colorata, assegna nome e colore (Blu/Rosa/Verde) nella finestra che compare.
3. Modalità "Punti di riferimento": clicca almeno 4 punti fisici facili da ritrovare
   identici in ogni foto (es. le viti di fissaggio), non i relè.
4. "Salva modello...": scegli una cartella. Vengono creati `templates_meta.json`,
   `templates/*.png` (ritagli) e una copia di `sample.png`.

### 2. Test singolo
1. "Carica modello..." (se non già caricato dal passo precedente).
2. "Carica foto di test..." e poi "Calibra ed esegui match": clicca, nello stesso ordine del
   campione, gli stessi punti fisici di riferimento sulla nuova foto.
3. I risultati compaiono nella griglia a destra e come overlay colorato sull'immagine
   (verde = OK, rosso = mancante, arancione = colore errato, giallo = incerto/da verificare
   a mano — tipicamente per riflesso del flash o occlusione).
4. Facoltativo: "Carica expected.json..." prima del match per calcolare precisione/recall/F1
   rispetto a una ground truth etichettata a mano (stesso schema di `expected.json` nel repo).
5. "Esporta report...": scrive `test_report.json`, `test_annotated.png` e una riga in
   `summary.csv` nella cartella scelta.

### 3. Batch (più foto)
Aggiungi più immagini di test, scegli una cartella di output e avvia: per ciascuna immagine
viene chiesta la calibrazione (i punti di riferimento vanno ricliccati per ogni foto, dato
che cambiano risoluzione/rotazione), poi i risultati si accumulano in un unico
`summary.csv` più una sottocartella per immagine con `test_report.json`/`test_annotated.png`.

## Schema JSON (compatibilità con i file già nel repo)

- `templates_meta.json`: stessi campi (`created_at`, `templates[].index/name/image/bbox/width/height/md5`)
  più `referencePoints[]`, `sampleWidth`, `sampleHeight` e `colorClass` per slot.
- `expected.json`: invariato (`images[].expected[].template/present/bbox/note`).
- `test_report.json` / `summary.csv`: stessi nomi di campo/colonna. `Score`, `ColorSim`,
  `GoodMatches`, `Bbox` ora hanno un significato reale (confidenza colore, pixel validi usati,
  rettangolo proiettato via omografia) invece dei valori del vecchio matcher rotto. Sono stati
  aggiunti campi extra (`DetectedColor`, `ExpectedColor`, `PresenceStatus`, ecc.) che un lettore
  che conosce solo lo schema originale ignora senza problemi.

## Limiti noti

- Calibrazione manuale per ogni foto di test (vedi sopra) — nessun rilevamento automatico dei
  punti di riferimento in questa versione.
- Le Bitmap create per l'anteprima annotata non vengono esplicitamente rilasciate ad ogni match
  nella UI (piccola perdita di memoria per sessioni molto lunghe con moltissimi match ripetuti;
  non un problema per un normale turno di controllo qualità).
- Le soglie di classificazione (`MinValidRatioForConfidentRead`, `DarkRatioForAbsent`,
  `MaxColorDistanceForMatch` in `MatchingService.cs`) sono punti di partenza ragionevoli, non
  tarati su un dataset ampio: se in pratica vedi troppi "Incerto" o falsi "Colore errato",
  vanno regolati lì.
