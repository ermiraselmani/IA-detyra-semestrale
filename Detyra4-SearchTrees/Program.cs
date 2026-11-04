using System.Globalization;
using System.Text;

// Search Trees për: 8-Puzzle, Missionaries and Cannibals, Rotating Workforce Scheduling.
// Përdorimi: dotnet run -- [all | puzzle | mc | rws] [--dot] [--mermaid] [--full]
//   puzzle [gjendja]  p.sh. "puzzle 023146758" (0 = vendi bosh)
//   --dot             ruan pemët edhe si Graphviz (.dot) në folderin output/
//   --mermaid         ruan pemët edhe si diagrame Mermaid (.mmd) në folderin output/
//   --full            te RWS i shfaq edhe fëmijët e prerë një nga një (pa i grupuar)

Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

bool dot = args.Contains("--dot");
bool mermaid = args.Contains("--mermaid");
bool full = args.Contains("--full");
string[] rest = args.Where(a => !a.StartsWith("--")).ToArray();
string which = rest.Length > 0 ? rest[0].ToLowerInvariant() : "all";

if (which is not ("all" or "puzzle" or "mc" or "rws"))
{
    Console.WriteLine("Përdorimi: dotnet run -- [all | puzzle [gjendja] | mc | rws] [--dot] [--mermaid] [--full]");
    return;
}

if (which is "all" or "puzzle")
{
    string start = which == "puzzle" && rest.Length > 1 ? rest[1] : "023146758";
    if (!EightPuzzle.IsValidInput(start))
    {
        Console.WriteLine("Gjendja e 8-Puzzle duhet të ketë 9 shifra të ndryshme 0..8 (0 = vendi bosh).");
        return;
    }
    var p = new EightPuzzle(start);
    if (!p.IsSolvable())
    {
        Console.WriteLine($"Gjendja {start} nuk është e zgjidhshme (numri i inversioneve është tek).");
        return;
    }
    Run(p, SearchTree<string>.Bfs(p), collapseInvalid: false, dot, mermaid);
}

if (which is "all" or "mc")
{
    var p = new MissionariesCannibals();
    Run(p, SearchTree<McState>.Bfs(p), collapseInvalid: false, dot, mermaid);
}

if (which is "all" or "rws")
{
    var p = new RotatingWorkforce();
    Run(p, SearchTree<char[]>.Dfs(p), collapseInvalid: !full, dot, mermaid);
    if (p.Solution is { } sol) p.PrintSchedule(sol);
}

static void Run<S>(IProblem<S> p, SearchTree<S> tree, bool collapseInvalid, bool dot, bool mermaid) where S : notnull
{
    Console.WriteLine(new string('=', 78));
    Console.WriteLine($"=== {p.Name} — Search Tree me {tree.Strategy} ===");
    Console.WriteLine(new string('=', 78));
    p.Describe();
    Console.WriteLine();
    Console.WriteLine("Legjenda: ★ goal | ◄ në rrugën e zgjidhjes | ↺ gjendje e përsëritur (nuk zgjerohet)");
    Console.WriteLine("          ✗ gjendje jo-valide (prerë) | ⊘ dead-end | … në frontier (e pazgjeruar)");
    Console.WriteLine();
    tree.Print(collapseInvalid);
    Console.WriteLine();
    tree.PrintStats();
    tree.PrintSolution();
    if (dot)
    {
        Directory.CreateDirectory("output");
        string file = Path.Combine("output", p.FileName + ".dot");
        File.WriteAllText(file, tree.ToDot());
        Console.WriteLine($"Pema u ruajt si Graphviz: {file}  (render: dot -Tsvg {file} -o {p.FileName}.svg)");
    }
    if (mermaid)
    {
        Directory.CreateDirectory("output");
        string file = Path.Combine("output", p.FileName + ".mmd");
        File.WriteAllText(file, tree.ToMermaid(collapseInvalid));
        Console.WriteLine($"Pema u ruajt si Mermaid: {file}");
    }
    Console.WriteLine();
}

// ---------------------------------------------------------------------------------------------
// Pjesa e përgjithshme: problemi dhe pema e kërkimit
// ---------------------------------------------------------------------------------------------

