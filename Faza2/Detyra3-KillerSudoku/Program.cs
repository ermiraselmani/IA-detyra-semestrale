using System.Diagnostics;
using System.Numerics;
using System.Text;

// Killer Sudoku si CSP: backtracking, forward checking + MRV dhe MAC (GAC mbi kafazet), me rregullin e 45.

if (args.Length > 0 && args[0] == "--gjenero")
{
    Generator.Run(args);
    return;
}

string[] rest = args.Where(a => !a.StartsWith("--")).ToArray();
long limit = LongArg("--limit", 20_000_000);
string path = rest.Length > 0 ? rest[0] : "instances/mesatare.txt";
if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, path);
if (!File.Exists(path))
{
    Console.WriteLine($"Skedari nuk u gjet: {rest.ElementAtOrDefault(0) ?? "instances/mesatare.txt"}");
    return;
}

Puzzle puzzle;
try { puzzle = Puzzle.Parse(File.ReadAllText(path)); }
catch (FormatException e) { Console.WriteLine($"Gabim në hyrje: {e.Message}"); return; }

Console.WriteLine($"=== Killer Sudoku (CSP): {Path.GetFileName(path)} ===");
Console.WriteLine($"Kafaze: {puzzle.Cages.Count}, madhësia më e madhe: {puzzle.Cages.Max(c => c.Cells.Length)}, " +
                  $"shuma e shumave: {puzzle.Cages.Sum(c => c.Sum)} (duhet 405)");
var derived = Csp.Rule45(puzzle);
Console.WriteLine($"Kufizime redundante nga rregulli i 45: {derived.Count}");
Console.WriteLine();
Console.WriteLine("Loja (shuma shënohet në qelizën e parë të çdo kafazi):");
Render.Board(puzzle, null);
Console.WriteLine();

var modes = new[] { Mode.Backtracking, Mode.ForwardChecking, Mode.Mac, Mode.Mac45 };
var results = new List<(Mode Mode, Solver S, TimeSpan T)>();
Console.WriteLine("--- Krahasimi i teknikave CSP (kërkohen deri në 2 zgjidhje për të vërtetuar unicitetin) ---");
Console.WriteLine($"{"Teknika",-42} {"Nyje",14} {"Backtrack",14} {"Koha",10}  Zgjidhje");
foreach (var mode in modes)
{
    var solver = new Solver(puzzle, mode, limit, maxSolutions: 2);
    var sw = Stopwatch.StartNew();
    solver.Run();
    sw.Stop();
    results.Add((mode, solver, sw.Elapsed));
    string sols = solver.HitLimit ? $"≥{solver.Solutions.Count} (limit)" :
                  solver.Solutions.Count switch { 0 => "0", 1 => "1 (unike)", _ => "≥2" };
    Console.WriteLine($"{Solver.Name(mode),-42} {solver.Nodes,14:N0} {solver.Backtracks,14:N0} " +
                      $"{sw.Elapsed.TotalMilliseconds,8:F0}ms  {sols}");
}
Console.WriteLine();

var best = results.First(r => r.Mode == Mode.Mac45).S;
if (best.Solutions.Count == 0)
{
    Console.WriteLine("Problemi NUK ka zgjidhje: CSP-ja është e paplotësueshme.");
    return;
}

int[] solution = best.Solutions[0];
Console.WriteLine("Zgjidhja:");
Render.Board(puzzle, solution);
Console.WriteLine();
var errors = puzzle.Verify(solution);
Console.WriteLine($"Verifikimi: rreshtat, kolonat, kutitë 3×3 dhe kafazet (shuma + pa përsëritje): " +
                  $"{(errors.Count == 0 ? "të gjitha në rregull" : string.Join("; ", errors))}");
Console.WriteLine($"Zgjidhje valide: {(errors.Count == 0 ? "po" : "JO")}");
if (best.Solutions.Count > 1)
    Console.WriteLine("Kujdes: loja ka më shumë se një zgjidhje.");

long LongArg(string name, long def)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && long.TryParse(args[i + 1], out long v) ? v : def;
}

sealed record Cage(int Sum, int[] Cells);

