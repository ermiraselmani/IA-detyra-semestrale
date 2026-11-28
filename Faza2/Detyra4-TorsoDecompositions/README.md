# Detyra 4 – Torso Decompositions me metaheuristika (Local Search, C#)

Përshkrimi: https://optimise.esa.int/challenge/spoc-3-torso-decompositions/About
(ESA SpOC-3, GECCO 2024). Grafet `easy.gr`, `medium.gr` dhe `hard.gr` janë marrë nga repo-ja zyrtare
https://github.com/esa/SpOC3 (licenca GPL-3.0).

## Ekzekutimi
```bash
cd Faza2/Detyra4-TorsoDecompositions
dotnet run -c Release                                   # instances/easy.gr, 10 s për metodë
dotnet run -c Release -- instances/medium.gr
dotnet run -c Release -- instances/hard.gr --sekonda 30 --seed 2
dotnet run -c Release -- instances/shembull.gr --vlereso "0,1,2,4,5,3,6,8,9,7,6"   # vlerëso një vektor
```
Programi i ekzekuton të gjitha metodat, i bashkon rezultatet në një arkiv dhe zgjedh **20 vektorët** që
japin hipervëllimin maksimal. Çdo gjerësi verifikohet me një implementim të dytë të pavarur. Zgjidhja
ruhet te `output/<instanca>_submission.json` në formatin e ESA Optimise. ID-të e problemeve janë
`small-graph`, `medium-graph` dhe `large-graph`.

| Grafi | n | m | Grada max | Grada mesatare |
|---|---|---|---|---|
| `shembull.gr` | 10 | 9 | 3 | 1.8 (shembulli nga përshkrimi) |
| `easy.gr` | 1,357 | 2,280 | 7 | 3.4 |
| `medium.gr` | 1,399 | 13,799 | 92 | 19.7 |
| `hard.gr` | 2,426 | 253,895 | 538 | 209.3 |

## 1. Problemi
Vektori i vendimit është `x = [p₁, …, pₙ, t]`, ku `π = (p₁, …, pₙ)` është permutacion i nyjeve dhe
`t ∈ {0, …, n−1}`.
- Permutacioni i orienton brinjët: `u → v` nëse `u` del para `v`.
- **Loja e eliminimit:** nyjet skanohen nga e majta në të djathtë. Për nyjen `u`, çdo dy out-fqinjë të saj
  `v, w` lidhen me brinjë të re, nëse nuk janë të lidhur.
- **Torso** është bashkësia e `n − t` nyjeve të fundit. **Gjerësia** është outdegree maksimal (me brinjët e reja)
  i një nyjeje në torso.
- **Dy objektiva:** maksimizo madhësinë `n − t` (pra minimizo `t`) dhe minimizo gjerësinë. Kërkohet një front
  Pareto me ≤ 20 vektorë. Ai vlerësohet me hipervëllim, ku gjerësitë > 500 llogariten si 501.

Shembulli nga përshkrimi (`shembull.gr`, `x = [0,1,2,4,5,3,6,8,9,7,6]`) jep **madhësi 4 dhe gjerësi 2**,
njësoj si te faqja e sfidës:
```
dotnet run -c Release -- instances/shembull.gr --vlereso "0,1,2,4,5,3,6,8,9,7,6"
Madhësia e torsos = 4, gjerësia = 2
```

## 2. Vlerësimi dhe vëzhgimet kryesore
- **Vlerësimi me bitset.** Fqinjësia ruhet si bitset (`⌈n/64⌉` fjalë për nyje). Eliminimi i `u` llogarit
  `out = Adj[u] ∧ Alive` dhe për çdo `v ∈ out` bën `Adj[v] |= out`. Një vlerësim i plotë kushton
  `O(Σ outdeg · n/64)`: rreth 0.7 ms në `easy` dhe rreth 9 ms në `hard`.
