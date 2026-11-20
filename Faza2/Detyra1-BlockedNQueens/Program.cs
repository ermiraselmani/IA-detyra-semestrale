using System.Diagnostics;
using System.Text.RegularExpressions;

// Blocked N-Queens me A* (formulim me gjendje të plotë: një mbretëreshë në çdo kolonë).


string[] rest = args.Where(a => !a.StartsWith("--")).ToArray();
int seed = IntArg("--seed", 1);
int limit = IntArg("--limit", 3_000_000);

string path = rest.Length > 0 ? rest[0] : "instances/q8.lp";
if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, path);
if (!File.Exists(path))
{
    Console.WriteLine($"Skedari nuk u gjet: {rest.ElementAtOrDefault(0) ?? "instances/q8.lp"}");
    return;
}

Board board = Board.Parse(File.ReadAllText(path));
Console.WriteLine($"=== Blocked N-Queens: {Path.GetFileName(path)}, N = {board.N}, " +
                  $"katrorë të bllokuar = {board.BlockedCount} ===");
Console.WriteLine();

if (board.N > 16)
{
    Console.WriteLine("N duhet të jetë ≤ 16 (gjendja kodohet me 4 bit për kolonë).");
    return;
}
if (board.Allowed.Any(a => a.Length == 0))
{
    Console.WriteLine("Një kolonë është krejtësisht e bllokuar, prandaj problemi nuk ka zgjidhje.");
    return;
}
if (!board.IsSatisfiable())
{
    Board.Print(board, null);
    Console.WriteLine("\nKontrolli paraprak (backtracking): instanca NUK ka zgjidhje. A* do ta kalonte " +
                      "gjithë hapësirën pa gjetur goal.");
    return;
}

int[] start = board.RandomStart(seed);
Console.WriteLine($"Gjendja fillestare (seed = {seed}), Q = mbretëreshë, # = i bllokuar:");
Board.Print(board, start);
Console.WriteLine();
Console.WriteLine($"Çifte që sulmohen: {Heuristics.Evaluate(Heuristic.H1, start)}, " +
                  $"mbretëresha në konflikt: {Heuristics.Evaluate(Heuristic.H2, start)}");
Console.WriteLine();

var heuristics = new[] { Heuristic.H0, Heuristic.H1, Heuristic.H2, Heuristic.H3, Heuristic.H4 };
var results = new Dictionary<Heuristic, AStarResult>();
foreach (var h in heuristics)
    results[h] = AStar.Solve(board, start, h, h == Heuristic.H0 ? Math.Min(limit, 1_000_000) : limit);

// Kostoja optimale h*(start) merret nga kërkimet me heuristikë admissible që kanë përfunduar.
int? optimal = new[] { Heuristic.H0, Heuristic.H3, Heuristic.H4 }
    .Select(h => results[h]).Where(r => r.Path is not null).Select(r => (int?)r.Cost).Min();

Console.WriteLine("--- Krahasimi i heuristikave (A*, f = g + h, kostoja e çdo lëvizjeje = 1) ---");
Console.WriteLine($"{"Heuristika",-34} {"h(start)",8} {"Lëvizje",8} {"Nyje zgjeruar",14} {"Gjeneruar",12} {"Koha",9}  Optimale?");
foreach (var h in heuristics)
{
    var r = results[h];
    string cost = r.Path is null ? "limit" : r.Cost.ToString();
    string opt = r.Path is null || optimal is null ? "-" : r.Cost == optimal ? "po" : "JO";
    Console.WriteLine($"{Heuristics.Name(h),-34} {Heuristics.Evaluate(h, start),8} {cost,8} " +
                      $"{r.Expanded,14:N0} {r.Generated,12:N0} {r.Elapsed.TotalMilliseconds,7:F0}ms  {opt}");
}
Console.WriteLine();

if (optimal is not null)
{
    Console.WriteLine($"--- Admissibility në gjendjen fillestare: h*(start) = {optimal} ---");
    foreach (var h in heuristics.Skip(1))
    {
        int v = Heuristics.Evaluate(h, start);
        Console.WriteLine($"{Heuristics.Name(h),-34} h = {v,3}  " +
                          (v > optimal ? $"> h* → MBIVLERËSON (jo admissible)" : "≤ h*"));
    }
    Console.WriteLine();
}

var best = results[Heuristic.H4].Path is not null ? results[Heuristic.H4] : results.Values.FirstOrDefault(r => r.Path is not null);
if (best?.Path is null)
{
    Console.WriteLine($"Asnjë kërkim nuk e gjeti zgjidhjen brenda limitit prej {limit:N0} nyjesh.");
    return;
}

// Kontrollohet edhe përgjatë rrugës optimale që H4(s) ≤ kostoja e mbetur.
bool admissibleOnPath = true;
for (int i = 0; i < best.Path.Count; i++)
    if (Heuristics.Evaluate(Heuristic.H4, best.Path[i]) > best.Path.Count - 1 - i) admissibleOnPath = false;