sealed class Puzzle
{
    public int[] CageOf = new int[81];
    public List<Cage> Cages = [];

    // Formati: 9 rreshta me id-në e kafazit për çdo qelizë, pastaj rreshta "id shuma".
    public static Puzzle Parse(string text)
    {
        var rows = new List<string>();
        var sums = new Dictionary<char, int>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var w = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 1 && w[0].Length == 9) rows.Add(w[0]);
            else if (w.Length == 2 && w[0].Length == 1 && int.TryParse(w[1], out int s)) sums[w[0][0]] = s;
            else throw new FormatException($"rresht i panjohur: '{line}'");
        }
        if (rows.Count != 9) throw new FormatException($"priten 9 rreshta të kafazeve, u gjetën {rows.Count}");

        var p = new Puzzle();
        var ids = new Dictionary<char, int>();
        var cells = new List<List<int>>();
        for (int c = 0; c < 81; c++)
        {
            char id = rows[c / 9][c % 9];
            if (!ids.TryGetValue(id, out int k))
            {
                if (!sums.ContainsKey(id)) throw new FormatException($"kafazi '{id}' nuk ka shumë");
                ids[id] = k = cells.Count;
                cells.Add([]);
            }
            cells[k].Add(c);
            p.CageOf[c] = k;
        }
        foreach (var (id, k) in ids.OrderBy(e => e.Value))
        {
            if (!IsConnected(cells[k])) throw new FormatException($"kafazi '{id}' nuk është i lidhur");
            p.Cages.Add(new Cage(sums[id], cells[k].ToArray()));
        }
        return p;
    }

    static bool IsConnected(List<int> cells)
    {
        var set = cells.ToHashSet();
        var stack = new Stack<int>([cells[0]]);
        var seen = new HashSet<int> { cells[0] };
        while (stack.Count > 0)
        {
            int c = stack.Pop();
            foreach (int n in Neighbours(c))
                if (set.Contains(n) && seen.Add(n)) stack.Push(n);
        }
        return seen.Count == cells.Count;
    }

    public static IEnumerable<int> Neighbours(int c)
    {
        int r = c / 9, k = c % 9;
        if (r > 0) yield return c - 9;
        if (r < 8) yield return c + 9;
        if (k > 0) yield return c - 1;
        if (k < 8) yield return c + 1;
    }

    public List<string> Verify(int[] g)
    {
        var errors = new List<string>();
        foreach (var (unit, name) in Csp.Units.Zip(Csp.UnitNames))
            if (unit.Select(c => g[c]).Distinct().Count() != 9 || unit.Any(c => g[c] is < 1 or > 9))
                errors.Add($"{name} nuk përmban 1..9");
        foreach (var cage in Cages)
        {
            if (cage.Cells.Sum(c => g[c]) != cage.Sum) errors.Add($"kafazi te {Render.Cell(cage.Cells[0])} ka shumë të gabuar");
            if (cage.Cells.Select(c => g[c]).Distinct().Count() != cage.Cells.Length)
                errors.Add($"kafazi te {Render.Cell(cage.Cells[0])} ka shifra të përsëritura");
        }
        return errors;
    }
}

// Kufizim shume me shifra të ndryshme: Σ cells = Sum dhe AllDifferent(cells).
sealed record SumConstraint(int Sum, int[] Cells, bool Derived);

static class Csp
{
    public const int All = 0x3FE;   // bitët 1..9

    public static readonly int[][] Units = BuildUnits();
    public static readonly string[] UnitNames =
        [.. Enumerable.Range(1, 9).Select(i => $"rreshti {i}"), .. Enumerable.Range(1, 9).Select(i => $"kolona {i}"),
         .. Enumerable.Range(1, 9).Select(i => $"kutia {i}")];
    public static readonly int[][] UnitsOfCell =
        Enumerable.Range(0, 81).Select(c => Enumerable.Range(0, 27).Where(u => Units[u].Contains(c)).ToArray()).ToArray();

