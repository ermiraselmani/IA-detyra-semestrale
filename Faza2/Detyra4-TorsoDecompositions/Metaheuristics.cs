using System.Diagnostics;

sealed record RunResult(string Name, Archive Archive, long Evaluations, TimeSpan Elapsed, int[] BestPerm);

static class Metaheuristics
{
    // Fitness i një permutacioni: HV i shkallës (t, width(t)) për të gjitha t. Një permutacion jep
    // njëherësh një front të tërë, sepse çdo t është zgjidhje e vlefshme.
    static double Fitness(Eliminator el, int[] perm, out int[] staircase)
    {
        staircase = Front.Staircase(el.OutDegrees(perm));
        return Front.Hypervolume(staircase);
    }

    // ---------- Baseline: permutacione të rastësishme ----------
    public static RunResult RandomSearch(Graph g, double seconds, int seed)
    {
        var rng = new Random(seed);
        var el = new Eliminator(g);
        var archive = new Archive(g.N);
        var sw = Stopwatch.StartNew();
        long evals = 0;
        double best = -1;
        int[] bestPerm = [];
        do
        {
            var perm = Enumerable.Range(0, g.N).ToArray();
            rng.Shuffle(perm);
            double f = Fitness(el, perm, out var st);
            archive.Add(perm, st);
            evals++;
            if (f > best) { best = f; bestPerm = perm; }
        } while (sw.Elapsed.TotalSeconds < seconds);
        return new("Kërkim i rastësishëm", archive, evals, sw.Elapsed, bestPerm);
    }

    // ---------- Konstruktive: min-degree (greedy) dhe varianti i rastësishëm për GRASP ----------
    // Në çdo hap eliminohet nyja me gradën më të vogël në grafin aktual (me brinjët e reja).
    // Me slack > 0 zgjidhet rastësisht një nyje me gradë ≤ min + slack (Restricted Candidate List).
    public static int[] MinDegree(Graph g, Eliminator el, Random rng, int slack, out int[] outDeg) =>
        MinDegree(g, el, rng, slack, [], out outDeg);

    // Varianti me prefiks: nyjet e prefix-it eliminohen të parat (jashtë torsos), pjesa tjetër me min-degree.
    public static int[] MinDegree(Graph g, Eliminator el, Random rng, int slack, IReadOnlyList<int> prefix, out int[] outDeg)
    {
        el.Reset();
        int n = g.N;
        var alive = new bool[n];
        Array.Fill(alive, true);
        var perm = new int[n];
        outDeg = new int[n];
        var touched = new List<int>();
        var rcl = new List<int>();
        for (int i = 0; i < prefix.Count; i++)
        {
            perm[i] = prefix[i];
            alive[prefix[i]] = false;
            outDeg[i] = el.Eliminate(prefix[i]);
        }
        var deg = new int[n];
        for (int v = 0; v < n; v++) if (alive[v]) deg[v] = el.CurrentDegree(v);
        for (int i = prefix.Count; i < n; i++)
        {
            int min = int.MaxValue;
            for (int v = 0; v < n; v++) if (alive[v] && deg[v] < min) min = deg[v];
            rcl.Clear();
            for (int v = 0; v < n; v++) if (alive[v] && deg[v] <= min + slack) rcl.Add(v);
            int u = rcl[rng.Next(rcl.Count)];
            alive[u] = false;
            perm[i] = u;
            touched.Clear();
            outDeg[i] = el.Eliminate(u, touched);
            foreach (int v in touched) deg[v] = el.CurrentDegree(v);
        }
        return perm;
    }

    public static RunResult Greedy(Graph g, int seed)
    {
        var sw = Stopwatch.StartNew();
        var el = new Eliminator(g);
        var perm = MinDegree(g, el, new Random(seed), 0, out var od);
        var archive = new Archive(g.N);
        archive.Add(perm, Front.Staircase(od));
        return new("Greedy min-degree", archive, 1, sw.Elapsed, perm);
    }

