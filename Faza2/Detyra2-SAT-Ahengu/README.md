# Detyra 2 – SAT Problem: Ahengu (100 mysafirë, 10 tavolina, C#)

**Problemi:** 100 mysafirë duhet të ulen në 10 tavolina me nga 10 karriga, ashtu që:
- çiftet nga lista *ndaras* nuk ulen në të njëjtën tavolinë,
- çiftet nga lista *bashke* ulen gjithsesi në të njëjtën tavolinë,
- në çdo tavolinë dallimi mes numrit të meshkujve dhe femrave është më së shumti ±3.

**Kërkesa:** të definohet formula SAT. Programi e ndërton formulën në CNF, e ruan në formatin DIMACS,
e zgjidh me një SAT solver CDCL të implementuar këtu dhe e verifikon zgjidhjen.

## Ekzekutimi
```bash
cd Faza2/Detyra2-SAT-Ahengu
dotnet run -c Release                                       # instances/aheng100.txt
dotnet run -c Release -- instances/aheng100_dendur.txt      # 1500 çifte ndaras, 60 bashke
dotnet run -c Release -- instances/i_pazgjidhshem.txt       # instancë pa zgjidhje (UNSAT)
dotnet run -c Release -- instances/aheng100.txt --pa-simetri
dotnet run -c Release -- --gjenero <seed> <ndaras> <bashke> <skedari>   # instancë e re
```
Formati i hyrjes (`#` = koment):
```
tavolina 10
karrige 10
diferenca 3
gjinia M1 f        # m = mashkull, f = femër
ndaras M1 M5       # M1 dhe M5 NUK mund të ulen bashkë
bashke M2 M7       # M2 dhe M7 DUHET të ulen bashkë
```
Formula CNF ruhet te `output/<instanca>.cnf` (DIMACS). Ky skedar mund të jepet edhe në çdo solver
standard (MiniSat, Kissat, CaDiCaL).

| Skedari | Meshkuj/Femra | ndaras | bashke | Rezultati |
|---|---|---|---|---|
| `aheng100.txt` | 47 / 53 | 300 | 30 | SAT |
| `aheng100_dendur.txt` | 54 / 46 | 1500 | 60 | SAT |
| `i_pazgjidhshem.txt` | 47 / 53 | 298 | 36 | UNSAT (7 meshkuj duhet të jenë bashkë) |

Instancat e zgjidhshme gjenerohen nga një ulje e fshehur ("planted solution"). Çiftet *ndaras* merren nga
tavolina të ndryshme dhe çiftet *bashke* nga e njëjta tavolinë. Kështu instanca ka të paktën një zgjidhje.

## 1. Variablat
Le të jenë `M = {1..100}` mysafirët, `T = {1..10}` tavolinat, `S = 10` karriget, `D = 3` dallimi i lejuar.
`B ⊆ M` janë meshkujt dhe `F = M \ B` femrat.

```
x(m,t) = true  ⇔  mysafiri m ulet në tavolinën t          (100 · 10 = 1000 variabla)
```
Në DIMACS: `x(m,t) = (m−1)·10 + t`.

## 2. Formula SAT

**(1) Çdo mysafir ulet në të paktën një tavolinë:**
```
∀m ∈ M:   x(m,1) ∨ x(m,2) ∨ … ∨ x(m,10)
```

**(2) Çdo mysafir ulet në më së shumti një tavolinë:**
```
∀m ∈ M, ∀t < u:   ¬x(m,t) ∨ ¬x(m,u)
```

**(3) Kapaciteti i tavolinës: çdo tavolinë ka më së shumti 10 mysafirë:**
```
∀t ∈ T:   Σ_{m ∈ M} x(m,t) ≤ 10
```
Nga (1)+(2) çdo mysafir është në saktësisht një tavolinë. Kemi 100 mysafirë dhe 10 tavolina me ≤ 10 vende,
prandaj **çdo tavolinë ka saktësisht 10**. Kufiri "≥ 10" nuk duhet të kodohet veçmas.

