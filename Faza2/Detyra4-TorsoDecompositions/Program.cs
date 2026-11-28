using System.Text;

// Torso Decompositions (ESA SpOC-3, GECCO 2024) me metaheuristika: front Pareto (madhësi e torsos ↑, gjerësi ↓).

string[] rest = args.Where(a => !a.StartsWith("--")).ToArray();
double seconds = DoubleArg("--sekonda", 10);
int seed = (int)DoubleArg("--seed", 1);
string path = rest.Length > 0 ? rest[0] : "instances/easy.gr";
if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, path);
if (!File.Exists(path))
{
    Console.WriteLine($"Skedari nuk u gjet: {rest.ElementAtOrDefault(0) ?? "instances/easy.gr"}");
    return;
}

var g = Graph.Load(path);
string name = Path.GetFileNameWithoutExtension(path);
Console.WriteLine($"=== Torso Decompositions: {name}, n = {g.N}, m = {g.M}, grada max = {g.Degree.Max()}, " +
                  $"grada mesatare = {2.0 * g.M / g.N:F1} ===");
Console.WriteLine();

// Vlerësimi i një vektori vendimi të dhënë: --vlereso "p1,p2,...,pn,t"
int vi = Array.IndexOf(args, "--vlereso");
if (vi >= 0 && vi + 1 < args.Length)
{
    var x = args[vi + 1].Split(',').Select(int.Parse).ToArray();
    var (size, width) = Verifier.Evaluate(g, x);
    Console.WriteLine($"Vektori: [{string.Join(",", x)}]");
    Console.WriteLine(size < 0 ? "Vektor i pavlefshëm (jo permutacion ose t jashtë kufijve)." :
                      $"Madhësia e torsos = {size}, gjerësia = {width}");
    return;
}

Console.WriteLine($"Buxheti kohor për çdo metaheuristikë: {seconds} s (seed = {seed})");
var results = new List<RunResult>();
void Report(RunResult r)
{
    results.Add(r);
    var front = r.Archive.ParetoFront();
    var top = r.Archive.SelectBest(20);
    Console.WriteLine($"{r.Name,-40} {r.Evaluations,9:N0} {r.Elapsed.TotalSeconds,7:F1}s " +
                      $"{front[0].W,11} {front.Count,7} {Front.Hypervolume(top, g.N),10:F5}");
}
Console.WriteLine($"{"Metoda",-40} {"Vlerësime",9} {"Koha",8} {"Gjerësia@t=0",11} {"Pareto",7} {"HV (20 pika)",10}");
var greedy = Metaheuristics.Greedy(g, seed);
Report(greedy);
Report(Metaheuristics.RandomSearch(g, seconds, seed));
var peeling = Metaheuristics.Peeling(g, seconds, seed);
Report(peeling);
var grasp = Metaheuristics.Grasp(g, seconds, seed);
Report(grasp);
var rnd = Enumerable.Range(0, g.N).ToArray();
new Random(seed).Shuffle(rnd);
Report(Metaheuristics.SimulatedAnnealing(g, rnd, seconds, seed, "SA (nga permutacion i rastësishëm)"));
Report(Metaheuristics.SimulatedAnnealing(g, greedy.BestPerm, seconds, seed, "SA (nga min-degree)"));
Report(Metaheuristics.SimulatedAnnealing(g, grasp.BestPerm, seconds, seed, "SA (nga GRASP)"));
Report(Metaheuristics.PrefixAnnealing(g, peeling.Archive, seconds, seed));
Console.WriteLine();
Console.WriteLine("Gjerësia@t=0 = gjerësia kur e gjithë permutacioni është torso; Pareto = numri i pikave jo të dominuara;");
Console.WriteLine("HV = hipervëllimi i normalizuar (referenca t = n, gjerësia = 501) i 20 pikave më të mira.");
Console.WriteLine();

