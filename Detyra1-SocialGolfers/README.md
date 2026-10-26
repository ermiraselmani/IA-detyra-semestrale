# Detyra 1 – Social Golfers Problem (DFS me Backtracking, C#)

## Ekzekutimi
```bash
cd Detyra1-SocialGolfers
dotnet run -c Release -- [g] [p] [w] [timeoutSek]
# default: 8 4 10 60  (32 lojtarë, grupe me 4, 10 javë, 60 sekonda)
dotnet run -c Release -- 4 4 5      # 16 lojtarë, 5 javë -> zgjidhje e menjëhershme
dotnet run -c Release -- 3 3 5      # Kirkman 9 lojtarë: vërteton që 5 javë NUK ekzistojnë
```

## 1. Modelimi i problemit
- **Hyrja:** `g` (numri i grupeve), `p` (lojtarë për grup), `w` (numri i javëve), `n = g·p`.
- **Gjendja:** një orar i mbushur pjesërisht (javë → grup → pozitë).
- **Gjendja fillestare:** orar bosh (java 1 fiksohet për thyerje simetrie).
- **Veprimi:** vendos lojtarin e radhës në pozitën e lirë të radhës.
- **Gjendja përfundimtare (goal):** të `w` javët janë të mbushura dhe asnjë kufizim nuk shkelet.

## 2. Reprezentimi (variablat, domain-i, kufizimet)
- **Variablat:** `x[t][k][s]` = lojtari në javën `t`, grupin `k`, pozitën `s`
  (`t ∈ 1..w`, `k ∈ 1..g`, `s ∈ 1..p`) → `w·g·p` variabla.
- **Domain:** `D(x[t][k][s]) = {0, 1, …, n−1}`.
- **Kufizimet:**
  1. Brenda një jave secili lojtar paraqitet saktësisht një herë (çdo javë është ndarje e lojtarëve).
  2. Për çdo çift `(i, j)`: takohen në të njëjtin grup **më së shumti një herë** gjatë të gjitha javëve.
- **Në kod:** `schedule[t][k][s]`, `met[i]` (bitmask `ulong` i lojtarëve që `i` i ka takuar) dhe `free[t]`
  (bitmask i lojtarëve ende të pa-vendosur në javën `t`). Kontrolli i kufizimit bëhet me operacione bitesh.
- **Thyerja e simetrisë** (nuk ndryshon ekzistencën e zgjidhjes):
  - java 1 fikse: `{0..p−1}, {p..2p−1}, …`;
  - brenda grupit lojtarët janë rritës; grupet renditen sipas lojtarit më të vogël;
  - javët renditen sipas partnerit të lojtarit 0 në grupin e parë.

## 3. Objective function
Le të jetë `c(i,j)` numri i javëve kur lojtarët `i` dhe `j` janë në të njëjtin grup:

```
f(S) = Σ_{i<j} max(0, c(i,j) − 1)      → minimizohet; S është zgjidhje ⇔ f(S) = 0
```
Pyetja "për sa javë?" është problemi i optimizimit: **maksimizo w** me kusht `f(S) = 0`.
Programi llogarit `f(S)` për orarin e gjetur dhe e verifikon që `f = 0`.

## 4. Landscape (madhësia e hapësirës së kërkimit), për g=8, p=4, w=10
| Reprezentimi | Formula | Madhësia |
|---|---|---|
| Naiv: secili lojtar merr një grup çdo javë | `g^(n·w) = 8^320` | ≈ 9.75·10^288 |
| Ndarje valide për një javë | `n! / ((p!)^g · g!)` | ≈ 5.93·10^19 |
| Ndarje valide për w javë | `(ndarje)^w` | ≈ 5.37·10^197 |
| Me thyerje simetrie (java 1 fikse, javët të renditura) | `(ndarje)^(w−1) / (w−1)!` | ≈ 2.49·10^172 |

- Çifte gjithsej: `C(32,2) = 496`; çdo javë konsumon `g·C(p,2) = 48` çifte.
- Kufiri i sipërm: secili lojtar takon `p−1 = 3` të rinj në javë nga `n−1 = 31` → `w ≤ ⌊31/3⌋ = 10`.

## 5. DFS me Backtracking
`Dfs(week, group, slot, groupMet)`:
1. Nëse grupi mbushet → kalo te grupi tjetër; nëse java mbushet → java tjetër; nëse `week == w` → **sukses**.
2. Kandidatët = lojtarët e lirë në këtë javë, që **nuk** e kanë takuar asnjë anëtar të grupit, dhe më të mëdhenj
   se anëtari i fundit (`free & ~groupMet & above`).
3. Forward checking: nëse kandidatët janë më pak se pozitat e mbetura → backtrack.
4. Për secilin kandidat (në thellësi): `Place` → thirrje rekursive → `Remove` (backtrack).

## Rezultatet
| Instanca | Rezultati | Nyje |
|---|---|---|
| 3-3-4 (Kirkman) | zgjidhje | 28 |
| 3-3-5 | **nuk ekziston** (hapësira u eksplorua plotësisht) | 926 |
| 4-4-5 | zgjidhje | 64 |
| 8-4-5 | zgjidhje, menjëherë | – |
| 8-4-6 … 8-4-10 | DFS e thjeshtë nuk e gjen brenda 60s (~7·10^9 nyje) | – |

Me DFS të thjeshtë (kërkim i painformuar) për 32 lojtarë gjenden **5 javë** menjëherë. Përtej kësaj kërkimi
ngec, sepse orari i javëve 1–5, i ndërtuar me radhë leksikografike, nuk mund të zgjerohet dhe backtracking-u
duhet të kthehet shumë thellë në një hapësirë ≈10^172. Optimumi teorik (dhe i njohur) është **10 javë**,
por kjo arrihet vetëm me teknika të informuara/heuristika ose me ndërtime matematikore, që nuk janë pjesë e
kësaj detyre.