    static int[][] BuildUnits()
    {
        var units = new List<int[]>();
        for (int r = 0; r < 9; r++) units.Add(Enumerable.Range(0, 9).Select(c => r * 9 + c).ToArray());
        for (int c = 0; c < 9; c++) units.Add(Enumerable.Range(0, 9).Select(r => r * 9 + c).ToArray());
        for (int b = 0; b < 9; b++)
            units.Add(Enumerable.Range(0, 9).Select(i => (b / 3 * 3 + i / 3) * 9 + b % 3 * 3 + i % 3).ToArray());
        return units.ToArray();
    }

    // Rregulli i 45: shifrat e çdo njësie (rresht/kolonë/kuti) kanë shumën 45. Nëse kafazet që janë
    // krejtësisht brenda njësisë mbulojnë të gjitha qelizat përveç L, atëherë Σ L = 45 − Σ(shumat e tyre).
    public static List<SumConstraint> Rule45(Puzzle p)
    {
        var result = new List<SumConstraint>();
        foreach (var unit in Units)
        {
            var set = unit.ToHashSet();
            var inside = p.Cages.Where(cg => cg.Cells.All(set.Contains)).ToList();
            var left = unit.Except(inside.SelectMany(cg => cg.Cells)).ToArray();
            if (left.Length is >= 1 and <= 5)
                result.Add(new SumConstraint(45 - inside.Sum(cg => cg.Sum), left, true));
        }
        return result;
    }
}

enum Mode { Backtracking, ForwardChecking, Mac, Mac45 }

sealed class Solver
{
    readonly Puzzle p;
    readonly Mode mode;
    readonly long limit;
    readonly int maxSolutions;
    readonly SumConstraint[] sums;
    readonly int[][] sumsOfCell;

    public long Nodes, Backtracks;
    public bool HitLimit;
    public readonly List<int[]> Solutions = [];

    public Solver(Puzzle puzzle, Mode mode, long limit, int maxSolutions)
    {
        p = puzzle;
        this.mode = mode;
        this.limit = limit;
        this.maxSolutions = maxSolutions;
        var list = p.Cages.Select(c => new SumConstraint(c.Sum, c.Cells, false)).ToList();
        if (mode == Mode.Mac45) list.AddRange(Csp.Rule45(p));
        sums = list.ToArray();
        sumsOfCell = Enumerable.Range(0, 81)
            .Select(c => Enumerable.Range(0, sums.Length).Where(i => sums[i].Cells.Contains(c)).ToArray()).ToArray();
    }

    public static string Name(Mode m) => m switch
    {
        Mode.Backtracking => "Backtracking (rresht pas rreshti)",
        Mode.ForwardChecking => "Forward checking + MRV",
        Mode.Mac => "MAC (GAC mbi kafazet) + MRV",
        _ => "MAC + MRV + rregulli i 45",
    };

    public void Run()
    {
        if (mode == Mode.Backtracking) { Backtrack(new int[81], 0); return; }
        var dom = Enumerable.Repeat(Csp.All, 81).ToArray();
        if (mode == Mode.ForwardChecking) { ForwardCheck(dom, new int[81]); return; }
        if (Propagate(dom)) Mac(dom);
    }

    bool Stop => Solutions.Count >= maxSolutions || HitLimit;

    bool Count()
    {
        if (++Nodes <= limit) return true;
        HitLimit = true;
        return false;
    }

    // ---------- 1. Backtracking i thjeshtë: kontrollon vetëm kufizimet mbi variablat e caktuara ----------
    void Backtrack(int[] g, int cell)
    {
        if (cell == 81) { Solutions.Add((int[])g.Clone()); return; }
        for (int d = 1; d <= 9 && !Stop; d++)
        {
            if (!Count()) return;
            if (!Consistent(g, cell, d)) continue;
            g[cell] = d;
            Backtrack(g, cell + 1);
            g[cell] = 0;
        }
        Backtracks++;
    }

