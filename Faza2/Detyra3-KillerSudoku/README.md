# Detyra 3 – Killer Sudoku me Constraint Programming (CSP, C#)

Përshkrimi: https://sudoku.com/killer

**Rregullat:** grid-i 9×9 mbushet me shifrat 1..9 ashtu që çdo rresht, kolonë dhe kuti 3×3 i përmban
të gjitha shifrat pa përsëritje (si te Sudoku). Përveç kësaj, grid-i është i ndarë në **kafaze** (cages):
shuma e shifrave në çdo kafaz duhet të jetë e barabartë me numrin e dhënë dhe brenda kafazit nuk lejohet
përsëritje. Në fillim nuk jepet asnjë shifër, vetëm kafazet dhe shumat.

## Ekzekutimi
```bash
cd Faza2/Detyra3-KillerSudoku
dotnet run -c Release                                   # instances/mesatare.txt
dotnet run -c Release -- instances/lehte.txt
dotnet run -c Release -- instances/veshtire.txt
dotnet run -c Release -- instances/i_pazgjidhshem.txt   # instancë pa zgjidhje
dotnet run -c Release -- instances/veshtire.txt --limit 50000000
dotnet run -c Release -- --gjenero <seed> <madhësia max e kafazit> <skedari>
```
Formati i hyrjes: 9 rreshta me id-në e kafazit për çdo qelizë, pastaj një rresht `id shuma` për çdo kafaz.
```
aabbbccdd        # qelizat me të njëjtin karakter i përkasin të njëjtit kafaz
aefbgccdh
...
a 12
b 20
```
Programi kontrollon që çdo kafaz të ketë shumë dhe të jetë i lidhur. Pastaj e zgjidh lojën me katër teknika
dhe e verifikon zgjidhjen.

| Skedari | Kafaze | Madhësia max | Rregulli i 45 | Rezultati |
|---|---|---|---|---|
| `lehte.txt` | 35 | 3 | 16 kufizime | 1 zgjidhje |
| `mesatare.txt` | 32 | 4 | 12 kufizime | 1 zgjidhje |
| `veshtire.txt` | 26 | 6 | 7 kufizime | 1 zgjidhje |
| `i_pazgjidhshem.txt` | 32 | 4 | 12 kufizime | pa zgjidhje |

Instancat janë gjeneruar me `--gjenero`. Gjeneratori krijon një grid të plotë të rastësishëm, e ndan në
kafaze të rastësishëm dhe e pranon ndarjen vetëm nëse solver-i vërteton se zgjidhja është **unike**. Te
`i_pazgjidhshem.txt` janë ndryshuar dy shuma të `mesatare.txt` (+1 dhe −1). Shuma totale mbetet 405,
prandaj kontradikta nuk shihet menjëherë.

## 1. Modelimi si CSP
- **Variablat:** `X = { x(r,c) : r, c ∈ 1..9 }`, 81 variabla, një për çdo qelizë.
- **Domain-i:** `D(x(r,c)) = {1, …, 9}`. Në kod domain-i ruhet si bitmask (bitët 1..9).
- **Kufizimet:**
  1. `AllDifferent` për çdo rresht (9), kolonë (9) dhe kuti 3×3 (9): **27 kufizime**.
  2. Për çdo kafaz `K` me shumë `s`:
     ```
     Σ_{(r,c) ∈ K} x(r,c) = s     dhe     AllDifferent(K)
     ```
     Ky është një kufizim **jo-binar** (n-ar), me aq variabla sa qeliza ka kafazi.
  3. **Kufizime redundante (rregulli i 45):** shifrat e çdo njësie kanë shumën `1 + … + 9 = 45`.
     Nëse kafazet që ndodhen krejtësisht brenda një njësie mbulojnë të gjitha qelizat përveç bashkësisë `L`, atëherë
     ```
     Σ_{(r,c) ∈ L} x(r,c) = 45 − Σ(shumat e atyre kafazeve)     dhe     AllDifferent(L)
     ```
     Këto kufizime rrjedhin nga (1) dhe (2), kështu nuk i ndryshojnë zgjidhjet. Por ato ndihmojnë
     propagimin. Shtohen vetëm kur `1 ≤ |L| ≤ 5`.

## 2. Teknikat CSP
| | Teknika | Përshkrimi |
|---|---|---|
| T1 | **Backtracking** | Qelizat caktohen sipas radhës (rresht pas rreshti), vlerat `1..9`. Kontrollohen vetëm kufizimet mbi variablat e caktuara: shifër e përsëritur në njësi ose kafaz dhe kufijtë e shumës së kafazit. |
| T2 | **Forward checking + MRV** | Pas çdo caktimi `x = d`, `d` hiqet nga domain-et e variablave të pacaktuara në të njëjtën njësi ose kafaz. Domain-et e qelizave të lira të kafazit priten sipas shumës së mbetur. Zgjidhet variabla me domain më të vogël (MRV). |
| T3 | **MAC + MRV** | *Maintaining Arc Consistency*: pas çdo vendimi propagohen të gjitha kufizimet deri në pikë fikse (shih më poshtë). |
| T4 | **MAC + MRV + rregulli i 45** | Si T3, me kufizimet redundante të shtuara. |