- **Një permutacion = një front i tërë.** Për permutacion fiks, `width(t) = max_{i ≥ t} outdeg(pᵢ)` është
  funksion jo-rritës i `t`, dhe çdo `t` jep vektor të vlefshëm. Prandaj nga një vlerësim merren njëherësh
  `n` pika `(t, width(t))`, dhe fitness-i i një permutacioni është **hipervëllimi i kësaj "shkalle"**:
  ```
  HV(π) = Σ_t (501 − min(width(t), 501)) / (n · 501)      (referenca: t = n, gjerësia = 501)
  ```
- **Torso varet nga bashkësia P, jo nga renditja e saj.** Le të jetë `P` bashkësia e `t` nyjeve të para.
  Grafi që mbetet pas eliminimit të `P` është `G[S]` plus një klikë mbi fqinjësinë e çdo komponente të
  `G[P]`, pavarësisht renditjes brenda `P`. Kjo motivon metaheuristikën e fundit (SA mbi P).
- **Arkivi.** Për çdo `t` ruhet gjerësia më e mirë e gjetur dhe permutacioni që e jep. Në fund zgjidhen
  20 pikat që maksimizojnë HV me **programim dinamik të saktë** (`O(k·m²)`), jo me zgjedhje greedy.

## 3. Metodat
| Metoda | Lloji | Përshkrimi |
|---|---|---|
| Greedy min-degree | konstruktive | Në çdo hap eliminohet nyja me gradën më të vogël në grafin aktual. Është heuristika klasike për treewidth. |
| Kërkim i rastësishëm | baseline | Permutacione të rastësishme brenda buxhetit kohor. |
| Peeling | konstruktive iterative | Nyja e torsos me outdegree maksimal kalon në prefiks (`P`), pastaj pjesa tjetër renditet sërish me min-degree. |
| **GRASP** | metaheuristikë | Min-degree i rastësishëm (RCL: gradë ≤ min + δ, `δ ∈ {0..3}`) + kërkim lokal *first-improvement* me 20 hapa. |
| **Simulated Annealing** | metaheuristikë (local search) | Mbi permutacionin, fitness = `HV(π)`. Lëvizjet janë *swap* dhe *insertion*: 70% brenda dritares `n/50` dhe 30% kudo. Temperatura fillestare është ndryshimi mesatar i fitness-it, me ftohje gjeometrike sipas kohës deri në `T₀·10⁻³`. Fillon nga permutacion i rastësishëm, nga min-degree ose nga GRASP. |
| **SA mbi P (torso-aware)** | metaheuristikë (local search) | Gjendja është bashkësia `P` e nyjeve jashtë torsos. Dekodimi: `P` e para, pastaj pjesa tjetër me min-degree. Për deri në 12 gjerësi të synuara `w` minimizohet `f(P) = |P| + 20 · max(0, width(P) − w)`. Lëvizjet: hiq një nyje nga `P`, shto në `P` një nyje *bottleneck* (outdegree > w) ose zëvendëso njërën me tjetrën. Fillon nga arkivi i Peeling. |

## 4. Rezultatet (10 s për metodë, `seed = 1`)
HV është hipervëllimi i normalizuar i 20 pikave më të mira të secilës metodë. Gjerësia@t=0 është gjerësia
kur i gjithë grafi është torso, pra një kufi i sipërm i treewidth.

| Metoda | easy: gjerësia@0 | easy: HV | medium: gjerësia@0 | medium: HV | hard: HV |
|---|---|---|---|---|---|
| Greedy min-degree | 21 | 0.95842 | 273 | 0.50597 | 0.10169 |
| Kërkim i rastësishëm | 56 | 0.90288 | 676 | 0.17498 | 0.09944 |
| Peeling | 20 | 0.96702 | 272 | 0.50759 | 0.10330 |
| GRASP | **19** | 0.96280 | 268 | 0.51441 | 0.10488 |
| SA (nga i rastësishëm) | 31 | 0.95102 | 940 | 0.17146 | 0.09877 |
| SA (nga min-degree) | 21 | 0.96288 | 273 | 0.50607 | **0.12574** |
| SA (nga GRASP) | **19** | 0.96649 | 268 | 0.51444 | 0.12272 |
| **SA mbi P** | **19** | **0.97139** | **264** | **0.52104** | 0.10677 |
| **Arkivi i përbashkët (20 pika)** | **19** | **0.97147** | **264** | **0.52133** | **0.12627** |