    bool Consistent(int[] g, int cell, int d)
    {
        foreach (int u in Csp.UnitsOfCell[cell])
            foreach (int c in Csp.Units[u])
                if (g[c] == d) return false;
        foreach (int si in sumsOfCell[cell])
        {
            var s = sums[si];
            int total = d, free = 0;
            foreach (int c in s.Cells)
            {
                if (c == cell) continue;
                if (g[c] == d) return false;
                if (g[c] == 0) free++;
                else total += g[c];
            }
            // Qelizat e lira duhet të marrin të paktën 1 + 2 + … + free dhe më së shumti 9 + 8 + …
            if (total + free * (free + 1) / 2 > s.Sum) return false;
            if (total + free * (19 - free) / 2 < s.Sum) return false;
        }
        return true;
    }

    // ---------- 2. Forward checking + MRV ----------
    void ForwardCheck(int[] dom, int[] val)
    {
        int cell = -1, best = 10;
        for (int c = 0; c < 81; c++)
            if (val[c] == 0 && BitOperations.PopCount((uint)dom[c]) < best)
            {
                best = BitOperations.PopCount((uint)dom[c]);
                cell = c;
            }
        if (cell == -1) { Solutions.Add((int[])val.Clone()); return; }

        foreach (int d in Digits(dom[cell]))
        {
            if (Stop || !Count()) return;
            var nd = (int[])dom.Clone();
            nd[cell] = 1 << d;
            val[cell] = d;
            if (ForwardPrune(nd, val, cell, d)) ForwardCheck(nd, val);
            val[cell] = 0;
        }
        Backtracks++;
    }

    // Pas caktimit cell = d, pastron domain-et e variablave të pacaktuara që ndajnë kufizim me cell.
    bool ForwardPrune(int[] dom, int[] val, int cell, int d)
    {
        int bit = 1 << d;
        foreach (int u in Csp.UnitsOfCell[cell])
            foreach (int c in Csp.Units[u])
                if (val[c] == 0 && (dom[c] &= ~bit) == 0) return false;

        foreach (int si in sumsOfCell[cell])
        {
            var s = sums[si];
            int total = 0, free = 0;
            foreach (int c in s.Cells)
            {
                if (val[c] != 0) total += val[c];
                else free++;
            }
            int remaining = s.Sum - total;
            if (free == 0) { if (remaining != 0) return false; continue; }
            // Çdo qelizë e lirë merr një vlerë v: pjesa tjetër (free − 1 qeliza) duhet të marrë remaining − v.
            int lo = remaining - (free - 1) * (19 - (free - 1)) / 2, hi = remaining - (free - 1) * free / 2;
            int mask = 0;
            for (int v = Math.Max(1, lo); v <= Math.Min(9, hi); v++) mask |= 1 << v;
            foreach (int c in s.Cells)
            {
                if (val[c] != 0) continue;
                if (c != cell && (dom[c] &= ~bit) == 0) return false;
                if ((dom[c] &= mask) == 0) return false;
            }
        }
        return true;
    }

    // ---------- 3. MAC: pas çdo vendimi propagimi deri në pikë fikse ----------
    void Mac(int[] dom)
    {
        int cell = -1, best = 10;
        for (int c = 0; c < 81; c++)
        {
            int n = BitOperations.PopCount((uint)dom[c]);
            if (n > 1 && n < best) { best = n; cell = c; }
        }
        if (cell == -1) { Solutions.Add(dom.Select(m => BitOperations.TrailingZeroCount(m)).ToArray()); return; }

        foreach (int d in Digits(dom[cell]))
        {
            if (Stop || !Count()) return;
            var nd = (int[])dom.Clone();
            nd[cell] = 1 << d;
            if (Propagate(nd)) Mac(nd);
        }
        Backtracks++;
    }

