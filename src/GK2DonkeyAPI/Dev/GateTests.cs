using System;
using System.Collections;
using GK2Shared;

namespace GK2DonkeyAPI
{
    // The start-up check of the game's code as it ran this session (GameCode; docs/dependency-check.md, "Scopes"): the
    // manifest's module-mvid line for the A* pathfinding read, the running assembly found by its name and counted, and
    // the verdict. The comparison itself is proven offline (DepManifest verify on a manifest with an altered hash).
    internal static class GateTests
    {
        private const string Pathfinding = "AstarPathfindingProject";

        internal static IEnumerator Scopes()
        {
            GameDependencies.Result r = GameCode.Result;
            if (r == null)
            {
                SelfTest.Skip("the start's check left no result");
                yield break;
            }
            bool listed = r.AuditedModuleMvids.TryGetValue(Pathfinding, out Guid audited);
            bool looked = r.ModuleMvids.TryGetValue(Pathfinding, out Guid now);
            SelfTest.Check(listed && looked, $"the manifest names the pathfinding's module id and the start looked it up (built against {(listed ? audited.ToString() : "none")}, running {(looked ? now.ToString() : "none")})");
            SelfTest.Check(looked && now != Guid.Empty, "the running pathfinding assembly was found by its name");
            SelfTest.Check(r.State == GameDependencies.State.Identical, $"the verdict: {r.State}{(r.Recompiled ? " (recompiled, " + (r.FromCache ? "from the cache" : "compared in " + r.Milliseconds + " ms") + ")" : " (the build the API was made against)")}; {GameDependencies.Builds(r)}");
        }
    }
}
