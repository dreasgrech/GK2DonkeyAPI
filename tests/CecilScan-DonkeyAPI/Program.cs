using Mono.Cecil; using Mono.Cecil.Cil;
var managed = GamePaths.Managed;
var res = new DefaultAssemblyResolver(); res.AddSearchDirectory(managed); res.AddSearchDirectory(GamePaths.BepInExCore);
var rp = new ReaderParameters { AssemblyResolver = res };
var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")); // tests/X/bin/Release/net8.0 -> repo root
var mod = ModuleDefinition.ReadModule(Path.Combine(repo, "src", "GK2DonkeyAPI", "bin", "Release", "netstandard2.1", "GK2DonkeyAPI.dll"), rp);
var game = ModuleDefinition.ReadModule(managed + "Assembly-CSharp.dll", rp);
Console.WriteLine("Game MVID " + game.Mvid);
int fails = 0, ok = 0;
var gameScopes = new HashSet<string> { "Assembly-CSharp", "LazyBearTechnology", "FlowCanvas" };
var seen = new HashSet<string>();
foreach (var t in mod.GetTypes()) foreach (var m in t.Methods.Where(m => m.HasBody)) foreach (var ins in m.Body.Instructions) {
  if (ins.Operand is MemberReference mr && !(mr is TypeReference) && mr.DeclaringType != null && !(mr.DeclaringType is ArrayType) && gameScopes.Contains(mr.DeclaringType.Scope.Name)) {
    if (!seen.Add(mr.FullName)) continue;
    IMemberDefinition? d = null;
    try { d = mr is MethodReference meth ? meth.Resolve() : mr is FieldReference f ? f.Resolve() : null; } catch {}
    if (d == null) { Console.WriteLine("FAIL unresolved: " + mr.FullName); fails++; } else ok++;
  }
}
Console.WriteLine($"IL references into game assemblies: {ok} resolved, {fails} unresolved");
// Harmony targets named by string ([HarmonyPatch(typeof(T), "name")] and friends): the method must be DECLARED on
// that type, not inherited (Harmony's AccessTools.DeclaredMethod), or patching fails at start and the mod disables
// itself (found 2026-10-06: ChestInteractionHandler inherits HasInteraction2). Classes using TargetMethod(s) are skipped.
{ int patches = 0;
  foreach (var t in mod.GetTypes()) {
    var attrs = t.CustomAttributes.Where(a => a.AttributeType.Name == "HarmonyPatch").ToList();
    if (attrs.Count == 0) continue;
    TypeReference? target = null; string? name = null; int methodType = 0;
    foreach (var a in attrs) foreach (var arg in a.ConstructorArguments) {
      if (arg.Value is TypeReference tr) target = tr;
      else if (arg.Value is string str) name = str;
      else if (arg.Type.Name == "MethodType" && arg.Value is int mt) methodType = mt;
    }
    if (target == null || name == null) continue; // TargetMethod(s): resolved at run time, skipped by Harmony when null
    patches++;
    string want = methodType == 1 ? "get_" + name : methodType == 2 ? "set_" + name : name;
    TypeDefinition? td = null; try { td = target.Resolve(); } catch {}
    bool good = td != null && td.Methods.Any(m => m.Name == want);
    Console.WriteLine((good?"PASS":"FAIL") + $" patch target {target.FullName}.{want} declared ({t.Name})"); if (!good) fails++;
  }
  Console.WriteLine($"Harmony patch targets named by string: {patches} checked"); }
void Field(string type, string name, string ftype, bool isStatic=false) { var td = game.GetType(type); var f = td?.Fields.FirstOrDefault(x => x.Name == name);
  bool good = f != null && f.FieldType.FullName == ftype && f.IsStatic == isStatic; Console.WriteLine((good?"PASS":"FAIL") + $" field {type}.{name} : {f?.FieldType.FullName} static={f?.IsStatic}"); if (!good) fails++; }
// Overload-aware: the member exists with `pars` parameters among `overloads` overloads (the first parameter's type when given).
void MethodOf(string type, string name, int pars, int overloads, string? firstParam = null) { var td = game.GetType(type); var ms = td?.Methods.Where(x => x.Name == name).ToList();
  var m = ms?.FirstOrDefault(x => x.Parameters.Count == pars && (firstParam == null || x.Parameters[0].ParameterType.FullName == firstParam));
  bool good = ms != null && ms.Count == overloads && m != null && m.HasBody; Console.WriteLine((good?"PASS":"FAIL") + $" method {type}.{name} overloads={ms?.Count} params={pars} found={m != null}"); if (!good) fails++; }