interface IProblem<S> where S : notnull
{
    string Name { get; }
    string FileName { get; }
    S Initial { get; }
    bool IsGoal(S s);
    // Successor function: (veprimi, gjendja e re, a është valide)
    IEnumerable<(string Action, S State, bool Valid)> Successors(S s);
    string Show(S s);
    string Key(S s);   // çelësi për të njohur gjendjet e përsëritura
    void Describe();
}

enum Mark { Expanded, Goal, Repeated, Invalid, DeadEnd, Frontier }

class Node<S>(int id, S state, string action, Node<S>? parent)
{
    public int Id { get; } = id;
    public S State { get; } = state;
    public string Action { get; } = action;
    public Node<S>? Parent { get; } = parent;
    public int Depth { get; } = parent is null ? 0 : parent.Depth + 1;
    public List<Node<S>> Children { get; } = [];
    public Mark Mark { get; set; } = Mark.Frontier;
    public bool OnPath { get; set; }
}

class SearchTree<S> where S : notnull
{
    readonly IProblem<S> problem;
    int nextId;
    public string Strategy { get; private init; } = "";
    public Node<S> Root { get; }
    public Node<S>? Goal { get; private set; }

    SearchTree(IProblem<S> problem)
    {
        this.problem = problem;
        Root = NewNode(problem.Initial, "", null);
    }

    Node<S> NewNode(S state, string action, Node<S>? parent)
    {
        var n = new Node<S>(nextId++, state, action, parent);
        parent?.Children.Add(n);
        return n;
    }

    // BFS (graph search): nyjet zgjerohen nivel pas niveli (FIFO). Goal test bëhet kur nyja gjenerohet.
    // Gjendjet që janë parë më herët shënohen ↺ dhe nuk zgjerohen, që pema të mos jetë e pafundme.
    public static SearchTree<S> Bfs(IProblem<S> problem, int maxNodes = 5000)
    {
        var t = new SearchTree<S>(problem) { Strategy = "Breadth First Search (BFS)" };
        if (problem.IsGoal(t.Root.State)) { t.SetGoal(t.Root); return t; }

        var seen = new HashSet<string> { problem.Key(t.Root.State) };
        var queue = new Queue<Node<S>>();
        queue.Enqueue(t.Root);

        while (queue.Count > 0 && t.nextId < maxNodes)
        {
            var node = queue.Dequeue();
            node.Mark = Mark.Expanded;
            bool alive = false;

            foreach (var (action, state, valid) in problem.Successors(node.State))
            {
                var child = t.NewNode(state, action, node);
                if (!valid) { child.Mark = Mark.Invalid; continue; }
                if (!seen.Add(problem.Key(state))) { child.Mark = Mark.Repeated; continue; }
                if (problem.IsGoal(state)) { t.SetGoal(child); return t; }
                queue.Enqueue(child);    // mbetet Frontier derisa të zgjerohet
                alive = true;
            }
            if (!alive) node.Mark = Mark.DeadEnd;
        }
        return t;
    }

    // DFS me backtracking: zgjerohet gjithmonë fëmija i parë valid; kur një nyje nuk ka asnjë
    // vazhdim valid, ajo shënohet ⊘ dhe kërkimi kthehet te prindi (backtrack).
    public static SearchTree<S> Dfs(IProblem<S> problem, int maxNodes = 200_000)
    {
        var t = new SearchTree<S>(problem) { Strategy = "Depth First Search (DFS) me Backtracking" };
        var onPath = new HashSet<string>();
        t.DfsVisit(t.Root, onPath, maxNodes);
        return t;
    }

    bool DfsVisit(Node<S> node, HashSet<string> onPath, int maxNodes)
    {
        if (problem.IsGoal(node.State)) { SetGoal(node); return true; }
        if (nextId >= maxNodes) return false;   // mbetet Frontier

        node.Mark = Mark.Expanded;
        onPath.Add(problem.Key(node.State));
        foreach (var (action, state, valid) in problem.Successors(node.State))
        {
            var child = NewNode(state, action, node);
            if (!valid) { child.Mark = Mark.Invalid; continue; }
            if (onPath.Contains(problem.Key(state))) { child.Mark = Mark.Repeated; continue; }
            if (DfsVisit(child, onPath, maxNodes)) return true;
        }
        onPath.Remove(problem.Key(node.State));
        if (node.Mark == Mark.Expanded) node.Mark = Mark.DeadEnd;   // backtrack
        return false;
    }

