using System.Diagnostics;
using System.Numerics;

// Social Golfers Problem: n = g * p lojtarë, g grupe me nga p lojtarë, për w javë.
// Përdorimi: dotnet run -- [g] [p] [w] [timeoutSek]
// Default: 8 grupe, 4 lojtarë, 10 javë, 60 sekonda.

int g = args.Length > 0 ? int.Parse(args[0]) : 8;
int p = args.Length > 1 ? int.Parse(args[1]) : 4;
int w = args.Length > 2 ? int.Parse(args[2]) : 10;
int timeout = args.Length > 3 ? int.Parse(args[3]) : 60;

if (g * p > 64)
{
    Console.WriteLine("Ky implementim mbështet deri në 64 lojtarë (bitmask ulong).");
    return;
}

Console.WriteLine($"=== Social Golfers Problem: g={g}, p={p}, n={g * p}, w={w} ===");
Console.WriteLine();
Landscape.Print(g, p, w);

var solver = new SocialGolfersSolver(g, p, w, TimeSpan.FromSeconds(timeout));
bool found = solver.Solve();

Console.WriteLine();
Console.WriteLine(found
    ? $"U gjet zgjidhje për {w} javë."
    : solver.TimedOut
        ? $"Koha mbaroi ({timeout}s) pa gjetur zgjidhje për {w} javë."
        : $"Nuk ekziston zgjidhje për {w} javë (hapësira u eksplorua plotësisht).");