void Method(string type, string name, int pars) { var td = game.GetType(type); var ms = td?.Methods.Where(x => x.Name == name).ToList();
  bool good = ms != null && ms.Count == 1 && ms[0].Parameters.Count == pars && ms[0].HasBody; Console.WriteLine((good?"PASS":"FAIL") + $" method {type}.{name} overloads={ms?.Count} params={ms?.FirstOrDefault()?.Parameters.Count}"); if (!good) fails++; }
Method("WgoDataScriptsManager", "CreateScript", 2);
// Corpses at the crossroads (Corpses.cs): patches applied on demand (no [HarmonyPatch] attribute), so their targets
// and the private ports the prefixes take are named here.
MethodOf("GK2.FlowCanvasNodes.Flow_TryToAddCorpseToPallet", "TryToAddCorpseToPallet", 1, 1, "FlowCanvas.Flow");
Field("GK2.FlowCanvasNodes.Flow_TryToAddCorpseToPallet", "item", "FlowCanvas.ValueInput`1<Item>");
Field("GK2.FlowCanvasNodes.Flow_TryToAddCorpseToPallet", "yes", "FlowCanvas.FlowOutput");
MethodOf("GK2.FlowCanvasNodes.Flow_DropItem", "Drop", 1, 1, "FlowCanvas.Flow");
Field("GK2.FlowCanvasNodes.Flow_DropItem", "out", "FlowCanvas.FlowOutput");
Field("GK2.FlowCanvasNodes.Flow_DropItem", "item", "FlowCanvas.ValueInput`1<Item>");
Field("GK2.FlowCanvasNodes.Flow_DropItem", "gdPointDataDropOn", "FlowCanvas.ValueInput`1<GDPointData>");
Field("GK2.FlowCanvasNodes.Flow_DropItem", "dropBody", "System.Boolean");
MethodOf("GK2.FlowCanvasNodes.Flow_GoTo", "GoTo", 1, 1, "FlowCanvas.Flow");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "out", "FlowCanvas.FlowOutput");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "onFinish", "FlowCanvas.FlowOutput");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "fireEventOnFinish", "FlowCanvas.ValueInput`1<System.String>");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "gdPointData", "FlowCanvas.ValueInput`1<GDPointData>");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "gdPointId", "FlowCanvas.ValueInput`1<System.String>");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "useGdDataInsteadId", "System.Boolean");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "navigation", "FlowCanvas.ValueInput`1<MovementType>");
Field("GK2.FlowCanvasNodes.Flow_GoTo", "speed", "FlowCanvas.ValueInput`1<System.Single>");
MethodOf("UINotificator", "ShowNewBodyNotification", 1, 2, "System.String"); // the parameterless one forwards to it
Method("UINotificator", "ShowSimpleTextWithIconNotification", 2);
Method("GKCustomFlowNodeWithWgoData", "GetWgoData", 0);
Field("WgoDataScript", "wgoData", "WgoData");
Method("GdPointsData", "GetGDPointDataById", 1);
Method("WgoData", "AddDelayedEvent", 2);
Method("WgoData", "FireEvent", 1);
Method("QuestSystemData", "IsQuestInStatus", 2);
MethodOf("WorldData", "GetWgoData", 1, 2, "System.String");
Method("WorldData", "GetWgoDataByCustomTag", 1);
Method("WorldData", "GetWgoDataListByGroup", 1);
MethodOf("MovementComponent", "StartPath", 9, 7, "UnityEngine.Vector3"); // the node's own overload, GDGraph
Method("MovementComponent", "TryFinishAlreadyAtDestination", 3); // the premise for LeaveHere: a walk to where it stands ends with onFinish only
Method("Wgo", "Spawn", 6);
// 1.008 added UpdateChunkVisibility(bool, bool); the mod calls the (bool) one.
{ var td = game.GetType("Wgo"); var m = td?.Methods.FirstOrDefault(x => x.Name == "UpdateChunkVisibility" && x.Parameters.Count == 1 && x.Parameters[0].ParameterType.FullName == "System.Boolean"); bool good = m != null && m.HasBody; Console.WriteLine((good?"PASS":"FAIL") + " method Wgo.UpdateChunkVisibility(bool) found=" + (m != null)); if (!good) fails++; }
Method("Wgo", "SetInteractableCollidersState", 1);
Method("Wgo", "SetLayerToAllColliders", 1);
Method("WgoData", "SetStateToAnimator", 1);
Method("AnimationComponentBase", "SetWalkAnimationSpeedMultiplier", 1);
{ var td = game.GetType("AnimationComponentBase"); var m = td?.Methods.FirstOrDefault(x => x.Name == "SetDirection" && x.Parameters.Count == 1 && x.Parameters[0].ParameterType.Name == "Vector2"); bool good = m != null && m.HasBody && !m.IsVirtual; Console.WriteLine((good?"PASS":"FAIL") + " method AnimationComponentBase.SetDirection(Vector2) found=" + (m != null)); if (!good) fails++; }
Method("GK2.FlowCanvasNodes.Flow_Body", "DoAction", 1);
Field("GK2.FlowCanvasNodes.Flow_Body", "success", "FlowCanvas.FlowOutput");
// Cart cargo investigation (CarryProbe.cs): the delivery script's own inventory path and the drawer that reacts to it.
Field("WGODef", "inventorySize", "System.Int32");
Field("ItemDef", "itemSize", "ItemSize");
Field("ItemDef", "iconId", "System.String");
Field("GameBalance", "itemDefs", "System.Collections.Generic.List`1<ItemDef>");
Method("Inventory", "RemoveItemById", 5);
Method("PlayerData", "TryAddOverheadItemNoReplace", 1);
Method("ConditionalDrawer", "UpdateVisualsByConditions", 0);
// Cargo proof of concept (Cargo.cs, SaveSystem_Save_Patch)
Method("SaveSystem", "Save", 6);
// Persistence (Persistence.cs, SaveSystem_Remove_Patch): side file per slot, cargo back by item GUID
Method("SaveSystem", "Remove", 2);
Method("SaveSystem", "get_SaveFolder", 0);
Method("MainGame", "get_SaveSlotData", 0);
Field("SaveSlotData", "slotName", "System.String");
Field("SaveSlotData", "saveDateTime", "System.String");
Field("SaveSlotData", "day", "System.Int32");
Method("Item", "get_UniqueId", 0);
Method("SGuid", "get_Id", 0);
Field("GameSceneData", "droppedItems", "System.Collections.Generic.List`1<DropData>");
Field("GameSceneData", "queuedDrops", "System.Collections.Generic.List`1<DropData>");
Method("DropSystem", "RemoveDrop", 2);
Method("WorldData", "GetGameSceneDataById", 1);
Field("MainGame", "OnGameStarted", "System.Action", true);
Method("DropData", "get_Position", 0);
Method("Inventory", "RemoveItemFromInventoryByUID", 2);
Method("SpecialPhysicsCastUtils", "GetPlayerDropPosition", 3);
Method("DropSystem", "DropItem", 4);
Method("DropViewAtomMesh", "Activate", 1);
Method("PlayerData", "get_HasFreeOverheadSlot", 0);
Method("PlayerData", "TryGetOverheadItem", 2);
Method("PlayerData", "AddOverheadItem", 1);
// Setting a cart item down with the interact press (PlayerInputHandler_UpdateInput_Patch).
Method("PlayerInputHandler", "UpdateInput", 0);
Method("PlayerInputHandler", "TryCallFireEvent", 1);
Field("PlayerInputHandler", "playerController", "PlayerController");
Field("PlayerInputHandler", "curInteractionHandler", "IWGOInteractionHandler");
Method("PlayerInteractionComponent", "get_BigDropUnderInteraction", 0);
Method("PlayerInteractionComponent", "get_WgoUnderInteraction", 0);
Method("DropView", "get_InteractionHandler", 0);
Method("Wgo", "get_InteractionHandler", 0);
{ var it = game.GetType("PlayerInputHandler")?.NestedTypes.FirstOrDefault(n => n.Name == "InteractionType"); var f = it?.Fields.FirstOrDefault(x => x.Name == "Interaction1"); bool good = f != null && System.Convert.ToInt32(f.Constant) == 0; Console.WriteLine((good?"PASS":"FAIL") + " enum PlayerInputHandler.InteractionType.Interaction1 == 0"); if (!good) fails++; }
{ var td = game.GetType("PlayerData"); var ms = td?.Methods.Where(x => x.Name == "RemoveOverheadItem").ToList(); bool good = ms != null && ms.Count == 2 && ms.Any(m => m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "Item" && m.HasBody); Console.WriteLine((good?"PASS":"FAIL") + $" method PlayerData.RemoveOverheadItem overloads={ms?.Count} (need 0 and Item)"); if (!good) fails++; }
Method("LazyExpressionEvaluationScope", "GetOverheadItemForMutation", 0);
Method("Inventory", "Create", 5);
{ var td = game.GetType("Inventory"); var ms = td?.Methods.Where(x => x.Name == "AddItemToInventory").ToList(); bool good = ms != null && ms.Count == 2 && ms.Any(m => m.Parameters.Count == 3 && m.HasBody); Console.WriteLine((good?"PASS":"FAIL") + $" method Inventory.AddItemToInventory overloads={ms?.Count} (need the 3-param one)"); if (!good) fails++; }
Method("Item", "set_Count", 1);
Method("FlowScriptAssetLoadPolicy", "Load", 1);
Method("FlowScriptAssetLoadPolicy", "ReleaseGraph", 1);
Field("QuestCheck", "triggerType", "GlobalEventsSystem/Event/Type");
Field("QuestCheck", "condExpressions", "System.Collections.Generic.List`1<LazyExpression>");
Field("GameBalance", "questDefs", "System.Collections.Generic.List`1<QuestDef>");
Field("WGODef", "attachedScript", "System.String");
// Round 11 (save-safety audit): definition fields the mount refuses on, the teleport control flag, the sheet abort.
Field("WGODef", "townQuality", "System.Int32");
Field("WGODef", "npcLifeSimGroup", "System.String");
Field("WGODef", "startItems", "System.Collections.Generic.List`1<NeedItemData>");
Method("PlayerController", "get_IsControlsEnabled", 0);
Method("Inventory", "Clear", 0);
Method("PlayerData", "AddOverheadItem", 1);
Method("WeatherSystem", "get_WindValue", 0);
Method("WeatherSystem", "get_Instance", 0);
Field("GlobalShaderParameters", "idSunLight", "System.Int32", true);
Method("PlayerData", "PlayOverheadItemTakeSound", 1);
Method("PlayerData", "RemoveInteractingItem", 0);
Method("PlayerData", "get_HasInteractingItem", 0);
Method("GlobalEventsSystem", "FireTrigger", 2);
Method("PlayerData", "get_OverheadStackLimit", 0);
Method("PlayerData", "get_OverheadCount", 0);
{ var td = game.GetType("PlayerData"); var ms = td?.Methods.Where(x => x.Name == "DropOverheadItem").ToList(); bool good = ms != null && ms.Count == 2 && ms.Any(m => m.Parameters.Count == 0 && m.HasBody); Console.WriteLine((good?"PASS":"FAIL") + $" method PlayerData.DropOverheadItem overloads={ms?.Count} (need 0 and Item)"); if (!good) fails++; }
{ var td = game.GetType("PlayerData"); var ms = td?.Methods.Where(x => x.Name == "InsertOverheadItemTo").ToList(); bool good = ms != null && ms.Count == 2 && ms.Any(m => m.Parameters.Count == 2 && m.HasBody); Console.WriteLine((good?"PASS":"FAIL") + $" method PlayerData.InsertOverheadItemTo overloads={ms?.Count} (need WgoData and WgoData,Item)"); if (!good) fails++; }
// Inventory.CanAddItemToInventory and PlayerData.InsertOverheadItemTo have two overloads each; the IL scan above covers them.
{ var pa = game.GetType("PlayerAnimation"); bool good = pa?.BaseType?.Name == "AnimationComponent" && game.GetType("AnimationComponent")?.BaseType?.Name == "AnimationComponentBase"; Console.WriteLine((good?"PASS":"FAIL") + " PlayerAnimation : AnimationComponent : AnimationComponentBase"); if (!good) fails++; }
// Round 12 (sounds): the donkey's surface probe, the keeper's step event and the animation-event PlaySound, patched
// on AnimationComponentBase; neither may be overridden below it or the patch would miss the keeper/donkey.
{ var td = game.GetType("IOrderedSurface"); bool good = td != null && td.IsInterface && td.Methods.Any(m => m.Name == "get_SurfaceType") && td.Methods.Any(m => m.Name == "get_Depth"); Console.WriteLine((good?"PASS":"FAIL") + " interface IOrderedSurface.SurfaceType/Depth"); if (!good) fails++; }
Method("AnimationComponentBase", "OnAnimationStepCompleted", 0);
Method("AnimationComponentBase", "PlaySound", 1);
Method("SurfaceStepSoundSettings", "GetStepSoundIdForSurfaceType", 1);
{ bool good = !game.Types.Any(t => t.Name != "AnimationComponentBase" && t.Methods.Any(m => (m.Name == "OnAnimationStepCompleted" || m.Name == "PlaySound") && m.IsVirtual && !m.IsNewSlot && IsSubOf(t, "AnimationComponentBase")));
  Console.WriteLine((good?"PASS":"FAIL") + " no override of AnimationComponentBase.OnAnimationStepCompleted/PlaySound"); if (!good) fails++; }
