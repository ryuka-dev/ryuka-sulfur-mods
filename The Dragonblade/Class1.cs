using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using ToggleMeleeStancePlugin = ToggleMeleeStance.Plugin;

namespace MeleeExpansion
{
    [BepInPlugin(
        PluginGuid,
        PluginName,
        PluginVersion
    )]
    [BepInDependency(
        ToggleMeleeStanceGuid,
        BepInDependency.DependencyFlags.HardDependency
    )]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "kumo.sulfur.melee_expansion";
        public const string PluginName = "The Dragonblade";
        public const string PluginVersion = "0.3.0";
        public const string ToggleMeleeStanceGuid = "kumo.sulfur.toggle_melee_stance";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<bool> FirePerformsMeleeAttack;
        internal static ConfigEntry<bool> KeepKatanaStanceAfterExternalWeaponSwitch;
        internal static ConfigEntry<bool> EnableTapHoldMeleeKey;
        internal static ConfigEntry<float> MeleeToggleTapThreshold;
        internal static ConfigEntry<bool> ResetMeleeAnimatorBeforeSheathe;
        internal static ConfigEntry<float> ReChargeRetryInterval;
        internal static ConfigEntry<bool> LogStateChanges;

        internal static ConfigEntry<bool> EnableKatanaDash;
        internal static ConfigEntry<string> KatanaNameKeywords;
        internal static ConfigEntry<float> DashCooldown;
        internal static ConfigEntry<float> DashDistance;
        internal static ConfigEntry<float> DashDuration;
        internal static ConfigEntry<float> DashHitRadius;
        internal static ConfigEntry<float> DashDamageMultiplier;
        internal static ConfigEntry<bool> DashHitEachUnitOnce;

        internal static ConfigEntry<bool> RefreshCooldownOnPlayerKill;
        internal static ConfigEntry<bool> RefreshRequiresKatanaStance;
        internal static ConfigEntry<bool> RefreshCooldownOnNonNpcUnitKill;
        internal static ConfigEntry<bool> HealOnEnemyKill;
        internal static ConfigEntry<float> HealAmountOnEnemyKill;

        internal static ConfigEntry<float> DashPostHangDuration;
        internal static ConfigEntry<float> DashPostHangMaxDownwardSpeed;
        internal static ConfigEntry<bool> SuppressFallingAnimationDuringPostHang;

        internal static ConfigEntry<bool> EnableDashHud;
        internal static ConfigEntry<string> DashIconFileName;
        internal static ConfigEntry<float> DashHudIconSize;
        internal static ConfigEntry<bool> DashHudUseActualSprintBinding;
        internal static ConfigEntry<string> DashHudFallbackKeyLabel;
        internal static ConfigEntry<float> DashKillRefreshFeedbackDuration;
        internal static ConfigEntry<float> DashHealFeedbackDuration;

        internal static ConfigEntry<bool> LogDash;

        private Harmony harmony;

        private static Type equipmentManagerType;
        private static Type weaponType;
        private static Type extendedWalkerType;
        private static Type moverType;
        private static Type playerType;
        private static Type unitType;
        private static Type npcType;
        private static Type entityStatsType;
        private static Type entityAttributesType;

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

        private static MethodInfo mUpdateSprinting;
        private static MethodInfo mToggleSprint;
        private static MethodInfo mMoverSetVelocity;
        private static MethodInfo mPlayerDirectionLooking;
        private static MethodInfo mWeaponGetDamage;
        private static MethodInfo mWeaponGetDamageType;
        private static MethodInfo mUnitReceiveDamageWithIDamager;
        private static MethodInfo mUnitReceiveDamageWithDamageSourceData;
        private static MethodInfo mEntityStatsModifyStatus;

        private static PropertyInfo pIsMelee;
        private static PropertyInfo pAnimator;
        private static PropertyInfo pSprintAction;
        private static PropertyInfo pFallingEnabled;
        private static PropertyInfo pSourceName;
        private static PropertyInfo pUnitState;
        private static PropertyInfo pUnitIsPlayer;
        private static PropertyInfo pUnitStats;
        private static PropertyInfo pNpcIsProtectedNpc;

        private static FieldInfo fAltFireAction;
        private static FieldInfo fMeleeFireAction;
        private static FieldInfo fMeleeFireActionAlternative;

        private static FieldInfo fCurrentHoldable;
        private static FieldInfo fEquipmentManager;
        private static FieldInfo fWalkerEquipmentManager;

        private static float LastActualHealAmount;

        private static FieldInfo fIsInMeleeCharge;
        private static FieldInfo fMeleePressed;
        private static FieldInfo fAlternativeMeleePressed;
        private static FieldInfo fAimingInputHeld;
        private static FieldInfo fMeleeInputCooldown;
        private static FieldInfo fCurrentParries;

        private static FieldInfo fWeaponDefinition;
        private static FieldInfo fUnitState;
        private static FieldInfo fUnitIsPlayer;
        private static FieldInfo fPlayerCamera;

        private static FieldInfo fDamageSourceDataIsPlayer;
        private static FieldInfo fDamageSourceDataSourceUnit;
        private static FieldInfo fDamageSourceDataSourceWeapon;

        private static Type hitmeshDataType;
        private static object hitmeshDataDefault;
        private static object entityAttributeStatusCurrentHealth;

        private static string PluginDirectory;
        private static Texture2D DashIconTexture;
        private static Texture2D HudPixelTexture;
        private static bool DashIconLoadAttempted;

        private static MethodInfo mEntityStatsGetStatus;
        private static MethodInfo mEntityStatsGetAttribute;

        private static object entityAttributeMaxHealth;

        private static readonly ConditionalWeakTable<object, ToggleState> ToggleStates =
            new ConditionalWeakTable<object, ToggleState>();

        private static readonly ConditionalWeakTable<object, SafeAnimatorState> SafeAnimatorStates =
            new ConditionalWeakTable<object, SafeAnimatorState>();

        private static readonly ConditionalWeakTable<object, KatanaDashState> DashStates =
            new ConditionalWeakTable<object, KatanaDashState>();

        private static object lastKnownWalkerController;

        private void Awake()
        {
            Log = Logger;
            PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            EnableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enable Melee Expansion."
            );

            EnableKatanaDash = Config.Bind(
                "KatanaDash",
                "EnableKatanaDash",
                true,
                "Enable Katana Dash Strike."
            );

            KatanaNameKeywords = Config.Bind(
                "KatanaDash",
                "KatanaNameKeywords",
                "Katana,Wakizashi,BiggerKatana",
                "Comma-separated keywords used to detect katana-like melee weapons."
            );

            DashCooldown = Config.Bind(
                "KatanaDash",
                "Cooldown",
                5f,
                new ConfigDescription(
                    "Dash cooldown in seconds.",
                    new AcceptableValueRange<float>(0.1f, 60f)
                )
            );

            DashDistance = Config.Bind(
                "KatanaDash",
                "Distance",
                8f,
                new ConfigDescription(
                    "Dash distance.",
                    new AcceptableValueRange<float>(0.5f, 50f)
                )
            );

            DashDuration = Config.Bind(
                "KatanaDash",
                "Duration",
                0.22f,
                new ConfigDescription(
                    "Dash duration in seconds.",
                    new AcceptableValueRange<float>(0.03f, 2f)
                )
            );

            DashHitRadius = Config.Bind(
                "KatanaDash",
                "HitRadius",
                1.0f,
                new ConfigDescription(
                    "Radius used to scan units along the dash path.",
                    new AcceptableValueRange<float>(0.1f, 5f)
                )
            );

            DashDamageMultiplier = Config.Bind(
                "KatanaDash",
                "DamageMultiplier",
                1.0f,
                new ConfigDescription(
                    "Dash damage multiplier applied to the current katana weapon damage.",
                    new AcceptableValueRange<float>(0f, 20f)
                )
            );

            DashHitEachUnitOnce = Config.Bind(
                "KatanaDash",
                "HitEachUnitOnce",
                true,
                "If true, each unit can only be damaged once per dash."
            );