    void SetGoal(Node<S> g)
    {
        Goal = g;
        g.Mark = Mark.Goal;
        for (var n = g; n is not null; n = n.Parent) n.OnPath = true;
    }

    IEnumerable<Node<S>> All()
    {
        var stack = new Stack<Node<S>>([Root]);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            foreach (var c in n.Children) stack.Push(c);
        }
    }

    static string Symbol(Mark m) => m switch
    {
        Mark.Goal => " ★ GOAL",
        Mark.Repeated => " ↺",
        Mark.Invalid => " ✗",
        Mark.DeadEnd => " ⊘",
        Mark.Frontier => " …",
        _ => "",
    };

    string Label(Node<S> n) =>
        (n.Parent is null ? "" : $"{n.Action} → ") +
        $"[{n.Id}] {problem.Show(n.State)}{Symbol(n.Mark)}{(n.OnPath && n.Mark != Mark.Goal ? " ◄" : "")}";

    // Printimi i pemës në ASCII. Me collapseInvalid, fëmijët jo-validë të një nyjeje grupohen në një rresht.
    public void Print(bool collapseInvalid)
    {
        Console.WriteLine(Label(Root));
        PrintChildren(Root, "", collapseInvalid);
    }

    void PrintChildren(Node<S> node, string indent, bool collapseInvalid)
    {
        var items = new List<(Node<S>? Node, string Text)>();
        var invalid = node.Children.Where(c => c.Mark == Mark.Invalid).ToList();
        foreach (var c in node.Children)
            if (!collapseInvalid || c.Mark != Mark.Invalid) items.Add((c, Label(c)));
        if (collapseInvalid && invalid.Count > 0)
            items.Insert(0, (null, $"✗ prerë: {string.Join(", ", invalid.Select(c => c.Action))}"));

        for (int i = 0; i < items.Count; i++)
        {
            bool last = i == items.Count - 1;
            Console.WriteLine(indent + (last ? "└── " : "├── ") + items[i].Text);
            if (items[i].Node is { } child)
                PrintChildren(child, indent + (last ? "    " : "│   "), collapseInvalid);
        }
    }

    public void PrintStats()
    {
        var all = All().ToList();
        int Count(Mark m) => all.Count(n => n.Mark == m);
        int expanded = Count(Mark.Expanded) + Count(Mark.DeadEnd);
        int validChildren = all.Count(n => n.Parent is not null && n.Mark != Mark.Invalid);
        Console.WriteLine("--- Statistikat e pemës ---");
        Console.WriteLine($"Nyje të gjeneruara: {all.Count}, të zgjeruara: {expanded}, në frontier: {Count(Mark.Frontier)}");
        Console.WriteLine($"Jo-valide (✗): {Count(Mark.Invalid)}, të përsëritura (↺): {Count(Mark.Repeated)}, dead-end (⊘): {Count(Mark.DeadEnd)}");
        Console.WriteLine($"Thellësia maksimale e pemës: {all.Max(n => n.Depth)}, " +
                          $"faktori mesatar i degëzimit (fëmijë valide / nyje e zgjeruar): " +
                          $"{(expanded == 0 ? 0 : validChildren / (double)expanded):F2}");
    }

    public void PrintSolution()
    {
        if (Goal is null) { Console.WriteLine("Nuk u gjet zgjidhje."); return; }
        var path = new List<Node<S>>();
        for (var n = Goal; n is not null; n = n.Parent) path.Add(n);
        path.Reverse();
        Console.WriteLine($"Zgjidhja (thellësia / kostoja = {Goal.Depth}): " +
                          string.Join(" ", path.Skip(1).Select(n => n.Action)));
    }

    // Diagrami Mermaid (GitHub e shfaq direkt në README). Rruga e zgjidhjes vizatohet me shigjeta të trasha.
    public string ToMermaid(bool collapseInvalid)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine("  classDef expanded fill:#ffffff,stroke:#555,color:#000");
        sb.AppendLine("  classDef path fill:#c8e6c9,stroke:#2e7d32,stroke-width:2px,color:#000");
        sb.AppendLine("  classDef goal fill:#ffd54f,stroke:#f57f17,stroke-width:3px,color:#000");
        sb.AppendLine("  classDef invalid fill:#ef9a9a,stroke:#c62828,color:#000");
        sb.AppendLine("  classDef repeated fill:#e0e0e0,stroke:#9e9e9e,color:#000");
        sb.AppendLine("  classDef deadend fill:#f8bbd0,stroke:#ad1457,color:#000");
        sb.AppendLine("  classDef frontier fill:#bbdefb,stroke:#1565c0,color:#000");

        int edge = 0;
        var pathEdges = new List<int>();
        string Text(string s) => s.Replace("\"", "#quot;").Replace("|", "<br/>");
        string Class(Node<S> n) => n.Mark switch
        {
            Mark.Goal => "goal",
            Mark.Invalid => "invalid",
            Mark.Repeated => "repeated",
            Mark.DeadEnd => "deadend",
            Mark.Frontier => "frontier",
            _ => n.OnPath ? "path" : "expanded",
        };

        var stack = new Stack<Node<S>>([Root]);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            sb.AppendLine($"  n{n.Id}[\"[{n.Id}] {Text(problem.Show(n.State))}{Symbol(n.Mark)}\"]:::{Class(n)}");
            if (n.Parent is { } p)
            {
                if (n.OnPath) pathEdges.Add(edge);
                sb.AppendLine($"  n{p.Id} -->|\"{Text(n.Action)}\"| n{n.Id}");
                edge++;
            }

            var invalid = n.Children.Where(c => c.Mark == Mark.Invalid).ToList();
            if (collapseInvalid && invalid.Count > 0)
            {
                sb.AppendLine($"  x{n.Id}[\"✗ prerë: {Text(string.Join(", ", invalid.Select(c => c.Action)))}\"]:::invalid");
                sb.AppendLine($"  n{n.Id} -.-> x{n.Id}");
                edge++;
            }
            for (int i = n.Children.Count - 1; i >= 0; i--)
                if (!collapseInvalid || n.Children[i].Mark != Mark.Invalid) stack.Push(n.Children[i]);
        }
        if (pathEdges.Count > 0)
            sb.AppendLine($"  linkStyle {string.Join(",", pathEdges)} stroke:#2e7d32,stroke-width:3px");
        return sb.ToString();
    }

    public string ToDot()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"digraph \"{problem.Name}\" {{");
        sb.AppendLine("  node [shape=box, fontname=\"Courier\", style=filled, fillcolor=white];");
        foreach (var n in All())
        {
            string color = n.Mark switch
            {
                Mark.Goal => "gold",
                Mark.Invalid => "lightcoral",
                Mark.Repeated => "lightgrey",
                Mark.DeadEnd => "mistyrose",
                Mark.Frontier => "lightblue",
                _ => n.OnPath ? "palegreen" : "white",
            };
            string label = $"[{n.Id}] {problem.Show(n.State)}{Symbol(n.Mark)}".Replace("\"", "\\\"");
            sb.AppendLine($"  n{n.Id} [label=\"{label}\", fillcolor={color}{(n.OnPath ? ", penwidth=2" : "")}];");
            if (n.Parent is { } p)
                sb.AppendLine($"  n{p.Id} -> n{n.Id} [label=\"{n.Action}\"{(n.OnPath ? ", penwidth=2" : "")}];");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }
}