Console.WriteLine($"Rruga e gjetur me {Heuristics.Name(best.Heuristic)} ({best.Cost} lëvizje):");
for (int i = 1; i < best.Path.Count; i++)
{
    int col = Enumerable.Range(0, board.N).First(c => best.Path[i][c] != best.Path[i - 1][c]);
    Console.WriteLine($"  {i}. mbretëresha në kolonën {col + 1}: rreshti {best.Path[i - 1][col] + 1} → {best.Path[i][col] + 1}" +
                      $"   (H4 = {Heuristics.Evaluate(Heuristic.H4, best.Path[i])}, kosto e mbetur = {best.Path.Count - 1 - i})");
}
Console.WriteLine($"H4(s) ≤ kostoja e mbetur për çdo gjendje në rrugë: {(admissibleOnPath ? "po" : "JO")}");
Console.WriteLine();

int[] goal = best.Path[^1];
Console.WriteLine("Zgjidhja:");
Board.Print(board, goal);
Console.WriteLine();
Console.WriteLine("Dalja në formatin e ASP Competition:");
Console.WriteLine(string.Join(" ", Enumerable.Range(0, board.N).Select(c => $"queen({goal[c] + 1},{c + 1}).")));
Console.WriteLine($"Zgjidhje valide: {(board.IsValidSolution(goal) ? "po" : "JO")}");

int IntArg(string name, int def)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v) ? v : def;
}

sealed class Board
{
    public int N { get; private init; }
    public bool[,] Blocked { get; private init; } = new bool[0, 0];
    public int[][] Allowed { get; private init; } = [];   // Allowed[c] = rreshtat e lirë në kolonën c
    public int BlockedCount { get; private init; }

    // Formati: num(1). ... num(N). block(i,j). ...  (i = rreshti, j = kolona, nga 1)
    public static Board Parse(string text)
    {
        int n = Regex.Matches(text, @"num\(\s*(\d+)\s*\)").Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        var blocked = new bool[n, n];
        int count = 0;
        foreach (Match m in Regex.Matches(text, @"block\(\s*(\d+)\s*,\s*(\d+)\s*\)"))
        {
            int r = int.Parse(m.Groups[1].Value) - 1, c = int.Parse(m.Groups[2].Value) - 1;
            if (r < 0 || r >= n || c < 0 || c >= n || blocked[r, c]) continue;
            blocked[r, c] = true;
            count++;
        }
        var allowed = new int[n][];
        for (int c = 0; c < n; c++)
            allowed[c] = Enumerable.Range(0, n).Where(r => !blocked[r, c]).ToArray();
        return new Board { N = n, Blocked = blocked, Allowed = allowed, BlockedCount = count };
    }

    public int[] RandomStart(int seed)
    {
        var rng = new Random(seed);
        return Allowed.Select(a => a[rng.Next(a.Length)]).ToArray();
    }

    public bool IsValidSolution(int[] rows)
    {
        for (int c = 0; c < N; c++)
        {
            if (Blocked[rows[c], c]) return false;
            for (int d = c + 1; d < N; d++)
                if (Heuristics.Attack(rows, c, d)) return false;
        }
        return true;
    }

    // Kontroll paraprak me backtracking, vetëm për të dalluar instancat pa zgjidhje.
    public bool IsSatisfiable()
    {
        var rows = new int[N];
        bool Place(int c)
        {
            if (c == N) return true;
            foreach (int r in Allowed[c])
            {
                rows[c] = r;
                bool ok = true;
                for (int p = 0; p < c && ok; p++) ok = !Heuristics.Attack(rows, p, c);
                if (ok && Place(c + 1)) return true;
            }
            return false;
        }
        return Place(0);
    }

    public static void Print(Board b, int[]? rows)
    {
        Console.Write("    ");
        for (int c = 0; c < b.N; c++) Console.Write($"{c + 1,3}");
        Console.WriteLine();
        for (int r = 0; r < b.N; r++)
        {
            Console.Write($"{r + 1,3} ");
            for (int c = 0; c < b.N; c++)
                Console.Write(b.Blocked[r, c] ? "  #" : rows is not null && rows[c] == r ? "  Q" : "  .");
            Console.WriteLine();
        }
    }
}

enum Heuristic { H0, H1, H2, H3, H4 }

static class Heuristics
{
    public static string Name(Heuristic h) => h switch
    {
        Heuristic.H0 => "H0: h = 0 (UCS, referencë)",
        Heuristic.H1 => "H1: çifte që sulmohen",
        Heuristic.H2 => "H2: mbretëresha në konflikt",
        Heuristic.H3 => "H3: max tepricë në vija",
        _ => "H4: max(H3, matching) [propozim]",
    };

    public static bool Attack(int[] rows, int a, int b) =>
        rows[a] == rows[b] || Math.Abs(rows[a] - rows[b]) == Math.Abs(a - b);