            RefreshCooldownOnPlayerKill = Config.Bind(
                "KatanaDash",
                "RefreshCooldownOnPlayerKill",
                true,
                "Refresh Katana Dash cooldown when the player kills an enemy."
            );

            RefreshRequiresKatanaStance = Config.Bind(
                "KatanaDash",
                "RefreshRequiresKatanaStance",
                true,
                "If true, kill refresh only works while the player is in toggled katana stance."
            );

            RefreshCooldownOnNonNpcUnitKill = Config.Bind(
                "KatanaDash",
                "RefreshCooldownOnNonNpcUnitKill",
                false,
                "If true, killing non-NPC Units such as breakable obstacles can also refresh Katana Dash cooldown. Healing never triggers from non-NPC Units."
            );

            HealOnEnemyKill = Config.Bind(
                "KatanaDash",
                "HealOnEnemyKill",
                true,
                "Heal the player when killing an enemy NPC while in katana stance. This never triggers from breakable obstacles."
            );

            HealAmountOnEnemyKill = Config.Bind(
                "KatanaDash",
                "HealAmountOnEnemyKill",
                5f,
                new ConfigDescription(
                    "Health restored when killing an enemy NPC.",
                    new AcceptableValueRange<float>(0f, 999f)
                )
            );

            DashPostHangDuration = Config.Bind(
                "KatanaDash",
                "PostHangDuration",
                0.12f,
                new ConfigDescription(
                    "Short hang time after Katana Dash ends. This prevents the player from immediately dropping at full falling speed.",
                    new AcceptableValueRange<float>(0f, 0.5f)
                )
            );

            DashPostHangMaxDownwardSpeed = Config.Bind(
                "KatanaDash",
                "PostHangMaxDownwardSpeed",
                0.5f,
                new ConfigDescription(
                    "Maximum downward speed during post-dash hang. 0 means no downward velocity during hang.",
                    new AcceptableValueRange<float>(0f, 20f)
                )
            );

            SuppressFallingAnimationDuringPostHang = Config.Bind(
                "KatanaDash",
                "SuppressFallingAnimationDuringPostHang",
                true,
                "Temporarily suppress the Falling animation during post-dash hang."
            );

            EnableDashHud = Config.Bind(
                "UI",
                "EnableDashHud",
                true,
                "Show a bottom-right Katana Dash cooldown HUD."
            );

            DashIconFileName = Config.Bind(
                "UI",
                "DashIconFileName",
                "dash_icon.png",
                "PNG file name for the Katana Dash HUD icon. Place it next to MeleeExpansion.dll."
            );

            DashHudIconSize = Config.Bind(
                "UI",
                "DashHudIconSize",
                76f,
                new ConfigDescription(
                    "Dash HUD icon size in pixels.",
                    new AcceptableValueRange<float>(48f, 128f)
                )
            );

            DashHudUseActualSprintBinding = Config.Bind(
                "UI",
                "DashHudUseActualSprintBinding",
                true,
                "Show the actual Sprint binding from the game's InputAction when possible."
            );

            DashHudFallbackKeyLabel = Config.Bind(
                "UI",
                "DashHudFallbackKeyLabel",
                "SHIFT",
                "Fallback key label shown on the Dash HUD."
            );

            DashKillRefreshFeedbackDuration = Config.Bind(
                "UI",
                "DashKillRefreshFeedbackDuration",
                0.45f,
                new ConfigDescription(
                    "Seconds to flash the Dash HUD when a kill refreshes the cooldown.",
                    new AcceptableValueRange<float>(0f, 3f)
                )
            );

            DashHealFeedbackDuration = Config.Bind(
                "UI",
                "DashHealFeedbackDuration",
                0.85f,
                new ConfigDescription(
                    "Seconds to show +HP feedback on enemy kill.",
                    new AcceptableValueRange<float>(0f, 3f)
                )
            );

            LogStateChanges = Config.Bind(
                "Debug",
                "LogStateChanges",
                false,
                "Log toggle melee state changes."
            );

            LogDash = Config.Bind(
                "Debug",
                "LogDash",
                false,
                "Log Katana Dash debug events."
            );

            if (!InitializeReflection())
            {
                Logger.LogError("Reflection initialization failed. Mod will not patch.");
                return;
            }

            harmony = new Harmony(PluginGuid);


            Patch(mUpdateSprinting, prefix: nameof(UpdateSprintingPrefix));
            Patch(mMoverSetVelocity, prefix: nameof(MoverSetVelocityPrefix));

            Patch(
                mUnitReceiveDamageWithDamageSourceData,
                prefix: nameof(UnitReceiveDamageDamageSourcePrefix),
                postfix: nameof(UnitReceiveDamageDamageSourcePostfix)
            );

            Logger.LogInfo("The Dragonblade loaded. Toggle melee stance is provided by Toggle Melee Stance.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            if (!IsModActive())
                return;

            if (EnableDashHud == null || !EnableDashHud.Value)
                return;

            if (lastKnownWalkerController == null)
                return;

            object equipmentManager = GetEquipmentManagerFromWalker(lastKnownWalkerController);

            if (equipmentManager == null)
                return;

            object katana;

            if (!IsKatanaDashHudContext(lastKnownWalkerController, equipmentManager, out katana))
                return;

            KatanaDashState dashState = GetDashState(lastKnownWalkerController);

            bool canUseDashNow = IsKatanaDashContext(
                lastKnownWalkerController,
                equipmentManager,
                out _
            );

            DrawKatanaDashHud(dashState, canUseDashNow);
        }

        private static bool InitializeReflection()
        {
            equipmentManagerType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Items.EquipmentManager"
            );

