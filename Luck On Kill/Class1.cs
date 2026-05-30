using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace LuckOnKill
{
    [BepInPlugin("kumo.sulfur.luck_on_kill", "Luck On Kill", "1.0.2")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<float> RewardMultiplier;
        internal static ConfigEntry<float> RewardFlatBonus;
        internal static ConfigEntry<float> RewardOverride;
        internal static ConfigEntry<bool> ApplyThroughModifyStatus;

        internal static ConfigEntry<bool> RequireHostileToPlayer;
        internal static ConfigEntry<bool> RewardOnlyExperienceUnits;
        internal static ConfigEntry<bool> RewardCivilians;
        internal static ConfigEntry<bool> RewardBreakables;
        internal static ConfigEntry<bool> PreventDuplicateRewards;

        internal static ConfigEntry<bool> LogRewards;

        private Harmony harmony;

        private const string UnitTypeName =
            "PerfectRandom.Sulfur.Core.Units.Unit";

        private const string GameManagerTypeName =
            "PerfectRandom.Sulfur.Core.World.GameManager";

        private const string EntityAttributesTypeName =
            "PerfectRandom.Sulfur.Core.Stats.EntityAttributes";

        private const int StatLuckGain = 58;
        private const int StatusLuck = 94;

        private static readonly HashSet<int> rewardedUnitIds = new HashSet<int>();

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enable Luck On Kill."
            );

            RewardMultiplier = Config.Bind(
                "Reward",
                "RewardMultiplier",
                1.0f,
                "Multiplier for the Luck gained on enemy death. Base reward is current Stat_LuckGain, which equals one minute of vanilla Luck recovery."
            );

            RewardFlatBonus = Config.Bind(
                "Reward",
                "RewardFlatBonus",
                0.0f,
                "Flat bonus added to the kill Luck reward after multiplier."
            );

            RewardOverride = Config.Bind(
                "Reward",
                "RewardOverride",
                -1.0f,
                "If >= 0, replaces the kill Luck reward with this fixed value. -1 = disabled."
            );

            ApplyThroughModifyStatus = Config.Bind(
                "Compatibility",
                "ApplyThroughModifyStatus",
                true,
                "If true, applies Luck through ModifyStatus. This allows Better Luck Control to affect kill rewards too."
            );

            RequireHostileToPlayer = Config.Bind(
                "Filter",
                "RequireHostileToPlayer",
                true,
                "If true, only units hostile to the player grant Luck."
            );

            RewardOnlyExperienceUnits = Config.Bind(
                "Filter",
                "RewardOnlyExperienceUnits",
                false,
                "If true, only units with ExperienceOnKill > 0 grant Luck. Default false allows all hostile enemies, including special 0 XP enemies."
            );

            RewardCivilians = Config.Bind(
                "Filter",
                "RewardCivilians",
                false,
                "If true, civilian units can grant Luck."
            );

            RewardBreakables = Config.Bind(
                "Filter",
                "RewardBreakables",
                false,
                "If true, Breakable units can grant Luck. Recommended false."
            );

            PreventDuplicateRewards = Config.Bind(
                "Safety",
                "PreventDuplicateRewards",
                true,
                "Prevents the same unit death from granting Luck more than once. Object-pool reuse is handled by clearing this state on Unit.Spawn()."
            );

            LogRewards = Config.Bind(
                "Debug",
                "LogRewards",
                true,
                "Log Luck rewards from enemy deaths."
            );

            harmony = new Harmony("kumo.sulfur.luck_on_kill");
            harmony.PatchAll();

            Logger.LogInfo("Luck On Kill loaded. Patched Unit.Die().");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        internal static void ResetRewardState(object unit)
        {
            if (unit == null)
                return;

            int id = GetObjectId(unit);
            rewardedUnitIds.Remove(id);
        }

        internal static bool ShouldRewardBeforeDeath(object unit)
        {
            if (!EnableMod.Value)
                return false;

            if (unit == null)
                return false;

            if (IsDead(unit))
                return false;

            if (IsPlayerUnit(unit))
                return false;

            if (!RewardBreakables.Value && IsBreakableUnit(unit))
                return false;

            if (!RewardCivilians.Value && IsCivilianUnit(unit))
                return false;

            if (RequireHostileToPlayer.Value)
            {
                bool? hostile = IsHostileToPlayer(unit);

                if (hostile.HasValue)
                {
                    if (!hostile.Value)
                        return false;
                }
                else
                {
                    int experienceOnKillFallback = GetIntMember(unit, "ExperienceOnKill", 0);
                    if (experienceOnKillFallback <= 0)
                        return false;
                }
            }

            if (RewardOnlyExperienceUnits.Value)
            {
                int experienceOnKill = GetIntMember(unit, "ExperienceOnKill", 0);

                if (experienceOnKill <= 0)
                    return false;
            }

            return true;
        }

        internal static void GrantLuckForDeath(object unit)
        {
            if (unit == null)
                return;

            int unitId = GetObjectId(unit);

            if (PreventDuplicateRewards.Value)
            {
                if (rewardedUnitIds.Contains(unitId))
                    return;

                rewardedUnitIds.Add(unitId);

                if (rewardedUnitIds.Count > 10000)
                    rewardedUnitIds.Clear();
            }

            object playerStats = GetPlayerStats(unit);

            if (playerStats == null)
            {
                if (LogRewards.Value)
                {
                    Log?.LogWarning(
                        "[LuckOnKill] Could not find current player stats. Unit=" + GetUnitName(unit)
                    );
                }

                return;
            }

            float luckGain = GetAttribute(playerStats, StatLuckGain);

            if (float.IsNaN(luckGain))
            {
                if (LogRewards.Value)
                    Log?.LogWarning("[LuckOnKill] Could not read Stat_LuckGain.");

                return;
            }

            float reward = CalculateReward(luckGain);

            if (reward <= 0f)
                return;

            float before = GetStatus(playerStats, StatusLuck);

            if (ApplyThroughModifyStatus.Value)
            {
                ModifyStatus(playerStats, StatusLuck, reward);
            }
            else
            {
                SetStatus(playerStats, StatusLuck, before + reward);
            }

            float after = GetStatus(playerStats, StatusLuck);

            if (LogRewards.Value)
            {
                Log?.LogInfo(
                    "[LuckOnKill] Unit died: " + GetUnitName(unit) +
                    " | LuckGain=" + luckGain.ToString("0.###") +
                    " | Reward=" + reward.ToString("0.###") +
                    " | Status_Luck " + before.ToString("0.###") +
                    " -> " + after.ToString("0.###")
                );
            }
        }

        private static object GetPlayerStats(object contextUnit)
        {
            object statsFromContext = TryGetPlayerStatsFromContextUnit(contextUnit);

            if (statsFromContext != null)
                return statsFromContext;

            object statsFromGameManager = TryGetPlayerStatsFromGameManager();

            if (statsFromGameManager != null)
                return statsFromGameManager;

            object statsFromScan = TryFindPlayerStatsByScanningUnits();

            if (statsFromScan != null)
                return statsFromScan;

            return null;
        }

        private static object TryGetPlayerStatsFromContextUnit(object contextUnit)
        {
            if (contextUnit == null)
                return null;

            object playerUnit = GetMember(contextUnit, "PlayerUnit");

            if (playerUnit == null)
                return null;

            return GetMember(playerUnit, "Stats");
        }

        private static object TryGetPlayerStatsFromGameManager()
        {
            object playerUnit = TryGetPlayerUnitFromGameManager();

            if (playerUnit == null)
                return null;

            return GetMember(playerUnit, "Stats");
        }

        private static object TryGetPlayerUnitFromGameManager()
        {
            Type gameManagerType = FindTypeByFullName(GameManagerTypeName);

            if (gameManagerType == null)
                return null;

            object gameManagerInstance = GetStaticMember(gameManagerType, "Instance");

            if (gameManagerInstance == null)
                return null;

            return GetMember(gameManagerInstance, "PlayerUnit");
        }

        private static object TryFindPlayerStatsByScanningUnits()
        {
            Type unitType = FindTypeByFullName(UnitTypeName);

            if (unitType == null)
                return null;

            UnityEngine.Object[] units;

            try
            {
                units = UnityEngine.Object.FindObjectsOfType(unitType);
            }
            catch
            {
                return null;
            }

            if (units == null)
                return null;

            foreach (UnityEngine.Object unit in units)
            {
                if (unit == null)
                    continue;

                if (!IsPlayerUnit(unit))
                    continue;

                object stats = GetMember(unit, "Stats");

                if (stats != null)
                    return stats;
            }

            return null;
        }

        private static float CalculateReward(float luckGain)
        {
            float overrideValue = RewardOverride.Value;

            if (overrideValue >= 0f)
                return overrideValue;

            float reward = luckGain * RewardMultiplier.Value + RewardFlatBonus.Value;

            if (float.IsNaN(reward) || float.IsInfinity(reward))
                reward = 0f;

            return Math.Max(0f, reward);
        }

        private static bool? IsHostileToPlayer(object unit)
        {
            if (unit == null)
                return null;

            object playerUnit = TryGetPlayerUnitFromGameManager();

            if (playerUnit == null)
                playerUnit = TryFindPlayerUnitByScanningUnits();

            if (playerUnit == null)
                return null;

            MethodInfo method = FindInstanceMethod(
                unit.GetType(),
                "IsHostileTo",
                parameters =>
                    parameters.Length == 1 &&
                    parameters[0].ParameterType.IsAssignableFrom(playerUnit.GetType())
            );

            if (method == null)
                return null;

            try
            {
                object result = method.Invoke(unit, new object[] { playerUnit });

                if (result is bool b)
                    return b;
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static object TryFindPlayerUnitByScanningUnits()
        {
            Type unitType = FindTypeByFullName(UnitTypeName);

            if (unitType == null)
                return null;

            UnityEngine.Object[] units;

            try
            {
                units = UnityEngine.Object.FindObjectsOfType(unitType);
            }
            catch
            {
                return null;
            }

            if (units == null)
                return null;

            foreach (UnityEngine.Object unit in units)
            {
                if (unit == null)
                    continue;

                if (IsPlayerUnit(unit))
                    return unit;
            }

            return null;
        }

        private static bool IsDead(object unit)
        {
            object state = GetMember(unit, "UnitState");

            if (state == null)
                state = GetMember(unit, "unitState");

            if (state == null)
                return false;

            string text = state.ToString();

            if (text == "Dead")
                return true;

            try
            {
                return Convert.ToInt32(state) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPlayerUnit(object unit)
        {
            bool isPlayer = GetBoolMember(unit, "isPlayer", false);

            if (isPlayer)
                return true;

            object value = GetMember(unit, "IsPlayer");

            if (value is bool b && b)
                return true;

            string unitName = GetUnitName(unit);

            return unitName.Contains("Unit_Player");
        }

        private static bool IsCivilianUnit(object unit)
        {
            object value = GetMember(unit, "IsCivilian");

            if (value is bool b)
                return b;

            return false;
        }

        private static bool IsBreakableUnit(object unit)
        {
            if (unit == null)
                return false;

            Type type = unit.GetType();

            while (type != null)
            {
                if (type.Name == "Breakable" || (type.FullName != null && type.FullName.Contains(".Breakable")))
                    return true;

                type = type.BaseType;
            }

            string unitName = GetUnitName(unit);

            return unitName.Contains("Breakable");
        }

        private static string GetUnitName(object unit)
        {
            if (unit == null)
                return "Unknown";

            UnityEngine.Object unityObject = unit as UnityEngine.Object;

            if (unityObject != null)
                return unityObject.name;

            return unit.ToString();
        }

        private static int GetObjectId(object obj)
        {
            UnityEngine.Object unityObject = obj as UnityEngine.Object;

            if (unityObject != null)
                return unityObject.GetInstanceID();

            return RuntimeHelpers.GetHashCode(obj);
        }

        private static float GetStatus(object stats, int attributeId)
        {
            return InvokeStatsGetter(stats, "GetStatus", attributeId);
        }

        private static float GetAttribute(object stats, int attributeId)
        {
            return InvokeStatsGetter(stats, "GetAttribute", attributeId);
        }

        private static float InvokeStatsGetter(object stats, string methodName, int attributeId)
        {
            if (stats == null)
                return float.NaN;

            Type enumType = FindTypeByFullName(EntityAttributesTypeName);

            if (enumType == null)
                return float.NaN;

            object enumValue = Enum.ToObject(enumType, attributeId);

            MethodInfo method = FindInstanceMethod(
                stats.GetType(),
                methodName,
                parameters =>
                    parameters.Length == 1 &&
                    parameters[0].ParameterType == enumType
            );

            if (method == null)
                return float.NaN;

            try
            {
                object result = method.Invoke(stats, new object[] { enumValue });
                return Convert.ToSingle(result);
            }
            catch
            {
                return float.NaN;
            }
        }

        private static void ModifyStatus(object stats, int statusId, float amount)
        {
            InvokeStatsSetter(stats, "ModifyStatus", statusId, amount);
        }

        private static void SetStatus(object stats, int statusId, float value)
        {
            InvokeStatsSetter(stats, "SetStatus", statusId, value);
        }

        private static void InvokeStatsSetter(object stats, string methodName, int statusId, float value)
        {
            if (stats == null)
                return;

            Type enumType = FindTypeByFullName(EntityAttributesTypeName);

            if (enumType == null)
                return;

            object enumValue = Enum.ToObject(enumType, statusId);

            MethodInfo method = FindInstanceMethod(
                stats.GetType(),
                methodName,
                parameters =>
                    parameters.Length >= 2 &&
                    parameters[0].ParameterType == enumType &&
                    parameters[1].ParameterType == typeof(float)
            );

            if (method == null)
            {
                if (LogRewards.Value)
                    Log?.LogWarning("[LuckOnKill] Could not find " + methodName + ".");

                return;
            }

            ParameterInfo[] parameterInfos = method.GetParameters();
            object[] args = new object[parameterInfos.Length];

            args[0] = enumValue;
            args[1] = value;

            for (int i = 2; i < args.Length; i++)
            {
                Type parameterType = parameterInfos[i].ParameterType;

                if (parameterType == typeof(bool))
                    args[i] = false;
                else if (parameterInfos[i].HasDefaultValue)
                    args[i] = parameterInfos[i].DefaultValue;
                else
                    args[i] = GetDefaultValue(parameterType);
            }

            try
            {
                method.Invoke(stats, args);
            }
            catch (Exception ex)
            {
                if (LogRewards.Value)
                    Log?.LogWarning("[LuckOnKill] Failed to invoke " + methodName + ": " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static object GetMember(object obj, string name)
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
                PropertyInfo property = type.GetProperty(name, flags);

                if (property != null)
                {
                    try
                    {
                        return property.GetValue(obj, null);
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

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null)
                return null;

            const BindingFlags flags =
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy;

            Type current = type;

            while (current != null)
            {
                PropertyInfo property = current.GetProperty(name, flags);

                if (property != null)
                {
                    try
                    {
                        return property.GetValue(null, null);
                    }
                    catch
                    {
                        return null;
                    }
                }

                FieldInfo field = current.GetField(name, flags);

                if (field != null)
                {
                    try
                    {
                        return field.GetValue(null);
                    }
                    catch
                    {
                        return null;
                    }
                }

                current = current.BaseType;
            }

            return null;
        }

        private static bool GetBoolMember(object obj, string name, bool fallback)
        {
            object value = GetMember(obj, name);

            if (value is bool b)
                return b;

            return fallback;
        }

        private static int GetIntMember(object obj, string name, int fallback)
        {
            object value = GetMember(obj, name);

            if (value == null)
                return fallback;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static Type FindTypeByFullName(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);

                if (type != null)
                    return type;
            }

            return null;
        }

        private static MethodInfo FindInstanceMethod(
            Type type,
            string methodName,
            Func<ParameterInfo[], bool> parameterPredicate
        )
        {
            if (type == null)
                return null;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            Type current = type;

            while (current != null)
            {
                MethodInfo[] methods = current.GetMethods(flags);

                foreach (MethodInfo method in methods)
                {
                    if (method.Name != methodName)
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameterPredicate(parameters))
                        return method;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static object GetDefaultValue(Type type)
        {
            if (type == null)
                return null;

            if (type.IsValueType)
                return Activator.CreateInstance(type);

            return null;
        }
    }

    [HarmonyPatch]
    internal static class Unit_Spawn_ResetRewardState_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type unitType = AccessTools.TypeByName("PerfectRandom.Sulfur.Core.Units.Unit");

            if (unitType == null)
                yield break;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type == null)
                        continue;

                    if (!unitType.IsAssignableFrom(type))
                        continue;

                    MethodInfo method = type.GetMethod("Spawn", flags);

                    if (method == null)
                        continue;

                    if (method.IsAbstract)
                        continue;

                    if (method.GetParameters().Length == 0)
                        yield return method;
                }
            }
        }

        private static void Postfix(object __instance)
        {
            Plugin.ResetRewardState(__instance);
        }
    }

    [HarmonyPatch]
    internal static class Unit_Die_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type unitType = AccessTools.TypeByName("PerfectRandom.Sulfur.Core.Units.Unit");

            if (unitType == null)
                yield break;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type == null)
                        continue;

                    if (!unitType.IsAssignableFrom(type))
                        continue;

                    MethodInfo method = type.GetMethod("Die", flags);

                    if (method == null)
                        continue;

                    if (method.IsAbstract)
                        continue;

                    if (method.GetParameters().Length == 0)
                        yield return method;
                }
            }
        }

        private static void Prefix(object __instance, out bool __state)
        {
            __state = Plugin.ShouldRewardBeforeDeath(__instance);
        }

        private static void Postfix(object __instance, bool __state)
        {
            if (!__state)
                return;

            Plugin.GrantLuckForDeath(__instance);
        }
    }
}