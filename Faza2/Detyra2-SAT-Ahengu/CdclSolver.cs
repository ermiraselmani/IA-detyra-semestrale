// SAT solver CDCL (Conflict-Driven Clause Learning):
// two watched literals, analizë 1-UIP, VSIDS, phase saving dhe restart-e Luby.
// Literalet merren në formatin DIMACS (v ose -v, v ≥ 1).
sealed class CdclSolver
{
    readonly int nVars;
    readonly List<int[]> clauses = [];
    readonly List<int>[] watches;          // watches[lit] = klauzolat që e vëzhgojnë literalin lit
    readonly sbyte[] assign;               // 0 = e pacaktuar, 1 = true, -1 = false
    readonly int[] level, reason;
    readonly bool[] phase, seen;
    readonly double[] activity;
    readonly List<int> trail = [], trailLim = [];
    readonly VarHeap heap;
    int qhead;
    double varInc = 1;
    bool unsatAtRoot;

    public long Conflicts { get; private set; }
    public long Decisions { get; private set; }
    public long Propagations { get; private set; }
    public int LearntClauses { get; private set; }

    public CdclSolver(int numVars, IEnumerable<int[]> input)
    {
        nVars = numVars;
        watches = new List<int>[2 * nVars];
        for (int i = 0; i < watches.Length; i++) watches[i] = [];
        assign = new sbyte[nVars];
        level = new int[nVars];
        reason = Enumerable.Repeat(-1, nVars).ToArray();
        phase = new bool[nVars];
        seen = new bool[nVars];
        activity = new double[nVars];
        heap = new VarHeap(activity);
        for (int v = 0; v < nVars; v++) heap.Insert(v);
        foreach (var c in input) AddInputClause(c);
    }

    // Literali i brendshëm: 2v për x_v, 2v+1 për ¬x_v (v nga 0).
    static int Lit(int dimacs) => dimacs > 0 ? 2 * (dimacs - 1) : 2 * (-dimacs - 1) + 1;

    int Value(int lit)
    {
        int a = assign[lit >> 1];
        return (lit & 1) == 0 ? a : -a;
    }

    void AddInputClause(int[] dimacs)
    {
        if (unsatAtRoot) return;
        var lits = new List<int>();
        foreach (int d in dimacs)
        {
            int l = Lit(d);
            if (lits.Contains(l ^ 1) || Value(l) == 1) return;   // tautologji ose e plotësuar në nivelin 0
            if (!lits.Contains(l) && Value(l) != -1) lits.Add(l);
        }
        if (lits.Count == 0) { unsatAtRoot = true; return; }
        if (lits.Count == 1)
        {
            Enqueue(lits[0], -1);
            if (Propagate() != -1) unsatAtRoot = true;
            return;
        }
        AttachClause(lits.ToArray());
    }

    int AttachClause(int[] c)
    {
        clauses.Add(c);
        int ci = clauses.Count - 1;
        watches[c[0]].Add(ci);
        watches[c[1]].Add(ci);
        return ci;
    }

    void Enqueue(int lit, int from)
    {
        int v = lit >> 1;
        assign[v] = (sbyte)((lit & 1) == 0 ? 1 : -1);
        level[v] = trailLim.Count;
        reason[v] = from;
        trail.Add(lit);
    }

    // Unit propagation. Kthen indeksin e klauzolës në konflikt ose -1.
    int Propagate()
    {
        while (qhead < trail.Count)
        {
            int falseLit = trail[qhead++] ^ 1;
            Propagations++;
            var ws = watches[falseLit];
            int i = 0, j = 0;
            while (i < ws.Count)
            {
                int ci = ws[i++];
                int[] c = clauses[ci];
                if (c[0] == falseLit) (c[0], c[1]) = (c[1], c[0]);
                if (Value(c[0]) == 1) { ws[j++] = ci; continue; }

                bool moved = false;
                for (int k = 2; k < c.Length; k++)
                {
                    if (Value(c[k]) == -1) continue;
                    (c[1], c[k]) = (c[k], c[1]);
                    watches[c[1]].Add(ci);
                    moved = true;
                    break;
                }
                if (moved) continue;

                ws[j++] = ci;
                if (Value(c[0]) == -1)
                {
                    while (i < ws.Count) ws[j++] = ws[i++];
                    ws.RemoveRange(j, ws.Count - j);
                    qhead = trail.Count;
                    return ci;
                }
                Enqueue(c[0], ci);   // c[0] është gjithmonë literali i nxjerrë nga klauzola reason
            }
            ws.RemoveRange(j, ws.Count - j);
        }
        return -1;
    }