    // Propagimi i kufizimeve: AllDifferent (naked + hidden singles) dhe GAC i plotë mbi kufizimet e shumës.
    bool Propagate(int[] dom)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var unit in Csp.Units)
            {
                int r = AllDifferent(dom, unit);
                if (r < 0) return false;
                changed |= r > 0;
            }
            foreach (var s in sums)
            {
                int r = SumGac(dom, s);
                if (r < 0) return false;
                changed |= r > 0;
            }
        }
        return true;
    }

    // Kthen −1 për konflikt, 1 nëse u ndryshua ndonjë domain, 0 përndryshe.
    static int AllDifferent(int[] dom, int[] unit)
    {
        int changed = 0;
        foreach (int c in unit)
        {
            if (BitOperations.PopCount((uint)dom[c]) != 1) continue;
            foreach (int o in unit)
            {
                if (o == c || (dom[o] & dom[c]) == 0) continue;
                if ((dom[o] &= ~dom[c]) == 0) return -1;
                changed = 1;
            }
        }
        for (int d = 1; d <= 9; d++)
        {
            int where = -1, count = 0;
            foreach (int c in unit)
                if ((dom[c] >> d & 1) != 0) { where = c; count++; }
            if (count == 0) return -1;   // shifra d nuk ka ku të vendoset në njësi
            if (count == 1 && dom[where] != 1 << d) { dom[where] = 1 << d; changed = 1; }
        }
        return changed;
    }

    // GAC: një vlerë mbetet në domain vetëm nëse ekziston një plotësim i kafazit (shifra të ndryshme,
    // shuma e saktë) që e përmban. Plotësimet gjenerohen me DFS dhe prerje sipas kufijve të shumës.
    static int SumGac(int[] dom, SumConstraint s)
    {
        int k = s.Cells.Length;
        Span<int> support = stackalloc int[k];
        Span<int> chosen = stackalloc int[k];
        support.Clear();
        Enumerate(dom, s, 0, 0, 0, chosen, support);

        int changed = 0;
        for (int i = 0; i < k; i++)
        {
            int c = s.Cells[i], nd = dom[c] & support[i];
            if (nd == 0) return -1;
            if (nd != dom[c]) { dom[c] = nd; changed = 1; }
        }
        return changed;
    }

    static void Enumerate(int[] dom, SumConstraint s, int i, int used, int partial, Span<int> chosen, Span<int> support)
    {
        int k = s.Cells.Length;
        if (i == k)
        {
            if (partial != s.Sum) return;
            for (int j = 0; j < k; j++) support[j] |= 1 << chosen[j];
            return;
        }
        int free = k - i - 1;
        int cand = dom[s.Cells[i]] & ~used;
        while (cand != 0)
        {
            int d = BitOperations.TrailingZeroCount(cand);
            cand &= cand - 1;
            int t = partial + d;
            if (t + free * (free + 1) / 2 > s.Sum) break;   // shifrat rriten, kështu që as më të mëdhatë s'bëjnë
            if (t + free * (19 - free) / 2 < s.Sum) continue;
            chosen[i] = d;
            Enumerate(dom, s, i + 1, used | 1 << d, t, chosen, support);
        }
    }

    static IEnumerable<int> Digits(int mask)
    {
        for (int d = 1; d <= 9; d++)
            if ((mask >> d & 1) != 0) yield return d;
    }
}

static class Render
{
    public static string Cell(int c) => $"R{c / 9 + 1}K{c % 9 + 1}";

    // Vizatimi i tabelës me kufijtë e kafazeve. Pa zgjidhje shfaqet shuma në qelizën e parë të kafazit.
    public static void Board(Puzzle p, int[]? values)
    {
        var anchors = p.Cages.ToDictionary(c => c.Cells.Min(), c => c.Sum);
        bool Diff(int a, int b) => p.CageOf[a] != p.CageOf[b];
        bool HEdge(int r, int c) => r == 0 || r == 9 || Diff((r - 1) * 9 + c, r * 9 + c);   // mbi rreshtin r
        bool VEdge(int r, int c) => c == 0 || c == 9 || Diff(r * 9 + c - 1, r * 9 + c);    // majtas kolonës c

        var sb = new StringBuilder();
        for (int r = 0; r <= 9; r++)
        {
            sb.Append("  ");
            for (int c = 0; c <= 9; c++)
            {
                bool any = (c < 9 && HEdge(r, c)) || (c > 0 && HEdge(r, c - 1)) ||
                           (r < 9 && VEdge(r, c)) || (r > 0 && VEdge(r - 1, c));
                sb.Append(any ? '+' : ' ');
                if (c < 9) sb.Append(HEdge(r, c) ? (r % 3 == 0 ? "===" : "---") : "   ");
            }
            sb.AppendLine();
            if (r == 9) break;
            sb.Append("  ");
            for (int c = 0; c <= 9; c++)
            {
                sb.Append(VEdge(r, c) ? (c % 3 == 0 ? '‖' : '|') : ' ');
                if (c == 9) break;
                int cell = r * 9 + c;
                string text = values is not null ? $" {values[cell]} " :
                              anchors.TryGetValue(cell, out int s) ? $"{s,-3}" : "   ";
                sb.Append(text);
            }
            sb.AppendLine();
        }
        Console.Write(sb);
    }
}