// ---------------------------------------------------------------------------------------------
// 1) 8-Puzzle
// ---------------------------------------------------------------------------------------------

class EightPuzzle(string start) : IProblem<string>
{
    const string GoalState = "123456780";
    public string Name => "8-Puzzle";
    public string FileName => "8puzzle";
    public string Initial => start;

    public bool IsGoal(string s) => s == GoalState;
    public string Key(string s) => s;
    public string Show(string s) => $"{s[..3]}|{s[3..6]}|{s[6..]}".Replace('0', '_');

    // Veprimet: lëvizja e vendit bosh lart, poshtë, majtas, djathtas.
    public IEnumerable<(string, string, bool)> Successors(string s)
    {
        int z = s.IndexOf('0'), r = z / 3, c = z % 3;
        (string Name, int Dr, int Dc)[] moves = [("U", -1, 0), ("D", 1, 0), ("L", 0, -1), ("R", 0, 1)];
        foreach (var (name, dr, dc) in moves)
        {
            int nr = r + dr, nc = c + dc;
            if (nr is < 0 or > 2 || nc is < 0 or > 2) continue;   // veprimi nuk aplikohet
            var a = s.ToCharArray();
            (a[z], a[nr * 3 + nc]) = (a[nr * 3 + nc], a[z]);
            yield return (name, new string(a), true);
        }
    }

