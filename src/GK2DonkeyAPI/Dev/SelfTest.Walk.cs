using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using GK2DonkeyAPI.Api;

namespace GK2DonkeyAPI
{
    // The keeper's stick answered by a test (LazyInput.GetDirection, patched only for the run that drives it): a survey
    // steers a ridden mount right and left with it (the integration tests' walk and garbage surveys).
    internal static partial class SelfTest
    {
        internal static Vector2? surveyStick;
        internal static Harmony surveyHarmony;

        internal static void SurveyStickPostfix(ref Vector2 __result)
        {
            if (surveyStick.HasValue) __result = surveyStick.Value;
        }

    }
}