bool IsSubOf(TypeDefinition t, string baseName) { var b = t.BaseType; while (b != null) { if (b.Name == baseName) return true; try { b = b.Resolve()?.BaseType; } catch { return false; } } return false; }
// The dependency manifest (docs/dependency-check.md): every game member the DLL uses, found in the installed game
// unchanged; and every class of the DLL that uses game members sorted in game-code.txt (GameCode decides per class
// what a changed member means; a class listed nowhere counts as core, so a new one gets sorted on purpose).
{
  string? Res(string name) { var r = mod.Resources.OfType<EmbeddedResource>().FirstOrDefault(x => x.Name == name); return r == null ? null : System.Text.Encoding.UTF8.GetString(r.GetResourceData()); }
  string? deps = Res("gk2deps.txt"), table = Res("GK2DonkeyAPI.game-code.txt");
  if (deps == null || table == null) { Console.WriteLine("FAIL resources: gk2deps.txt " + (deps != null) + ", game-code.txt " + (table != null)); fails++; }
  else
  {
    var owners = new SortedSet<string>(); int entries = 0; var changed = new List<string>(); var missing = new List<string>();
    foreach (var raw in deps.Split('\n'))
    {
      var line = raw.TrimEnd('\r'); if (line.Length < 2 || line[1] != ' ') continue;
      entries++;
      int tab = line.IndexOf('\t');
      if (tab >= 0) foreach (var o in line.Substring(tab + 1).Split(',')) owners.Add(o.Substring(o.LastIndexOf('.') + 1));
    }
    var listed = new HashSet<string>();
    foreach (var raw in table.Split('\n'))
    {
      var line = raw.Trim(); if (line.Length == 0 || line[0] == '#') continue;
      int sp = line.IndexOf(' '); if (sp < 0) continue;
      var rest = line.Substring(sp + 1); int colon = rest.IndexOf(':'); if (colon >= 0) rest = rest.Substring(colon + 1);
      foreach (var n in rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)) listed.Add(n);
    }
    var unsorted = owners.Where(o => !listed.Contains(o)).ToList();
    Console.WriteLine((unsorted.Count == 0 ? "PASS" : "FAIL") + $" game-code.txt sorts every class that uses game members ({owners.Count} classes, {entries} manifest entries)" + (unsorted.Count == 0 ? "" : ": unsorted " + string.Join(", ", unsorted)));
    if (unsorted.Count > 0) fails++;
    var stale = listed.Where(l => !owners.Contains(l)).ToList();
    Console.WriteLine((stale.Count == 0 ? "PASS" : "FAIL") + " game-code.txt names only classes that use game members" + (stale.Count == 0 ? "" : ": " + string.Join(", ", stale)));
    if (stale.Count > 0) fails++;
    var lbt = ModuleDefinition.ReadModule(managed + "LazyBearTechnology.dll", rp);
    var manifest = GK2Shared.DepManifest.Parse(deps);
    var verdict = GK2Shared.DepCompare.Compare(manifest, scope => scope == "Assembly-CSharp" ? game : scope == "LazyBearTechnology" ? lbt : null!);
    Console.WriteLine((verdict.Identical ? "PASS" : "FAIL") + $" dependency manifest: {verdict.Checked} game members (made against game {manifest.GameMvid}{(manifest.GameMvid == game.Mvid ? "" : ", the installed game is a different build")}): {verdict.Kind}");
    foreach (var s in verdict.Missing) Console.WriteLine("  MISSING " + s);
    foreach (var s in verdict.Changed) Console.WriteLine("  CHANGED " + s);
    if (!verdict.Identical) fails++;
  }
}
Console.WriteLine(fails == 0 ? "ALL PASS" : "FAILURES: " + fails);
return fails;