    public static bool IsValidInput(string s) =>
        s.Length == 9 && s.OrderBy(ch => ch).SequenceEqual("012345678");

    // Një gjendje e 8-Puzzle është e arritshme nga goal-i vetëm nëse numri i inversioneve është çift.
    public bool IsSolvable()
    {
        var t = start.Where(ch => ch != '0').ToArray();
        int inv = 0;
        for (int i = 0; i < t.Length; i++)
            for (int j = i + 1; j < t.Length; j++)
                if (t[i] > t[j]) inv++;
        return inv % 2 == 0;
    }

    public void Describe()
    {
        Console.WriteLine("Gjendja: grid 3×3 me pllakat 1..8 dhe vendin bosh '_' (shkruhet rresht pas rreshti: abc|def|ghi).");
        Console.WriteLine($"Gjendja fillestare: {Show(start)}    Goal: {Show(GoalState)}");
        Console.WriteLine("Veprimet: vendi bosh lëviz U (lart), D (poshtë), L (majtas), R (djathtas); kostoja e çdo veprimi = 1.");
        Console.WriteLine("Landscape: 9! = 362,880 rregullime, prej të cilave 9!/2 = 181,440 të arritshme; b ∈ {2, 3, 4}, mesatarisht ≈ 2.67.");
    }
}

// ---------------------------------------------------------------------------------------------
// 2) Missionaries and Cannibals
// ---------------------------------------------------------------------------------------------

// M, C = misionarët dhe kanibalët në bregun e majtë; BoatLeft = varka është në bregun e majtë.
readonly record struct McState(int M, int C, bool BoatLeft);

class MissionariesCannibals : IProblem<McState>
{
    const int N = 3, Capacity = 2;
    public string Name => "Missionaries and Cannibals";
    public string FileName => "missionaries_cannibals";
    public McState Initial => new(N, N, true);

    public bool IsGoal(McState s) => s is { M: 0, C: 0, BoatLeft: false };
    public string Key(McState s) => s.ToString();

    public string Show(McState s) =>
        $"{s.M}M{s.C}C {(s.BoatLeft ? "B~~~ " : " ~~~B")} {N - s.M}M{N - s.C}C";

    // Në asnjë breg misionarët (nëse ka) nuk duhet të jenë më pak se kanibalët.
    static bool Safe(McState s) =>
        (s.M == 0 || s.M >= s.C) && (N - s.M == 0 || N - s.M >= N - s.C);

    // Veprimet: varka kalon lumin me (m, c) persona, 1 ≤ m + c ≤ 2.
    public IEnumerable<(string, McState, bool)> Successors(McState s)
    {
        (int M, int C)[] loads = [(1, 0), (2, 0), (0, 1), (0, 2), (1, 1)];
        int dir = s.BoatLeft ? -1 : 1;
        foreach (var (m, c) in loads)
        {
            var next = new McState(s.M + dir * m, s.C + dir * c, !s.BoatLeft);
            if (next.M is < 0 or > N || next.C is < 0 or > N) continue;   // nuk ka aq persona në breg
            string action = $"{(m > 0 ? m + "M" : "")}{(c > 0 ? c + "C" : "")}{(s.BoatLeft ? "→" : "←")}";
            yield return (action, next, Safe(next));
        }
    }