### Propagimi në MAC
- **AllDifferent:** nëse domain-i i një qelize ka vetëm një vlerë, ajo hiqet nga qelizat e tjera të njësisë
  (*naked single*). Nëse një shifër mund të vendoset vetëm në një qelizë të njësisë, qeliza merr atë vlerë
  (*hidden single*). Nëse një shifër nuk ka ku të vendoset, ka konflikt.
- **Shuma e kafazit, GAC (Generalized Arc Consistency):** vlera `v` mbetet në domain-in e qelizës vetëm nëse
  ekziston një plotësim i kafazit me shifra të ndryshme dhe shumë të saktë ku qeliza merr `v`. Plotësimet
  gjenerohen me DFS mbi domain-et aktuale. Degët priten me kufijtë
  `partial + (1+…+free) ≤ s ≤ partial + (9+8+…)`. Kështu çdo vlerë që nuk ka mbështetje hiqet.
- Propagimi përsëritet derisa asnjë domain të mos ndryshojë. Nëse një domain mbetet bosh, ka konflikt dhe bëhet backtrack.

Kërkimi nuk ndalet te zgjidhja e parë. Ai vazhdon për të kërkuar të dytën, kështu programi vërteton edhe
që zgjidhja është **unike**.

## 3. Rezultatet
`Nyje` = numri i vlerave të provuara, `Backtrack` = numri i variablave për të cilat u provuan të gjitha
vlerat. Limiti është 20M nyje.

| Instanca | T1 Backtracking | T2 FC + MRV | T3 MAC | T4 MAC + 45 |
|---|---|---|---|---|
| `lehte` | 162,000 nyje / 12 ms | 3,572 / 4 ms | **0** / 1 ms | **0** / 1 ms |
| `mesatare` | 2,315,259 / 170 ms | 19,162 / 15 ms | 37 / 9 ms | **0** / 1 ms |
| `veshtire` | **> 20M (limit)** | 6,678,704 / 1,037 ms | 82 / 99 ms | **8** / 11 ms |
| `i_pazgjidhshem` | 2,499,309 / 185 ms | 12,037 / 9 ms | 27 / 7 ms | **0** / 1 ms |

### Çfarë tregojnë rezultatet
- **Backtracking-u** nuk e sheh që një caktim i hershëm e bën kafazin të pamundur derisa të arrijë te ai.
  Te `veshtire` (kafaze deri në 6 qeliza) nuk e gjen zgjidhjen as me 20 milionë nyje.
- **Forward checking + MRV** e zvogëlon kërkimin rreth 100 herë. Por ai shikon vetëm një hap përpara dhe
  nuk i kombinon kufizimet, prandaj te `veshtire` duhen ende 6.7 milionë nyje.
- **MAC me GAC mbi kafazet** është rreth 5 rend madhësie më i mirë se FC. Te `lehte` loja zgjidhet vetëm me
  propagim, me **0 vendime**. Kjo është mënyra si njeriu zgjidh Killer Sudoku: kombinimet e mundshme të
  kafazit + eliminimi në njësi.
- **Rregulli i 45** e bën propagimin edhe më të fortë. `mesatare` dhe `i_pazgjidhshem` zgjidhen ose
  vërtetohen të pazgjidhshme pa asnjë vendim, kurse `veshtire` kërkon vetëm 8 nyje, 10 herë më pak se T3.
  Kufizimet redundante nuk e ndryshojnë bashkësinë e zgjidhjeve, por e bëjnë modelin më efikas.

Për instancën pa zgjidhje, të katër teknikat japin 0 zgjidhje dhe e kalojnë hapësirën deri në fund.

## Shembull dalje (`veshtire.txt`)
```
Kafaze: 26, madhësia më e madhe: 6, shuma e shumave: 405 (duhet 405)
Kufizime redundante nga rregulli i 45: 7

--- Krahasimi i teknikave CSP (kërkohen deri në 2 zgjidhje për të vërtetuar unicitetin) ---
Teknika                                              Nyje      Backtrack       Koha  Zgjidhje
Backtracking (rresht pas rreshti)              20 000 001      2 222 236      364ms  ≥0 (limit)
Forward checking + MRV                          6 678 704      4 069 876     1037ms  1 (unike)
MAC (GAC mbi kafazet) + MRV                            82             36       99ms  1 (unike)
MAC + MRV + rregulli i 45                               8              4       11ms  1 (unike)

Zgjidhja:
  +===+===+===+===+===+===+===+===+===+
  ‖ 6   5 | 3   7   2 | 9   8 | 4   1 ‖
  +   +---+---+   +---+       +   +---+
  ‖ 1 | 9 | 7 ‖ 5 | 8 | 4   2 | 3 | 6 ‖
  +---+   +   +---+---+       +---+   +
  ‖ 4   2 | 8   1   6 | 3   5 | 9 | 7 ‖
  ...
Verifikimi: rreshtat, kolonat, kutitë 3×3 dhe kafazet (shuma + pa përsëritje): të gjitha në rregull
Zgjidhje valide: po
```
Vijat `|` dhe `---` tregojnë kufijtë e kafazeve, kurse `‖` dhe `===` kufijtë e kafazeve që bien mbi vijat
e kutive 3×3. Para zgjidhjes shuma e kafazit shfaqet në qelizën e tij të parë.