    // Analiza 1-UIP: kthen klauzolën e mësuar (learnt[0] = literali asertiv) dhe nivelin e backjump-it.
    (int[] Learnt, int BackLevel) Analyze(int confl)
    {
        var learnt = new List<int> { -1 };
        int pathC = 0, p = -1, idx = trail.Count - 1, current = trailLim.Count;
        do
        {
            int[] c = clauses[confl];
            for (int k = p == -1 ? 0 : 1; k < c.Length; k++)
            {
                int v = c[k] >> 1;
                if (seen[v] || level[v] == 0) continue;
                seen[v] = true;
                Bump(v);
                if (level[v] == current) pathC++;
                else learnt.Add(c[k]);
            }
            while (!seen[trail[idx] >> 1]) idx--;
            p = trail[idx--];
            confl = reason[p >> 1];
            seen[p >> 1] = false;
            pathC--;
        } while (pathC > 0);
        learnt[0] = p ^ 1;

        int back = 0;
        for (int k = 1; k < learnt.Count; k++)
        {
            seen[learnt[k] >> 1] = false;
            if (level[learnt[k] >> 1] > back)
            {
                back = level[learnt[k] >> 1];
                (learnt[1], learnt[k]) = (learnt[k], learnt[1]);
            }
        }
        return (learnt.ToArray(), back);
    }

    void Bump(int v)
    {
        if ((activity[v] += varInc) > 1e100)
        {
            for (int i = 0; i < nVars; i++) activity[i] *= 1e-100;
            varInc *= 1e-100;
        }
        heap.Increased(v);
    }

    void Backtrack(int lvl)
    {
        if (trailLim.Count <= lvl) return;
        for (int i = trail.Count - 1; i >= trailLim[lvl]; i--)
        {
            int v = trail[i] >> 1;
            phase[v] = assign[v] == 1;
            assign[v] = 0;
            reason[v] = -1;
            heap.Insert(v);
        }
        trail.RemoveRange(trailLim[lvl], trail.Count - trailLim[lvl]);
        trailLim.RemoveRange(lvl, trailLim.Count - lvl);
        qhead = trail.Count;
    }

    static long Luby(int i)
    {
        long size = 1;
        int seq = 0;
        while (size < i + 1) { seq++; size = 2 * size + 1; }
        long x = 1;
        while (size - 1 != i)
        {
            size = (size - 1) >> 1;
            seq--;
            i %= (int)size;
        }
        for (int k = 0; k < seq; k++) x *= 2;
        return x;
    }

    // Kthen modelin (model[v] për v = 1..n) ose null nëse formula është e paplotësueshme.
    public bool[]? Solve()
    {
        if (unsatAtRoot || Propagate() != -1) return null;
        int restart = 0;
        long budget = 100 * Luby(restart), sinceRestart = 0;

        while (true)
        {
            int confl = Propagate();
            if (confl != -1)
            {
                Conflicts++;
                sinceRestart++;
                if (trailLim.Count == 0) return null;
                var (learnt, back) = Analyze(confl);
                Backtrack(back);
                if (learnt.Length == 1) Enqueue(learnt[0], -1);
                else
                {
                    Enqueue(learnt[0], AttachClause(learnt));
                    LearntClauses++;
                }
                varInc /= 0.95;
                continue;
            }

            if (sinceRestart >= budget)
            {
                Backtrack(0);
                sinceRestart = 0;
                budget = 100 * Luby(++restart);
            }

            int next = -1;
            while (!heap.IsEmpty)
            {
                int v = heap.PopMax();
                if (assign[v] == 0) { next = v; break; }
            }
            if (next == -1)
            {
                var model = new bool[nVars + 1];
                for (int v = 0; v < nVars; v++) model[v + 1] = assign[v] == 1;
                return model;
            }
            Decisions++;
            trailLim.Add(trail.Count);
            Enqueue(2 * next + (phase[next] ? 0 : 1), -1);
        }
    }
}

// Max-heap i variablave sipas aktivitetit VSIDS.
sealed class VarHeap(double[] activity)
{
    readonly List<int> heap = [];
    readonly Dictionary<int, int> pos = [];

    public bool IsEmpty => heap.Count == 0;

    public void Insert(int v)
    {
        if (pos.ContainsKey(v)) return;
        heap.Add(v);
        pos[v] = heap.Count - 1;
        Up(heap.Count - 1);
    }

    public void Increased(int v)
    {
        if (pos.TryGetValue(v, out int i)) Up(i);
    }

    public int PopMax()
    {
        int top = heap[0], last = heap[^1];
        heap.RemoveAt(heap.Count - 1);
        pos.Remove(top);
        if (heap.Count > 0)
        {
            heap[0] = last;
            pos[last] = 0;
            Down(0);
        }
        return top;
    }

    void Up(int i)
    {
        int v = heap[i];
        while (i > 0)
        {
            int p = (i - 1) / 2;
            if (activity[heap[p]] >= activity[v]) break;
            heap[i] = heap[p];
            pos[heap[i]] = i;
            i = p;
        }
        heap[i] = v;
        pos[v] = i;
    }

    void Down(int i)
    {
        int v = heap[i];
        while (true)
        {
            int c = 2 * i + 1;
            if (c >= heap.Count) break;
            if (c + 1 < heap.Count && activity[heap[c + 1]] > activity[heap[c]]) c++;
            if (activity[heap[c]] <= activity[v]) break;
            heap[i] = heap[c];
            pos[heap[i]] = i;
            i = c;
        }
        heap[i] = v;
        pos[v] = i;
    }
}