    public static int Evaluate(Heuristic h, int[] rows)
    {
        if (h == Heuristic.H0) return 0;
        int n = rows.Length;
        Span<int> row = stackalloc int[n], diag = stackalloc int[2 * n], anti = stackalloc int[2 * n];
        row.Clear(); diag.Clear(); anti.Clear();
        for (int c = 0; c < n; c++)
        {
            row[rows[c]]++;
            diag[rows[c] - c + n]++;
            anti[rows[c] + c]++;
        }

        switch (h)
        {
            case Heuristic.H1:
            {
                // Dy mbretëresha në kolona të ndryshme ndajnë më së shumti një vijë.
                int pairs = 0;
                for (int i = 0; i < 2 * n; i++)
                {
                    if (i < n) pairs += row[i] * (row[i] - 1) / 2;
                    pairs += diag[i] * (diag[i] - 1) / 2 + anti[i] * (anti[i] - 1) / 2;
                }
                return pairs;
            }
            case Heuristic.H2:
            {
                int queens = 0;
                for (int c = 0; c < n; c++)
                    if (row[rows[c]] > 1 || diag[rows[c] - c + n] > 1 || anti[rows[c] + c] > 1) queens++;
                return queens;
            }
            default:
            {
                int r = 0, d = 0, a = 0;
                for (int i = 0; i < 2 * n; i++)
                {
                    if (i < n) r += Math.Max(0, row[i] - 1);
                    d += Math.Max(0, diag[i] - 1);
                    a += Math.Max(0, anti[i] - 1);
                }
                int h3 = Math.Max(r, Math.Max(d, a));
                if (h == Heuristic.H3) return h3;
                return Math.Max(h3, GreedyMatching(rows));
            }
        }
    }

    // Matching maksimal (greedy) në grafin e konflikteve: çifte që sulmohen dhe nuk kanë mbretëreshë të përbashkët.
    static int GreedyMatching(int[] rows)
    {
        int n = rows.Length, matched = 0, size = 0;
        for (int a = 0; a < n; a++)
        {
            if ((matched >> a & 1) != 0) continue;
            for (int b = a + 1; b < n; b++)
            {
                if ((matched >> b & 1) != 0 || !Attack(rows, a, b)) continue;
                matched |= 1 << a | 1 << b;
                size++;
                break;
            }
        }
        return size;
    }
}

sealed record AStarResult(Heuristic Heuristic, List<int[]>? Path, long Expanded, long Generated, TimeSpan Elapsed)
{
    public int Cost => Path is null ? -1 : Path.Count - 1;
}

static class AStar
{
    // Gjendja kodohet në një ulong: 4 bit për rreshtin e mbretëreshës në secilën kolonë (N ≤ 16).
    static ulong Encode(int[] rows)
    {
        ulong key = 0;
        for (int c = 0; c < rows.Length; c++) key |= (ulong)rows[c] << (4 * c);
        return key;
    }

    static int[] Decode(ulong key, int n)
    {
        var rows = new int[n];
        for (int c = 0; c < n; c++) rows[c] = (int)(key >> (4 * c) & 0xF);
        return rows;
    }

    public static AStarResult Solve(Board board, int[] start, Heuristic h, int maxExpanded)
    {
        var sw = Stopwatch.StartNew();
        int n = board.N;
        var open = new PriorityQueue<(ulong Key, int G), (int F, int H)>();
        var bestG = new Dictionary<ulong, int>();
        var parent = new Dictionary<ulong, ulong>();

        ulong startKey = Encode(start);
        int h0 = Heuristics.Evaluate(h, start);
        bestG[startKey] = 0;
        open.Enqueue((startKey, 0), (h0, h0));
        long expanded = 0, generated = 1;

        while (open.TryDequeue(out var node, out _))
        {
            if (node.G > bestG[node.Key]) continue;   // hyrje e vjetër në radhë
            int[] rows = Decode(node.Key, n);
            if (Heuristics.Evaluate(Heuristic.H1, rows) == 0)
                return new(h, Reconstruct(parent, node.Key, startKey, n), expanded, generated, sw.Elapsed);
            if (++expanded > maxExpanded) break;

            // Veprimi: zhvendos mbretëreshën e kolonës c në një rresht tjetër të lirë të së njëjtës kolonë.
            for (int c = 0; c < n; c++)
            {
                int old = rows[c];
                foreach (int r in board.Allowed[c])
                {
                    if (r == old) continue;
                    ulong child = node.Key & ~(0xFUL << (4 * c)) | (ulong)r << (4 * c);
                    int g = node.G + 1;
                    // Nyjet rihapen kur gjendet g më i vogël, kështu A* mbetet optimal edhe pa konsistencë.
                    if (bestG.TryGetValue(child, out int known) && known <= g) continue;
                    bestG[child] = g;
                    parent[child] = node.Key;
                    rows[c] = r;
                    int hv = Heuristics.Evaluate(h, rows);
                    open.Enqueue((child, g), (g + hv, hv));
                    generated++;
                }
                rows[c] = old;
            }
        }
        return new(h, null, expanded, generated, sw.Elapsed);
    }

    static List<int[]> Reconstruct(Dictionary<ulong, ulong> parent, ulong key, ulong startKey, int n)
    {
        var path = new List<int[]> { Decode(key, n) };
        while (key != startKey)
        {
            key = parent[key];
            path.Add(Decode(key, n));
        }
        path.Reverse();
        return path;
    }
}