// Arkivi i përbashkët dhe zgjedhja e 20 vektorëve që maksimizojnë HV.
var all = new Archive(g.N);
foreach (var r in results) all.Merge(r.Archive);
var chosen = all.SelectBest(20).OrderBy(p => p.T).ToList();
Console.WriteLine($"--- Fronti final (arkivi i të gjitha metodave), {chosen.Count} pika, " +
                  $"HV = {Front.Hypervolume(chosen, g.N):F5} ---");
Console.WriteLine($"{"t",6} {"Madhësia (n−t)",15} {"Gjerësia",9} {"Verifikuar",11}");
var vectors = new List<int[]>();
var cache = new Dictionary<int[], int[]>(ReferenceEqualityComparer.Instance);
bool allOk = true;
foreach (var (t, w) in chosen)
{
    var perm = all.PermOf[t]!;
    var x = perm.Append(t).ToArray();
    vectors.Add(x);
    // Verifikim i pavarur me matricë bool (pa bitset), një herë për çdo permutacion.
    if (!cache.TryGetValue(perm, out var od)) cache[perm] = od = Verifier.OutDegrees(g, perm);
    int vw = od.Skip(t).Max();
    allOk &= vw == w;
    Console.WriteLine($"{t,6} {g.N - t,15} {w,9} {(vw == w ? "po" : $"JO ({vw})"),11}");
}
Console.WriteLine($"Të gjitha gjerësitë përputhen me verifikuesin e pavarur: {(allOk ? "po" : "JO")}");

string problem = name switch { "easy" => "small-graph", "medium" => "medium-graph", "hard" => "large-graph", _ => name };
string outDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
Directory.CreateDirectory(outDir);
string outFile = Path.Combine(outDir, $"{name}_submission.json");
var sb = new StringBuilder();
sb.AppendLine("[");
sb.AppendLine("  {");
sb.AppendLine($"    \"decisionVector\": [\n{string.Join(",\n", vectors.Select(v => "      [" + string.Join(",", v) + "]"))}\n    ],");
sb.AppendLine($"    \"problem\": \"{problem}\",");
sb.AppendLine("    \"challenge\": \"spoc-3-torso-decompositions\"");
sb.AppendLine("  }");
sb.AppendLine("]");
File.WriteAllText(outFile, sb.ToString());
Console.WriteLine($"Zgjidhja në formatin e ESA Optimise u ruajt: output/{Path.GetFileName(outFile)}");

double DoubleArg(string key, double def)
{
    int i = Array.IndexOf(args, key);
    return i >= 0 && i + 1 < args.Length && double.TryParse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : def;
}

// Implementim i dytë, i thjeshtë, i lojës së eliminimit (matricë bool), vetëm për verifikim.
static class Verifier
{
    public static int[] OutDegrees(Graph g, int[] perm)
    {
        int n = g.N;
        var adj = new bool[n, n];
        for (int u = 0; u < n; u++)
            for (int v = 0; v < n; v++)
                adj[u, v] = (g.Adj[u * g.W + (v >> 6)] >> v & 1) != 0;
        var pos = new int[n];
        for (int i = 0; i < n; i++) pos[perm[i]] = i;
        var od = new int[n];
        var outs = new List<int>();
        for (int i = 0; i < n; i++)
        {
            int u = perm[i];
            outs.Clear();
            for (int v = 0; v < n; v++)
                if (adj[u, v] && pos[v] > i) outs.Add(v);
            od[i] = outs.Count;
            foreach (int a in outs)
                foreach (int b in outs)
                    if (a != b) adj[a, b] = true;
        }
        return od;
    }

    // Kthen (madhësia, gjerësia) ose (−1, −1) për vektor të pavlefshëm.
    public static (int Size, int Width) Evaluate(Graph g, int[] x)
    {
        int n = g.N;
        if (x.Length != n + 1) return (-1, -1);
        var perm = x[..n];
        int t = x[n];
        if (t < 0 || t >= n || perm.Distinct().Count() != n || perm.Any(p => p < 0 || p >= n)) return (-1, -1);
        return (n - t, OutDegrees(g, perm).Skip(t).Max());
    }
}
