# Detyra 1 – Blocked N-Queens (A* me heuristika, C#)

Përshkrimi: https://dtai.cs.kuleuven.be/events/ASP-competition/Benchmarks/BlockedQueens.shtml

## Ekzekutimi
```bash
cd Faza2/Detyra1-BlockedNQueens
dotnet run -c Release                                  # instances/q8.lp
dotnet run -c Release -- instances/shembull4.lp        # shembulli nga faqja e problemit
dotnet run -c Release -- instances/q10.lp --seed 7     # gjendje fillestare tjetër
dotnet run -c Release -- instances/i_pazgjidhshem.lp   # instancë pa zgjidhje
dotnet run -c Release -- instances/q10.lp --limit 500000
```
Hyrja ka formatin e ASP Competition: `num(1). … num(N).` dhe `block(i,j).` (rreshti `i`, kolona `j`).
Dalja jepet në të njëjtin format: `queen(i,j).`

| Skedari | N | Të bllokuar | Shënim |
|---|---|---|---|
| `shembull4.lp` | 4 | 3 | shembulli nga faqja |
| `q8.lp` | 8 | 10 | |
| `q10.lp` | 10 | 15 | |
| `i_pazgjidhshem.lp` | 4 | 6 | nuk ka zgjidhje |

## 1. Modelimi i problemit për A*
Në formulimin inkremental (vendos mbretëreshat një nga një) çdo zgjidhje ndodhet në thellësinë `N`.
Kostoja deri te goal-i është gjithmonë `N − k`, prandaj heuristikat nuk kanë çfarë të vlerësojnë.
Për këtë arsye përdoret **formulimi me gjendje të plotë**, ku kostoja e rrugës ka kuptim:

- **Gjendja:** `rows[c]` = rreshti i mbretëreshës në kolonën `c`. Ka saktësisht një mbretëreshë për kolonë,
  gjithmonë në katror të pabllokuar. Konfliktet në kolonë janë të pamundura.
- **Gjendja fillestare:** për çdo kolonë zgjidhet rastësisht një rresht i lirë (`--seed`).
- **Veprimi:** zhvendos mbretëreshën e një kolone në një rresht tjetër të lirë të së njëjtës kolonë.
  Faktori i degëzimit është `Σ_c (|Allowed[c]| − 1) ≤ N(N−1)`.
- **Kostoja:** çdo lëvizje kushton 1. A* gjen numrin **minimal të lëvizjeve** nga gjendja fillestare
  te një vendosje pa konflikte.
- **Goal test:** asnjë çift mbretëreshash nuk sulmohet (rresht, diagonale `r−c`, antidiagonale `r+c`).
- **Landscape:** `Π_c |Allowed[c]|` gjendje (për `q8.lp`: `6²·7⁶ ≈ 4.2·10^6`).

### A*
```
open ← {start}, g(start) = 0, f = g + h
derisa open ≠ ∅:
    s ← nyja me f më të vogël (barazim: h më i vogël)
    nëse s është goal → rindërto rrugën me parent[]
    për çdo fqinj s' (një lëvizje):
        nëse g(s) + 1 < bestG[s']: bestG[s'] ← g(s)+1, parent[s'] ← s, shto s' në open
```
- Gjendja kodohet në një `ulong` (4 bit për kolonë, `N ≤ 16`) dhe përdoret si çelës në `bestG`.
- Nëse një gjendje arrihet më vonë me `g` më të vogël, ajo rihapet. Kështu A* mbetet optimal me çdo
  heuristikë admissible, edhe kur ajo nuk është konsistente.
- Para kërkimit bëhet një kontroll i shpejtë me backtracking për instancat pa zgjidhje, sepse përndryshe A*
  do ta kalonte gjithë hapësirën.

## 2. Heuristikat
Në gjendjen `s` le të jenë `row[i]`, `diag[i]`, `anti[i]` numrat e mbretëreshave në secilën vijë.

| | Heuristika | Formula |
|---|---|---|
| H1 | Çifte që sulmohen | `Σ_vija C(cnt, 2)` (dy mbretëresha ndajnë më së shumti një vijë) |
| H2 | Mbretëresha në konflikt | numri i mbretëreshave që sulmohen nga të paktën një tjetër |
| H3 | Teprica maksimale në vija | `max(R, D, A)`, ku `R = Σ_rreshta max(0, cnt − 1)`, `D` dhe `A` njësoj për diagonalet |
| H0 | `h = 0` | UCS, vetëm si referencë për koston optimale |

