using System.Diagnostics;
using System.Numerics;

// Sudoku Solver me Breadth First Search (BFS) dhe Backtracking.
// Përdorimi: dotnet run -- [easy|medium|hard|all|<81 karaktere, 0 ose . për bosh>]

var puzzles = new Dictionary<string, string>
{
    ["Easy"] = "530070000600195000098000060800060003400803001700020006060000280000419005000080079",
    ["Medium"] = "200080300060070084030500209000105408000000000402706000301007040720040060004010003",
    ["Hard"] = "800000000003600000070090200050007000000045700000100030001000068008500010090000400",
};

string arg = args.Length > 0 ? args[0] : "all";
IEnumerable<(string Name, string Grid)> selected = arg.ToLowerInvariant() switch
{
    "all" => puzzles.Select(kv => (kv.Key, kv.Value)),
    "easy" or "medium" or "hard" => [(char.ToUpper(arg[0]) + arg[1..].ToLower(), puzzles[char.ToUpper(arg[0]) + arg[1..].ToLower()])],
    _ when arg.Length == 81 && arg.All(c => c == '.' || char.IsAsciiDigit(c)) =>[("Custom", arg.Replace('.', '0'))],
    _ => throw new ArgumentException("Argument i pavlefshëm: easy | medium | hard | all | 81 karaktere"),
};

foreach (var (name, grid) in selected)
{
    var start = Sudoku.Parse(grid);
    Console.WriteLine($"==================== Niveli: {name} ====================");
    Sudoku.Print(start);
    Console.WriteLine();
    if (Sudoku.Objective(start) > start.Count(v => v == 0))
    {
        Console.WriteLine("Sudoku e pavlefshme: shifrat e dhëna shkelin kufizimet.");
        Console.WriteLine();
        continue;
    }
    Sudoku.PrintLandscape(start);

    var solver = new BfsSolver();
    var solution = solver.Solve(start);

    Console.WriteLine();
    if (solution is null)
        Console.WriteLine(solver.FrontierLimitHit
            ? $"Kufiri i memories u arrit ({BfsSolver.MaxFrontier:N0} gjendje në radhë) – nuk u gjet zgjidhje."
            : "Sudoku nuk ka zgjidhje.");
    else
    {
        Console.WriteLine("Zgjidhja:");
        Sudoku.Print(solution);
        Console.WriteLine($"Objective function f(S) = {Sudoku.Objective(solution)} (f = 0 => zgjidhje valide)");
    }
    Console.WriteLine($"Nyje të zgjeruara: {solver.Expanded:N0}, gjendje të gjeneruara: {solver.Generated:N0}, " +
                      $"dead-end (backtrack): {solver.DeadEnds:N0}");
    Console.WriteLine($"Madhësia maksimale e radhës (frontier): {solver.MaxQueue:N0}, koha: {solver.Elapsed.TotalMilliseconds:F0} ms");
    Console.WriteLine();
}

static class Sudoku
{
    public static byte[] Parse(string s) => s.Select(c => (byte)(c - '0')).ToArray();

    public static int Box(int cell) => cell / 27 * 3 + cell % 9 / 3;

    // Bitmask i shifrave të lejuara (bit d = shifra d) për qelizën bosh 'cell'.
    public static int Candidates(byte[] g, int cell)
    {
        int r = cell / 9, c = cell % 9, b = Box(cell), used = 0;
        for (int i = 0; i < 9; i++)
        {
            used |= 1 << g[r * 9 + i];
            used |= 1 << g[i * 9 + c];
            used |= 1 << g[(b / 3 * 3 + i / 3) * 9 + b % 3 * 3 + i % 3];
        }
        return ~used & 0x3FE;
    }

    // Objective function: f(S) = qeliza bosh + Σ (çifte me shifër të njëjtë në rresht/kolonë/kuti)
    public static int Objective(byte[] g)
    {
        int f = g.Count(v => v == 0);
        for (int a = 0; a < 81; a++)
            for (int b = a + 1; b < 81; b++)
                if (g[a] != 0 && g[a] == g[b] &&
                    (a / 9 == b / 9 || a % 9 == b % 9 || Box(a) == Box(b)))
                    f++;
        return f;
    }

    public static void PrintLandscape(byte[] g)
    {
        int empty = g.Count(v => v == 0);
        BigInteger reduced = BigInteger.One;
        for (int i = 0; i < 81; i++)
            if (g[i] == 0) reduced *= BitOperations.PopCount((uint)Candidates(g, i));

        Console.WriteLine("--- Landscape ---");
        Console.WriteLine($"Qeliza të dhëna: {81 - empty}, qeliza bosh (variabla): {empty}");
        Console.WriteLine($"1) Grid krejt bosh: 9^81 ≈ {Sci(BigInteger.Pow(9, 81))}");
        Console.WriteLine($"2) Vetëm qelizat bosh: 9^{empty} ≈ {Sci(BigInteger.Pow(9, empty))}");
        Console.WriteLine($"3) Pas heqjes së vlerave që shkelin kufizimet fillestare: Π|D(x)| ≈ {Sci(reduced)}");
        Console.WriteLine("   (Për krahasim: grid-a valide Sudoku gjithsej ≈ 6.67e21)");
    }

    static string Sci(BigInteger x) =>
        x < 1_000_000 ? x.ToString() : ((double)x).ToString("0.00e0", System.Globalization.CultureInfo.InvariantCulture);

    public static void Print(byte[] g)
    {
        for (int r = 0; r < 9; r++)
        {
            if (r % 3 == 0) Console.WriteLine("+-------+-------+-------+");
            for (int c = 0; c < 9; c++)
            {
                if (c % 3 == 0) Console.Write("| ");
                Console.Write(g[r * 9 + c] == 0 ? ". " : $"{g[r * 9 + c]} ");
            }
            Console.WriteLine("|");
        }
        Console.WriteLine("+-------+-------+-------+");
    }
}

class BfsSolver
{
    public const int MaxFrontier = 10_000_000;
    readonly Stopwatch sw = new();

    public long Expanded { get; private set; }
    public long Generated { get; private set; }
    public long DeadEnds { get; private set; }
    public int MaxQueue { get; private set; }
    public bool FrontierLimitHit { get; private set; }
    public TimeSpan Elapsed => sw.Elapsed;

    // BFS: gjendjet zgjerohen nivel pas niveli (niveli = numri i qelizave të mbushura).
    // Backtracking: degët që shkelin kufizimet nuk gjenerohen fare, dhe gjendjet pa asnjë
    // vlerë të lejuar (dead-end) braktisen – kërkimi "kthehet" te alternativat e tjera në radhë.
    public byte[]? Solve(byte[] start)
    {
        sw.Restart();
        var queue = new Queue<byte[]>();
        queue.Enqueue(start);
        Generated = 1;

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();
            Expanded++;

            int cell = Array.IndexOf(state, (byte)0);   // qeliza e parë bosh (rresht pas rreshti)
            if (cell < 0) { sw.Stop(); return state; }  // goal test

            int cand = Sudoku.Candidates(state, cell);
            if (cand == 0) { DeadEnds++; continue; }

            while (cand != 0)
            {
                int d = BitOperations.TrailingZeroCount(cand);
                cand &= cand - 1;

                var child = (byte[])state.Clone();
                child[cell] = (byte)d;
                queue.Enqueue(child);
                Generated++;
            }

            if (queue.Count > MaxQueue) MaxQueue = queue.Count;
            if (queue.Count > MaxFrontier) { FrontierLimitHit = true; break; }
        }

        sw.Stop();
        return null;
    }
}
