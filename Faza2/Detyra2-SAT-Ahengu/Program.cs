using System.Diagnostics;
using System.Text;

// Ahengu me 100 mysafirë në 10 tavolina: kodimi në SAT (CNF) dhe zgjidhja me CDCL.

if (args.Length > 0 && args[0] == "--gjenero")
{
    Generator.Run(args);
    return;
}

string[] rest = args.Where(a => !a.StartsWith("--")).ToArray();
bool symmetry = !args.Contains("--pa-simetri");
string path = rest.Length > 0 ? rest[0] : "instances/aheng100.txt";
if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory, path);
if (!File.Exists(path))
{
    Console.WriteLine($"Skedari nuk u gjet: {rest.ElementAtOrDefault(0) ?? "instances/aheng100.txt"}");
    return;
}

Party party;
try { party = Party.Parse(File.ReadAllText(path)); }
catch (FormatException e) { Console.WriteLine($"Gabim në hyrje: {e.Message}"); return; }

int men = party.IsMale.Count(m => m);
Console.WriteLine($"=== SAT: Ahengu, {Path.GetFileName(path)} ===");
Console.WriteLine($"Mysafirë: {party.Guests} ({men} meshkuj, {party.Guests - men} femra), " +
                  $"tavolina: {party.Tables} × {party.Seats} karrige, |M − F| ≤ {party.MaxDiff} në çdo tavolinë");
Console.WriteLine($"Çifte që NUK mund të ulen bashkë: {party.Apart.Count}, " +
                  $"çifte që DUHET të ulen bashkë: {party.Together.Count}");
Console.WriteLine();

if (party.Guests != party.Tables * party.Seats)
{
    Console.WriteLine("Numri i mysafirëve duhet të jetë i barabartë me numrin e karrigeve (tavolina × karrige).");
    return;
}

var cnf = PartyEncoder.Encode(party, symmetry);
Console.WriteLine("--- Formula CNF ---");
Console.WriteLine($"Variabla: {cnf.NumVars:N0} ({party.Guests * party.Tables:N0} x(m,t) + " +
                  $"{cnf.NumVars - party.Guests * party.Tables:N0} ndihmëse të sequential counter)");
Console.WriteLine($"Klauzola: {cnf.Clauses.Count:N0}");
foreach (var (group, count) in cnf.Groups)
    Console.WriteLine($"  {group,-52} {count,8:N0}");
Console.WriteLine();

string outDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
Directory.CreateDirectory(outDir);
string cnfPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + ".cnf");
cnf.WriteDimacs(cnfPath, party);
Console.WriteLine($"Formula u ruajt në formatin DIMACS: output/{Path.GetFileName(cnfPath)}");
Console.WriteLine();

var sw = Stopwatch.StartNew();
var solver = new CdclSolver(cnf.NumVars, cnf.Clauses);
bool[]? model = solver.Solve();
sw.Stop();
Console.WriteLine($"--- CDCL solver: {(model is null ? "UNSAT" : "SAT")} për {sw.Elapsed.TotalMilliseconds:F0} ms ---");
Console.WriteLine($"Vendime: {solver.Decisions:N0}, konflikte: {solver.Conflicts:N0}, " +
                  $"propagime: {solver.Propagations:N0}, klauzola të mësuara: {solver.LearntClauses:N0}");
Console.WriteLine();

if (model is null)
{
    Console.WriteLine("Formula është e paplotësueshme: nuk ekziston asnjë ulje që i plotëson të gjitha kushtet.");
    return;
}

// Dekodimi: mysafiri m ulet në tavolinën t ⇔ x(m,t) = true.
var table = new int[party.Guests];
for (int g = 0; g < party.Guests; g++)
    table[g] = Enumerable.Range(0, party.Tables).Where(t => model[PartyEncoder.X(party, g, t)]).DefaultIfEmpty(-1).First();