    public void Describe()
    {
        Console.WriteLine("Gjendja: (M, C, B) = misionarët dhe kanibalët në bregun e majtë dhe pozita e varkës.");
        Console.WriteLine("         Shfaqet si 'bregu majtë  ~~~  bregu djathtë', B = varka.");
        Console.WriteLine($"Gjendja fillestare: {Show(Initial)}    Goal: {Show(new McState(0, 0, false))}");
        Console.WriteLine("Veprimet: varka (kapaciteti 2) kalon me 1M, 2M, 1C, 2C ose 1M1C; → majtas-djathtas, ← djathtas-majtas.");
        Console.WriteLine("Kufizimi: në asnjë breg misionarët nuk mund të jenë më pak se kanibalët (përveç kur janë 0).");
        Console.WriteLine("Landscape: 4·4·2 = 32 gjendje teorike; 20 prej tyre janë të sigurta, 16 të arritshme nga gjendja fillestare.");
    }
}

// ---------------------------------------------------------------------------------------------
// 3) Rotating Workforce Scheduling
// ---------------------------------------------------------------------------------------------

// Orari ciklik: n punëtorë, një matricë n × 7. Rreshti i javës i ndjek javën i−1, dhe pas javës së
// fundit vjen java e parë (secili punëtor e kalon gjithë ciklin me radhë). Gjendja është sekuenca
// e slot-eve të caktuara deri tani ('?' = ende e pacaktuar).
class RotatingWorkforce : IProblem<char[]>
{
    static readonly string[] Days = ["Hën", "Mar", "Mër", "Enj", "Pre", "Sht", "Die"];
    const string Values = "DN-";    // D = ndërrimi i ditës, N = ndërrimi i natës, - = pushim

    readonly int n = 3;                          // punëtorë = javë në cikël
    readonly int[] demandD = [1, 1, 1, 1, 1, 1, 1];  // sa punëtorë duhen në ndërrimin D çdo ditë
    readonly int[] demandN = [1, 1, 1, 1, 1, 1, 1];  // sa punëtorë duhen në ndërrimin N çdo ditë
    const int MinWork = 2, MaxWork = 5;          // gjatësia e bllokut të ditëve të punës
    const int MinOff = 1, MaxOff = 3;            // gjatësia e bllokut të pushimit
    const int MaxNights = 3;                     // netë rresht maksimalisht

    int L => 7 * n;
    public char[]? Solution { get; private set; }
    public string Name => "Rotating Workforce Scheduling";
    public string FileName => "rotating_workforce";
    public char[] Initial => Enumerable.Repeat('?', L).ToArray();

    static int Filled(char[] s) => Array.IndexOf(s, '?') is var i && i < 0 ? s.Length : i;

    public bool IsGoal(char[] s)
    {
        if (Filled(s) < L || !CyclicValid(s)) return false;
        Solution = s;
        return true;
    }

    public string Key(char[] s) => new(s);

    // Shfaqet java aktuale e mbushur deri tani.
    public string Show(char[] s)
    {
        int k = Filled(s);
        if (k == 0) return "orar bosh";
        int w = (k - 1) / 7;
        return $"J{w + 1}: {new string(s, w * 7, k - w * 7)}";
    }

    // Variabli i radhës është slot-i i parë i pacaktuar (java w, dita d); domain-i {D, N, -}.
    public IEnumerable<(string, char[], bool)> Successors(char[] s)
    {
        int t = Filled(s);
        if (t >= L) yield break;
        foreach (char v in Values)
        {
            var next = (char[])s.Clone();
            next[t] = v;
            yield return ($"J{t / 7 + 1}{Days[t % 7]}={v}", next, Consistent(next, t));
        }
    }

    static bool Work(char ch) => ch is 'D' or 'N';

    // Kontrollon kufizimet që mund të verifikohen pasi slot-i t merr vlerë.
    bool Consistent(char[] s, int t)
    {
        char v = s[t];
        int d = t % 7;

        // 1) Kërkesa ditore: në kolonën e ditës d nuk mund të ketë më shumë D/N se sa duhen.
        int cnt = 0;
        for (int i = d; i <= t; i += 7) if (s[i] == v) cnt++;
        int need = v switch { 'D' => demandD[d], 'N' => demandN[d], _ => n - demandD[d] - demandN[d] };
        if (cnt > need) return false;

        if (t > 0)
        {
            char prev = s[t - 1];
            // 2) Sekuencë e ndaluar: N → D (pa pushim pas natës)
            if (prev == 'N' && v == 'D') return false;
            // 3) Gjatësia minimale e bllokut që sapo mbaroi (blloku që fillon te slot-i 0
            //    mund të vazhdojë nga fundi i ciklit, prandaj kontrollohet në fund).
            if (Work(prev) != Work(v))
            {
                int len = RunLength(s, t - 1, Work(prev));
                if (len < t && len < (Work(prev) ? MinWork : MinOff)) return false;
            }
        }
        // 4) Gjatësia maksimale e bllokut aktual
        if (RunLength(s, t, Work(v)) > (Work(v) ? MaxWork : MaxOff)) return false;
        if (v == 'N' && NightRun(s, t) > MaxNights) return false;

        return t < L - 1 || CyclicValid(s);
    }

