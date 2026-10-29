# Detyra 3 – Latin Square (IDDFS me Backtracking, C#)

## Ekzekutimi
```bash
cd Detyra3-LatinSquare
dotnet run -c Release -- 5      # hyrja n (default 5)
```

## 1. Modelimi i problemit
- **Hyrja:** numri `n`.
- **Gjendja:** matrica `n×n`, ku disa qeliza janë të mbushura.
- **Gjendja fillestare:** matrica bosh. Pas thyerjes së simetrisë, rreshti i parë dhe kolona e parë janë `1..n`.
- **Veprimi:** mbush qelizën e radhës bosh (rresht pas rreshti) me një vlerë `1..n`.
- **Goal test:** të gjitha qelizat janë të mbushura dhe asnjë vlerë nuk përsëritet në rresht apo kolonë.
- **Kostoja:** çdo veprim kushton 1, prandaj thellësia e zgjidhjes është numri i variablave.

## 2. Reprezentimi (variablat, domain-i, kufizimet)
- **Variablat:** `x[r][c]` për `r, c ∈ 0..n−1`, pra `n²` variabla.
- **Domain:** `D(x[r][c]) = {1, …, n}`.
- **Kufizimet:**
  1. `x[r][c] ≠ x[r][c']` për `c ≠ c'` (rreshti AllDifferent);
  2. `x[r][c] ≠ x[r'][c]` për `r ≠ r'` (kolona AllDifferent).
- **Thyerja e simetrisë:** çdo katror latin mund të sillet në formë të reduktuar (rreshti 1 dhe kolona 1
  janë `1, 2, …, n`) duke ndërruar rreshtat, kolonat dhe simbolet. Prandaj këto `2n−1` qeliza fiksohen,
  dhe mbeten vetëm `(n−1)²` variabla.
- **Në kod:** `grid[r,c]`, `rowUsed[r]` dhe `colUsed[c]` (bitmask i vlerave të përdorura). Kontrolli i një
  kufizimi bëhet me një operacion bitesh, në `O(1)`.

## 3. Objective function
```
f(S) = Σ_rreshta (n − |vlera të ndryshme në rresht|) + Σ_kolona (n − |vlera të ndryshme në kolonë|)
```
Funksioni minimizohet: `S` është katror latin ⇔ `f(S) = 0`. Programi e llogarit `f` për matricën e gjetur.

## 4. Landscape (madhësia e hapësirës së kërkimit)
| Reprezentimi | Formula | n = 5 |
|---|---|---|
| Naiv (çdo qelizë 1..n) | `n^(n²)` | 2.98e17 |
| Çdo rresht permutacion | `(n!)^n` | 2.49e10 |
| Kufiri i sipërm van Lint–Wilson | `Π_(k=1..n) (k!)^(n/k)` | 714,396 |
| Numri i saktë i katrorëve latinë | `L(n)` | 161,280 |
| Katrorë të reduktuar | `L(n) / (n!·(n−1)!)` | 56 |
| Me thyerje simetrie, naiv | `n^((n−1)²)` | 1.53e11 |

- Faktori i degëzimit është `b ≤ n−1` dhe thellësia e zgjidhjes `d = (n−1)²`.
- IDDFS ka kompleksitet `O(b^d)` në kohë dhe `O(d)` në memorie (si DFS-ja), por gjen zgjidhjen më të cekët (si BFS-ja).

## 5. IDDFS me Backtracking
```
for limit = 0, 1, 2, …, (n−1)²:
    rezultati ← DLS(depth = 0, limit)
    nëse rezultati == Found   → kthe matricën
    nëse rezultati == Failure → nuk ka zgjidhje (asnjë degë nuk u ndërpre nga kufiri)

DLS(depth, limit):
    nëse të gjitha variablat janë mbushur → Found
    nëse depth == limit                    → Cutoff
    për çdo vlerë v të lejuar në qelizën e radhës:     // kontrolli i kufizimeve
        vendos v → DLS(depth+1, limit) → hiq v          // backtracking
    kthe Cutoff nëse ndonjë degë u ndërpre, përndryshe Failure
```
- **Iterative deepening:** kufiri i thellësisë rritet me 1 në çdo iterim, dhe çdo iterim fillon kërkimin nga e para.
- **Backtracking:** vlerat që e shkelin kufizimin nuk provohen fare. Kur një qelizë nuk ka asnjë vlerë
  të lejuar, vlera e fundit hiqet dhe provohet alternativa e radhës.
- Programi printon numrin e nyjeve për secilin iterim.

## Rezultatet
| n | Iterime | Nyje gjithsej | Koha |
|---|---|---|---|
| 3 | 5 | 19 | < 1 ms |
| 5 | 17 | 4,914 | ~1 ms |
| 6 | 26 | 1,194,077 | ~50 ms |
| 7 | 37 | 3,355,852,684 | ~40 s |

Për çdo `n` programi gjen një katror latin valid me `f(S) = 0`. Shembulli për `n = 5`:
```
1 2 3 4 5
2 1 4 5 3
3 4 5 1 2
4 5 2 3 1
5 3 1 2 4
```
Katrori i gjetur ndryshon nga ai në shembullin e detyrës (zhvendosja ciklike), sepse ka shumë katrorë latinë
valid dhe DFS-ja kthen të parin sipas radhës së vlerave `1..n`.

**Kufizimi i IDDFS-së:** zgjidhjet ekzistojnë vetëm në thellësinë `(n−1)²`, prandaj çdo iterim me kufi më të
vogël e kalon të gjithë pemën deri në atë kufi pa gjetur zgjidhje. Pa thyerjen e simetrisë, `n = 6` nuk
mbaronte as pas disa minutash. Me të, `n = 7` mbaron për ~40 s. Për `n ≥ 8` numri i katrorëve latinë të
reduktuar (≈5.4·10^11) e bën IDDFS-në e plotë jopraktike.