Console.WriteLine("Ulja e mysafirëve:");
for (int t = 0; t < party.Tables; t++)
{
    var at = Enumerable.Range(0, party.Guests).Where(g => table[g] == t).ToList();
    int m = at.Count(g => party.IsMale[g]);
    Console.WriteLine($"  Tavolina {t + 1,2} ({m}M/{at.Count - m}F): " +
                      string.Join(" ", at.Select(g => $"{party.Names[g]}{(party.IsMale[g] ? "♂" : "♀")}")));
}
Console.WriteLine();

// Verifikimi i pavarur i çdo kushti mbi zgjidhjen e dekoduar (jo mbi formulën).
var errors = party.Verify(table);
Console.WriteLine("Verifikimi:");
var checks = new (string Label, string Prefix)[]
{
    ("çdo mysafir në saktësisht një tavolinë", "ulje"),
    ($"çdo tavolinë me saktësisht {party.Seats} mysafirë", "kapacitet"),
    ($"|M − F| ≤ {party.MaxDiff} në çdo tavolinë", "gjini"),
    ("çiftet 'ndaras' në tavolina të ndryshme", "ndaras"),
    ("çiftet 'bashke' në të njëjtën tavolinë", "bashke"),
};
foreach (var (label, prefix) in checks)
    Console.WriteLine($"  {label,-42} {(errors.Any(e => e.StartsWith(prefix)) ? "JO" : "po")}");
foreach (var e in errors.Take(10)) Console.WriteLine($"  ! {e}");
Console.WriteLine($"Zgjidhje valide: {(errors.Count == 0 ? "po" : "JO")}");

sealed class Party
{
    public int Guests, Tables = 10, Seats = 10, MaxDiff = 3;
    public List<string> Names = [];
    public List<bool> IsMale = [];
    public List<(int A, int B)> Apart = [], Together = [];

    // Formati:  tavolina 10 / karrige 10 / diferenca 3 / gjinia M1 m|f / ndaras M1 M5 / bashke M2 M7
    public static Party Parse(string text)
    {
        var p = new Party();
        var index = new Dictionary<string, int>();
        var pending = new List<(string Kind, string A, string B, int Line)>();
        int lineNo = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var w = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            switch (w[0])
            {
                case "tavolina": p.Tables = int.Parse(w[1]); break;
                case "karrige": p.Seats = int.Parse(w[1]); break;
                case "diferenca": p.MaxDiff = int.Parse(w[1]); break;
                case "gjinia" when w.Length == 3 && (w[2] == "m" || w[2] == "f"):
                    if (index.ContainsKey(w[1])) throw new FormatException($"rreshti {lineNo}: {w[1]} është përsëritur");
                    index[w[1]] = p.Names.Count;
                    p.Names.Add(w[1]);
                    p.IsMale.Add(w[2] == "m");
                    break;
                case "ndaras" or "bashke" when w.Length == 3:
                    pending.Add((w[0], w[1], w[2], lineNo));
                    break;
                default: throw new FormatException($"rreshti {lineNo}: '{line}'");
            }
        }
        foreach (var (kind, a, b, ln) in pending)
        {
            if (!index.TryGetValue(a, out int ia) || !index.TryGetValue(b, out int ib))
                throw new FormatException($"rreshti {ln}: mysafir i panjohur");
            (kind == "ndaras" ? p.Apart : p.Together).Add((ia, ib));
        }
        p.Guests = p.Names.Count;
        return p;
    }

    public List<string> Verify(int[] table)
    {
        var errors = new List<string>();
        for (int g = 0; g < Guests; g++)
            if (table[g] < 0) errors.Add($"ulje: {Names[g]} nuk ka tavolinë");
        for (int t = 0; t < Tables; t++)
        {
            int n = 0, m = 0;
            for (int g = 0; g < Guests; g++)
                if (table[g] == t) { n++; if (IsMale[g]) m++; }
            if (n != Seats) errors.Add($"kapacitet: tavolina {t + 1} ka {n} mysafirë");
            if (Math.Abs(m - (n - m)) > MaxDiff) errors.Add($"gjini: tavolina {t + 1} ka {m}M/{n - m}F");
        }
        foreach (var (a, b) in Apart)
            if (table[a] == table[b]) errors.Add($"ndaras: {Names[a]} dhe {Names[b]} janë bashkë");
        foreach (var (a, b) in Together)
            if (table[a] != table[b]) errors.Add($"bashke: {Names[a]} dhe {Names[b]} janë ndaras");
        return errors;
    }
}