**(4) Gjinia: |meshkuj − femra| ≤ 3 në çdo tavolinë.** Tavolina është plot, pra `B_t + F_t = 10`:
```
|B_t − F_t| ≤ 3  ⇔  |2·B_t − 10| ≤ 3  ⇔  3.5 ≤ B_t ≤ 6.5  ⇔  B_t ∈ {4, 5, 6}
```
Kjo kodohet me dy kufizime "at-most":
```
∀t ∈ T:   Σ_{m ∈ B} x(m,t) ≤ 6    dhe    Σ_{m ∈ F} x(m,t) ≤ 6
```
(`F_t ≤ 6` është e njëjta gjë si `B_t ≥ 4`.) Në përgjithësi kufiri është `⌊(S + D)/2⌋`.

**(5) Çiftet që nuk mund të ulen bashkë.** Për çdo `(a, b)` nga lista *ndaras*:
```
∀t ∈ T:   ¬x(a,t) ∨ ¬x(b,t)
```

**(6) Çiftet që duhet të ulen bashkë.** Për çdo `(a, b)` nga lista *bashke*, `x(a,t) ↔ x(b,t)`:
```
∀t ∈ T:   (¬x(a,t) ∨ x(b,t)) ∧ (¬x(b,t) ∨ x(a,t))
```

**Formula e plotë:**
```
Φ = (1) ∧ (2) ∧ (3) ∧ (4) ∧ (5) ∧ (6)  [ ∧ (7) ]
```
`Φ` është e plotësueshme **atëherë dhe vetëm atëherë** kur ekziston një ulje që i plotëson të gjitha kushtet.
Nga modeli lexohet ulja: mysafiri `m` ulet në tavolinën `t` për të cilën `x(m,t) = true`.

### Kodimi i kufizimeve "Σ ≤ k" në CNF (sequential counter)
Kodimi naiv i `Σ x_i ≤ k` ndalon çdo nënbashkësi me `k+1` elemente: `¬x_{i1} ∨ … ∨ ¬x_{i(k+1)}`.
Për (3) kjo jep `C(100, 11) ≈ 1.4·10^14` klauzola për tavolinë, gjë që nuk është e realizueshme. Prandaj
përdoret **sequential counter** (Sinz, 2005) me variabla ndihmëse `s(i,j)`, ku `s(i,j)` do të thotë
"të paktën `j` nga `x_1..x_i` janë true":
```
¬x_1 ∨ s(1,1)
¬s(1,j)                                       j = 2..k
për 1 < i < n:
    ¬x_i ∨ s(i,1)
    ¬s(i−1,1) ∨ s(i,1)
    ¬x_i ∨ ¬s(i−1,j−1) ∨ s(i,j)               j = 2..k     (numëruesi rritet)
    ¬s(i−1,j) ∨ s(i,j)                        j = 2..k     (numëruesi nuk zvogëlohet)
    ¬x_i ∨ ¬s(i−1,k)                                       (k+1-shi është i ndaluar)
¬x_n ∨ ¬s(n−1,k)
```
Ky kodim ka `O(n·k)` klauzola dhe variabla. Ai ruan edhe arc-consistency: sapo `k` variabla bëhen true,
unit propagation i bën false të gjitha të tjerat.

### (7) Thyerja e simetrisë (opsionale)
Tavolinat janë identike, kështu çdo zgjidhje ka `10! ≈ 3.6·10^6` kopje me tavolina të rinumëruara.
Tavolinat mund të rinumërohen sipas mysafirit me indeksin më të vogël në secilën. Atëherë mysafiri `m` nuk mund
të jetë në tavolinë me numër më të madh se `m`:
```
∀m < 10, ∀t > m:   ¬x(m,t)          (45 klauzola njësi)
```
Kjo nuk e ndryshon plotësueshmërinë e formulës, vetëm e zvogëlon hapësirën e kërkimit. Çaktivizohet me `--pa-simetri`.

