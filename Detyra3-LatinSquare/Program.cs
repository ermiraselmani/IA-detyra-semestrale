using System.Diagnostics;
using System.Numerics;

// Latin Square me Iterative Deepening Depth First Search (IDDFS) dhe Backtracking.


int n = args.Length > 0 ? int.Parse(args[0]) : 5;
if (n < 1 || n > 30)
{
    Console.WriteLine("n duhet të jetë ndërmjet 1 dhe 30.");
    return;
}

Console.WriteLine($"=== Latin Square: n = {n} ===");
Console.WriteLine();
Landscape.Print(n);
Console.WriteLine();

if (n >= 8)
    Console.WriteLine($"Kujdes: për n ≥ 8 IDDFS e plotë është shumë e ngadaltë (n = 7 kërkon ~3·10^9 nyje).\n");

var solver = new IddfsSolver(n);
int[,]? square = solver.Solve();

Console.WriteLine();
if (square is null)
{
    Console.WriteLine("Nuk u gjet katror latin.");
    return;
}

Console.WriteLine($"Dalja (u gjet në thellësinë {(n - 1) * (n - 1)}):");
for (int r = 0; r < n; r++)
{
    for (int c = 0; c < n; c++) Console.Write($"{square[r, c]} ");
    Console.WriteLine();
}
Console.WriteLine();
Console.WriteLine($"Objective function f(S) = {IddfsSolver.Objective(square)} (f = 0 => katror latin valid)");
Console.WriteLine($"Iterime IDDFS: {solver.Iterations}, nyje gjithsej: {solver.TotalNodes:N0}, " +
                  $"backtrack-e: {solver.Backtracks:N0}, koha: {solver.Elapsed.TotalMilliseconds:F0} ms");

static class Landscape
{
    // Numri i njohur i katrorëve latinë L(n) (OEIS A002860), n = 1..11
    static readonly string[] Known =
    [
        "1", "2", "12", "576", "161280", "812851200", "61479419904000",
        "108776032459082956800", "5524751496156892842531225600",
        "9982437658213039871725064756920320000",
        "776966836171770144107444346734230682311065600000",
    ];

    static BigInteger Factorial(int k)
    {
        BigInteger r = BigInteger.One;
        for (int i = 2; i <= k; i++) r *= i;
        return r;
    }

    public static void Print(int n)
    {
        Console.WriteLine("--- Landscape (madhësia e hapësirës së kërkimit) ---");
        Console.WriteLine($"Variabla: n² = {n * n}, domain-i: {{1..{n}}}");
        Console.WriteLine($"1) Reprezentim naiv: n^(n²) = {n}^{n * n} ≈ {Sci(BigInteger.Pow(n, n * n))}");
        Console.WriteLine($"2) Çdo rresht permutacion: (n!)^n ≈ {Sci(BigInteger.Pow(Factorial(n), n))}");
        Console.WriteLine($"3) Kufiri i sipërm (van Lint–Wilson): Π_(k=1..n) (k!)^(n/k) ≈ {Sci(UpperBound(n))}");
        if (n <= Known.Length)
        {
            var exact = BigInteger.Parse(Known[n - 1]);
            Console.WriteLine($"4) Numri i saktë i katrorëve latinë L({n}) = {exact:N0}");
            Console.WriteLine($"   Katrorë të reduktuar (rreshti 1 dhe kolona 1 = 1..n): L(n)/(n!(n−1)!) = " +
                              $"{exact / (Factorial(n) * Factorial(n - 1)):N0}");
        }
        int d = (n - 1) * (n - 1);
        Console.WriteLine($"5) Me thyerje simetrie: variabla (n−1)² = {d}, hapësira naive n^((n−1)²) ≈ {Sci(BigInteger.Pow(n, d))}");
        Console.WriteLine($"   Thellësia e zgjidhjes d = {d}, faktori i degëzimit b ≤ {n - 1}");
    }