    // ---------- Peeling: nyja që e kufizon gjerësinë e torsos kalon jashtë torsos (në prefiks) ----------
    public static RunResult Peeling(Graph g, double seconds, int seed)
    {
        var rng = new Random(seed);
        var el = new Eliminator(g);
        var archive = new Archive(g.N);
        var sw = Stopwatch.StartNew();
        var prefix = new List<int>();
        long evals = 0;
        int[] perm = [];
        while (prefix.Count < g.N - 1 && sw.Elapsed.TotalSeconds < seconds)
        {
            perm = MinDegree(g, el, rng, 0, prefix, out var od);
            archive.Add(perm, Front.Staircase(od));
            evals++;
            int worst = prefix.Count;
            for (int i = prefix.Count; i < g.N; i++) if (od[i] > od[worst]) worst = i;
            prefix.Add(perm[worst]);
        }
        return new("Peeling", archive, evals, sw.Elapsed, perm);
    }

    // ---------- SA mbi bashkësinë P (nyjet jashtë torsos), për disa gjerësi të synuara ----------
    // Për gjerësinë e synuar w minimizohet f(P) = |P| + λ · max(0, width(P) − w), ku width(P) llogaritet
    // duke eliminuar P të parat dhe pastaj pjesën tjetër me min-degree. Lëvizjet: hiq një nyje nga P,
    // shto në P një nyje "bottleneck" (me outdegree > w) ose zëvendëso njërën me tjetrën.
    public static RunResult PrefixAnnealing(Graph g, Archive seedArchive, double seconds, int seed)
    {
        var rng = new Random(seed);
        var el = new Eliminator(g);
        var archive = new Archive(g.N);
        archive.Merge(seedArchive);
        var sw = Stopwatch.StartNew();
        long evals = 0;
        const double lambda = 20;

        // Deri në 12 gjerësi të synuara, të shpërndara njëtrajtësisht nëpër frontin fillestar.
        var all = seedArchive.ParetoFront().Select(p => p.W - 1).Where(w => w >= 1).Distinct().ToList();
        var targets = all.Count <= 12 ? all :
            Enumerable.Range(0, 12).Select(i => all[(int)((long)i * (all.Count - 1) / 11)]).Distinct().ToList();
        if (targets.Count == 0) return new("SA mbi P (torso-aware)", archive, 0, sw.Elapsed, []);
        double perTarget = seconds / targets.Count;
        int[] bestPerm = [];

        foreach (int w in targets)
        {
            double tStart = sw.Elapsed.TotalSeconds;
            // Fillimi: zgjidhja më e mirë e arkivit me gjerësi ≤ w + 1 (prefiksi i saj).
            var front = archive.ParetoFront();
            var (t0, _) = front.First(p => p.W <= w + 1);
            var inP = new bool[g.N];
            var P = archive.PermOf[t0]![..t0].ToList();
            foreach (int v in P) inP[v] = true;

            double Eval(List<int> prefix, out int[] perm, out int[] od, out int width)
            {
                perm = MinDegree(g, el, rng, 0, prefix, out od);
                var st = Front.Staircase(od);
                archive.Add(perm, st);
                evals++;
                width = prefix.Count < g.N ? st[prefix.Count] : 0;
                return prefix.Count + lambda * Math.Max(0, width - w);
            }

            double fCur = Eval(P, out var curPerm, out var curOd, out _);
            double temp0 = 2.0, tempEnd = 0.05;
            while (true)
            {
                double progress = (sw.Elapsed.TotalSeconds - tStart) / perTarget;
                if (progress >= 1) break;
                double temp = temp0 * Math.Pow(tempEnd / temp0, progress);

                var cand = new List<int>(P);
                double r = rng.NextDouble();
                if ((r < 0.5 || cand.Count == g.N - 1) && cand.Count > 0)
                    cand.RemoveAt(rng.Next(cand.Count));
                else
                {
                    // Bottleneck: nyjet e torsos me outdegree > w në dekodimin aktual.
                    var hot = new List<int>();
                    for (int i = P.Count; i < g.N; i++) if (curOd[i] > w) hot.Add(curPerm[i]);
                    int add = hot.Count > 0 ? hot[rng.Next(hot.Count)] : curPerm[rng.Next(P.Count, g.N)];
                    if (r < 0.8 || cand.Count == 0) cand.Add(add);
                    else cand[rng.Next(cand.Count)] = add;
                }

                double f = Eval(cand, out var perm, out var od, out _);
                if (f <= fCur || rng.NextDouble() < Math.Exp((fCur - f) / temp))
                {
                    P = cand;
                    fCur = f;
                    curPerm = perm;
                    curOd = od;
                    bestPerm = perm;
                }
            }
        }
        return new("SA mbi P (torso-aware)", archive, evals, sw.Elapsed, bestPerm);
    }