// Gjeneron Killer Sudoku me zgjidhje unike: grid i plotë i rastësishëm + ndarje e rastësishme në kafaze.
static class Generator
{
    const string Ids = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@$%&*+=?!";

    // --gjenero <seed> <madhësia max e kafazit> <skedari>
    public static void Run(string[] args)
    {
        int seed = int.Parse(args[1]), maxSize = int.Parse(args[2]);
        string file = args[3];
        var rng = new Random(seed);
        int[] grid = RandomGrid(rng);

        for (int attempt = 1; attempt <= 500; attempt++)
        {
            var cageOf = RandomCages(rng, grid, maxSize, out int count);
            if (count > Ids.Length) continue;
            var text = Format(grid, cageOf, count, seed, maxSize);
            var solver = new Solver(Puzzle.Parse(text), Mode.Mac45, 5_000_000, maxSolutions: 2);
            solver.Run();
            if (solver.Solutions.Count != 1 || solver.HitLimit) continue;
            File.WriteAllText(file, text);
            Console.WriteLine($"U krijua {file}: {count} kafaze, zgjidhje unike (prova {attempt})");
            return;
        }
        Console.WriteLine("Nuk u gjet ndarje me zgjidhje unike; provo një seed tjetër.");
    }

    static int[] RandomGrid(Random rng)
    {
        var g = new int[81];
        bool Fill(int cell)
        {
            if (cell == 81) return true;
            foreach (int d in Enumerable.Range(1, 9).OrderBy(_ => rng.Next()))
            {
                if (Csp.UnitsOfCell[cell].Any(u => Csp.Units[u].Any(c => g[c] == d))) continue;
                g[cell] = d;
                if (Fill(cell + 1)) return true;
                g[cell] = 0;
            }
            return false;
        }
        Fill(0);
        return g;
    }

    // Rrit kafaze nga qeliza të lira, duke shtuar fqinjë me shifër që nuk është ende në kafaz.
    static int[] RandomCages(Random rng, int[] grid, int maxSize, out int count)
    {
        var cageOf = Enumerable.Repeat(-1, 81).ToArray();
        count = 0;
        for (int start = 0; start < 81; start++)
        {
            if (cageOf[start] >= 0) continue;
            int target = rng.Next(100) < 8 ? 1 : rng.Next(2, maxSize + 1);
            var cells = new List<int> { start };
            cageOf[start] = count;
            while (cells.Count < target)
            {
                var options = cells.SelectMany(Puzzle.Neighbours).Distinct()
                    .Where(n => cageOf[n] < 0 && cells.All(c => grid[c] != grid[n])).ToList();
                if (options.Count == 0) break;
                int pick = options[rng.Next(options.Count)];
                cageOf[pick] = count;
                cells.Add(pick);
            }
            count++;
        }
        return cageOf;
    }

    static string Format(int[] grid, int[] cageOf, int count, int seed, int maxSize)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Killer Sudoku e gjeneruar (seed {seed}, kafaze me madhësi ≤ {maxSize})");
        sb.AppendLine("# 9 rreshta: id-ja e kafazit për çdo qelizë; pastaj: id shuma");
        for (int r = 0; r < 9; r++)
            sb.AppendLine(new string(Enumerable.Range(0, 9).Select(c => Ids[cageOf[r * 9 + c]]).ToArray()));
        for (int k = 0; k < count; k++)
            sb.AppendLine($"{Ids[k]} {Enumerable.Range(0, 81).Where(c => cageOf[c] == k).Sum(c => grid[c])}");
        return sb.ToString();
    }
}
