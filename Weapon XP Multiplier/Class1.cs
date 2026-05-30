using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;

namespace WeaponXpMultiplier
{
    [BepInPlugin("kumo.sulfur.weapon_xp_multiplier", "Weapon XP Multiplier", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<float> XpMultiplier;
        internal static ConfigEntry<bool> LogXpChanges;

        private Harmony harmony;

        private const string TargetTypeName =
            "PerfectRandom.Sulfur.Core.Weapons.Weapon";

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enable weapon XP multiplier."
            );

            XpMultiplier = Config.Bind(
                "General",
                "XpMultiplier",
                2.0f,
                new ConfigDescription(
                    "Weapon XP multiplier. 1.0 = vanilla, 2.0 = double XP, 10.0 = ten times XP.",
                    new AcceptableValueRange<float>(0f, 100f)
                )
            );

            LogXpChanges = Config.Bind(
                "Debug",
                "LogXpChanges",
                false,
                "Log weapon XP changes. Keep false for normal gameplay."
            );

            harmony = new Harmony("kumo.sulfur.weapon_xp_multiplier");

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
                    nameof(WeaponXpMultiplierPrefix),
                    BindingFlags.Static | BindingFlags.NonPublic
                )
            );

            harmony.Patch(addExperienceMethod, prefix: prefix);

            Logger.LogInfo("Weapon XP Multiplier loaded. Patched Weapon.AddExperience(float).");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private static void WeaponXpMultiplierPrefix(object __instance, ref float __0)
        {
            if (!EnableMod.Value)
                return;

            if (__0 <= 0f)
                return;

            float multiplier = XpMultiplier.Value;

            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                multiplier = 1f;

            multiplier = Math.Max(0f, Math.Min(100f, multiplier));

            if (Math.Abs(multiplier - 1f) < 0.0001f)
                return;

            float originalXp = __0;
            __0 *= multiplier;

            if (LogXpChanges.Value)
            {
                string weaponName = __instance != null ? __instance.ToString() : "Unknown Weapon";
                Log?.LogInfo("Weapon XP changed for " + weaponName + ": " + originalXp + " -> " + __0);
            }
        }
    }
}