    // ---------- GRASP: ndërtim i rastësishëm min-degree + kërkim lokal i shkurtër ----------
    public static RunResult Grasp(Graph g, double seconds, int seed)
    {
        var rng = new Random(seed);
        var el = new Eliminator(g);
        var archive = new Archive(g.N);
        var sw = Stopwatch.StartNew();
        long evals = 0;
        double best = -1;
        int[] bestPerm = [];
        do
        {
            int slack = rng.Next(0, 4);
            var perm = MinDegree(g, el, rng, slack, out var od);
            var st = Front.Staircase(od);
            double f = Front.Hypervolume(st);
            archive.Add(perm, st);
            evals++;
            // Kërkimi lokal: first-improvement me lëvizje insertion, me pak hapa.
            for (int step = 0; step < 20 && sw.Elapsed.TotalSeconds < seconds; step++)
            {
                var cand = Neighbour(perm, rng);
                double fc = Fitness(el, cand, out var stc);
                archive.Add(cand, stc);
                evals++;
                if (fc >= f) { perm = cand; f = fc; }
            }
            if (f > best) { best = f; bestPerm = perm; }
        } while (sw.Elapsed.TotalSeconds < seconds);
        return new("GRASP (min-degree i rastësishëm + LS)", archive, evals, sw.Elapsed, bestPerm);
    }

    // ---------- Simulated Annealing mbi permutacionin ----------
    public static RunResult SimulatedAnnealing(Graph g, int[] start, double seconds, int seed, string name)
    {
        var rng = new Random(seed);
        var el = new Eliminator(g);
        var archive = new Archive(g.N);
        var sw = Stopwatch.StartNew();
        var cur = (int[])start.Clone();
        double fCur = Fitness(el, cur, out var st0);
        archive.Add(cur, st0);
        var best = cur;
        double fBest = fCur;
        long evals = 1, accepted = 0;

        // Temperatura fillestare: ndryshimi mesatar i fitness-it për disa fqinjë të rastësishëm.
        double sum = 0;
        int samples = 0;
        for (; samples < 20 && sw.Elapsed.TotalSeconds < seconds * 0.1; samples++)
        {
            var c = Neighbour(cur, rng);
            sum += Math.Abs(Fitness(el, c, out var stc) - fCur);
            archive.Add(c, stc);
            evals++;
        }
        double t0 = Math.Max(1e-6, samples > 0 ? sum / samples : 1e-3), tEnd = t0 * 1e-3;

        while (true)
        {
            double progress = sw.Elapsed.TotalSeconds / seconds;
            if (progress >= 1) break;
            double temp = t0 * Math.Pow(tEnd / t0, progress);   // ftohje gjeometrike sipas kohës
            var cand = Neighbour(cur, rng);
            double f = Fitness(el, cand, out var st);
            archive.Add(cand, st);
            evals++;
            double delta = f - fCur;   // maksimizim
            if (delta >= 0 || rng.NextDouble() < Math.Exp(delta / temp))
            {
                cur = cand;
                fCur = f;
                accepted++;
                if (f > fBest) { fBest = f; best = cand; }
            }
        }
        return new(name, archive, evals, sw.Elapsed, best);
    }

    // Lëvizjet: insertion (zhvendos një nyje në pozitë tjetër) dhe swap. Pozitat zgjidhen më shpesh
    // afër njëra-tjetrës, sepse ndryshimet e vogla lokale e ruajnë strukturën e mirë të renditjes.
    static int[] Neighbour(int[] perm, Random rng)
    {
        int n = perm.Length;
        var p = (int[])perm.Clone();
        int i = rng.Next(n);
        int span = rng.NextDouble() < 0.7 ? Math.Max(2, n / 50) : n;
        int j = Math.Clamp(i + rng.Next(-span, span + 1), 0, n - 1);
        if (i == j) j = (i + 1) % n;
        if (rng.NextDouble() < 0.5)
        {
            (p[i], p[j]) = (p[j], p[i]);
        }
        else
        {
            int v = p[i];
            if (i < j) Array.Copy(p, i + 1, p, i, j - i);
            else Array.Copy(p, j, p, j + 1, i - j);
            p[j] = v;
        }
        return p;
    }
}
