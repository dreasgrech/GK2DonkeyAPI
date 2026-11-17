using System.Collections.Generic;
using GK2ModTestBench;

namespace GK2DonkeyAPI.IntegrationTests
{
    // The scenarios of the API and of the mods built on it, handed to GK2 Mod Test Bench (a developer's plugin with a
    // repository of its own: one runner and one panel, F4, for every GK2 mod's in-game tests; since 2026-11-17 the
    // API has no runner of its own). The API knows nothing of the bench; this plugin knows both:
    // - every scenario of SelfTest.Scenarios (the API's own and, added before this is called, the integration
    //   tests') becomes a test of one suite, "GK2 Donkey API", in the list's order, which is the order they have
    //   always run in. The checks of the whole run ("After the run") are the suite's closing checks, the world's
    //   check the very last.
    // - the API's own steps around a run become the suite's hooks: StartRun before the first scenario (it may refuse
    //   the run), BeforeScenario and AfterScenario around each, FinishRun after the last; HoldRun and LetRunGo are the
    //   suite's hold, so what a run holds comes back also when the bench ends a run from outside.
    // - a check made by a scenario (SelfTest.Check and the rest) goes to the bench through the sinks given here.
    internal static class Bench
    {
        internal const string SuiteId = "dreas.gk2.donkeyapi";

        private static readonly Dictionary<TestCase, SelfTest.Scenario> scenarioOf = new Dictionary<TestCase, SelfTest.Scenario>();

        // How many scenarios the bench has now.
        internal static int Register()
        {
            SelfTest.PassSink = GK2ModTestBench.Test.Pass;
            SelfTest.FailSink = GK2ModTestBench.Test.Fail;
            SelfTest.WarnSink = GK2ModTestBench.Test.Warn;
            SelfTest.InfoSink = GK2ModTestBench.Test.Info;
            SelfTest.SkipSink = GK2ModTestBench.Test.Skip;
            SelfTest.AbortSource = () => GK2ModTestBench.Test.Aborted;

            TestSuite suite = TestBench.Suite(SuiteId, "GK2 Donkey API");
            suite.AddHold("the API's run (the flags its code reads, the settings a run holds)", SelfTest.HoldRun, SelfTest.LetRunGo);
            suite.BeforeRun = run => SelfTest.StartRun(run.Refuse);
            suite.BeforeEach = test =>
            {
                SelfTest.BeforeScenario(scenarioOf[test]);
                return null;
            };
            suite.AfterEach = test => SelfTest.AfterScenario(scenarioOf[test]);
            // clean: nothing failed and nothing warned in the suite's part of the run (a spot is remembered only then)
            suite.AfterRun = run => SelfTest.FinishRun(run.Full, run.Failed == 0 && run.Warned == 0, run.Aborted);

            foreach (SelfTest.Scenario s in SelfTest.Scenarios)
            {
                TestCase test = s.Group != SelfTest.GroupAfter ? suite.Add(s.Group, s.Name, s.Body)
                    : s.Name == SelfTest.AfterRunName ? suite.AddVeryLast(s.Name, s.Body)
                    : suite.AddLast(s.Name, s.Body);
                test.HidesBench = s.HidesPanel; // a measurement: the bench's panel out of the frame too
                scenarioOf[test] = s;
            }
            return scenarioOf.Count;
        }
    }
}