### Rezultatet (`seed = 1`)
| Instanca | Heuristika | h(start) | Lëvizje | Nyje zgjeruar | Optimale? |
|---|---|---|---|---|---|
| q8 | H0 (UCS) | 0 | 5 | 141,067 | po |
| q8 | H1 | 8 | **6** | 46 | **jo** |
| q8 | H2 | 7 | **6** | 22 | **jo** |
| q8 | H3 | 3 | 5 | 266 | po |
| q8 | **H4** | 3 | 5 | **136** | po |
| q10 | H0 (UCS) | 0 | limit (1M) | 1,000,001 | – |
| q10 | H1 | 13 | **8** | 21 | **jo** |
| q10 | H2 | 10 | **8** | 16 | **jo** |
| q10 | H3 | 5 | 6 | 844 | po |
| q10 | **H4** | 5 | 6 | **308** | po |
| shembull4 | të gjitha | – | 3 | 5–44 | po |

### Çfarë tregojnë rezultatet
- **H1 dhe H2 nuk janë admissible.** Në `q8` kemi `H1(start) = 8` dhe `H2(start) = 7`, kurse `h* = 5`.
  Arsyeja është se një lëvizje e vetme mund t'i zgjidhë disa konflikte njëherësh. P.sh. nëse një mbretëreshë
  sulmon tri të tjera që nuk sulmohen mes vete, `H1 = 3`, `H2 = 4`, por mjafton 1 lëvizje. Për këtë arsye A*
  me H1/H2 është shumë i shpejtë (16–46 nyje), por gjen rrugë me **6 dhe 8 lëvizje në vend të 5 dhe 6**.
- **H3 është admissible.** Një lëvizje e heq mbretëreshën nga një rresht i vetëm, kështu `R` zvogëlohet
  më së shumti për 1. E njëjta vlen për `D` dhe `A`. Në goal `R = D = A = 0`, prandaj `h* ≥ max(R, D, A)`.
  Gjithashtu `|H3(s) − H3(s')| ≤ 1` për çdo lëvizje, pra H3 është edhe konsistente. Por H3 është e dobët
  sepse sheh vetëm një lloj vije njëherësh.
- UCS (H0) është optimal, por për `N = 10` nuk mbaron as me 1 milion nyje.

## 3. Heuristika admissible e propozuar: H4
Nga H1/H2 shihet se mbivlerësimi ndodh kur i njëjti çift ose e njëjta mbretëreshë numërohet disa herë
për konflikte që zgjidhen me një lëvizje. Zgjidhja është që të numërohen **vetëm konfliktet që kërkojnë
lëvizje të ndryshme**:

```
H4(s) = max( H3(s), |M(s)| )
M(s)  = matching (greedy) në grafin e konflikteve: çifte që sulmohen dhe nuk kanë asnjë mbretëreshë të përbashkët
```

**Pse është admissible:** le të jetë `Q` bashkësia e mbretëreshave që lëvizin në një zgjidhje optimale.
Nëse çifti `(a, b)` sulmohet dhe asnjëra nuk lëviz, ai sulmohet edhe në fund. Prandaj çdo çift në `M(s)`
ka të paktën një mbretëreshë në `Q`. Meqë çiftet në `M(s)` nuk kanë mbretëresha të përbashkëta,
`|M(s)| ≤ |Q| ≤ h*(s)`. Maksimumi i dy heuristikave admissible është admissible dhe dominon të dyja.

H4 nuk është domosdoshmërisht konsistente, sepse matching-u greedy mund të ndryshojë me më shumë se 1
mes dy fqinjëve. Për këtë A* i rihap nyjet (shih seksionin 1), kështu optimaliteti ruhet.

**Rezultati:** H4 jep gjithmonë koston optimale, si H3 dhe UCS, por zgjeron rreth **2–3 herë më pak nyje
se H3** (136 kundrejt 266 në `q8`, 308 kundrejt 844 në `q10`) dhe **~1000 herë më pak se UCS**. Programi
kontrollon edhe përgjatë rrugës së gjetur që `H4(s) ≤ kostoja e mbetur` për çdo gjendje.

## Shembull dalje (`shembull4.lp`)
```
      1  2  3  4
  1   #  .  Q  .
  2   Q  #  .  .
  3   .  .  .  Q
  4   .  Q  #  .

queen(2,1). queen(4,2). queen(1,3). queen(3,4).
```
Kjo është e njëjta zgjidhje që jepet në faqen e problemit.
