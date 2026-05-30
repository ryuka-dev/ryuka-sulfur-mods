using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace RyukaLabs.Sulfur.LuckTweaks
{
    [BepInPlugin("ryukalabs.sulfur.lucktweaks", "Luck Tweaks", "1.0.1")]
    public class LuckTweaksPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<float> RecoveryMultiplier;
        internal static ConfigEntry<float> RecoveryFlatBonus;
        internal static ConfigEntry<float> RecoveryOverridePerMinute;
        internal static ConfigEntry<float> ConsumptionMultiplier;
        internal static ConfigEntry<bool> VerboseLogging;

        internal const int StatLuckGain = 58;
        internal const int StatusLuck = 94;

        internal static bool IsTweakingLuckStatus;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            RecoveryMultiplier = Config.Bind(
                "Luck Recovery",
                "RecoveryMultiplier",
                1f,
                "Multiplier for positive Status_Luck changes. 1 = vanilla. 2 = double luck recovery."
            );

            RecoveryFlatBonus = Config.Bind(
                "Luck Recovery",
                "RecoveryFlatBonus",
                0f,
                "Flat bonus added to every positive Status_Luck recovery tick after multiplier."
            );

            RecoveryOverridePerMinute = Config.Bind(
                "Luck Recovery",
                "RecoveryOverridePerMinute",
                -1f,
                "If >= 0, replaces each positive Status_Luck recovery tick with this value. -1 = disabled."
            );

            ConsumptionMultiplier = Config.Bind(
                "Luck Consumption",
                "ConsumptionMultiplier",
                1f,
                "Multiplier for negative Status_Luck changes. 1 = vanilla. 0.5 = half cost. 0 = no luck consumption."
            );

            VerboseLogging = Config.Bind(
                "Debug",
                "VerboseLogging",
                true,
                "Print Status_Luck changes to BepInEx log."
            );

            _harmony = new Harmony("ryukalabs.sulfur.lucktweaks");
            _harmony.PatchAll();

            Log.LogInfo("Luck Tweaks loaded.");
            Log.LogInfo("Watching Status_Luck = 94. Stat_LuckGain = 58.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        internal static bool IsStatusLuckArg(object arg)
        {
            int? id = TryExtractAttributeId(arg);
            return id.HasValue && id.Value == StatusLuck;
        }

        private static int? TryExtractAttributeId(object arg)
        {
            if (arg == null)
                return null;

            Type type = arg.GetType();

            if (type.IsEnum)
            {
                try
                {
                    return Convert.ToInt32(arg);
                }
                catch
                {
                    return null;
                }
            }

            if (arg is int i)
                return i;

            if (arg is long l)
                return (int)l;

            string text = arg.ToString();
            if (text == "Status_Luck")
                return StatusLuck;

            if (text == "Stat_LuckGain")
                return StatLuckGain;

            object idValue = GetMember(arg, "id");
            if (idValue != null && !ReferenceEquals(idValue, arg))
            {
                int? nested = TryExtractAttributeId(idValue);
                if (nested.HasValue)
                    return nested.Value;
            }

            object valueValue = GetMember(arg, "value");
            if (valueValue != null && !ReferenceEquals(valueValue, arg))
            {
                int? nested = TryExtractAttributeId(valueValue);
                if (nested.HasValue)
                    return nested.Value;
            }

            try
            {
                return Convert.ToInt32(arg);
            }
            catch
            {
                return null;
            }
        }

        internal static object GetMember(object obj, string name)
        {
            if (obj == null)
                return null;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            Type type = obj.GetType();

            while (type != null)
            {
                PropertyInfo prop = type.GetProperty(name, flags);
                if (prop != null)
                {
                    try
                    {
                        return prop.GetValue(obj, null);
                    }
                    catch
                    {
                        return null;
                    }
                }

                FieldInfo field = type.GetField(name, flags);
                if (field != null)
                {
                    try
                    {
                        return field.GetValue(obj);
                    }
                    catch
                    {
                        return null;
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        internal static Type FindTypeByFullName(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = asm.GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }
    }

    [HarmonyPatch]
    internal static class EntityStats_ModifyStatus_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type type = LuckTweaksPlugin.FindTypeByFullName("PerfectRandom.Sulfur.Core.Stats.EntityStats");
            if (type == null)
                yield break;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (method.Name != "ModifyStatus")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length >= 2 && parameters[1].ParameterType == typeof(float))
                    yield return method;
            }
        }

        private static void Prefix(object __0, ref float __1, out bool __state)
        {
            __state = false;

            if (!LuckTweaksPlugin.IsStatusLuckArg(__0))
                return;

            if (LuckTweaksPlugin.IsTweakingLuckStatus)
                return;

            LuckTweaksPlugin.IsTweakingLuckStatus = true;
            __state = true;

            float original = __1;
            float modified = original;

            if (original > 0f)
            {
                float overrideValue = LuckTweaksPlugin.RecoveryOverridePerMinute.Value;

                if (overrideValue >= 0f)
                {
                    modified = overrideValue;
                }
                else
                {
                    modified =
                        original * LuckTweaksPlugin.RecoveryMultiplier.Value
                        + LuckTweaksPlugin.RecoveryFlatBonus.Value;
                }

                if (modified < 0f)
                    modified = 0f;
            }
            else if (original < 0f)
            {
                float consumptionMultiplier = LuckTweaksPlugin.ConsumptionMultiplier.Value;

                if (consumptionMultiplier < 0f)
                    consumptionMultiplier = 0f;

                modified = original * consumptionMultiplier;
            }

            __1 = modified;

            if (LuckTweaksPlugin.VerboseLogging.Value)
            {
                LuckTweaksPlugin.Log.LogInfo(
                    $"[LuckTweaks] Status_Luck delta changed: {original:0.###} -> {modified:0.###}"
                );
            }
        }

        private static void Postfix(bool __state)
        {
            if (__state)
                LuckTweaksPlugin.IsTweakingLuckStatus = false;
        }
    }
}