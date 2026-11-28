using System.Numerics;

// Grafi ruhet si matricë fqinjësie me bitset: W fjalë 64-bitëshe për çdo nyje.
sealed class Graph
{
    public int N, M, W;
    public ulong[] Adj = [];
    public int[] Degree = [];

    public static Graph Load(string path)
    {
        var edges = new List<(int, int)>();
        int max = -1;
        foreach (var line in File.ReadLines(path))
        {
            var w = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length < 2 || w[0].StartsWith('#')) continue;
            int a = int.Parse(w[0]), b = int.Parse(w[1]);
            if (a == b) continue;
            edges.Add((a, b));
            max = Math.Max(max, Math.Max(a, b));
        }
        var g = new Graph { N = max + 1, W = (max + 64) / 64 };
        g.Adj = new ulong[g.N * g.W];
        foreach (var (a, b) in edges)
        {
            g.Adj[a * g.W + (b >> 6)] |= 1UL << b;
            g.Adj[b * g.W + (a >> 6)] |= 1UL << a;
        }
        g.Degree = new int[g.N];
        for (int v = 0; v < g.N; v++)
            for (int k = 0; k < g.W; k++) g.Degree[v] += BitOperations.PopCount(g.Adj[v * g.W + k]);
        g.M = g.Degree.Sum() / 2;
        return g;
    }
}

// Loja e eliminimit: nyjet hiqen sipas radhës; kur hiqet u, fqinjët e tij të mbetur (out-fqinjët)
// lidhen mes vete (brinjët e reja). Outdegree i u është numri i fqinjëve të mbetur në atë moment.
sealed class Eliminator
{
    readonly Graph g;
    readonly int W;
    public readonly ulong[] Adj;
    public readonly ulong[] Alive;
    readonly ulong[] outSet;
    readonly int[] outList;

    public Eliminator(Graph graph)
    {
        g = graph;
        W = g.W;
        Adj = new ulong[g.Adj.Length];
        Alive = new ulong[W];
        outSet = new ulong[W];
        outList = new int[g.N];
        Reset();
    }

    public void Reset()
    {
        Array.Copy(g.Adj, Adj, Adj.Length);
        Array.Fill(Alive, ~0UL);
        if (g.N % 64 != 0) Alive[W - 1] = (1UL << (g.N % 64)) - 1;
    }

    public bool IsAlive(int v) => (Alive[v >> 6] >> v & 1) != 0;

    public int CurrentDegree(int v)
    {
        int d = 0, b = v * W;
        for (int k = 0; k < W; k++) d += BitOperations.PopCount(Adj[b + k] & Alive[k]);
        return d;
    }

    // Eliminon u. Kthen outdegree-n; nëse touched != null, aty shtohen out-fqinjët (për rifreskim gradash).
    public int Eliminate(int u, List<int>? touched = null)
    {
        Alive[u >> 6] &= ~(1UL << u);
        int bu = u * W, count = 0;
        for (int k = 0; k < W; k++)
        {
            ulong x = outSet[k] = Adj[bu + k] & Alive[k];
            while (x != 0)
            {
                outList[count++] = (k << 6) + BitOperations.TrailingZeroCount(x);
                x &= x - 1;
            }
        }
        for (int i = 0; i < count; i++)
        {
            int v = outList[i], bv = v * W;
            for (int k = 0; k < W; k++) Adj[bv + k] |= outSet[k];
            Adj[bv + (v >> 6)] &= ~(1UL << v);
            touched?.Add(v);
        }
        return count;
    }

    // Outdegree-t sipas pozitës në permutacion.
    public int[] OutDegrees(int[] perm)
    {
        Reset();
        var od = new int[perm.Length];
        for (int i = 0; i < perm.Length; i++) od[i] = Eliminate(perm[i]);
        return od;
    }
}

static class Front
{
    public const int WidthCap = 501;   // gjerësitë > 500 vlerësohen si 501 (sipas rregullave të sfidës)