### Fronti final për `easy` (20 vektorë, të gjithë të verifikuar)
| t | Madhësia | Gjerësia | | t | Madhësia | Gjerësia |
|---|---|---|---|---|---|---|
| 0 | 1357 | 19 | | 1098 | 259 | 9 |
| 379 | 978 | 18 | | 1103 | 254 | 8 |
| 381 | 976 | 17 | | 1109 | 248 | 7 |
| 544 | 813 | 16 | | 1114 | 243 | 6 |
| 671 | 686 | 15 | | 1208 | 149 | 5 |
| 1036 | 321 | 14 | | 1240 | 117 | 4 |
| 1044 | 313 | 13 | | 1260 | 97 | 3 |
| 1049 | 308 | 12 | | 1326 | 31 | 2 |
| 1054 | 303 | 11 | | 1333 | 24 | 1 |
| 1092 | 265 | 10 | | 1356 | 1 | 0 |

### Çfarë tregojnë rezultatet
- **Struktura ka rëndësi më shumë se kërkimi i verbër.** Kërkimi i rastësishëm dhe SA nga permutacion i
  rastësishëm janë shumë larg (gjerësi 56 kundrejt 19 në `easy`, 940 kundrejt 264 në `medium`). Hapësira
  `n!` është shumë e madhe për lëvizje lokale pa një pikënisje të mirë. Prandaj metaheuristikat inicializohen
  me konstruktive (min-degree, GRASP).
- **GRASP** e ul gjerësinë e plotë (treewidth i sipërm) nga 21 në 19 në `easy` dhe nga 273 në 268 në `medium`.
- **SA mbi permutacionin** është më i miri në `hard`: HV 0.126 kundrejt 0.102 të greedy. Ai i ul gjerësitë
  e torsove të mëdha, p.sh. torso me 1,814 nyje ka gjerësi 488 dhe torso me 1,125 nyje ka gjerësi 473.
- **SA mbi P** jep HV më të mirë në `easy` dhe `medium`. Ky përfaqësim e kërkon drejtpërdrejt bashkësinë
  jashtë torsos. P.sh. në `easy` gjerësia 3 arrihet me torso me 97 nyje, kurse metodat e tjera arrijnë vetëm 65.
  Në `hard` çdo vlerësim është më i shtrenjtë dhe 12 gjerësi të synuara në 10 s janë pak, prandaj aty SA mbi
  permutacionin është më efektiv.
- **Arkivi i përbashkët** i kombinon pikat më të mira të të gjitha metodave dhe jep HV maksimal te të tre grafet.
- Në `medium` dhe `hard` pjesa e djathtë e frontit ka `gjerësi ≈ madhësi − 1`: torso-t e vogla janë pothuajse
  klika. Këta grafe janë të dendur dhe pas eliminimit të pjesës tjetër nyjet e fundit lidhen pothuajse të gjitha mes tyre.

Me buxhet më të madh (`--sekonda`) metaheuristikat vazhdojnë të përmirësohen, sidomos SA mbi P te `medium`/`hard`.

**Shënim:** HV këtu llogaritet me pikën referuese `(n, 501)` dhe normalizohet në `[0, 1]`. Ky është
rindërtimi ynë i vlerësimit sipas përshkrimit. Vlerësuesi zyrtar i ESA mund të përdorë shkallë
ose pikë referuese tjetër, prandaj vlerat absolute të HV nuk krahasohen drejtpërdrejt me tabelën e renditjes.
