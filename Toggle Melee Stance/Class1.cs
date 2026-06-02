using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToggleMeleeStance
{
    [BepInPlugin(
        PluginGuid,
        PluginName,
        PluginVersion
    )]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "kumo.sulfur.toggle_melee_stance";
        public const string PluginName = "Toggle Melee Stance";
        public const string PluginVersion = "1.2.0";

        public const string MeleeExpansionGuid = "kumo.sulfur.melee_expansion";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<bool> FirePerformsMeleeAttack;
        internal static ConfigEntry<bool> ResetMeleeAnimatorBeforeSheathe;
        internal static ConfigEntry<bool> DisableWhenMeleeExpansionDetected;
        internal static ConfigEntry<bool> KeepMeleeStanceAfterExternalWeaponSwitch;
        internal static ConfigEntry<bool> EnableTapHoldMeleeKey;
        internal static ConfigEntry<float> MeleeToggleTapThreshold;
        internal static ConfigEntry<float> ReChargeRetryInterval;
        internal static ConfigEntry<bool> LogStateChanges;

        private Harmony harmony;

        private static Type equipmentManagerType;
        private static Type weaponType;

        private static MethodInfo mHandleAimInput;
        private static MethodInfo mHandleMeleeInput;
        private static MethodInfo mPullTrigger;
        private static MethodInfo mReleaseTrigger;
        private static MethodInfo mChargeBasicMelee;
        private static MethodInfo mUseBasicMelee;
        private static MethodInfo mOnMeleeDone;
        private static MethodInfo mReportMeleeDone;
        private static MethodInfo mIsMeleeCharging;
        private static MethodInfo mChargeMelee;
        private static MethodInfo mSetAlternativeState;

        private static PropertyInfo pIsMelee;
        private static PropertyInfo pAnimator;

        private static FieldInfo fAltFireAction;
        private static FieldInfo fMeleeFireAction;
        private static FieldInfo fMeleeFireActionAlternative;

        private static FieldInfo fCurrentHoldable;
        private static FieldInfo fEquipmentManager;

        private static FieldInfo fIsInMeleeCharge;
        private static FieldInfo fMeleePressed;
        private static FieldInfo fAlternativeMeleePressed;
        private static FieldInfo fAimingInputHeld;
        private static FieldInfo fMeleeInputCooldown;
        private static FieldInfo fCurrentParries;

        private static readonly ConditionalWeakTable<object, ToggleState> States =
            new ConditionalWeakTable<object, ToggleState>();

        private static readonly ConditionalWeakTable<object, SafeAnimatorState> SafeAnimatorStates =
            new ConditionalWeakTable<object, SafeAnimatorState>();

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enable Toggle Melee Stance."
            );

            FirePerformsMeleeAttack = Config.Bind(
                "Melee",
                "FirePerformsMeleeAttack",
                true,
                "When melee stance is toggled on, pressing the normal Fire action performs one melee attack."
            );

            ReChargeRetryInterval = Config.Bind(
                "Melee",
                "ReChargeRetryInterval",
                0.08f,
                new ConfigDescription(
                    "Seconds between retry attempts when the mod tries to re-enter melee charge after an attack.",
                    new AcceptableValueRange<float>(0.01f, 0.5f)
                )
            );

            ResetMeleeAnimatorBeforeSheathe = Config.Bind(
                "Visual",
                "ResetMeleeAnimatorBeforeSheathe",
                true,
                "Before sheathing a toggled melee weapon, reset its Animator to the cached safe equip state. This prevents stale idle states from causing a slash-frame flash on the next draw."
            );

            DisableWhenMeleeExpansionDetected = Config.Bind(
                "Compatibility",
                "DisableWhenMeleeExpansionDetected",
                false,
                "Deprecated. Kept only for old config compatibility. Toggle Melee Stance no longer auto-disables when The Dragonblade is installed, because it is now the shared prerequisite melee stance module."
            );

            KeepMeleeStanceAfterExternalWeaponSwitch = Config.Bind(
                "Compatibility",
                "KeepMeleeStanceAfterExternalWeaponSwitch",
                false,
                "When another mod changes the current weapon while toggle melee stance is active, keep melee stance instead of exiting it. Default false: exit stance and allow the new weapon to fire normally."
            );

            EnableTapHoldMeleeKey = Config.Bind(
                "Melee",
                "EnableTapHoldMeleeKey",
                true,
                "If true, tap the melee key to toggle melee stance, but hold the melee key to use the original melee behavior."
            );

            MeleeToggleTapThreshold = Config.Bind(
                "Melee",
                "MeleeToggleTapThreshold",
                0.13f,
                new ConfigDescription(
                    "Maximum press duration treated as a tap for toggling melee stance. Holding longer uses original melee behavior.",
                    new AcceptableValueRange<float>(0.05f, 0.6f)
                )
            );

            LogStateChanges = Config.Bind(
                "Debug",
                "LogStateChanges",
                false,
                "Log toggle melee state changes. Keep false for normal gameplay."
            );


            if (!InitializeReflection())
            {
                Logger.LogError("Reflection initialization failed. Mod will not patch.");
                return;
            }

            harmony = new Harmony(PluginGuid);

            Patch(mHandleAimInput, prefix: nameof(HandleAimInputPrefix));
            Patch(mHandleMeleeInput, prefix: nameof(HandleMeleeInputPrefix));
            Patch(mPullTrigger, prefix: nameof(PullTriggerPrefix));
            Patch(mReleaseTrigger, prefix: nameof(ReleaseTriggerPrefix));
            Patch(mReportMeleeDone, prefix: nameof(ReportMeleeDonePrefix), postfix: nameof(ReportMeleeDonePostfix));
            Patch(mChargeMelee, postfix: nameof(ChargeMeleePostfix));

            Logger.LogInfo("Toggle Melee Stance loaded.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private static bool InitializeReflection()
        {
            equipmentManagerType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Items.EquipmentManager"
            );

            weaponType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Weapons.Weapon"
            );

            if (equipmentManagerType == null)
            {
                Log?.LogError("Could not find EquipmentManager type.");
                return false;
            }

            if (weaponType == null)
            {
                Log?.LogError("Could not find Weapon type.");
                return false;
            }

            mHandleAimInput = AccessTools.Method(
                equipmentManagerType,
                "HandleAimInput",
                new Type[] { typeof(bool) }
            );

            mHandleMeleeInput = AccessTools.Method(
                equipmentManagerType,
                "HandleMeleeInput",
                new Type[] { typeof(bool) }
            );

            mPullTrigger = AccessTools.Method(equipmentManagerType, "PullTrigger");
            mReleaseTrigger = AccessTools.Method(equipmentManagerType, "ReleaseTrigger");
            mChargeBasicMelee = AccessTools.Method(equipmentManagerType, "ChargeBasicMelee");
            mUseBasicMelee = AccessTools.Method(equipmentManagerType, "UseBasicMelee");
            mOnMeleeDone = AccessTools.Method(equipmentManagerType, "OnMeleeDone");

            mReportMeleeDone = AccessTools.Method(weaponType, "ReportMeleeDone");
            mIsMeleeCharging = AccessTools.Method(weaponType, "IsMeleeCharging");

            mChargeMelee = AccessTools.Method(
                weaponType,
                "ChargeMelee",
                new Type[] { typeof(bool) }
            );

            mSetAlternativeState = AccessTools.Method(
                weaponType,
                "SetAlternativeState",
                new Type[] { typeof(int) }
            );

            pIsMelee = AccessTools.Property(weaponType, "IsMelee");
            pAnimator = FindPropertyRecursive(weaponType, "Animator");

            fAltFireAction = FindFieldRecursive(equipmentManagerType, "altFireAction");
            fMeleeFireAction = FindFieldRecursive(equipmentManagerType, "meleeFireAction");
            fMeleeFireActionAlternative = FindFieldRecursive(equipmentManagerType, "meleeFireActionAlternative");

            fCurrentHoldable = FindFieldRecursive(equipmentManagerType, "currentHoldable");
            fEquipmentManager = FindFieldRecursive(weaponType, "equipmentManager");

            fIsInMeleeCharge = FindFieldRecursive(
                equipmentManagerType,
                "<isInMeleeCharge>k__BackingField",
                "isInMeleeCharge"
            );

            fMeleePressed = FindFieldRecursive(
                equipmentManagerType,
                "<meleePressed>k__BackingField",
                "meleePressed"
            );

            fAlternativeMeleePressed = FindFieldRecursive(
                equipmentManagerType,
                "<alternativeMeleePressed>k__BackingField",
                "alternativeMeleePressed"
            );

            fAimingInputHeld = FindFieldRecursive(
                equipmentManagerType,
                "<AimingInputHeld>k__BackingField",
                "AimingInputHeld"
            );

            fMeleeInputCooldown = FindFieldRecursive(
                equipmentManagerType,
                "meleeInputCooldown"
            );

            fCurrentParries = FindFieldRecursive(
                weaponType,
                "currentParries"
            );

            return Require(mHandleAimInput, "EquipmentManager.HandleAimInput(bool)") &&
                   Require(mHandleMeleeInput, "EquipmentManager.HandleMeleeInput(bool)") &&
                   Require(mPullTrigger, "EquipmentManager.PullTrigger()") &&
                   Require(mReleaseTrigger, "EquipmentManager.ReleaseTrigger()") &&
                   Require(mChargeBasicMelee, "EquipmentManager.ChargeBasicMelee()") &&
                   Require(mUseBasicMelee, "EquipmentManager.UseBasicMelee()") &&
                   Require(mOnMeleeDone, "EquipmentManager.OnMeleeDone()") &&
                   Require(mReportMeleeDone, "Weapon.ReportMeleeDone()") &&
                   Require(mIsMeleeCharging, "Weapon.IsMeleeCharging()") &&
                   Require(mChargeMelee, "Weapon.ChargeMelee(bool)") &&
                   Require(mSetAlternativeState, "Weapon.SetAlternativeState(int)") &&
                   Require(pIsMelee, "Weapon.IsMelee") &&
                   Require(pAnimator, "Holdable.Animator") &&
                   Require(fAltFireAction, "EquipmentManager.altFireAction") &&
                   Require(fMeleeFireAction, "EquipmentManager.meleeFireAction") &&
                   Require(fMeleeFireActionAlternative, "EquipmentManager.meleeFireActionAlternative") &&
                   Require(fCurrentHoldable, "EquipmentManager.currentHoldable") &&
                   Require(fEquipmentManager, "Weapon.equipmentManager") &&
                   Require(fIsInMeleeCharge, "EquipmentManager.isInMeleeCharge") &&
                   Require(fMeleePressed, "EquipmentManager.meleePressed") &&
                   Require(fAlternativeMeleePressed, "EquipmentManager.alternativeMeleePressed") &&
                   Require(fAimingInputHeld, "EquipmentManager.AimingInputHeld") &&
                   Require(fMeleeInputCooldown, "EquipmentManager.meleeInputCooldown");
        }

        private static bool Require(MemberInfo member, string name)
        {
            if (member != null)
                return true;

            Log?.LogError("Missing required member: " + name);
            return false;
        }

        private void Patch(
            MethodInfo original,
            string prefix = null,
            string postfix = null
        )
        {
            HarmonyMethod prefixMethod = null;
            HarmonyMethod postfixMethod = null;

            if (!string.IsNullOrEmpty(prefix))
            {
                prefixMethod = new HarmonyMethod(
                    AccessTools.Method(typeof(Plugin), prefix)
                );
            }

            if (!string.IsNullOrEmpty(postfix))
            {
                postfixMethod = new HarmonyMethod(
                    AccessTools.Method(typeof(Plugin), postfix)
                );
            }

            harmony.Patch(
                original,
                prefix: prefixMethod,
                postfix: postfixMethod
            );
        }

        private static FieldInfo FindFieldRecursive(Type type, params string[] names)
        {
            Type current = type;

            while (current != null)
            {
                foreach (string name in names)
                {
                    FieldInfo field = AccessTools.Field(current, name);

                    if (field != null)
                        return field;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static PropertyInfo FindPropertyRecursive(Type type, string name)
        {
            Type current = type;

            while (current != null)
            {
                PropertyInfo property = AccessTools.Property(current, name);

                if (property != null)
                    return property;

                current = current.BaseType;
            }

            return null;
        }

        private static ToggleState GetState(object equipmentManager)
        {
            return States.GetValue(
                equipmentManager,
                _ => new ToggleState()
            );
        }

        public static bool IsRuntimeReady
        {
            get
            {
                return EnableMod != null && equipmentManagerType != null && weaponType != null;
            }
        }

        public static bool IsMeleeStanceActive(object equipmentManager)
        {
            if (equipmentManager == null || !IsRuntimeReady || !IsModActive())
                return false;

            ToggleState state = GetState(equipmentManager);
            return state != null && state.IsToggled;
        }

        public static bool IsAttackInProgress(object equipmentManager)
        {
            if (equipmentManager == null || !IsRuntimeReady || !IsModActive())
                return false;

            ToggleState state = GetState(equipmentManager);
            return state != null && state.AttackInProgress;
        }

        public static bool IsSheatheAfterAttack(object equipmentManager)
        {
            if (equipmentManager == null || !IsRuntimeReady || !IsModActive())
                return false;

            ToggleState state = GetState(equipmentManager);
            return state != null && state.SheatheAfterAttack;
        }

        public static bool IsMeleeChargeActive(object equipmentManager)
        {
            if (equipmentManager == null || !IsRuntimeReady || !IsModActive())
                return false;

            return IsInMeleeCharge(equipmentManager);
        }

        public static object GetCurrentHoldableForExternal(object equipmentManager)
        {
            if (equipmentManager == null || !IsRuntimeReady)
                return null;

            return GetCurrentHoldable(equipmentManager);
        }

        public static bool IsCurrentHoldableMeleeForExternal(object equipmentManager)
        {
            object holdable = GetCurrentHoldableForExternal(equipmentManager);
            return IsMeleeWeapon(holdable);
        }

        private static bool IsModActive()
        {
            return EnableMod != null && EnableMod.Value;
        }

        private static void HandleAimInputPrefix(
            object __instance,
            ref bool holdingMeleeAction
        )
        {
            if (!IsModActive())
                return;

            ToggleState state = GetState(__instance);

            if (!state.IsToggled)
                return;

            holdingMeleeAction = true;
        }

        private static bool HandleMeleeInputPrefix(
            object __instance,
            bool holdingMeleeAction
        )
        {
            if (!IsModActive())
                return true;

            ToggleState state = GetState(__instance);

            if (state.SuppressMeleeUntilReleased)
            {
                bool heldWhileSuppressed = holdingMeleeAction || IsMeleeHeld(__instance);

                state.WasMeleeHeldLastFrame = heldWhileSuppressed;

                if (heldWhileSuppressed)
                {
                    return false;
                }

                state.SuppressMeleeUntilReleased = false;

                if (LogStateChanges.Value)
                {
                    Log?.LogInfo("Melee input released after manual sheathe. Input suppression cleared.");
                }

                return false;
            }

            if (!state.IsToggled && IsAimingInputHeld(__instance) && holdingMeleeAction)
                return true;

            if (!state.IsToggled && IsMeleeInputCoolingDown(__instance))
                return true;

            if (!state.IsToggled &&
                EnableTapHoldMeleeKey != null &&
                EnableTapHoldMeleeKey.Value)
            {
                bool prefixResult;

                if (HandleTapHoldMeleeInput(__instance, state, holdingMeleeAction, out prefixResult))
                {
                    return prefixResult;
                }
            }
            else
            {
                bool meleePressedThisFrame = WasMeleePressedThisFrame(__instance);

                if (meleePressedThisFrame)
                {
                    if (!state.IsToggled)
                    {
                        ToggleOn(__instance, state);
                        return false;
                    }

                    ToggleOff(__instance, state);
                    return false;
                }
            }

            if (state.IsToggled)
            {
                bool meleePressedThisFrame = WasMeleePressedThisFrame(__instance);

                if (meleePressedThisFrame)
                {
                    ToggleOff(__instance, state);
                    return false;
                }

                MaintainToggledMelee(__instance, state);
                return false;
            }

            return true;
        }

        private static bool PullTriggerPrefix(object __instance)
        {
            if (!IsModActive())
                return true;

            ToggleState state = GetState(__instance);

            if (!state.IsToggled)
                return true;

            if (ExitToggledStateIfExternalNonMeleeHoldable(__instance, state, "PullTrigger"))
            {
                return true;
            }

            if (!FirePerformsMeleeAttack.Value)
                return false;

            object currentHoldable = GetCurrentHoldable(__instance);

            if (!IsMeleeWeapon(currentHoldable))
            {
                MaintainToggledMelee(__instance, state);
                return false;
            }

            if (!IsMeleeCharging(currentHoldable))
            {
                MaintainToggledMelee(__instance, state);
                return false;
            }

            if (state.AttackInProgress)
                return false;

            state.AttackInProgress = true;
            state.SheatheAfterAttack = false;
            state.SuppressMeleeUntilReleased = false;

            if (LogStateChanges.Value)
            {
                Log?.LogInfo("Fire action converted to melee attack.");
            }

            InvokeUseBasicMelee(__instance);

            return false;
        }

        private static bool ReleaseTriggerPrefix(object __instance)
        {
            if (!IsModActive())
                return true;

            ToggleState state = GetState(__instance);

            if (!state.IsToggled &&
                !state.AttackInProgress &&
                !state.SheatheAfterAttack)
            {
                return true;
            }

            return false;
        }

        private static void ReportMeleeDonePrefix(object __instance)
        {
            if (!IsModActive())
                return;

            object equipmentManager = GetEquipmentManagerFromWeapon(__instance);

            if (equipmentManager == null)
                return;

            ToggleState state = GetState(equipmentManager);

            if (!state.IsToggled &&
                !state.AttackInProgress &&
                !state.SheatheAfterAttack)
            {
                return;
            }

            PrepareMeleeAnimatorForFutureDraw(__instance);
        }

        private static void ReportMeleeDonePostfix(object __instance)
        {
            if (!IsModActive())
                return;

            object equipmentManager = GetEquipmentManagerFromWeapon(__instance);

            if (equipmentManager == null)
                return;

            ToggleState state = GetState(equipmentManager);

            if (state.SheatheAfterAttack)
            {
                state.AttackInProgress = false;
                state.SheatheAfterAttack = false;
                state.IsToggled = false;
                state.SuppressMeleeUntilReleased = true;
                state.NextChargeAttemptTime = 0f;
                state.PendingTapHold = false;
                state.LongPressPassThrough = false;
                state.WasMeleeHeldLastFrame = false;
                state.MeleePressStartTime = 0f;

                if (LogStateChanges.Value)
                {
                    Log?.LogInfo("Melee attack finished. Staying sheathed.");
                }

                return;
            }

            if (!state.IsToggled)
            {
                state.AttackInProgress = false;
                state.NextChargeAttemptTime = 0f;
                return;
            }

            state.AttackInProgress = false;
            state.NextChargeAttemptTime = Time.time + ClampRetryInterval();

            state.PendingTapHold = false;
            state.LongPressPassThrough = false;
            state.WasMeleeHeldLastFrame = false;
            state.MeleePressStartTime = 0f;

            if (LogStateChanges.Value)
            {
                Log?.LogInfo("Melee attack finished. Re-entering toggled melee stance.");
            }

            InvokeChargeBasicMelee(equipmentManager);
        }

        private static void ChargeMeleePostfix(object __instance, bool state)
        {
            if (!IsModActive())
                return;

            if (!state)
                return;

            CacheSafeMeleeAnimatorStateIfUseful(__instance, "ChargeMelee(true)");
        }

        private static void ToggleOn(object equipmentManager, ToggleState state)
        {
            state.IsToggled = true;
            state.AttackInProgress = false;
            state.SheatheAfterAttack = false;
            state.SuppressMeleeUntilReleased = false;
            state.NextChargeAttemptTime = Time.time + ClampRetryInterval();

            state.PendingTapHold = false;
            state.LongPressPassThrough = false;
            state.WasMeleeHeldLastFrame = false;
            state.MeleePressStartTime = 0f;

            if (LogStateChanges.Value)
            {
                Log?.LogInfo("Toggle melee ON.");
            }

            InvokeChargeBasicMelee(equipmentManager);
            SetMeleePressed(equipmentManager, true);
        }

        private static void ToggleOff(object equipmentManager, ToggleState state)
        {
            if (LogStateChanges.Value)
            {
                Log?.LogInfo("Toggle melee OFF.");
            }

            state.IsToggled = false;
            state.NextChargeAttemptTime = 0f;

            state.SuppressMeleeUntilReleased = true;
            state.PendingTapHold = false;
            state.LongPressPassThrough = false;
            state.WasMeleeHeldLastFrame = false;
            state.MeleePressStartTime = 0f;

            SetAlternativeMeleePressed(equipmentManager, false);
            SetMeleePressed(equipmentManager, false);

            object currentHoldable = GetCurrentHoldable(equipmentManager);

            if (state.AttackInProgress)
            {
                state.SheatheAfterAttack = true;

                if (LogStateChanges.Value)
                {
                    Log?.LogInfo("Melee attack is in progress. Will sheathe after current attack finishes.");
                }

                return;
            }

            state.AttackInProgress = false;
            state.SheatheAfterAttack = false;

            CleanCurrentMeleeWeaponState(currentHoldable);
            InvokeOnMeleeDone(equipmentManager);
        }

        private static void MaintainToggledMelee(
            object equipmentManager,
            ToggleState state
        )
        {
            if (state.AttackInProgress || state.SheatheAfterAttack)
                return;

            bool altHeld = IsAltFirePressed(equipmentManager);

            SetAlternativeMeleePressed(equipmentManager, altHeld);
            UpdateCurrentMeleeAlternativeAnimator(equipmentManager, altHeld);

            if (IsInMeleeCharge(equipmentManager))
                return;

            if (Time.time < state.NextChargeAttemptTime)
                return;

            state.NextChargeAttemptTime = Time.time + ClampRetryInterval();

            InvokeChargeBasicMelee(equipmentManager);
            SetMeleePressed(equipmentManager, true);
        }

        private static void CacheSafeMeleeAnimatorStateIfUseful(
            object weapon,
            string source
        )
        {
            if (!IsMeleeWeapon(weapon))
                return;

            Animator animator = GetAnimator(weapon);

            if (animator == null)
                return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            string clipName = GetCurrentClipName(animator);

            if (!IsSafeMeleeDrawClip(clipName))
                return;

            SafeAnimatorState safeState = SafeAnimatorStates.GetValue(
                weapon,
                _ => new SafeAnimatorState()
            );

            safeState.FullPathHash = stateInfo.fullPathHash;
            safeState.ShortNameHash = stateInfo.shortNameHash;
            safeState.ClipName = clipName;
            safeState.Valid = true;

            if (LogStateChanges.Value)
            {
                Log?.LogInfo(
                    "Cached safe melee animator state from "
                    + source
                    + ": clip="
                    + clipName
                    + " fullPathHash="
                    + stateInfo.fullPathHash
                );
            }
        }

        private static bool IsSafeMeleeDrawClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return false;

            if (ContainsIgnoreCase(clipName, "Slash"))
                return false;

            if (ContainsIgnoreCase(clipName, "Attack"))
                return false;

            if (ContainsIgnoreCase(clipName, "Fire"))
                return false;

            if (ContainsIgnoreCase(clipName, "ADS"))
                return false;

            if (ContainsIgnoreCase(clipName, "ToADS"))
                return false;

            if (ContainsIgnoreCase(clipName, "Charge") ||
                ContainsIgnoreCase(clipName, "Charged"))
                return false;

            return ContainsIgnoreCase(clipName, "Equip");
        }

        private static bool ContainsIgnoreCase(string value, string part)
        {
            return value != null &&
                   part != null &&
                   value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void PrepareMeleeAnimatorForFutureDraw(object weapon)
        {
            if (!ResetMeleeAnimatorBeforeSheathe.Value)
                return;

            if (!IsMeleeWeapon(weapon))
                return;

            try
            {
                InvokeSetAlternativeState(weapon, 0);

                if (fCurrentParries != null)
                {
                    fCurrentParries.SetValue(weapon, 0);
                }

                Animator animator = GetAnimator(weapon);

                if (animator == null)
                    return;

                animator.SetBool("Charge", false);
                animator.SetBool("Sprinting", false);
                animator.SetBool("AlternativePressed", false);
                animator.ResetTrigger("Parry");

                SafeAnimatorState safeState;

                if (!SafeAnimatorStates.TryGetValue(weapon, out safeState) ||
                    safeState == null ||
                    !safeState.Valid)
                {
                    CacheSafeMeleeAnimatorStateIfUseful(weapon, "PrepareMeleeAnimatorForFutureDraw fallback");

                    if (!SafeAnimatorStates.TryGetValue(weapon, out safeState) ||
                        safeState == null ||
                        !safeState.Valid)
                    {
                        if (LogStateChanges.Value)
                        {
                            Log?.LogInfo("No cached safe melee animator state available for " + weapon);
                        }

                        return;
                    }
                }

                animator.Play(safeState.FullPathHash, 0, 0f);
                animator.Update(0f);

                if (LogStateChanges.Value)
                {
                    Log?.LogInfo(
                        "Reset melee animator to safe draw state before sheathe: "
                        + safeState.ClipName
                        + " hash="
                        + safeState.FullPathHash
                    );
                }
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to prepare melee animator for future draw: " + ex);
            }
        }

        private static void CleanCurrentMeleeWeaponState(object currentHoldable)
        {
            if (!IsMeleeWeapon(currentHoldable))
                return;

            try
            {
                PrepareMeleeAnimatorForFutureDraw(currentHoldable);
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to clean melee weapon state: " + ex);
            }
        }

        private static string GetCurrentClipName(Animator animator)
        {
            if (animator == null)
                return string.Empty;

            try
            {
                AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);

                if (clips != null &&
                    clips.Length > 0 &&
                    clips[0].clip != null)
                {
                    return clips[0].clip.name;
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static float ClampRetryInterval()
        {
            if (ReChargeRetryInterval == null)
                return 0.08f;

            float value = ReChargeRetryInterval.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.08f;

            return Mathf.Clamp(value, 0.01f, 0.5f);
        }

        private static bool WasMeleePressedThisFrame(object equipmentManager)
        {
            InputAction melee = GetInputAction(fMeleeFireAction, equipmentManager);
            InputAction meleeAlternative = GetInputAction(fMeleeFireActionAlternative, equipmentManager);

            return WasPerformedThisFrame(melee) ||
                   WasPerformedThisFrame(meleeAlternative);
        }

        private static bool IsMeleeHeld(object equipmentManager)
        {
            InputAction melee = GetInputAction(fMeleeFireAction, equipmentManager);
            InputAction meleeAlternative = GetInputAction(fMeleeFireActionAlternative, equipmentManager);

            bool meleeHeld = melee != null && melee.IsPressed();
            bool meleeAlternativeHeld = meleeAlternative != null && meleeAlternative.IsPressed();

            return meleeHeld || meleeAlternativeHeld;
        }

        private static bool HandleTapHoldMeleeInput(
            object equipmentManager,
            ToggleState state,
            bool holdingMeleeAction,
            out bool prefixResult
        )
        {
            prefixResult = true;

            bool held = holdingMeleeAction || IsMeleeHeld(equipmentManager);
            bool justPressed = held && !state.WasMeleeHeldLastFrame;
            bool justReleased = !held && state.WasMeleeHeldLastFrame;

            state.WasMeleeHeldLastFrame = held;

            if (state.LongPressPassThrough)
            {
                if (justReleased || !held)
                {
                    state.LongPressPassThrough = false;
                    state.PendingTapHold = false;
                    state.MeleePressStartTime = 0f;

                    if (LogStateChanges.Value)
                    {
                        Log?.LogInfo("Long melee input released. Original melee behavior finished.");
                    }
                }

                prefixResult = true;
                return true;
            }

            if (state.PendingTapHold)
            {
                float heldTime = Time.time - state.MeleePressStartTime;

                if (justReleased || !held)
                {
                    state.PendingTapHold = false;

                    if (heldTime <= GetMeleeToggleTapThreshold())
                    {
                        ToggleOn(equipmentManager, state);

                        if (LogStateChanges.Value)
                        {
                            Log?.LogInfo("Melee key tap detected. Toggle melee ON.");
                        }

                        prefixResult = false;
                        return true;
                    }

                    prefixResult = true;
                    return true;
                }

                if (heldTime >= GetMeleeToggleTapThreshold())
                {
                    state.PendingTapHold = false;
                    state.LongPressPassThrough = true;

                    if (LogStateChanges.Value)
                    {
                        Log?.LogInfo("Melee key hold detected. Passing through to original melee behavior.");
                    }

                    prefixResult = true;
                    return true;
                }

                prefixResult = false;
                return true;
            }

            if (justPressed)
            {
                state.PendingTapHold = true;
                state.MeleePressStartTime = Time.time;

                prefixResult = false;
                return true;
            }

            return false;
        }

        private static float GetMeleeToggleTapThreshold()
        {
            if (MeleeToggleTapThreshold == null)
                return 0.13f;

            float value = MeleeToggleTapThreshold.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.13f;

            return Mathf.Clamp(value, 0.05f, 0.6f);
        }

        private static bool ExitToggledStateIfExternalNonMeleeHoldable(
            object equipmentManager,
            ToggleState state,
            string source
        )
        {
            if (equipmentManager == null || state == null)
                return false;

            if (!state.IsToggled)
                return false;

            if (KeepMeleeStanceAfterExternalWeaponSwitch != null &&
                KeepMeleeStanceAfterExternalWeaponSwitch.Value)
            {
                return false;
            }

            object currentHoldable = GetCurrentHoldable(equipmentManager);

            if (currentHoldable == null)
                return false;

            if (IsMeleeWeapon(currentHoldable))
                return false;

            ForceExitToggledStateAfterExternalWeaponSwitch(
                equipmentManager,
                state,
                currentHoldable,
                source
            );

            return true;
        }

        private static void ForceExitToggledStateAfterExternalWeaponSwitch(
            object equipmentManager,
            ToggleState state,
            object currentHoldable,
            string source
        )
        {
            state.IsToggled = false;
            state.AttackInProgress = false;
            state.SheatheAfterAttack = false;
            state.SuppressMeleeUntilReleased = false;
            state.NextChargeAttemptTime = 0f;

            state.PendingTapHold = false;
            state.LongPressPassThrough = false;
            state.WasMeleeHeldLastFrame = false;
            state.MeleePressStartTime = 0f;

            SetMeleePressed(equipmentManager, false);
            SetAlternativeMeleePressed(equipmentManager, false);

            if (LogStateChanges != null && LogStateChanges.Value)
            {
                Log?.LogInfo(
                    "Exited toggle melee stance because current holdable is no longer melee. " +
                    "source=" + source +
                    " holdable=" + currentHoldable
                );
            }
        }

        private static bool IsAltFirePressed(object equipmentManager)
        {
            InputAction action = GetInputAction(fAltFireAction, equipmentManager);

            return action != null && action.IsPressed();
        }

        private static bool WasPerformedThisFrame(InputAction action)
        {
            return action != null && action.WasPerformedThisFrame();
        }

        private static InputAction GetInputAction(
            FieldInfo field,
            object instance
        )
        {
            if (field == null || instance == null)
                return null;

            return field.GetValue(instance) as InputAction;
        }

        private static object GetCurrentHoldable(object equipmentManager)
        {
            if (fCurrentHoldable == null || equipmentManager == null)
                return null;

            return fCurrentHoldable.GetValue(equipmentManager);
        }

        private static object GetEquipmentManagerFromWeapon(object weapon)
        {
            if (fEquipmentManager == null || weapon == null)
                return null;

            return fEquipmentManager.GetValue(weapon);
        }

        private static bool IsMeleeWeapon(object holdable)
        {
            if (holdable == null)
                return false;

            if (!weaponType.IsInstanceOfType(holdable))
                return false;

            object value = pIsMelee.GetValue(holdable, null);

            return value is bool && (bool)value;
        }

        private static bool IsMeleeCharging(object weapon)
        {
            if (weapon == null || mIsMeleeCharging == null)
                return false;

            object value = mIsMeleeCharging.Invoke(weapon, null);

            return value is bool && (bool)value;
        }

        private static bool IsInMeleeCharge(object equipmentManager)
        {
            return GetBoolField(fIsInMeleeCharge, equipmentManager);
        }

        private static bool IsAimingInputHeld(object equipmentManager)
        {
            return GetBoolField(fAimingInputHeld, equipmentManager);
        }

        private static bool IsMeleeInputCoolingDown(object equipmentManager)
        {
            if (fMeleeInputCooldown == null || equipmentManager == null)
                return false;

            object value = fMeleeInputCooldown.GetValue(equipmentManager);

            if (!(value is float))
                return false;

            return Time.time < (float)value;
        }

        private static bool GetBoolField(FieldInfo field, object instance)
        {
            if (field == null || instance == null)
                return false;

            object value = field.GetValue(instance);

            return value is bool && (bool)value;
        }

        private static void SetMeleePressed(object equipmentManager, bool value)
        {
            SetBoolField(fMeleePressed, equipmentManager, value);
        }

        private static void SetAlternativeMeleePressed(
            object equipmentManager,
            bool value
        )
        {
            SetBoolField(fAlternativeMeleePressed, equipmentManager, value);
        }

        private static void SetBoolField(
            FieldInfo field,
            object instance,
            bool value
        )
        {
            if (field == null || instance == null)
                return;

            field.SetValue(instance, value);
        }

        private static void UpdateCurrentMeleeAlternativeAnimator(
            object equipmentManager,
            bool altHeld
        )
        {
            object currentHoldable = GetCurrentHoldable(equipmentManager);

            if (!IsMeleeWeapon(currentHoldable))
                return;

            Animator animator = GetAnimator(currentHoldable);

            if (animator == null)
                return;

            animator.SetBool("AlternativePressed", altHeld);
        }

        private static Animator GetAnimator(object holdable)
        {
            if (holdable == null || pAnimator == null)
                return null;

            return pAnimator.GetValue(holdable, null) as Animator;
        }

        private static void InvokeChargeBasicMelee(object equipmentManager)
        {
            try
            {
                mChargeBasicMelee.Invoke(equipmentManager, null);
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to invoke ChargeBasicMelee: " + ex);
            }
        }

        private static void InvokeUseBasicMelee(object equipmentManager)
        {
            try
            {
                mUseBasicMelee.Invoke(equipmentManager, null);
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to invoke UseBasicMelee: " + ex);
            }
        }

        private static void InvokeOnMeleeDone(object equipmentManager)
        {
            try
            {
                mOnMeleeDone.Invoke(equipmentManager, null);
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to invoke OnMeleeDone: " + ex);
            }
        }

        private static void InvokeSetAlternativeState(object weapon, int value)
        {
            if (weapon == null || mSetAlternativeState == null)
                return;

            try
            {
                mSetAlternativeState.Invoke(weapon, new object[] { value });
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to invoke SetAlternativeState: " + ex);
            }
        }

        private sealed class ToggleState
        {
            public bool IsToggled;
            public bool AttackInProgress;
            public bool SheatheAfterAttack;
            public bool SuppressMeleeUntilReleased;
            public float NextChargeAttemptTime;

            public bool PendingTapHold;
            public bool LongPressPassThrough;
            public bool WasMeleeHeldLastFrame;
            public float MeleePressStartTime;
        }

        private sealed class SafeAnimatorState
        {
            public bool Valid;
            public int FullPathHash;
            public int ShortNameHash;
            public string ClipName;
        }
    }
}