            weaponType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Weapons.Weapon"
            );

            extendedWalkerType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Movement.ExtendedAdvancedWalkerController"
            );

            moverType = AccessTools.TypeByName(
                "CMF.Mover"
            );

            playerType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Units.Player"
            );

            unitType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Units.Unit"
            );

            npcType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Units.Npc"
            );

            entityStatsType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Stats.EntityStats"
            );

            entityAttributesType = AccessTools.TypeByName(
                "PerfectRandom.Sulfur.Core.Stats.EntityAttributes"
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

            if (extendedWalkerType == null)
            {
                Log?.LogError("Could not find ExtendedAdvancedWalkerController type.");
                return false;
            }

            if (moverType == null)
            {
                Log?.LogError("Could not find CMF.Mover type.");
                return false;
            }

            if (playerType == null)
            {
                Log?.LogError("Could not find Player type.");
                return false;
            }

            if (unitType == null)
            {
                Log?.LogError("Could not find Unit type.");
                return false;
            }

            if (entityStatsType == null)
            {
                Log?.LogError("Could not find EntityStats type.");
                return false;
            }

            if (entityAttributesType == null)
            {
                Log?.LogError("Could not find EntityAttributes type.");
                return false;
            }

            try
            {
                entityAttributeStatusCurrentHealth = Enum.Parse(
    entityAttributesType,
    "Status_CurrentHealth"
);

                entityAttributeMaxHealth = TryParseEntityAttribute(
                    "Stat_MaxHealth",
                    "Status_MaxHealth",
                    "MaxHealth",
                    "HealthMax",
                    "Stat_HealthMax"
                );

                if (entityAttributeMaxHealth == null)
                {
                    Log?.LogError("Could not resolve max health EntityAttributes value.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log?.LogError("Could not parse EntityAttributes.Status_CurrentHealth: " + ex);
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

            mWeaponGetDamage = AccessTools.Method(weaponType, "GetDamage");
            mWeaponGetDamageType = AccessTools.Method(weaponType, "GetDamageType");

            mUpdateSprinting = AccessTools.Method(
                extendedWalkerType,
                "UpdateSprinting"
            );

            mToggleSprint = AccessTools.Method(
                extendedWalkerType,
                "ToggleSprint",
                new Type[] { typeof(bool) }
            );

            mMoverSetVelocity = AccessTools.Method(
                moverType,
                "SetVelocity",
                new Type[] { typeof(Vector3) }
            );

            mPlayerDirectionLooking = AccessTools.Method(
                playerType,
                "DirectionLooking",
                new Type[] { typeof(bool) }
            );

            mUnitReceiveDamageWithIDamager = FindUnitReceiveDamageWithIDamager();
            mUnitReceiveDamageWithDamageSourceData = FindUnitReceiveDamageWithDamageSourceData();

            if (mUnitReceiveDamageWithIDamager != null)
            {
                ParameterInfo[] parameters = mUnitReceiveDamageWithIDamager.GetParameters();
                hitmeshDataType = parameters[3].ParameterType;
                hitmeshDataDefault = GetHitmeshDataDefault(hitmeshDataType);
            }

            if (mUnitReceiveDamageWithDamageSourceData != null)
            {
                Type damageSourceDataType =
                    mUnitReceiveDamageWithDamageSourceData.GetParameters()[2].ParameterType;

                fDamageSourceDataIsPlayer = AccessTools.Field(
                    damageSourceDataType,
                    "isPlayer"
                );

                fDamageSourceDataSourceUnit = AccessTools.Field(
                    damageSourceDataType,
                    "sourceUnit"
                );

                fDamageSourceDataSourceWeapon = AccessTools.Field(
                    damageSourceDataType,
                    "sourceWeapon"
                );
            }

            pIsMelee = AccessTools.Property(weaponType, "IsMelee");
            pAnimator = FindPropertyRecursive(weaponType, "Animator");
            pSprintAction = FindPropertyRecursive(extendedWalkerType, "sprintAction");
            pFallingEnabled = FindPropertyRecursive(extendedWalkerType, "fallingEnabled");
            pSourceName = FindPropertyRecursive(weaponType, "SourceName");

            pUnitState = FindPropertyRecursive(unitType, "UnitState");
            pUnitIsPlayer = FindPropertyRecursive(unitType, "isPlayer");
            pUnitStats = FindPropertyRecursive(unitType, "Stats");

            if (npcType != null)
            {
                pNpcIsProtectedNpc = FindPropertyRecursive(npcType, "IsProtectedNpc");
            }

            mEntityStatsModifyStatus = AccessTools.Method(
                entityStatsType,
                "ModifyStatus",
                new Type[]
                {
                    entityAttributesType,
                    typeof(float),
                    typeof(bool)
                }
            );

            mEntityStatsGetStatus = AccessTools.Method(
    entityStatsType,
    "GetStatus",
    new Type[]
    {
        entityAttributesType
    }
);

            mEntityStatsGetAttribute = AccessTools.Method(
                entityStatsType,
                "GetAttribute",
                new Type[]
                {
        entityAttributesType
                }
            );

            fAltFireAction = FindFieldRecursive(equipmentManagerType, "altFireAction");
            fMeleeFireAction = FindFieldRecursive(equipmentManagerType, "meleeFireAction");
            fMeleeFireActionAlternative = FindFieldRecursive(equipmentManagerType, "meleeFireActionAlternative");

            fCurrentHoldable = FindFieldRecursive(equipmentManagerType, "currentHoldable");
            fEquipmentManager = FindFieldRecursive(weaponType, "equipmentManager");
            fWalkerEquipmentManager = FindFieldRecursive(extendedWalkerType, "equipmentManager");

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

            fWeaponDefinition = FindFieldRecursive(
                weaponType,
                "weaponDefinition",
                "<weaponDefinition>k__BackingField"
            );

            fUnitState = FindFieldRecursive(
                unitType,
                "UnitState",
                "<UnitState>k__BackingField"
            );

            fUnitIsPlayer = FindFieldRecursive(
                unitType,
                "isPlayer",
                "<isPlayer>k__BackingField"
            );

            fPlayerCamera = FindFieldRecursive(
                playerType,
                "playerCamera"
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
                   Require(mWeaponGetDamage, "Weapon.GetDamage()") &&
                   Require(entityAttributeMaxHealth, "EntityAttributes max health") &&
Require(mEntityStatsGetStatus, "EntityStats.GetStatus(EntityAttributes)") &&
                   Require(mWeaponGetDamageType, "Weapon.GetDamageType()") &&
                   Require(mUpdateSprinting, "ExtendedAdvancedWalkerController.UpdateSprinting()") &&
                   Require(mToggleSprint, "ExtendedAdvancedWalkerController.ToggleSprint(bool)") &&
                   Require(mMoverSetVelocity, "Mover.SetVelocity(Vector3)") &&
                   Require(mUnitReceiveDamageWithIDamager, "Unit.ReceiveDamage(float, DamageTypes, IDamager, Hitmesh.Data, Vector3?)") &&
                   Require(mUnitReceiveDamageWithDamageSourceData, "Unit.ReceiveDamage(float, DamageTypes, DamageSourceData, Hitmesh.Data, Vector3?)") &&
                   Require(hitmeshDataType, "Hitmesh.Data parameter type") &&
                   Require(hitmeshDataDefault, "Hitmesh.Data.Default") &&
                   Require(fDamageSourceDataIsPlayer, "DamageSourceData.isPlayer") &&
                   Require(fDamageSourceDataSourceUnit, "DamageSourceData.sourceUnit") &&
                   Require(pIsMelee, "Weapon.IsMelee") &&
                   Require(pAnimator, "Holdable.Animator") &&
                   Require(pSprintAction, "ExtendedAdvancedWalkerController.sprintAction") &&
                   Require(pUnitStats, "Unit.Stats") &&
                   Require(mEntityStatsModifyStatus, "EntityStats.ModifyStatus(EntityAttributes, float, bool)") &&
                   Require(entityAttributeStatusCurrentHealth, "EntityAttributes.Status_CurrentHealth") &&
                   Require(fAltFireAction, "EquipmentManager.altFireAction") &&
                   Require(fMeleeFireAction, "EquipmentManager.meleeFireAction") &&
                   Require(fMeleeFireActionAlternative, "EquipmentManager.meleeFireActionAlternative") &&
                   Require(fCurrentHoldable, "EquipmentManager.currentHoldable") &&
                   Require(fEquipmentManager, "Weapon.equipmentManager") &&
                   Require(fWalkerEquipmentManager, "ExtendedAdvancedWalkerController.equipmentManager") &&
                   Require(fIsInMeleeCharge, "EquipmentManager.isInMeleeCharge") &&
                   Require(fMeleePressed, "EquipmentManager.meleePressed") &&
                   Require(fAlternativeMeleePressed, "EquipmentManager.alternativeMeleePressed") &&
                   Require(fAimingInputHeld, "EquipmentManager.AimingInputHeld") &&
                   Require(fMeleeInputCooldown, "EquipmentManager.meleeInputCooldown");
        }

        private static bool Require(object member, string name)
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

        private static MethodInfo FindUnitReceiveDamageWithIDamager()
        {
            MethodInfo[] methods = unitType.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method.Name != "ReceiveDamage")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length != 5)
                    continue;

                if (parameters[0].ParameterType != typeof(float))
                    continue;

                if (parameters[2].ParameterType.Name != "IDamager")
                    continue;

                if (parameters[4].ParameterType != typeof(Vector3?))
                    continue;

                return method;
            }

            return null;
        }

        private static MethodInfo FindUnitReceiveDamageWithDamageSourceData()
        {
            MethodInfo[] methods = unitType.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];

                if (method.Name != "ReceiveDamage")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length != 5)
                    continue;

                if (parameters[0].ParameterType != typeof(float))
                    continue;

                if (parameters[2].ParameterType.Name != "DamageSourceData")
                    continue;

                if (parameters[4].ParameterType != typeof(Vector3?))
                    continue;

                return method;
            }

            return null;
        }

        private static object GetHitmeshDataDefault(Type type)
        {
            if (type == null)
                return null;

            FieldInfo field = AccessTools.Field(type, "Default");

            if (field != null)
                return field.GetValue(null);

            PropertyInfo property = AccessTools.Property(type, "Default");

            if (property != null)
                return property.GetValue(null, null);

            try
            {
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
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

        private static bool IsModActive()
        {
            return EnableMod != null && EnableMod.Value;
        }

        private static ToggleState GetToggleState(object equipmentManager)
        {
            return ToggleStates.GetValue(
                equipmentManager,
                _ => new ToggleState()
            );
        }

        private static KatanaDashState GetDashState(object walkerController)
        {
            return DashStates.GetValue(
                walkerController,
                _ => new KatanaDashState()
            );
        }

        private static void HandleAimInputPrefix(
            object __instance,
            ref bool holdingMeleeAction
        )
        {
            if (!IsModActive())
                return;

            ToggleState state = GetToggleState(__instance);

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

            ToggleState state = GetToggleState(__instance);

            UpdateMeleeHeldEdgeState(__instance, state, holdingMeleeAction);

            if (state.SuppressMeleeUntilReleased)
            {
                if (state.MeleeHeldThisFrame)
                {
                    return false;
                }

                state.SuppressMeleeUntilReleased = false;
                ClearTapHoldState(state, false);

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

            if (!state.IsToggled && EnableTapHoldMeleeKey != null && EnableTapHoldMeleeKey.Value)
            {
                bool prefixResult;

                if (HandleTapHoldMeleeInput(__instance, state, out prefixResult))
                {
                    return prefixResult;
                }
            }

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

            if (state.IsToggled)
            {
                MaintainToggledMelee(__instance, state);
                return false;
            }

            return true;
        }

        private static bool PullTriggerPrefix(object __instance)
        {
            if (!IsModActive())
                return true;

            ToggleState state = GetToggleState(__instance);

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
            ClearTapHoldState(state, false);

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

            ToggleState state = GetToggleState(__instance);

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

            ToggleState state = GetToggleState(equipmentManager);

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

            ToggleState state = GetToggleState(equipmentManager);

            if (state.SheatheAfterAttack)
            {
                state.AttackInProgress = false;
                state.SheatheAfterAttack = false;
                state.IsToggled = false;
                state.SuppressMeleeUntilReleased = true;
                state.NextChargeAttemptTime = 0f;

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

        private static bool UpdateSprintingPrefix(object __instance)
        {
            if (!IsModActive() || !EnableKatanaDash.Value)
                return true;

            KatanaDashState dashState = GetDashState(__instance);

            object equipmentManager = GetEquipmentManagerFromWalker(__instance);

            if (equipmentManager == null)
                return true;

            object katana;

            if (!IsKatanaDashContext(__instance, equipmentManager, out katana))
            {
                dashState.ForcedSprintActive = false;
                return true;
            }

            lastKnownWalkerController = __instance;

            InputAction sprintAction = GetSprintAction(__instance);

            if (WasPerformedThisFrame(sprintAction))
            {
                TryStartKatanaDash(__instance, equipmentManager, katana);
            }

            dashState.ForcedSprintActive = true;
            InvokeToggleSprint(__instance, true);

            return false;
        }

        private static void MoverSetVelocityPrefix(
            object __instance,
            ref Vector3 _velocity
        )
        {
            if (!IsModActive() || !EnableKatanaDash.Value)
                return;

            Component moverComponent = __instance as Component;

            if (moverComponent == null)
                return;

            Component walker = moverComponent.GetComponent(extendedWalkerType);

            if (walker == null)
                return;

            KatanaDashState dashState = GetDashState(walker);

            if (dashState.IsDashing)
            {
                UpdateKatanaDash(walker, dashState);

                if (dashState.IsDashing)
                {
                    float speed = GetDashSpeed();
                    _velocity = dashState.Direction * speed;
                    return;
                }
            }

            if (dashState.PostHangActive)
            {
                ApplyPostDashHangVelocity(walker, dashState, ref _velocity);
            }
        }

        private static void UnitReceiveDamageDamageSourcePrefix(
            object __instance,
            ref bool __state
        )
        {
            __state = false;

            if (!IsModActive())
                return;

            if (RefreshCooldownOnPlayerKill == null ||
                !RefreshCooldownOnPlayerKill.Value)
            {
                return;
            }

            if (__instance == null)
                return;

            if (IsDeadUnit(__instance))
                return;

            if (!IsKilledUnitEligibleForDashRefresh(__instance))
                return;

            __state = true;
        }

        private static void UnitReceiveDamageDamageSourcePostfix(
            object __instance,
            bool __state,
            object[] __args
        )
        {
            if (!__state)
                return;

            if (__instance == null)
                return;

            if (!IsDeadUnit(__instance))
                return;

            if (!IsKilledUnitEligibleForDashRefresh(__instance))
                return;

            if (__args == null || __args.Length < 3)
                return;

            object damageSourceData = __args[2];

            object sourceUnit;

            if (!IsPlayerDamageSource(damageSourceData, out sourceUnit))
                return;

            RefreshKatanaDashCooldownAndHealForKill(sourceUnit, __instance);
        }

        private static bool IsPlayerDamageSource(
            object damageSourceData,
            out object sourceUnit
        )
        {
            sourceUnit = null;

            if (damageSourceData == null)
                return false;

            try
            {
                object isPlayerValue = fDamageSourceDataIsPlayer.GetValue(damageSourceData);

                bool isPlayer = isPlayerValue is bool && (bool)isPlayerValue;

                if (!isPlayer)
                    return false;

                if (fDamageSourceDataSourceUnit != null)
                {
                    sourceUnit = fDamageSourceDataSourceUnit.GetValue(damageSourceData);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to read DamageSourceData: " + ex);
                return false;
            }
        }

        private static void RefreshKatanaDashCooldownAndHealForKill(
            object sourceUnit,
            object killedUnit
        )
        {
            object walkerController = null;

            Component sourceUnitComponent = sourceUnit as Component;

            if (sourceUnitComponent != null)
            {
                walkerController = sourceUnitComponent.GetComponent(extendedWalkerType);
            }

            if (walkerController == null)
            {
                walkerController = lastKnownWalkerController;
            }

            if (walkerController == null)
                return;

            object equipmentManager = GetEquipmentManagerFromWalker(walkerController);

            if (equipmentManager == null)
                return;

            if (RefreshRequiresKatanaStance != null &&
                RefreshRequiresKatanaStance.Value)
            {
                object katana;

                if (!IsKatanaDashHudContext(
                    walkerController,
                    equipmentManager,
                    out katana
                ))
                {
                    return;
                }
            }

            KatanaDashState dashState = GetDashState(walkerController);

            float cooldownLeftBefore = Mathf.Max(0f, dashState.NextDashTime - Time.time);
            dashState.NextDashTime = Time.time;

            dashState.KillRefreshFeedbackEndTime = Time.time + GetDashKillRefreshFeedbackDuration();

            LastActualHealAmount = 0f;

            bool healed = TryHealPlayerFromEnemyKill(sourceUnit, killedUnit);

            if (healed && LastActualHealAmount > 0f)
            {
                dashState.HealFeedbackAmount = LastActualHealAmount;
                dashState.HealFeedbackEndTime = Time.time + GetDashHealFeedbackDuration();
            }

            if (LogDash != null && LogDash.Value)
            {
                Log?.LogInfo(
                    "Katana Dash cooldown refreshed by kill. Previous cooldown left: "
                    + cooldownLeftBefore.ToString("F2")
                    + "s healed="
                    + healed
                    + " killedUnit="
                    + killedUnit
                );
            }
        }

        private static bool TryHealPlayerFromEnemyKill(
            object sourceUnit,
            object killedUnit
        )
        {
            if (HealOnEnemyKill == null || !HealOnEnemyKill.Value)
                return false;

            float healAmount = GetHealAmountOnEnemyKill();

            if (healAmount <= 0f)
                return false;

            if (!HasNpcComponent(killedUnit))
                return false;

            if (IsPlayerUnit(killedUnit))
                return false;

            if (IsProtectedNpc(killedUnit))
                return false;

            if (sourceUnit == null)
                return false;

            if (!IsPlayerUnit(sourceUnit))
                return false;

            return HealUnit(sourceUnit, healAmount);
        }

        private static bool HealUnit(object unit, float amount)
        {
            if (unit == null || amount <= 0f)
                return false;

            try
            {
                object stats = null;

                if (pUnitStats != null)
                {
                    stats = pUnitStats.GetValue(unit, null);
                }

                if (stats == null)
                    return false;

                float currentHealth = GetEntityStatValue(
                    stats,
                    entityAttributeStatusCurrentHealth,
                    preferStatus: true
                );

                float maxHealth = GetEntityStatValue(
                    stats,
                    entityAttributeMaxHealth,
                    preferStatus: false
                );

                if (maxHealth <= 0f)
                    return false;

                float missingHealth = Mathf.Max(0f, maxHealth - currentHealth);
                float actualHeal = Mathf.Min(amount, missingHealth);

                if (actualHeal <= 0f)
                    return false;

                mEntityStatsModifyStatus.Invoke(
                    stats,
                    new object[]
                    {
                entityAttributeStatusCurrentHealth,
                actualHeal,
                false
                    }
                );

                if (LogDash != null && LogDash.Value)
                {
                    Log?.LogInfo(
                        "Healed player for "
                        + actualHeal.ToString("F1")
                        + " on enemy kill. Current="
                        + currentHealth.ToString("F1")
                        + " Max="
                        + maxHealth.ToString("F1")
                    );
                }

                LastActualHealAmount = actualHeal;
                return true;
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to heal player on enemy kill: " + ex);
                return false;
            }
        }

        private static bool IsKilledUnitEligibleForDashRefresh(object unit)
        {
            if (unit == null)
                return false;

            if (IsPlayerUnit(unit))
                return false;

            if (IsProtectedNpc(unit))
                return false;

            if (HasNpcComponent(unit))
                return true;

            return RefreshCooldownOnNonNpcUnitKill != null &&
                   RefreshCooldownOnNonNpcUnitKill.Value;
        }

        private static bool HasNpcComponent(object unit)
        {
            if (unit == null || npcType == null)
                return false;

            Component component = unit as Component;

            if (component == null)
                return false;

            Component npc = component.GetComponent(npcType);

            return npc != null;
        }

        private static void ToggleOn(object equipmentManager, ToggleState state)
        {
            state.IsToggled = true;
            state.AttackInProgress = false;
            state.SheatheAfterAttack = false;
            state.SuppressMeleeUntilReleased = false;
            state.NextChargeAttemptTime = Time.time + ClampRetryInterval();
            ClearTapHoldState(state, false);

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
            ClearTapHoldState(state, false);

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

        private static bool IsKatanaDashContext(
            object walkerController,
            object equipmentManager,
            out object katana
        )
        {
            katana = null;

            if (walkerController == null || equipmentManager == null)
                return false;

            if (!ToggleMeleeStancePlugin.IsMeleeStanceActive(equipmentManager) ||
                ToggleMeleeStancePlugin.IsAttackInProgress(equipmentManager) ||
                ToggleMeleeStancePlugin.IsSheatheAfterAttack(equipmentManager))
            {
                return false;
            }

            object currentHoldable = GetCurrentHoldable(equipmentManager);

            if (!IsKatanaWeapon(currentHoldable))
                return false;

            if (!ToggleMeleeStancePlugin.IsMeleeChargeActive(equipmentManager))
                return false;

            katana = currentHoldable;
            return true;
        }

        private static bool IsKatanaDashHudContext(
            object walkerController,
            object equipmentManager,
            out object katana
        )
        {
            katana = null;

            if (walkerController == null || equipmentManager == null)
                return false;

            if (!ToggleMeleeStancePlugin.IsMeleeStanceActive(equipmentManager) ||
                ToggleMeleeStancePlugin.IsSheatheAfterAttack(equipmentManager))
            {
                return false;
            }

            object currentHoldable = GetCurrentHoldable(equipmentManager);

            if (!IsKatanaWeapon(currentHoldable))
                return false;

            katana = currentHoldable;
            return true;
        }

        private static void TryStartKatanaDash(
            object walkerController,
            object equipmentManager,
            object katana
        )
        {
            if (walkerController == null ||
                equipmentManager == null ||
                katana == null)
            {
                return;
            }

            KatanaDashState dashState = GetDashState(walkerController);

            EndPostDashHang(walkerController, dashState);

            if (dashState.IsDashing)
                return;

            if (Time.time < dashState.NextDashTime)
            {
                if (LogDash.Value)
                {
                    Log?.LogInfo(
                        "Katana Dash is cooling down: "
                        + Mathf.Max(0f, dashState.NextDashTime - Time.time).ToString("F2")
                        + "s"
                    );
                }

                return;
            }

            Vector3 direction = GetLookingDirection(walkerController);

            if (direction.sqrMagnitude < 0.0001f)
                return;

            direction.Normalize();

            Component walkerComponent = walkerController as Component;

            if (walkerComponent == null)
                return;

            dashState.IsDashing = true;
            dashState.DashEndTime = Time.time + GetDashDuration();
            dashState.NextDashTime = Time.time + GetDashCooldown();
            dashState.Direction = direction;
            dashState.LastPosition = walkerComponent.transform.position;
            dashState.KatanaWeapon = katana;
            dashState.HitUnitIds.Clear();

            if (LogDash.Value)
            {
                Log?.LogInfo(
                    "Katana Dash started. direction="
                    + direction
                    + " duration="
                    + GetDashDuration().ToString("F2")
                    + " distance="
                    + GetDashDistance().ToString("F2")
                );
            }
        }

        private static void UpdateKatanaDash(
            Component walkerComponent,
            KatanaDashState dashState
        )
        {
            if (walkerComponent == null || dashState == null)
                return;

            Vector3 currentPosition = walkerComponent.transform.position;

            ProcessDashHits(walkerComponent, dashState, dashState.LastPosition, currentPosition);

            dashState.LastPosition = currentPosition;

            if (Time.time >= dashState.DashEndTime)
            {
                dashState.IsDashing = false;
                BeginPostDashHang(walkerComponent, dashState);

                if (LogDash.Value)
                {
                    Log?.LogInfo("Katana Dash ended. Post hang started.");
                }
            }
        }

        private static void BeginPostDashHang(
            Component walkerComponent,
            KatanaDashState dashState
        )
        {
            if (walkerComponent == null || dashState == null)
                return;

            float duration = GetDashPostHangDuration();

            if (duration <= 0f)
            {
                EndPostDashHang(walkerComponent, dashState);
                return;
            }

            dashState.PostHangActive = true;
            dashState.PostHangEndTime = Time.time + duration;

            if (SuppressFallingAnimationDuringPostHang.Value)
            {
                SaveAndSetFallingEnabled(walkerComponent, dashState, false);
            }
        }

        private static void ApplyPostDashHangVelocity(
            Component walkerComponent,
            KatanaDashState dashState,
            ref Vector3 velocity
        )
        {
            if (walkerComponent == null || dashState == null)
                return;

            if (Time.time >= dashState.PostHangEndTime)
            {
                EndPostDashHang(walkerComponent, dashState);
                return;
            }

            Vector3 up = walkerComponent.transform.up;

            float verticalSpeed = Vector3.Dot(velocity, up);
            float maxDownwardSpeed = GetDashPostHangMaxDownwardSpeed();

            if (verticalSpeed < -maxDownwardSpeed)
            {
                velocity += up * (-maxDownwardSpeed - verticalSpeed);
            }
        }

        private static void EndPostDashHang(
            object walkerController,
            KatanaDashState dashState
        )
        {
            if (dashState == null)
                return;

            Component walkerComponent = walkerController as Component;

            if (walkerComponent != null)
            {
                RestoreFallingEnabled(walkerComponent, dashState);
            }

            dashState.PostHangActive = false;
            dashState.PostHangEndTime = 0f;
        }

        private static void SaveAndSetFallingEnabled(
            Component walkerComponent,
            KatanaDashState dashState,
            bool value
        )
        {
            if (walkerComponent == null ||
                dashState == null ||
                pFallingEnabled == null)
            {
                return;
            }

            try
            {
                if (!dashState.FallingEnabledWasSaved)
                {
                    object currentValue = pFallingEnabled.GetValue(walkerComponent, null);

                    if (currentValue is bool)
                    {
                        dashState.PreviousFallingEnabled = (bool)currentValue;
                        dashState.FallingEnabledWasSaved = true;
                    }
                }

                pFallingEnabled.SetValue(walkerComponent, value, null);
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to set fallingEnabled during post dash hang: " + ex);
            }
        }

        private static void RestoreFallingEnabled(
            Component walkerComponent,
            KatanaDashState dashState
        )
        {
            if (walkerComponent == null ||
                dashState == null ||
                pFallingEnabled == null ||
                !dashState.FallingEnabledWasSaved)
            {
                return;
            }

            try
            {
                pFallingEnabled.SetValue(
                    walkerComponent,
                    dashState.PreviousFallingEnabled,
                    null
                );
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to restore fallingEnabled after post dash hang: " + ex);
            }
            finally
            {
                dashState.FallingEnabledWasSaved = false;
                dashState.PreviousFallingEnabled = true;
            }
        }

        private static void ProcessDashHits(
            Component walkerComponent,
            KatanaDashState dashState,
            Vector3 start,
            Vector3 end
        )
        {
            if (walkerComponent == null ||
                dashState == null ||
                dashState.KatanaWeapon == null)
            {
                return;
            }

            float radius = GetDashHitRadius();

            Collider[] colliders;

            if ((end - start).sqrMagnitude <= 0.0001f)
            {
                colliders = Physics.OverlapSphere(
                    start,
                    radius,
                    ~0,
                    QueryTriggerInteraction.Collide
                );
            }
            else
            {
                colliders = Physics.OverlapCapsule(
                    start,
                    end,
                    radius,
                    ~0,
                    QueryTriggerInteraction.Collide
                );
            }

            if (colliders == null || colliders.Length == 0)
                return;

            object playerUnit = GetComponent(walkerComponent, unitType);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];

                if (collider == null)
                    continue;

                object unit = collider.GetComponentInParent(unitType);

                if (unit == null)
                    continue;

                if (ReferenceEquals(unit, playerUnit))
                    continue;

                if (IsPlayerUnit(unit))
                    continue;

                if (IsDeadUnit(unit))
                    continue;

                if (IsProtectedNpc(unit))
                    continue;

                int unitId = ((UnityEngine.Object)unit).GetInstanceID();

                if (DashHitEachUnitOnce.Value && dashState.HitUnitIds.Contains(unitId))
                    continue;

                Vector3 hitPoint = collider.ClosestPoint(end);

                if (ApplyKatanaDashDamage(unit, dashState.KatanaWeapon, hitPoint))
                {
                    dashState.HitUnitIds.Add(unitId);

                    if (LogDash.Value)
                    {
                        Log?.LogInfo("Katana Dash hit unit: " + unit);
                    }
                }
            }
        }

        private static bool ApplyKatanaDashDamage(
            object unit,
            object katana,
            Vector3 hitPoint
        )
        {
            if (unit == null || katana == null)
                return false;

            try
            {
                float baseDamage = Convert.ToSingle(mWeaponGetDamage.Invoke(katana, null));
                float damage = baseDamage * GetDashDamageMultiplier();
                object damageType = mWeaponGetDamageType.Invoke(katana, null);

                object[] args = new object[]
                {
                    damage,
                    damageType,
                    katana,
                    hitmeshDataDefault,
                    new Vector3?(hitPoint)
                };

                object result = mUnitReceiveDamageWithIDamager.Invoke(unit, args);

                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to apply Katana Dash damage: " + ex);
                return false;
            }
        }

        private static Vector3 GetLookingDirection(object walkerController)
        {
            Component walkerComponent = walkerController as Component;

            if (walkerComponent == null)
                return Vector3.zero;

            object player = GetComponent(walkerComponent, playerType);

            if (player != null && mPlayerDirectionLooking != null)
            {
                try
                {
                    object result = mPlayerDirectionLooking.Invoke(
                        player,
                        new object[] { false }
                    );

                    if (result is Vector3)
                        return (Vector3)result;
                }
                catch
                {
                }
            }

            if (player != null && fPlayerCamera != null)
            {
                try
                {
                    Camera camera = fPlayerCamera.GetValue(player) as Camera;

                    if (camera != null)
                        return camera.transform.forward;
                }
                catch
                {
                }
            }

            if (Camera.main != null)
                return Camera.main.transform.forward;

            return walkerComponent.transform.forward;
        }

        private static bool IsKatanaWeapon(object weapon)
        {
            if (!IsMeleeWeapon(weapon))
                return false;

            string keywords = KatanaNameKeywords.Value;

            if (string.IsNullOrEmpty(keywords))
                return false;

            string[] parts = keywords.Split(',');

            string text = GetWeaponSearchText(weapon);

            for (int i = 0; i < parts.Length; i++)
            {
                string keyword = parts[i].Trim();

                if (string.IsNullOrEmpty(keyword))
                    continue;

                if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string GetWeaponSearchText(object weapon)
        {
            if (weapon == null)
                return string.Empty;

            string text = string.Empty;

            text += weapon.ToString();
            text += " ";

            Component component = weapon as Component;

            if (component != null)
            {
                text += component.name;
                text += " ";
                text += component.gameObject.name;
                text += " ";
            }

            if (pSourceName != null)
            {
                try
                {
                    object sourceName = pSourceName.GetValue(weapon, null);

                    if (sourceName != null)
                    {
                        text += sourceName.ToString();
                        text += " ";
                    }
                }
                catch
                {
                }
            }

            if (fWeaponDefinition != null)
            {
                try
                {
                    object definition = fWeaponDefinition.GetValue(weapon);

                    if (definition != null)
                    {
                        text += definition.ToString();
                        text += " ";
                    }
                }
                catch
                {
                }
            }

            return text;
        }

        private static object GetEquipmentManagerFromWalker(object walkerController)
        {
            if (walkerController == null || fWalkerEquipmentManager == null)
                return null;

            return fWalkerEquipmentManager.GetValue(walkerController);
        }

        private static object TryParseEntityAttribute(params string[] names)
        {
            if (entityAttributesType == null || names == null)
                return null;

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];

                if (string.IsNullOrEmpty(name))
                    continue;

                try
                {
                    if (Enum.IsDefined(entityAttributesType, name))
                    {
                        return Enum.Parse(entityAttributesType, name);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static InputAction GetSprintAction(object walkerController)
        {
            if (walkerController == null || pSprintAction == null)
                return null;

            try
            {
                return pSprintAction.GetValue(walkerController, null) as InputAction;
            }
            catch
            {
                return null;
            }
        }

        private static void InvokeToggleSprint(object walkerController, bool state)
        {
            if (walkerController == null || mToggleSprint == null)
                return;

            try
            {
                mToggleSprint.Invoke(walkerController, new object[] { state });
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to invoke ToggleSprint: " + ex);
            }
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

        private static float GetDashCooldown()
        {
            return Mathf.Clamp(DashCooldown.Value, 0.1f, 60f);
        }

        private static float GetDashDistance()
        {
            return Mathf.Clamp(DashDistance.Value, 0.5f, 50f);
        }

        private static float GetDashDuration()
        {
            return Mathf.Clamp(DashDuration.Value, 0.03f, 2f);
        }

        private static float GetDashSpeed()
        {
            return GetDashDistance() / GetDashDuration();
        }

        private static float GetDashHitRadius()
        {
            return Mathf.Clamp(DashHitRadius.Value, 0.1f, 5f);
        }

        private static float GetEntityStatValue(
    object stats,
    object attribute,
    bool preferStatus
)
        {
            if (stats == null || attribute == null)
                return 0f;

            if (preferStatus && mEntityStatsGetStatus != null)
            {
                try
                {
                    object value = mEntityStatsGetStatus.Invoke(
                        stats,
                        new object[] { attribute }
                    );

                    return Convert.ToSingle(value);
                }
                catch
                {
                }
            }

            if (mEntityStatsGetAttribute != null)
            {
                try
                {
                    object value = mEntityStatsGetAttribute.Invoke(
                        stats,
                        new object[] { attribute }
                    );

                    return Convert.ToSingle(value);
                }
                catch
                {
                }
            }

            if (!preferStatus && mEntityStatsGetStatus != null)
            {
                try
                {
                    object value = mEntityStatsGetStatus.Invoke(
                        stats,
                        new object[] { attribute }
                    );

                    return Convert.ToSingle(value);
                }
                catch
                {
                }
            }

            return 0f;
        }

        private static float GetDashDamageMultiplier()
        {
            return Mathf.Clamp(DashDamageMultiplier.Value, 0f, 20f);
        }

        private static float GetHealAmountOnEnemyKill()
        {
            if (HealAmountOnEnemyKill == null)
                return 5f;

            float value = HealAmountOnEnemyKill.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 5f;

            return Mathf.Clamp(value, 0f, 999f);
        }

        private static float GetDashPostHangDuration()
        {
            if (DashPostHangDuration == null)
                return 0.12f;

            float value = DashPostHangDuration.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.12f;

            return Mathf.Clamp(value, 0f, 0.5f);
        }

        private static float GetDashPostHangMaxDownwardSpeed()
        {
            if (DashPostHangMaxDownwardSpeed == null)
                return 0.5f;

            float value = DashPostHangMaxDownwardSpeed.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.5f;

            return Mathf.Clamp(value, 0f, 20f);
        }

        private static float GetDashKillRefreshFeedbackDuration()
        {
            if (DashKillRefreshFeedbackDuration == null)
                return 0.45f;

            float value = DashKillRefreshFeedbackDuration.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.45f;

            return Mathf.Clamp(value, 0f, 3f);
        }

        private static float GetDashHealFeedbackDuration()
        {
            if (DashHealFeedbackDuration == null)
                return 0.85f;

            float value = DashHealFeedbackDuration.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.85f;

            return Mathf.Clamp(value, 0f, 3f);
        }

        private static bool HandleTapHoldMeleeInput(
            object equipmentManager,
            ToggleState state,
            out bool prefixResult
        )
        {
            prefixResult = true;

            if (state.LongPressPassThrough)
            {
                if (state.MeleeReleasedThisFrame || !state.MeleeHeldThisFrame)
                {
                    ClearTapHoldState(state, false);

                    if (LogStateChanges.Value)
                    {
                        Log?.LogInfo("Long melee input released. Original melee behavior should finish.");
                    }
                }

                prefixResult = true;
                return true;
            }

            if (state.PendingTapHold)
            {
                float heldTime = Time.time - state.MeleePressStartTime;

                if (state.MeleeReleasedThisFrame || !state.MeleeHeldThisFrame)
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

                    ClearTapHoldState(state, false);
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

            if (state.MeleePressedThisFrame)
            {
                state.PendingTapHold = true;
                state.MeleePressStartTime = Time.time;

                prefixResult = false;
                return true;
            }

            return false;
        }

        private static void UpdateMeleeHeldEdgeState(
            object equipmentManager,
            ToggleState state,
            bool holdingMeleeAction
        )
        {
            bool held = holdingMeleeAction || IsMeleeHeld(equipmentManager);

            state.MeleePressedThisFrame = held && !state.WasMeleeHeldLastFrame;
            state.MeleeReleasedThisFrame = !held && state.WasMeleeHeldLastFrame;
            state.MeleeHeldThisFrame = held;
            state.WasMeleeHeldLastFrame = held;
        }

        private static void ClearTapHoldState(ToggleState state, bool preserveHeldEdge)
        {
            if (state == null)
                return;

            state.PendingTapHold = false;
            state.LongPressPassThrough = false;
            state.MeleePressStartTime = 0f;
            state.MeleePressedThisFrame = false;
            state.MeleeReleasedThisFrame = false;

            if (!preserveHeldEdge)
            {
                state.MeleeHeldThisFrame = false;
                state.WasMeleeHeldLastFrame = false;
            }
        }

        private static float GetMeleeToggleTapThreshold()
        {
            if (MeleeToggleTapThreshold == null)
                return 0.22f;

            float value = MeleeToggleTapThreshold.Value;

            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.22f;

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

            if (KeepKatanaStanceAfterExternalWeaponSwitch != null &&
                KeepKatanaStanceAfterExternalWeaponSwitch.Value)
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
            ClearTapHoldState(state, false);

            SetMeleePressed(equipmentManager, false);
            SetAlternativeMeleePressed(equipmentManager, false);

            if (LogStateChanges != null && LogStateChanges.Value)
            {
                Log?.LogInfo(
                    "Exited Dragonblade stance because current holdable is no longer melee. " +
                    "source=" + source +
                    " holdable=" + currentHoldable
                );
            }
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

        private static object GetComponent(Component component, Type type)
        {
            if (component == null || type == null)
                return null;

            return component.GetComponent(type);
        }

        private static bool IsPlayerUnit(object unit)
        {
            if (unit == null)
                return false;

            try
            {
                object value = null;

                if (pUnitIsPlayer != null)
                    value = pUnitIsPlayer.GetValue(unit, null);
                else if (fUnitIsPlayer != null)
                    value = fUnitIsPlayer.GetValue(unit);

                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDeadUnit(object unit)
        {
            if (unit == null)
                return true;

            try
            {
                object value = null;

                if (pUnitState != null)
                    value = pUnitState.GetValue(unit, null);
                else if (fUnitState != null)
                    value = fUnitState.GetValue(unit);

                if (value == null)
                    return false;

                return string.Equals(
                    value.ToString(),
                    "Dead",
                    StringComparison.OrdinalIgnoreCase
                );
            }
            catch
            {
                return false;
            }
        }

        private static bool IsProtectedNpc(object unit)
        {
            if (unit == null ||
                npcType == null ||
                pNpcIsProtectedNpc == null)
            {
                return false;
            }

            Component component = unit as Component;

            if (component == null)
                return false;

            Component npc = component.GetComponent(npcType);

            if (npc == null)
                return false;

            try
            {
                object value = pNpcIsProtectedNpc.GetValue(npc, null);

                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private static void DrawKatanaDashHud(
            KatanaDashState dashState,
            bool canUseDashNow
        )
        {
            EnsureDashHudTextures();

            float size = Mathf.Clamp(DashHudIconSize.Value, 48f, 128f);
            float panelWidth = size + 26f;
            float panelHeight = size + 42f;

            float x = Screen.width - panelWidth - 34f;
            float y = Screen.height - panelHeight - 34f;

            Rect panelRect = new Rect(x, y, panelWidth, panelHeight);
            Rect iconRect = new Rect(x + 13f, y + 10f, size, size);
            Rect keyRect = new Rect(x + 13f, y + 13f + size, size, 22f);

            float cooldownLeft = Mathf.Max(0f, dashState.NextDashTime - Time.time);
            float cooldownTotal = Mathf.Max(0.01f, GetDashCooldown());
            bool ready = cooldownLeft <= 0f && !dashState.IsDashing && canUseDashNow;
            float cooldownRatio = Mathf.Clamp01(cooldownLeft / cooldownTotal);

            bool refreshFeedbackActive = Time.time < dashState.KillRefreshFeedbackEndTime;
            bool healFeedbackActive = Time.time < dashState.HealFeedbackEndTime;

            Color oldColor = GUI.color;

            Color readyBorder = refreshFeedbackActive
                ? new Color(0.45f, 1f, 0.55f, 1f)
                : new Color(0.95f, 0.95f, 0.9f, 0.85f);

            DrawRect(new Rect(panelRect.x + 4f, panelRect.y + 4f, panelRect.width, panelRect.height), new Color(0f, 0f, 0f, 0.35f));
            DrawRect(panelRect, new Color(0.02f, 0.02f, 0.025f, 0.78f));

            DrawBorder(panelRect, refreshFeedbackActive ? 3f : 2f, ready ? readyBorder : new Color(0.45f, 0.45f, 0.45f, 0.85f));
            DrawBorder(new Rect(iconRect.x - 3f, iconRect.y - 3f, iconRect.width + 6f, iconRect.height + 6f), refreshFeedbackActive ? 3f : 2f, refreshFeedbackActive ? new Color(0.3f, 1f, 0.45f, 1f) : new Color(0f, 0f, 0f, 0.9f));

            if (DashIconTexture != null)
            {
                GUI.color = ready || refreshFeedbackActive ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
                GUI.DrawTexture(iconRect, DashIconTexture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                DrawRect(iconRect, new Color(0.15f, 0.15f, 0.16f, 1f));
                DrawOutlinedLabel(iconRect, "DASH", 18, Color.white, TextAnchor.MiddleCenter);
            }

            if (!ready)
            {
                DrawRect(iconRect, new Color(0f, 0f, 0f, 0.45f));

                Rect cooldownCover = new Rect(
                    iconRect.x,
                    iconRect.y,
                    iconRect.width,
                    iconRect.height * cooldownRatio
                );

                DrawRect(cooldownCover, new Color(0f, 0f, 0f, 0.55f));

                string cooldownText;

                if (cooldownLeft > 0f)
                {
                    if (cooldownLeft >= 1f)
                    {
                        cooldownText = Mathf.CeilToInt(cooldownLeft).ToString();
                    }
                    else
                    {
                        cooldownText = cooldownLeft.ToString("F1");
                    }
                }
                else
                {
                    cooldownText = "—";
                }

                DrawOutlinedLabel(
                    iconRect,
                    cooldownText,
                    cooldownLeft >= 1f ? 34 : 28,
                    refreshFeedbackActive ? new Color(0.7f, 1f, 0.75f, 1f) : Color.white,
                    TextAnchor.MiddleCenter
                );
            }
            else
            {
                DrawBorder(iconRect, refreshFeedbackActive ? 3f : 2f, refreshFeedbackActive ? new Color(0.35f, 1f, 0.45f, 1f) : new Color(0.75f, 1f, 0.85f, 0.95f));
            }

            if (refreshFeedbackActive)
            {
                DrawOutlinedLabel(
                    new Rect(iconRect.x, iconRect.y - 22f, iconRect.width, 20f),
                    "RESET",
                    14,
                    new Color(0.45f, 1f, 0.55f, 1f),
                    TextAnchor.MiddleCenter
                );
            }

            if (healFeedbackActive)
            {
                DrawOutlinedLabel(
                    new Rect(iconRect.x, iconRect.y - 42f, iconRect.width, 20f),
                    "+" + dashState.HealFeedbackAmount.ToString("F0") + " HP",
                    15,
                    new Color(0.45f, 1f, 0.55f, 1f),
                    TextAnchor.MiddleCenter
                );
            }

            string keyLabel = GetDashKeyLabel();

            DrawRect(keyRect, ready ? new Color(0.1f, 0.1f, 0.1f, 0.9f) : new Color(0.05f, 0.05f, 0.05f, 0.9f));
            DrawBorder(keyRect, 1f, refreshFeedbackActive ? new Color(0.45f, 1f, 0.55f, 0.9f) : new Color(0.7f, 0.7f, 0.7f, 0.75f));

            DrawOutlinedLabel(
                keyRect,
                keyLabel,
                14,
                ready || refreshFeedbackActive ? Color.white : new Color(0.65f, 0.65f, 0.65f, 1f),
                TextAnchor.MiddleCenter
            );

            GUI.color = oldColor;
        }

        private static void EnsureDashHudTextures()
        {
            if (HudPixelTexture == null)
            {
                HudPixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                HudPixelTexture.name = "MeleeExpansion_HudPixel";
                HudPixelTexture.SetPixel(0, 0, Color.white);
                HudPixelTexture.Apply(false, true);
            }

            if (DashIconLoadAttempted)
                return;

            DashIconLoadAttempted = true;

            try
            {
                string fileName = DashIconFileName != null ? DashIconFileName.Value : "dash_icon.png";

                if (string.IsNullOrEmpty(fileName))
                    return;

                string path = Path.Combine(PluginDirectory, fileName);

                if (!File.Exists(path))
                {
                    Log?.LogWarning("Dash icon PNG not found: " + path);
                    return;
                }

                byte[] bytes = File.ReadAllBytes(path);

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "MeleeExpansion_DashIcon";

                if (!LoadImageReflective(texture, bytes, false))
                {
                    Log?.LogWarning("Failed to load dash icon PNG: " + path);
                    UnityEngine.Object.Destroy(texture);
                    return;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                DashIconTexture = texture;

                if (LogDash != null && LogDash.Value)
                {
                    Log?.LogInfo("Loaded dash icon PNG: " + path);
                }
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to load dash icon PNG: " + ex);
            }
        }

        private static bool LoadImageReflective(
            Texture2D texture,
            byte[] bytes,
            bool markNonReadable
        )
        {
            if (texture == null || bytes == null || bytes.Length == 0)
                return false;

            try
            {
                Type imageConversionType = AccessTools.TypeByName(
                    "UnityEngine.ImageConversion"
                );

                if (imageConversionType == null)
                {
                    Log?.LogError("Could not find UnityEngine.ImageConversion type.");
                    return false;
                }

                MethodInfo loadImageMethod = AccessTools.Method(
                    imageConversionType,
                    "LoadImage",
                    new Type[]
                    {
                        typeof(Texture2D),
                        typeof(byte[]),
                        typeof(bool)
                    }
                );

                if (loadImageMethod == null)
                {
                    Log?.LogError("Could not find ImageConversion.LoadImage(Texture2D, byte[], bool).");
                    return false;
                }

                object result = loadImageMethod.Invoke(
                    null,
                    new object[]
                    {
                        texture,
                        bytes,
                        markNonReadable
                    }
                );

                return result is bool && (bool)result;
            }
            catch (Exception ex)
            {
                Log?.LogError("Failed to load image through reflection: " + ex);
                return false;
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            if (HudPixelTexture == null)
                return;

            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, HudPixelTexture);
            GUI.color = oldColor;
        }

        private static void DrawBorder(Rect rect, float thickness, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void DrawOutlinedLabel(
            Rect rect,
            string text,
            int fontSize,
            Color color,
            TextAnchor alignment
        )
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = alignment;
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = color;

            GUIStyle outlineStyle = new GUIStyle(style);
            outlineStyle.normal.textColor = Color.black;

            Rect offset;

            offset = rect;
            offset.x -= 1f;
            GUI.Label(offset, text, outlineStyle);

            offset = rect;
            offset.x += 1f;
            GUI.Label(offset, text, outlineStyle);

            offset = rect;
            offset.y -= 1f;
            GUI.Label(offset, text, outlineStyle);

            offset = rect;
            offset.y += 1f;
            GUI.Label(offset, text, outlineStyle);

            GUI.Label(rect, text, style);
        }

        private static string GetDashKeyLabel()
        {
            if (DashHudUseActualSprintBinding != null &&
                DashHudUseActualSprintBinding.Value &&
                lastKnownWalkerController != null)
            {
                try
                {
                    InputAction action = GetSprintAction(lastKnownWalkerController);

                    if (action != null)
                    {
                        string binding = action.GetBindingDisplayString();

                        if (!string.IsNullOrEmpty(binding))
                        {
                            return binding.ToUpperInvariant();
                        }
                    }
                }
                catch
                {
                }
            }

            if (DashHudFallbackKeyLabel != null &&
                !string.IsNullOrEmpty(DashHudFallbackKeyLabel.Value))
            {
                return DashHudFallbackKeyLabel.Value.ToUpperInvariant();
            }

            return "SPRINT";
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
            public bool MeleeHeldThisFrame;
            public bool MeleePressedThisFrame;
            public bool MeleeReleasedThisFrame;
            public float MeleePressStartTime;
        }

        private sealed class SafeAnimatorState
        {
            public bool Valid;
            public int FullPathHash;
            public int ShortNameHash;
            public string ClipName;
        }
        private sealed class KatanaDashState
        {
            public bool IsDashing;
            public bool ForcedSprintActive;
            public float DashEndTime;
            public float NextDashTime;
            public Vector3 Direction;
            public Vector3 LastPosition;
            public object KatanaWeapon;
            public readonly HashSet<int> HitUnitIds = new HashSet<int>();

            public bool PostHangActive;
            public float PostHangEndTime;
            public bool FallingEnabledWasSaved;
            public bool PreviousFallingEnabled = true;

            public float KillRefreshFeedbackEndTime;
            public float HealFeedbackEndTime;
            public float HealFeedbackAmount;
        }
    }
}