    static int RunLength(char[] s, int end, bool work)
    {
        int len = 0;
        for (int i = end; i >= 0 && Work(s[i]) == work; i--) len++;
        return len;
    }

    static int NightRun(char[] s, int end)
    {
        int len = 0;
        for (int i = end; i >= 0 && s[i] == 'N'; i--) len++;
        return len;
    }

    // Kontrolli i plotë ciklik i orarit (përfshirë kalimin nga java e fundit te java e parë).
    bool CyclicValid(char[] s)
    {
        int len = s.Length;
        for (int i = 0; i < len; i++)
            if (s[i] == 'N' && s[(i + 1) % len] == 'D') return false;

        // Gjej fillimin e një blloku, pastaj kontrollo çdo bllok rreth ciklit.
        int start = 0;
        while (start < len && Work(s[start]) == Work(s[(start - 1 + len) % len])) start++;
        if (start == len) return false;   // i gjithë cikli punë ose pushim
        for (int k = 0; k < len;)
        {
            bool w = Work(s[(start + k) % len]);
            int run = 0, nights = 0, maxNights = 0;
            while (k < len && Work(s[(start + k) % len]) == w)
            {
                nights = s[(start + k) % len] == 'N' ? nights + 1 : 0;
                maxNights = Math.Max(maxNights, nights);
                run++; k++;
            }
            if (run < (w ? MinWork : MinOff) || run > (w ? MaxWork : MaxOff) || maxNights > MaxNights) return false;
        }

        for (int d = 0; d < 7; d++)
        {
            int cd = 0, cn = 0;
            for (int i = d; i < len; i += 7) { if (s[i] == 'D') cd++; if (s[i] == 'N') cn++; }
            if (cd != demandD[d] || cn != demandN[d]) return false;
        }
        return true;
    }

    public void Describe()
    {
        Console.WriteLine($"Instanca: {n} punëtorë, cikël {n} javë = {L} ditë; ndërrimet D (ditë), N (natë), - (pushim).");
        Console.WriteLine($"Kërkesa çdo ditë: D = {demandD[0]}, N = {demandN[0]}, pushim = {n - demandD[0] - demandN[0]}.");
        Console.WriteLine($"Kufizimet: blloku i punës {MinWork}..{MaxWork} ditë, pushimi {MinOff}..{MaxOff} ditë, " +
                          $"maksimum {MaxNights} netë rresht, sekuenca N→D e ndaluar; orari është ciklik.");
        Console.WriteLine($"Variablat: x[j][d] për javën j ∈ 1..{n} dhe ditën d ∈ Hën..Die ({L} variabla), domain {{D, N, -}}.");
        Console.WriteLine($"Landscape: 3^{L} ≈ {Math.Pow(3, L):0.00e0} orare; me kërkesën ditore (një permutacion i D,N,- për çdo kolonë) 6^7 = {Math.Pow(6, 7):N0}.");
        Console.WriteLine("Thellësia e zgjidhjes d = " + L + ", b ≤ 3; fëmijët që shkelin kufizimet priten (backtracking).");
    }

    public void PrintSchedule(char[] s)
    {
        Console.WriteLine("Orari ciklik i gjetur (punëtori i fillon në javën i dhe rrotullohet çdo javë):");
        Console.WriteLine("       " + string.Join(" ", Days));
        for (int w = 0; w < n; w++)
            Console.WriteLine($"Java {w + 1}: " + string.Join(" ", Enumerable.Range(0, 7).Select(d => $" {s[w * 7 + d]} ")));
        Console.WriteLine();
    }
}