sealed class Cnf
{
    public int NumVars;
    public readonly List<int[]> Clauses = [];
    public readonly List<(string Group, int Count)> Groups = [];
    int groupStart;
    string? group;

    public int NewVar() => ++NumVars;
    public void Add(params int[] lits) => Clauses.Add(lits);

    public void BeginGroup(string name)
    {
        EndGroup();
        group = name;
        groupStart = Clauses.Count;
    }

    public void EndGroup()
    {
        if (group is not null) Groups.Add((group, Clauses.Count - groupStart));
        group = null;
    }

    // Σ xs ≤ k me sequential counter (Sinz, 2005): s(i,j) ⇔ "të paktën j nga x1..xi janë true".
    // O(n·k) klauzola dhe variabla ndihmëse, në vend të C(n, k+1) klauzolave të kodimit naiv.
    public void AtMost(IReadOnlyList<int> xs, int k)
    {
        int n = xs.Count;
        if (k >= n) return;
        if (k == 0)
        {
            foreach (int x in xs) Add(-x);
            return;
        }
        var s = new int[n - 1, k];
        for (int i = 0; i < n - 1; i++)
            for (int j = 0; j < k; j++) s[i, j] = NewVar();

        Add(-xs[0], s[0, 0]);
        for (int j = 1; j < k; j++) Add(-s[0, j]);
        for (int i = 1; i < n - 1; i++)
        {
            Add(-xs[i], s[i, 0]);
            Add(-s[i - 1, 0], s[i, 0]);
            for (int j = 1; j < k; j++)
            {
                Add(-xs[i], -s[i - 1, j - 1], s[i, j]);
                Add(-s[i - 1, j], s[i, j]);
            }
            Add(-xs[i], -s[i - 1, k - 1]);
        }
        Add(-xs[n - 1], -s[n - 2, k - 1]);
    }

    public void WriteDimacs(string file, Party party)
    {
        var sb = new StringBuilder();
        sb.AppendLine("c Ahengu: x(m,t) = mysafiri m ulet në tavolinën t");
        sb.AppendLine($"c variabla x(m,t) = (m-1)*{party.Tables} + t, për m = 1..{party.Guests}, t = 1..{party.Tables}");
        sb.AppendLine($"c variablat > {party.Guests * party.Tables} janë ndihmëse të sequential counter");
        sb.AppendLine($"p cnf {NumVars} {Clauses.Count}");
        foreach (var c in Clauses) sb.Append(string.Join(' ', c)).AppendLine(" 0");
        File.WriteAllText(file, sb.ToString());
    }
}

static class PartyEncoder
{
    public static int X(Party p, int guest, int table) => guest * p.Tables + table + 1;

