// IL size per type of a DLL (method body bytes, Cecil), grouped by a prefix list given on the command line, plus the
// DLL's embedded resources. Written for docs/ui-library.md (2026-11-13); any "how big is this part" question.
//   dotnet run -c Release --project tools/IlSize -- <dll> <TypeName|TypeName...> [<group2>]
using System;
using System.Collections.Generic;
using Mono.Cecil;

class Program
{
    static int Main(string[] args)
    {
        var asm = AssemblyDefinition.ReadAssembly(args[0]);
        long total = 0;
        var perType = new Dictionary<string, long>();
        foreach (TypeDefinition t in asm.MainModule.Types) Walk(t, perType, ref total);
        long res = 0;
        foreach (Resource r in asm.MainModule.Resources) if (r is EmbeddedResource e) res += e.GetResourceData().Length;
        Console.WriteLine($"{args[0]}: {total:N0} bytes of IL in {perType.Count} types; {res:N0} bytes of embedded resources");
        for (int g = 1; g < args.Length; g++)
        {
            long sum = 0;
            var names = args[g].Split('|');
            foreach (var kv in perType)
                foreach (var n in names)
                    if (kv.Key == n || kv.Key.StartsWith(n + "/") || kv.Key.StartsWith(n + "`")) { sum += kv.Value; break; }
            Console.WriteLine($"  group {g}: {sum:N0} bytes IL ({args[g]})");
        }
        var list = new List<KeyValuePair<string, long>>(perType);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        Console.WriteLine("  largest types:");
        for (int i = 0; i < Math.Min(25, list.Count); i++) Console.WriteLine($"    {list[i].Value,8:N0}  {list[i].Key}");
        return 0;
    }

    static void Walk(TypeDefinition t, Dictionary<string, long> per, ref long total)
    {
        long s = 0;
        foreach (MethodDefinition m in t.Methods) if (m.HasBody) s += m.Body.CodeSize;
        string key = t.FullName.Replace("GK2DonkeyAPI.", "");
        per[key] = s;
        total += s;
        foreach (TypeDefinition n in t.NestedTypes) Walk(n, per, ref total);
    }
}
