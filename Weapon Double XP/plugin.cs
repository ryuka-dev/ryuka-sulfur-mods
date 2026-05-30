using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;

namespace WeaponDoubleXp
{
    [BepInPlugin("kumo.sulfur.weapon_double_xp", "Weapon Double XP", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private Harmony harmony;

        private const string TargetTypeName =
            "PerfectRandom.Sulfur.Core.Weapons.Weapon";

        private void Awake()
        {
            Log = Logger;

            harmony = new Harmony("kumo.sulfur.weapon_double_xp");

            Type weaponType = AccessTools.TypeByName(TargetTypeName);

            if (weaponType == null)
            {
                Logger.LogError("Could not find type: " + TargetTypeName);
                return;
            }

            MethodInfo addExperienceMethod = AccessTools.Method(
                weaponType,
                "AddExperience",
                new Type[] { typeof(float) }
            );

            if (addExperienceMethod == null)
            {
                Logger.LogError("Could not find Weapon.AddExperience(float).");
                return;
            }

            HarmonyMethod prefix = new HarmonyMethod(
                typeof(Plugin).GetMethod(
                    nameof(DoubleWeaponXpPrefix),
                    BindingFlags.Static | BindingFlags.NonPublic
                )
            );

            harmony.Patch(addExperienceMethod, prefix: prefix);

            Logger.LogInfo("Weapon Double XP loaded. Patched Weapon.AddExperience(float).");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private static void DoubleWeaponXpPrefix(ref float __0)
        {
            if (__0 <= 0f)
                return;

            __0 *= 2f;
        }
    }
}