    // width(t) = max outdegree në pozitat t..n−1: funksion jo-rritës i t-së.
    public static int[] Staircase(int[] outDeg)
    {
        int n = outDeg.Length;
        var w = new int[n];
        int m = 0;
        for (int t = n - 1; t >= 0; t--) w[t] = m = Math.Max(m, outDeg[t]);
        return w;
    }

    // Hipervëllimi (minimizim i t dhe i width, pika referuese (n, 501)) i të gjitha pikave (t, width(t)),
    // i normalizuar në [0, 1]. Kolona [t, t+1] mbulohet nga lartësia 501 − width(t).
    public static double Hypervolume(int[] staircase)
    {
        long hv = 0;
        foreach (int w in staircase) hv += WidthCap - Math.Min(w, WidthCap);
        return hv / ((double)staircase.Length * WidthCap);
    }

    // HV i një bashkësie pikash (t, w), të normalizuar.
    public static double Hypervolume(IReadOnlyList<(int T, int W)> points, int n)
    {
        long hv = 0;
        var sorted = points.Where(p => p.W < WidthCap).OrderBy(p => p.T).ToList();
        int bestW = WidthCap;
        for (int i = 0; i < sorted.Count; i++)
        {
            bestW = Math.Min(bestW, sorted[i].W);
            int next = i + 1 < sorted.Count ? sorted[i + 1].T : n;
            hv += (long)(next - sorted[i].T) * (WidthCap - bestW);
        }
        return hv / ((double)n * WidthCap);
    }
}

// Arkivi: për çdo prag t ruhet gjerësia më e mirë e gjetur dhe permutacioni që e jep.
sealed class Archive
{
    readonly int n;
    public readonly int[] BestW;
    public readonly int[]?[] PermOf;

    public Archive(int n)
    {
        this.n = n;
        BestW = Enumerable.Repeat(int.MaxValue, n).ToArray();
        PermOf = new int[n][];
    }

    public bool Add(int[] perm, int[] staircase)
    {
        int[]? copy = null;
        for (int t = 0; t < n; t++)
        {
            if (staircase[t] >= BestW[t]) continue;
            BestW[t] = staircase[t];
            PermOf[t] = copy ??= (int[])perm.Clone();
        }
        return copy is not null;
    }

    public void Merge(Archive other)
    {
        for (int t = 0; t < n; t++)
            if (other.BestW[t] < BestW[t]) { BestW[t] = other.BestW[t]; PermOf[t] = other.PermOf[t]; }
    }

    // Pikat jo të dominuara: (t, w) ku w është më e vogël se çdo gjerësi me t' < t.
    public List<(int T, int W)> ParetoFront()
    {
        var front = new List<(int, int)>();
        int best = int.MaxValue;
        for (int t = 0; t < n; t++)
            if (BestW[t] < best) { best = BestW[t]; front.Add((t, best)); }
        return front;
    }

    // Zgjedhja optimale e k pikave që maksimizojnë HV (programim dinamik, O(k·m²)).
    public List<(int T, int W)> SelectBest(int k)
    {
        var f = ParetoFront().Where(p => p.W < Front.WidthCap).ToList();
        int m = f.Count;
        if (m <= k) return f;
        // g[c, i] = HV maksimal me c pika, ku i është pika e parë (me t më të vogël) e zgjedhur.
        var g = new long[k + 1, m];
        var next = new int[k + 1, m];
        for (int i = 0; i < m; i++) g[1, i] = (long)(n - f[i].T) * (Front.WidthCap - f[i].W);
        for (int c = 2; c <= k; c++)
            for (int i = 0; i < m; i++)
            {
                g[c, i] = g[1, i];
                next[c, i] = -1;
                for (int j = i + 1; j < m; j++)
                {
                    long v = (long)(f[j].T - f[i].T) * (Front.WidthCap - f[i].W) + g[c - 1, j];
                    if (v > g[c, i]) { g[c, i] = v; next[c, i] = j; }
                }
            }
        int start = Enumerable.Range(0, m).MaxBy(i => g[k, i]);
        var chosen = new List<(int, int)>();
        for (int c = k, i = start; i >= 0 && c >= 1; i = c >= 2 ? next[c, i] : -1, c--) chosen.Add(f[i]);
        return chosen;
    }
}