    // Kufiri i sipërm i van Lint–Wilson për numrin e katrorëve latinë: L(n) ≤ Π_{k=1..n} (k!)^(n/k)
    static BigInteger UpperBound(int n)
    {
        double log = 0;
        for (int k = 1; k <= n; k++) log += n / (double)k * BigInteger.Log(Factorial(k));
        return new BigInteger(Math.Exp(Math.Min(log, 700)));
    }

    static string Sci(BigInteger x) =>
        x < 1_000_000 ? x.ToString() : ((double)x).ToString("0.00e0", System.Globalization.CultureInfo.InvariantCulture);
}

class IddfsSolver
{
    enum Result { Found, Cutoff, Failure }

    readonly int n;
    readonly int[,] grid;
    readonly int[] rowUsed, colUsed;   // bitmask: bit v = vlera v është përdorur në rresht/kolonë
    readonly Stopwatch sw = new();
    long nodes;

    public int Iterations { get; private set; }
    public long TotalNodes { get; private set; }
    public long Backtracks { get; private set; }
    public TimeSpan Elapsed => sw.Elapsed;

    public IddfsSolver(int n)
    {
        this.n = n;
        grid = new int[n, n];
        rowUsed = new int[n];
        colUsed = new int[n];
    }

    // IDDFS: kufiri i thellësisë rritet 0, 1, 2, ... deri në n²; në çdo iterim
    // bëhet Depth-Limited Search (DLS) nga fillimi.
    public int[,]? Solve()
    {
        sw.Start();

        // Thyerje simetrie: çdo katror latin mund të shndërrohet (me ndërrim rreshtash, kolonash
        // dhe simbolesh) në formë të reduktuar, prandaj rreshti i parë dhe kolona e parë fiksohen 1..n.
        for (int i = 0; i < n; i++)
        {
            Assign(0, i, i + 1);
            if (i > 0) Assign(i, 0, i + 1);
        }

        int cells = (n - 1) * (n - 1);
        for (int limit = 0; limit <= cells; limit++)
        {
            nodes = 0;
            Result r = Dls(0, limit);
            Iterations++;
            TotalNodes += nodes;
            Console.WriteLine($"Iterimi limit = {limit,3}: {nodes,12:N0} nyje -> {r}");

            if (r == Result.Found) { sw.Stop(); return grid; }
            if (r == Result.Failure) break;   // asnjë degë nuk u ndërpre nga kufiri => nuk ka zgjidhje
        }
        sw.Stop();
        return null;
    }

    void Assign(int r, int c, int v)
    {
        grid[r, c] = v; rowUsed[r] |= 1 << v; colUsed[c] |= 1 << v;
    }

    // DLS me backtracking: variabli i radhës është qeliza e brendshme 'depth' (rresht pas rreshti).
    Result Dls(int depth, int limit)
    {
        nodes++;
        if (depth == (n - 1) * (n - 1)) return Result.Found;   // goal test
        if (depth == limit) return Result.Cutoff;

        int r = 1 + depth / (n - 1), c = 1 + depth % (n - 1);
        bool cutoff = false;

        for (int v = 1; v <= n; v++)
        {
            int bit = 1 << v;
            if (((rowUsed[r] | colUsed[c]) & bit) != 0) continue;  

            Assign(r, c, v);
            Result res = Dls(depth + 1, limit);
            if (res == Result.Found) return res;
            if (res == Result.Cutoff) cutoff = true;
            grid[r, c] = 0; rowUsed[r] &= ~bit; colUsed[c] &= ~bit;   // backtrack
        }

        if (!cutoff) Backtracks++;
        return cutoff ? Result.Cutoff : Result.Failure;
    }

    // Objective function: f(S) = Σ_rreshta (n − |vlera të ndryshme|) + Σ_kolona (n − |vlera të ndryshme|)
    public static int Objective(int[,] s)
    {
        int n = s.GetLength(0), f = 0;
        for (int i = 0; i < n; i++)
        {
            var row = new HashSet<int>();
            var col = new HashSet<int>();
            for (int j = 0; j < n; j++)
            {
                if (s[i, j] != 0) row.Add(s[i, j]);
                if (s[j, i] != 0) col.Add(s[j, i]);
            }
            f += (n - row.Count) + (n - col.Count);
        }
        return f;
    }
}