Console.WriteLine($"Nyje të vizituara: {solver.Nodes:N0}, backtrack-e: {solver.Backtracks:N0}, koha: {solver.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"Numri maksimal i javëve të arritura: {solver.BestWeeks}");
Console.WriteLine();

int[][][] schedule = solver.BestSchedule;
SocialGolfersSolver.PrintSchedule(schedule, g);
Console.WriteLine();
Console.WriteLine($"Objective function f(S) = {SocialGolfersSolver.Objective(schedule, g * p)} " +
                  "(numri i takimeve të përsëritura; f = 0 => zgjidhje valide)");

static class Landscape
{
    public static BigInteger Factorial(int k)
    {
        BigInteger r = BigInteger.One;
        for (int i = 2; i <= k; i++) r *= i;
        return r;
    }

    // Numri i ndarjeve të n lojtarëve në g grupe të pa-renditura me nga p lojtarë:
    // n! / ((p!)^g * g!)
    public static BigInteger PartitionsPerWeek(int g, int p) =>
        Factorial(g * p) / (BigInteger.Pow(Factorial(p), g) * Factorial(g));

    public static void Print(int g, int p, int w)
    {
        int n = g * p;
        BigInteger naive = BigInteger.Pow(g, n * w);
        BigInteger perWeek = PartitionsPerWeek(g, p);
        BigInteger partitions = BigInteger.Pow(perWeek, w);
        BigInteger symmetric = w > 1 ? BigInteger.Pow(perWeek, w - 1) / Factorial(w - 1) : BigInteger.One;

        Console.WriteLine("--- Landscape (madhësia e hapësirës së kërkimit) ---");
        Console.WriteLine($"1) Reprezentim naiv x[javë][lojtar] ∈ {{0..{g - 1}}}:  g^(n·w) = {g}^{n * w} ≈ {Sci(naive)}");
        Console.WriteLine($"2) Ndarje valide për një javë: n!/((p!)^g·g!) = {perWeek:N0} ≈ {Sci(perWeek)}");
        Console.WriteLine($"   Për w javë: (ndarje)^w ≈ {Sci(partitions)}");
        Console.WriteLine($"3) Me thyerje simetrie (java 1 fikse, javët e renditura): ≈ {Sci(symmetric)}");
        Console.WriteLine($"Numri i çifteve: C(n,2) = {n * (n - 1) / 2}, çifte që konsumohen për javë: g·C(p,2) = {g * p * (p - 1) / 2}");
        Console.WriteLine($"Kufiri i sipërm teorik: w ≤ (n-1)/(p-1) = {(n - 1) / (p - 1)}");
    }

    static string Sci(BigInteger x)
    {
        string s = x.ToString();
        return s.Length <= 6 ? s : ((double)x).ToString("0.00e0", System.Globalization.CultureInfo.InvariantCulture);
    }
}

class SocialGolfersSolver
{
    readonly int G, P, W, N;
    readonly int[][][] schedule;   // schedule[java][grupi][pozita] = lojtari
    readonly ulong[] met;          // met[i] = bitmask i lojtarëve që i ka takuar lojtari i
    readonly ulong[] free;         // free[java] = lojtarët ende të pa-caktuar në atë javë
    readonly Stopwatch sw = new();
    readonly TimeSpan limit;

    public long Nodes { get; private set; }
    public long Backtracks { get; private set; }
    public bool TimedOut { get; private set; }
    public int BestWeeks { get; private set; }
    public int[][][] BestSchedule { get; private set; } = [];
    public TimeSpan Elapsed => sw.Elapsed;

    public SocialGolfersSolver(int g, int p, int w, TimeSpan limit)
    {
        G = g; P = p; W = w; N = g * p;
        this.limit = limit;
        schedule = new int[w][][];
        for (int i = 0; i < w; i++)
        {
            schedule[i] = new int[g][];
            for (int j = 0; j < g; j++) schedule[i][j] = new int[p];
        }
        met = new ulong[N];
        free = new ulong[w];
        ulong all = N == 64 ? ulong.MaxValue : (1UL << N) - 1;
        for (int i = 0; i < w; i++) free[i] = all;
    }

    public bool Solve()
    {
        sw.Start();

        // Thyerje simetrie: java e parë fikse {0..p-1}, {p..2p-1}, ...
        for (int grp = 0; grp < G; grp++)
            for (int s = 0; s < P; s++)
                Place(0, grp, s, grp * P + s);
        BestWeeks = 1;
        SaveBest(1);

        bool ok = W == 1 || Dfs(1, 0, 0, 0);
        sw.Stop();
        return ok;
    }

    // DFS me backtracking: mbush javë -> grup -> pozitë.
    // groupMet = bashkimi i lojtarëve që i kanë takuar anëtarët aktualë të grupit.
    bool Dfs(int week, int grp, int slot, ulong groupMet)
    {
        if (slot == P) { grp++; slot = 0; groupMet = 0; }
        if (grp == G)
        {
            if (week + 1 > BestWeeks) { BestWeeks = week + 1; SaveBest(week + 1); }
            week++; grp = 0;
            if (week == W) return true;
        }

        Nodes++;
        if ((Nodes & 0xFFFFF) == 0 && sw.Elapsed > limit) TimedOut = true;
        if (TimedOut) return false;

        ulong candidates;
        if (slot == 0)
        {
            // Grupet renditen sipas lojtarit më të vogël: pozita e parë merr lojtarin më të vogël të lirë.
            candidates = free[week] & (~free[week] + 1);
        }
        else
        {
            int last = schedule[week][grp][slot - 1];
            ulong above = last == 63 ? 0 : ~((1UL << (last + 1)) - 1);
            candidates = free[week] & ~groupMet & above;

            // Javët renditen sipas partnerit të lojtarit 0 (simetri e javëve).
            if (grp == 0 && slot == 1 && week > 1)
            {
                int prev = schedule[week - 1][0][1];
                candidates &= ~((1UL << (prev + 1)) - 1);
            }

            // Forward checking: duhen mjaft kandidatë për pozitat e mbetura.
            if (BitOperations.PopCount(candidates) < P - slot) { Backtracks++; return false; }
        }

        while (candidates != 0)
        {
            int x = BitOperations.TrailingZeroCount(candidates);
            candidates &= candidates - 1;

            Place(week, grp, slot, x);
            if (Dfs(week, grp, slot + 1, groupMet | met[x])) return true;
            Remove(week, grp, slot, x);
            if (TimedOut) return false;
        }

        Backtracks++;
        return false;
    }

    void Place(int week, int grp, int slot, int x)
    {
        schedule[week][grp][slot] = x;
        free[week] &= ~(1UL << x);
        for (int s = 0; s < slot; s++)
        {
            int m = schedule[week][grp][s];
            met[m] |= 1UL << x;
            met[x] |= 1UL << m;
        }
    }

    void Remove(int week, int grp, int slot, int x)
    {
        free[week] |= 1UL << x;
        for (int s = 0; s < slot; s++)
        {
            int m = schedule[week][grp][s];
            met[m] &= ~(1UL << x);
            met[x] &= ~(1UL << m);
        }
    }

    void SaveBest(int weeks) =>
        BestSchedule = schedule.Take(weeks)
            .Select(wk => wk.Select(gr => (int[])gr.Clone()).ToArray())
            .ToArray();

    // Objective function: f(S) = Σ_{i<j} max(0, takime(i,j) - 1)
    public static int Objective(int[][][] s, int n)
    {
        var count = new int[n, n];
        foreach (var week in s)
            foreach (var grp in week)
                for (int a = 0; a < grp.Length; a++)
                    for (int b = a + 1; b < grp.Length; b++)
                        count[grp[a], grp[b]]++;

        int f = 0;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                f += Math.Max(0, count[i, j] - 1);
        return f;
    }

    public static void PrintSchedule(int[][][] s, int g)
    {
        Console.Write($"{"",-8}");
        for (int j = 0; j < g; j++) Console.Write($"{"Grupi " + (j + 1),-14}");
        Console.WriteLine();
        for (int i = 0; i < s.Length; i++)
        {
            Console.Write($"{"Java " + (i + 1),-8}");
            foreach (var grp in s[i]) Console.Write($"{string.Join(",", grp),-14}");
            Console.WriteLine();
        }
    }
}