    public static Cnf Encode(Party p, bool symmetry)
    {
        var cnf = new Cnf { NumVars = p.Guests * p.Tables };
        int G = p.Guests, T = p.Tables;

        cnf.BeginGroup("(1) çdo mysafir në të paktën një tavolinë");
        for (int g = 0; g < G; g++)
            cnf.Add(Enumerable.Range(0, T).Select(t => X(p, g, t)).ToArray());

        cnf.BeginGroup("(2) çdo mysafir në më së shumti një tavolinë");
        for (int g = 0; g < G; g++)
            for (int t = 0; t < T; t++)
                for (int u = t + 1; u < T; u++)
                    cnf.Add(-X(p, g, t), -X(p, g, u));

        cnf.BeginGroup($"(3) çdo tavolinë me më së shumti {p.Seats} mysafirë");
        for (int t = 0; t < T; t++)
            cnf.AtMost(Enumerable.Range(0, G).Select(g => X(p, g, t)).ToList(), p.Seats);

        // Tavolina është plot (M + F = S), prandaj |M − F| ≤ D ⇔ M ≤ ⌊(S+D)/2⌋ dhe F ≤ ⌊(S+D)/2⌋.
        int maxSame = (p.Seats + p.MaxDiff) / 2;
        cnf.BeginGroup($"(4) gjinia: ≤ {maxSame} meshkuj dhe ≤ {maxSame} femra për tavolinë");
        for (int t = 0; t < T; t++)
        {
            cnf.AtMost(Enumerable.Range(0, G).Where(g => p.IsMale[g]).Select(g => X(p, g, t)).ToList(), maxSame);
            cnf.AtMost(Enumerable.Range(0, G).Where(g => !p.IsMale[g]).Select(g => X(p, g, t)).ToList(), maxSame);
        }

        cnf.BeginGroup("(5) çiftet që nuk mund të ulen bashkë");
        foreach (var (a, b) in p.Apart)
            for (int t = 0; t < T; t++)
                cnf.Add(-X(p, a, t), -X(p, b, t));

        cnf.BeginGroup("(6) çiftet që duhet të ulen bashkë");
        foreach (var (a, b) in p.Together)
            for (int t = 0; t < T; t++)
            {
                cnf.Add(-X(p, a, t), X(p, b, t));
                cnf.Add(-X(p, b, t), X(p, a, t));
            }

        if (symmetry)
        {
            // Tavolinat janë të njëjta: i rinumërojmë sipas mysafirit me indeksin më të vogël në secilën.
            // Atëherë mysafiri m (nga 0) nuk mund të jetë në tavolinë t > m.
            cnf.BeginGroup("(7) thyerja e simetrisë: x(m,t) = false për t > m");
            for (int g = 0; g < Math.Min(G, T); g++)
                for (int t = g + 1; t < T; t++)
                    cnf.Add(-X(p, g, t));
        }
        cnf.EndGroup();
        return cnf;
    }
}

// Gjeneron instanca të zgjidhshme nga një ulje e fshehur ("planted solution").
static class Generator
{
    // --gjenero <seed> <ndaras> <bashke> <skedari>
    public static void Run(string[] args)
    {
        int seed = int.Parse(args[1]), apart = int.Parse(args[2]), together = int.Parse(args[3]);
        string file = args[4];
        const int G = 100, T = 10, S = 10, D = 3;
        var rng = new Random(seed);

        // Ndarja e meshkujve: çdo tavolinë merr 4..6 meshkuj.
        int men = rng.Next(44, 57);
        var menAt = Enumerable.Repeat(4, T).ToArray();
        for (int left = men - 4 * T; left > 0;)
        {
            int t = rng.Next(T);
            if (menAt[t] < 6) { menAt[t]++; left--; }
        }
        var seats = new List<(bool Male, int Table)>();
        for (int t = 0; t < T; t++)
            for (int i = 0; i < S; i++) seats.Add((i < menAt[t], t));
        var guests = seats.OrderBy(_ => rng.Next()).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine($"# Instancë e gjeneruar (seed {seed}): {G} mysafirë, {T} tavolina, {apart} ndaras, {together} bashke");
        sb.AppendLine($"tavolina {T}\nkarrige {S}\ndiferenca {D}");
        for (int g = 0; g < G; g++) sb.AppendLine($"gjinia M{g + 1} {(guests[g].Male ? "m" : "f")}");

        var used = new HashSet<(int, int)>();
        (int, int) Pair(bool sameTable)
        {
            while (true)
            {
                int a = rng.Next(G), b = rng.Next(G);
                if (a == b || (guests[a].Table == guests[b].Table) != sameTable) continue;
                var key = (Math.Min(a, b), Math.Max(a, b));
                if (used.Add(key)) return key;
            }
        }
        for (int i = 0; i < apart; i++) { var (a, b) = Pair(false); sb.AppendLine($"ndaras M{a + 1} M{b + 1}"); }
        for (int i = 0; i < together; i++) { var (a, b) = Pair(true); sb.AppendLine($"bashke M{a + 1} M{b + 1}"); }
        File.WriteAllText(file, sb.ToString());
        Console.WriteLine($"U krijua {file}: {men} meshkuj, {G - men} femra");
    }
}