## 3. Madhësia e formulës (`aheng100.txt`)
| Grupi i klauzolave | Klauzola |
|---|---|
| (1) të paktën një tavolinë | 100 |
| (2) më së shumti një tavolinë | 4,500 |
| (3) kapaciteti ≤ 10 (sequential counter) | 20,690 |
| (4) gjinia ≤ 6 M dhe ≤ 6 F | 12,620 |
| (5) 300 çifte ndaras × 10 | 3,000 |
| (6) 30 çifte bashke × 10 × 2 | 600 |
| (7) thyerja e simetrisë | 45 |
| **Gjithsej** | **41,555** |

Variabla: 1,000 `x(m,t)` + 15,780 ndihmëse = **16,780**.

## 4. SAT solver-i (CDCL)
`CdclSolver.cs` është një solver CDCL i implementuar nga fillimi, pa librari të jashtme:
- **two watched literals** për unit propagation,
- **analizë 1-UIP** e konflikteve, mësim klauzolash dhe *non-chronological backjumping*,
- **VSIDS** (aktivitet i variablave me heap) për zgjedhjen e variablës,
- **phase saving** dhe **restart-e Luby**.

Solver-i është testuar kundrejt brute-force në 3,400 formula të rastësishme. Disa prej tyre janë 3-SAT
në pragun e vështirësisë (`m/n = 4.26`). Nuk u gjet asnjë mospërputhje SAT/UNSAT dhe çdo model i kthyer
i plotëson të gjitha klauzolat.

### Rezultatet
| Instanca | Rezultati | Koha | Vendime | Konflikte |
|---|---|---|---|---|
| `aheng100.txt` | SAT | ~60 ms | 1,488 | 407 |
| `aheng100.txt --pa-simetri` | SAT | ~80 ms | 2,208 | 608 |
| `aheng100_dendur.txt` | SAT | ~20 ms | 45 | 2 |
| `i_pazgjidhshem.txt` | UNSAT | ~10 ms | 0 | 0 (në nivelin 0) |
| `i_pazgjidhshem.txt --pa-simetri` | UNSAT | ~15 ms | 120 | 17 |

- Instanca e dendur zgjidhet më shpejt, sepse çiftet *ndaras* dhe *bashke* japin shumë unit propagation.
- Në `i_pazgjidhshem.txt`, 7 meshkuj lidhen me zinxhir *bashke*, prandaj duhet të jenë në të njëjtën
  tavolinë. Kjo e kalon kufirin prej 6 meshkujsh. Me thyerjen e simetrisë mysafiri i parë i zinxhirit
  kufizohet në pak tavolina dhe kontradikta gjendet vetëm me propagation. Pa të, solver-i e vërteton
  UNSAT pas 17 konflikteve.

Pas zgjidhjes, programi e dekodon uljen dhe i verifikon **të gjitha kushtet drejtpërdrejt mbi uljen**,
jo mbi formulën: një tavolinë për mysafir, 10 mysafirë për tavolinë, `|M − F| ≤ 3`, çiftet *ndaras* dhe *bashke*.

## Shembull dalje (`aheng100.txt`)
```
--- CDCL solver: SAT për 59 ms ---
Vendime: 1 488, konflikte: 407, propagime: 321 919, klauzola të mësuara: 407

Ulja e mysafirëve:
  Tavolina  1 (6M/4F): M1♀ M2♀ M7♂ M12♂ M21♀ M32♂ M38♂ M57♂ M80♀ M92♂
  Tavolina  2 (5M/5F): M18♂ M42♀ M43♀ M44♂ M46♀ M58♂ M59♂ M72♂ M74♀ M98♀
  Tavolina  3 (4M/6F): M3♀ M5♂ M34♀ M53♀ M61♀ M66♂ M67♂ M69♀ M89♂ M95♀
  ...
Verifikimi:
  çdo mysafir në saktësisht një tavolinë     po
  çdo tavolinë me saktësisht 10 mysafirë     po
  |M − F| ≤ 3 në çdo tavolinë                po
  çiftet 'ndaras' në tavolina të ndryshme    po
  çiftet 'bashke' në të njëjtën tavolinë     po
Zgjidhje valide: po
```
