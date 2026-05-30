using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace WeaponDurabilityLossMultiplier
{
    [BepInPlugin(
        "kumo.sulfur.weapon_durability_loss_multiplier",
        "Weapon Durability Loss Multiplier",
        "1.0.0"
    )]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<float> WeaponDurabilityLossMultiplier;
        internal static ConfigEntry<bool> LogDurabilityChanges;

        private Harmony harmony;

        private static MethodInfo modifyDurabilityMethod;
        private static MethodInfo applyMultiplierMethod;

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enable weapon durability loss multiplier."
            );

            WeaponDurabilityLossMultiplier = Config.Bind(
                "Weapon",
                "WeaponDurabilityLossMultiplier",
                0.5f,
                new ConfigDescription(
                    "Multiplier applied to final weapon durability loss from shooting. 1.0 = vanilla, 0.5 = half loss, 0 = no loss, 2 = double loss.",
                    new AcceptableValueRange<float>(0f, 10f)
                )
            );

            LogDurabilityChanges = Config.Bind(
                "Debug",
                "LogDurabilityChanges",
                false,
                "Log adjusted shooting durability loss. Keep false for normal gameplay."
            );

            harmony = new Harmony("kumo.sulfur.weapon_durability_loss_multiplier");

            Type inventoryItemType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Items.InventoryItem"
            );

            if (inventoryItemType == null)
            {
                Logger.LogError("Could not find type: PerfectRandom.Sulfur.Core.Items.InventoryItem");
                return;
            }

            MethodInfo takeDurabilityLossFromShootMethod = AccessTools.Method(
                inventoryItemType,
                "TakeDurabilityLossFromShoot"
            );

            if (takeDurabilityLossFromShootMethod == null)
            {
                Logger.LogError("Could not find method: InventoryItem.TakeDurabilityLossFromShoot()");
                return;
            }

            modifyDurabilityMethod = AccessTools.Method(
                inventoryItemType,
                "ModifyDurability",
                new Type[] { typeof(float) }
            );

            if (modifyDurabilityMethod == null)
            {
                Logger.LogError("Could not find method: InventoryItem.ModifyDurability(float)");
                return;
            }

            applyMultiplierMethod = AccessTools.Method(
                typeof(Plugin),
                nameof(ApplyWeaponDurabilityLossMultiplier)
            );

            if (applyMultiplierMethod == null)
            {
                Logger.LogError("Could not find method: ApplyWeaponDurabilityLossMultiplier(float)");
                return;
            }

            HarmonyMethod transpiler = new HarmonyMethod(
                typeof(Plugin).GetMethod(
                    nameof(TakeDurabilityLossFromShootTranspiler),
                    BindingFlags.Static | BindingFlags.NonPublic
                )
            );

            harmony.Patch(
                takeDurabilityLossFromShootMethod,
                transpiler: transpiler
            );

            Logger.LogInfo("Weapon Durability Loss Multiplier loaded.");
            Logger.LogInfo("Patched InventoryItem.TakeDurabilityLossFromShoot().");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private static IEnumerable<CodeInstruction> TakeDurabilityLossFromShootTranspiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            int injectionCount = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (IsModifyDurabilityCall(instruction))
                {
                    CodeInstruction applyMultiplierInstruction = new CodeInstruction(
                        OpCodes.Call,
                        applyMultiplierMethod
                    );

                    // If the original call has labels, move them to the inserted instruction
                    // so any branch still runs the multiplier before ModifyDurability.
                    applyMultiplierInstruction.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();

                    yield return applyMultiplierInstruction;
                    injectionCount++;
                }

                yield return instruction;
            }

            if (injectionCount == 0)
            {
                Log?.LogError(
                    "Transpiler failed: no call to InventoryItem.ModifyDurability(float) was found."
                );
            }
            else
            {
                Log?.LogInfo(
                    "Transpiler applied. Injected weapon durability multiplier before ModifyDurability call(s): "
                    + injectionCount
                );
            }
        }

        private static bool IsModifyDurabilityCall(CodeInstruction instruction)
        {
            if (modifyDurabilityMethod == null)
                return false;

            if (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                return false;

            MethodInfo calledMethod = instruction.operand as MethodInfo;

            if (calledMethod == null)
                return false;

            return calledMethod == modifyDurabilityMethod;
        }

        private static float ApplyWeaponDurabilityLossMultiplier(float durabilityChange)
        {
            if (!EnableMod.Value)
                return durabilityChange;

            // This mod is only intended to modify durability loss.
            // In TakeDurabilityLossFromShoot(), the value should be negative.
            if (durabilityChange >= 0f)
                return durabilityChange;

            float multiplier = WeaponDurabilityLossMultiplier.Value;

            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                multiplier = 1f;

            multiplier = Math.Max(0f, Math.Min(10f, multiplier));

            float adjustedDurabilityChange = durabilityChange * multiplier;

            if (LogDurabilityChanges.Value)
            {
                Log?.LogInfo(
                    "Weapon shooting durability loss adjusted: "
                    + durabilityChange
                    + " -> "
                    + adjustedDurabilityChange
                    + " multiplier="
                    + multiplier
                );
            }

            return adjustedDurabilityChange;
        }
    }
}