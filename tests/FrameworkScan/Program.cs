using Mono.Cecil;
using Mono.Cecil.Cil;

// The optional GK2 Mod Framework integration, checked in every mod that has it (docs/gk2-mod-framework.md).
// One DLL must load and run without GK2.Framework.dll, so for each mod:
//  1. every [HarmonyPatch] class is in Plugin.PatchClasses (patched by class; PatchAll(assembly) would list the
//     framework-typed class and make Harmony warn when the framework is absent);
//  2. Plugin has no method Unity calls by name (a helper named Start once ran twice and patched everything twice);
//  3. only FrameworkLink's entry points (Register; RegisterApi, which lists GK2 Donkey API itself in the Mods menu)
//     and the classes nested in FrameworkLink touch GK2.Framework: base types, interfaces, attributes, fields,
//     signatures, locals and every IL operand are checked;
//  4. the entry points are NoInlining (so a caller's JIT never loads the framework), and Plugin has a soft
//     BepInDependency on the framework.
// Usage: dotnet run -c Release [ModName ...]   (default: every mod below; each must be built in Release first)
string[] integrated = { "GK2DonkeyAPI" }; // it links player mods' settings pages to the framework (Saddle Up has no link of its own); the four small mods are checked in GK2QOLMods
string[] mods = args.Length > 0 ? args : integrated;

var res = new DefaultAssemblyResolver();
res.AddSearchDirectory(GamePaths.Managed);
res.AddSearchDirectory(GamePaths.BepInExCore);
var rp = new ReaderParameters { AssemblyResolver = res };
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")); // tests/X/bin/Release/net8.0 -> repo root

int fails = 0;
foreach (string name in mods)
{
    Console.WriteLine($"=== {name}");
    var mod = ModuleDefinition.ReadModule(Path.Combine(repo, "src", name, "bin", "Release", "netstandard2.1", name + ".dll"), rp);
    fails += Check(mod, name);
}
Console.WriteLine(fails == 0 ? "ALL PASS" : "FAILURES: " + fails);
return fails;

