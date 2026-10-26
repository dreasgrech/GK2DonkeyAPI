using Mono.Cecil;

// The public API of GK2 Donkey API (namespace GK2DonkeyAPI.Api), written as one line per type and member and compared with
// the committed snapshot GK2DonkeyAPI.Api.txt. Any difference fails, so an API change is always a reviewed diff:
// additions bump Mounts.ApiVersion's minor, changes or removals its major (docs/api-design.md).
// Usage: dotnet run -c Release            compare (build GK2DonkeyAPI in Release first)
//        dotnet run -c Release -- --update  write the snapshot from the current build
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")); // tests/X/bin/Release/net8.0 -> repo root
var dll = Path.Combine(repo, "src", "GK2DonkeyAPI", "bin", "Release", "netstandard2.1", "GK2DonkeyAPI.dll");
var snapshot = Path.Combine(repo, "tests", "ApiSurface", "GK2DonkeyAPI.Api.txt");
var mod = ModuleDefinition.ReadModule(dll);

// Besides the members, what a mod's compiled code leans on (docs/multi-mod-loading.md 5.3): sealed / abstract, the base
// type and the interfaces, public nested types, and which members are abstract or virtual. Adding a member to an
// interface or an abstract one to a class breaks every mod that implements or derives from it, though it reads as an
// addition: such lines are marked BREAKING in the diff.
var lines = new List<string>();
var types = new List<TypeDefinition>();
void Collect(TypeDefinition t)
{
    types.Add(t);
    foreach (var n in t.NestedTypes.Where(n => n.IsNestedPublic)) Collect(n);
}
foreach (var t in mod.Types.Where(t => t.IsPublic && t.Namespace == "GK2DonkeyAPI.Api")) Collect(t);
foreach (var t in types.OrderBy(t => t.FullName))
{
    string kind = t.IsEnum ? "enum" : t.IsValueType ? "struct" : t.IsInterface ? "interface" : (t.IsAbstract && t.IsSealed) ? "static class" : t.IsAbstract ? "abstract class" : t.IsSealed ? "sealed class" : "class";
    var bases = new List<string>();
    if (t.BaseType != null && !t.IsEnum && !t.IsValueType && t.BaseType.FullName != "System.Object") bases.Add(Name(t.BaseType));
    foreach (var i in t.Interfaces.OrderBy(i => i.InterfaceType.FullName)) bases.Add(Name(i.InterfaceType));
    lines.Add($"{kind} {t.FullName}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}");
    if (t.IsEnum)
    {
        foreach (var f in t.Fields.Where(f => f.IsStatic && f.IsPublic))
            lines.Add($"  {f.Name} = {f.Constant}");
        continue;
    }
    foreach (var f in t.Fields.Where(f => f.IsPublic).OrderBy(f => f.Name))
        lines.Add($"  field {(f.IsStatic ? "static " : "")}{(f.IsLiteral ? "const " : f.IsInitOnly ? "readonly " : "")}{Name(f.FieldType)} {f.Name}{(f.IsLiteral ? " = " + f.Constant : "")}");
    foreach (var p in t.Properties.Where(p => (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false)).OrderBy(p => p.Name))
        lines.Add($"  property {((p.GetMethod ?? p.SetMethod)!.IsStatic ? "static " : "")}{Over(t, (p.GetMethod ?? p.SetMethod)!)}{Name(p.PropertyType)} {p.Name} {{{(p.GetMethod?.IsPublic == true ? " get;" : "")}{(p.SetMethod?.IsPublic == true ? " set;" : "")} }}");
    foreach (var e in t.Events.Where(e => e.AddMethod?.IsPublic ?? false).OrderBy(e => e.Name))
        lines.Add($"  event {(e.AddMethod.IsStatic ? "static " : "")}{Name(e.EventType)} {e.Name}");
    foreach (var m in t.Methods.Where(m => m.IsPublic && !m.IsGetter && !m.IsSetter && !m.IsAddOn && !m.IsRemoveOn && m.Name != ".cctor")
                 .OrderBy(m => m.Name).ThenBy(m => m.Parameters.Count))
        lines.Add($"  method {(m.IsStatic ? "static " : "")}{Over(t, m)}{Name(m.ReturnType)} {m.Name}({string.Join(", ", m.Parameters.Select(p => Name(p.ParameterType) + " " + p.Name + (p.HasConstant ? " = " + (p.Constant ?? "null") : "")))})");
}

string text = string.Join("\n", lines) + "\n";
if (args.Contains("--update"))
{
    File.WriteAllText(snapshot, text);
    Console.WriteLine($"Snapshot written: {lines.Count} lines -> {snapshot}");
    return 0;
}
string old = File.Exists(snapshot) ? File.ReadAllText(snapshot).Replace("\r\n", "\n") : "";
if (old == text)
{
    Console.WriteLine($"PASS public API unchanged ({lines.Count} lines)");
    Console.WriteLine("ALL PASS");
    return 0;
}
var oldLines = old.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
var newLines = lines.ToHashSet();
foreach (var l in oldLines.Except(newLines)) Console.WriteLine("REMOVED  " + l);
// an addition a mod's own subclass or implementation can't have: a member of an interface, an abstract member
string owner = "";
var ownerOf = new Dictionary<string, string>();
foreach (var l in lines)
{
    if (!l.StartsWith(" ")) owner = l;
    else ownerOf[l + "\u0001" + owner] = owner;
}
foreach (var l in newLines.Except(oldLines))
{
    string typeLine = "";
    foreach (var kv in ownerOf) if (kv.Key.StartsWith(l + "\u0001")) { typeLine = kv.Value; break; }
    bool breaking = l.StartsWith("  ") && (typeLine.StartsWith("interface ") || l.Contains(" abstract "));
    Console.WriteLine((breaking ? "ADDED (BREAKING: every mod implementing or deriving from it) " : "ADDED    ") + l);
}
Console.WriteLine("FAIL the public API differs from tests/ApiSurface/GK2DonkeyAPI.Api.txt. Review, bump Mounts.ApiVersion, then run with --update.");
return 1;

// "abstract " or "virtual " for a member a mod may override; nothing for an interface's (all of them are).
static string Over(TypeDefinition t, MethodDefinition m)
{
    if (t.IsInterface || m.IsStatic) return "";
    if (m.IsAbstract) return "abstract ";
    return m.IsVirtual && !m.IsFinal && !(t.IsSealed) ? (m.IsNewSlot ? "virtual " : "override ") : "";
}

static string Name(TypeReference t)
{
    if (t is GenericInstanceType g)
        return Name(g.ElementType).Split('`')[0] + "<" + string.Join(", ", g.GenericArguments.Select(Name)) + ">";
    return t.FullName switch
    {
        "System.Void" => "void", "System.String" => "string", "System.Boolean" => "bool", "System.Int32" => "int",
        "System.Single" => "float", "System.Object" => "object",
        _ => t.FullName,
    };
}
