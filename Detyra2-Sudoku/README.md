# Detyra 2 – Sudoku Solver (BFS me Backtracking, C#)

## Ekzekutimi
```bash
cd Detyra2-Sudoku
dotnet run -c Release -- all        # zgjidh të tri nivelet: Easy, Medium, Hard
dotnet run -c Release -- hard       # vetëm një nivel (easy | medium | hard)
dotnet run -c Release -- 530070000600195000098000060800060003400803001700020006060000280000419005000080079
                                    # Sudoku e çfarëdoshme: 81 karaktere, 0 ose . për qelizë bosh
```

| Niveli | Burimi i shembullit | Qeliza të dhëna |
|---|---|---|
| Easy | shembulli klasik i Wikipedia-s | 30 |
| Medium | Sudoku standarde e nivelit mesatar | 30 |
| Hard | "World's hardest Sudoku" (Arto Inkala) | 21 |

## 1. Modelimi i problemit
- **Gjendja:** grid 9×9, ku disa qeliza janë të mbushura.
- **Gjendja fillestare:** grid-i i dhënë nga Sudoku.
- **Veprimi:** mbush qelizën e parë bosh (rresht pas rreshti) me një shifër `1..9`.
- **Goal test:** nuk ka qeliza bosh dhe asnjë kufizim nuk shkelet.
- **Kostoja:** çdo veprim kushton 1. Të gjitha zgjidhjet janë në thellësinë `d` = numri i qelizave bosh.

## 2. Reprezentimi (variablat, domain-i, kufizimet)
- **Variablat:** `x[r][c]` për çdo qelizë bosh (`r, c ∈ 0..8`). P.sh. Hard ka 60 variabla.
- **Domain:** `D(x[r][c]) = {1, …, 9}`. Qelizat e dhëna kanë domain me një vlerë të vetme.
- **Kufizimet (AllDifferent):**
  1. çdo rresht përmban 1..9 pa përsëritje;
  2. çdo kolonë përmban 1..9 pa përsëritje;
  3. çdo kuti 3×3 përmban 1..9 pa përsëritje.
- **Në kod:** gjendja është `byte[81]` (0 = bosh). `Candidates()` kthen bitmask-un e shifrave
  të lejuara për një qelizë, pasi heq shifrat që janë në rresht, kolonë dhe kuti.

## 3. Objective function
```
f(S) = (numri i qelizave bosh) + Σ (çifte qelizash me shifër të njëjtë në të njëjtin rresht/kolonë/kuti)
```
Funksioni minimizohet: `S` është zgjidhje ⇔ `f(S) = 0`. Programi e llogarit `f` për zgjidhjen e gjetur.
E përdor edhe për të refuzuar një hyrje që i shkel kufizimet që në fillim.

## 4. Landscape (madhësia e hapësirës së kërkimit)
| Reprezentimi | Easy | Medium | Hard |
|---|---|---|---|
| Grid krejt bosh `9^81` | 1.97e77 | 1.97e77 | 1.97e77 |
| Vetëm qelizat bosh `9^k` | 4.64e48 (k=51) | 4.64e48 (k=51) | 1.80e57 (k=60) |
| Pas heqjes së vlerave të ndaluara `Π|D(x)|` | 4.26e22 | 3.33e25 | 9.59e36 |

Për krahasim, numri i të gjitha grid-ave valide Sudoku është ≈ 6.67·10^21.
Faktori i degëzimit (branching factor) është `b ≤ 9` dhe thellësia `d = k`, prandaj BFS-ja e thjeshtë
ka kompleksitet `O(b^d)` në kohë dhe në memorie.

## 5. BFS me Backtracking
```
queue ← [gjendja fillestare]
while queue jo bosh:
    S ← queue.Dequeue()
    nëse S nuk ka qeliza bosh → ZGJIDHJE
    cell ← qeliza e parë bosh
    për çdo shifër d të lejuar në cell:        // kontrolli i kufizimeve
        queue.Enqueue(S me cell = d)
    nëse nuk ka asnjë shifër të lejuar → dead-end, gjendja braktiset (backtrack)
```
- **BFS:** gjendjet zgjerohen nivel pas niveli me radhë FIFO (`Queue<byte[]>`).
- **Backtracking:** fëmijët që shkelin kufizimet nuk gjenerohen fare. Kur një gjendje nuk ka asnjë vlerë
  të lejuar (dead-end), ajo degë braktiset dhe kërkimi vazhdon me alternativat që janë ende në radhë.
- Radha kufizohet në 10 milionë gjendje, që programi të mos e mbushë memorien.

## Rezultatet
| Niveli | Nyje të zgjeruara | Dead-end | Frontier maksimal | Koha |
|---|---|---|---|---|
| Easy | 4,632 | 1,371 | 738 | ~1 ms |
| Medium | 2,170 | 658 | 235 | ~1 ms |
| Hard | 2,068,781 | 705,261 | 356,812 | ~270 ms |

Për të tri nivelet u gjet zgjidhja me `f(S) = 0`. Te niveli Hard duket kostoja e BFS-së: frontier-i rritet
deri në rreth 357 mijë gjendje, sepse BFS-ja i mban në memorie të gjitha gjendjet e një niveli njëkohësisht.