static int Check(ModuleDefinition mod, string ns)
{
    int fails = 0;
    void Result(bool good, string what) { Console.WriteLine((good ? "PASS " : "FAIL ") + what); if (!good) fails++; }

    var plugin = mod.GetType(ns + ".Plugin");
    var link = mod.GetType(ns + ".FrameworkLink");
    Result(plugin != null, "Plugin class found");
    Result(link != null, "FrameworkLink class found");
    if (plugin == null || link == null)
    {
        return fails;
    }

    // 1. patch list
    var cctor = plugin.Methods.FirstOrDefault(m => m.Name == ".cctor");
    var listed = cctor == null ? new HashSet<string>() : cctor.Body.Instructions
        .Where(i => i.OpCode == OpCodes.Ldtoken && i.Operand is TypeReference)
        .Select(i => ((TypeReference)i.Operand).FullName).ToHashSet();
    var patchClasses = mod.GetTypes().Where(t => t.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")).ToList();
    // A mod without [HarmonyPatch] classes has nothing to list; the PatchAll(Assembly) check below still applies.
    Console.WriteLine($"INFO {patchClasses.Count} [HarmonyPatch] classes found");
    foreach (var t in patchClasses)
    {
        Result(listed.Contains(t.FullName), $"patch class {t.Name} in Plugin.PatchClasses");
    }
    bool patchAllAssembly = plugin.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
        .Any(i => i.Operand is MethodReference r && r.Name == "PatchAll" && r.Parameters.Count == 1 && r.Parameters[0].ParameterType.FullName == "System.Reflection.Assembly");
    Result(!patchAllAssembly, "Plugin never calls PatchAll(Assembly)");

    // 2. Unity messages
    // GK2 Donkey API runs its frame in LateUpdate on purpose (after the game's own Update).
    foreach (var m in new[] { "Start", "OnEnable", "OnDisable", "FixedUpdate", "LateUpdate", "OnApplicationQuit" })
    {
        if (m == "LateUpdate" && ns == "GK2DonkeyAPI") continue;
        if (m == "FixedUpdate" && ns == "GK2DonkeyAPI") continue; // the free flight and the jump move the keeper's body on the physics step (Flight.FixedStep), on purpose
        if (plugin.Methods.Any(x => x.Name == m))
        {
            Result(false, $"Plugin has a method named {m} (Unity would call it)");
        }
    }
    Result(true, "Plugin has no unintended Unity message methods (Start, OnEnable, OnDisable, ...)");

    // 3. framework isolation
    string[] entryPoints = { "Register", "RegisterApi" };
    int registerUses = 0;
    foreach (var t in mod.GetTypes())
    {
        if (IsNestedIn(t, link))
        {
            continue; // the framework-facing mod class (and its compiler-generated closures)
        }
        void Bad(string what) => Result(false, $"{t.FullName} uses GK2.Framework: {what}");
        if (Fw(t.BaseType)) Bad("base type");
        foreach (var i in t.Interfaces) if (Fw(i.InterfaceType)) Bad("interface");
        foreach (var a in t.CustomAttributes) if (Fw(a.AttributeType)) Bad("attribute");
        foreach (var f in t.Fields) if (Fw(f.FieldType)) Bad("field " + f.Name);
        foreach (var m in t.Methods)
        {
            bool uses = Fw(m.ReturnType) || m.Parameters.Any(p => Fw(p.ParameterType))
                || (m.HasBody && (m.Body.Variables.Any(v => Fw(v.VariableType)) || m.Body.Instructions.Any(i => FwOperand(i.Operand))));
            if (!uses) continue;
            if (t == link && entryPoints.Contains(m.Name)) registerUses++;
            else Bad("method " + m.Name);
        }
    }
    Result(registerUses >= 1, "GK2.Framework is used only by FrameworkLink's entry points (" + string.Join(", ", entryPoints) + ") and its nested classes");

    // 4. inlining and load order
    var register = link.Methods.FirstOrDefault(m => m.Name == "Register");
    Result(register != null, "FrameworkLink.Register exists");
    foreach (var m in link.Methods.Where(m => entryPoints.Contains(m.Name)))
    {
        Result(m.ImplAttributes.HasFlag(MethodImplAttributes.NoInlining), $"FrameworkLink.{m.Name} is NoInlining");
    }
    Result(plugin.CustomAttributes.Any(a => a.AttributeType.Name == "BepInDependency"
            && (string)a.ConstructorArguments[0].Value == "ru.superman4eg.gk2.framework"
            && Convert.ToInt32(a.ConstructorArguments[1].Value) == 2),
        "soft BepInDependency on ru.superman4eg.gk2.framework");

    // 5. no framework member looked up by name: a later framework version may add an overload, and the lookup then
    // throws (typeof(Gk2Settings).GetMethod("AddEnum") was AmbiguousMatchException with framework 0.1.19, the page
    // "Faulted" in the Mods menu; a player's report, 2026-10-19). Calls the compiler binds keep their exact signature.
    string[] byName = { "GetMethod", "GetMethods", "GetProperty", "GetField", "GetMember", "GetEvent" };
    int lookups = 0;
    foreach (var t in mod.GetTypes())
    {
        foreach (var m in t.Methods.Where(m => m.HasBody))
        {
            TypeReference? lastToken = null;
            foreach (var i in m.Body.Instructions)
            {
                if (i.OpCode == OpCodes.Ldtoken && i.Operand is TypeReference tok) lastToken = tok;
                if (i.Operand is MethodReference r && r.DeclaringType.FullName == "System.Type" && byName.Contains(r.Name) && lastToken != null && Fw(lastToken))
                {
                    lookups++;
                    Result(false, $"{t.FullName}.{m.Name} looks up a member of {lastToken.FullName} by name ({r.Name})");
                }
            }
        }
    }
    Result(lookups == 0, "no GK2.Framework member is looked up by name (reflection on its types)");
    return fails;
}

static bool IsNestedIn(TypeDefinition t, TypeDefinition outer)
{
    for (var d = t.DeclaringType; d != null; d = d.DeclaringType)
    {
        if (d == outer) return true;
    }
    return false;
}

static bool Fw(TypeReference? r)
{
    if (r == null) return false;
    if (r is GenericInstanceType g && g.GenericArguments.Any(Fw)) return true;
    if (r is TypeSpecification ts) return Fw(ts.ElementType);
    return r.Scope?.Name == "GK2.Framework";
}

static bool FwOperand(object? o) => o switch
{
    TypeReference tr => Fw(tr),
    MethodReference mr => Fw(mr.DeclaringType) || Fw(mr.ReturnType) || mr.Parameters.Any(p => Fw(p.ParameterType))
        || (mr is GenericInstanceMethod gm && gm.GenericArguments.Any(Fw)),
    FieldReference fr => Fw(fr.DeclaringType) || Fw(fr.FieldType),
    _ => false,
};
