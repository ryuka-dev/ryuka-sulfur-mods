using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Input;
using PerfectRandom.Sulfur.Core.Movement;
using PerfectRandom.Sulfur.Core.Stats;
using PerfectRandom.Sulfur.Core.Units;
using PerfectRandom.Sulfur.Core.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ryuka.Sulfur.DeadeyeInstinct
{
    internal enum AssistMode
    {
        Magnet = 0,
        Natural = 1,
        HardLock = 2
    }

    internal enum AssistPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Custom = 3
    }

    internal enum HardLockTargetPriority
    {
        Nearest = 0,
        LowestHealth = 1,
        WeakspotThenDistance = 2,
        ThreatWeighted = 3
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DeadeyeInstinctPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "ryuka.sulfur.deadeyeinstinct";
        private const string PluginName = "Deadeye Instinct";
        private const string PluginVersion = "1.2.5";

        internal static ManualLogSource Log;
        private Harmony harmony;
        private GameObject weakspotDebugOverlayObject;

        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<bool> DebugLogging;

        internal static ConfigEntry<AssistMode> Mode;
        internal static ConfigEntry<AssistPreset> MagnetPreset;
        internal static ConfigEntry<AssistPreset> NaturalPreset;

        internal static ConfigEntry<bool> EnableMouseConsumesRotationPullDelta;
        internal static ConfigEntry<bool> EnableContinuousRotationalAssist;

        internal static ConfigEntry<bool> RequirePlayerInput;
        internal static ConfigEntry<bool> CountLookInput;
        internal static ConfigEntry<bool> CountMoveInput;
        internal static ConfigEntry<float> LookInputThreshold;
        internal static ConfigEntry<float> MoveInputThreshold;

        internal static ConfigEntry<float> AssistCoefficient;
        internal static ConfigEntry<float> RotationDeltaPerSecond;
        internal static ConfigEntry<float> MaxRotationDeltaPerFrame;

        internal static ConfigEntry<float> MagnetCenterBias;
        internal static ConfigEntry<float> MagnetMovementOnlyScale;
        internal static ConfigEntry<float> MagnetSmoothing;
        internal static ConfigEntry<float> MagnetReleaseSmoothing;
        internal static ConfigEntry<bool> MagnetPreferWeakspot;
        internal static ConfigEntry<float> MagnetWeakspotBias;
        internal static ConfigEntry<bool> MagnetWeakspotOverrideOfficialPull;
        internal static ConfigEntry<float> MagnetEdgeSofteningPower;
        internal static ConfigEntry<float> MagnetOuterEdgeStrength;
        internal static ConfigEntry<float> MagnetSnapBackAnglePadding;
        internal static ConfigEntry<bool> MagnetAllowLookAwayEscape;
        internal static ConfigEntry<float> MagnetLookAwayThreshold;
        internal static ConfigEntry<float> MagnetLookAwayMinScale;
        internal static ConfigEntry<float> MagnetLookAwayCurvePower;
        internal static ConfigEntry<float> MagnetLookAwayMovementScale;

        internal static ConfigEntry<float> TargetSwitchFadeSeconds;
        internal static ConfigEntry<float> TargetSwitchMinScale;

        internal static ConfigEntry<float> OuterBubbleAngleDegrees;
        internal static ConfigEntry<float> InnerBubbleAngleDegrees;
        internal static ConfigEntry<float> BubbleCurvePower;
        internal static ConfigEntry<float> DistanceFalloffPower;

        internal static ConfigEntry<float> NaturalTowardAssist;
        internal static ConfigEntry<float> NaturalSideAssist;
        internal static ConfigEntry<float> NaturalAwayDamping;
        internal static ConfigEntry<float> NaturalMovementOnlyScale;
        internal static ConfigEntry<float> NaturalMaxInputFraction;
        internal static ConfigEntry<float> NaturalMinDeltaPerFrame;
        internal static ConfigEntry<float> NaturalSmoothing;
        internal static ConfigEntry<float> NaturalReleaseSmoothing;
        internal static ConfigEntry<float> NaturalDirectionSmoothing;
        internal static ConfigEntry<float> NaturalMinLookInput;
        internal static ConfigEntry<bool> NaturalKeepOfficialGamepadPull;
        internal static ConfigEntry<float> NaturalReleaseCurvePower;
        internal static ConfigEntry<float> NaturalOuterEdgeStrength;

        internal static ConfigEntry<bool> OverrideOfficialScanner;
        internal static ConfigEntry<float> OfficialMaxAssistDistance;
        internal static ConfigEntry<float> OfficialScanConeDegrees;
        internal static ConfigEntry<float> OfficialScoreThreshold;
        internal static ConfigEntry<float> OfficialDistanceVsDotBlend;
        internal static ConfigEntry<int> OfficialScanIntervalFrames;
        internal static ConfigEntry<float> ScannerKeepAliveStrength;

        internal static ConfigEntry<bool> InvertX;
        internal static ConfigEntry<bool> InvertY;

        internal static ConfigEntry<bool> EnableWeakspotDebugOverlay;
        internal static ConfigEntry<bool> WeakspotDebugOnlyTrackedTarget;
        internal static ConfigEntry<bool> WeakspotDebugShowAllPositiveShapes;
        internal static ConfigEntry<float> WeakspotDebugMinimumMultiplier;
        internal static ConfigEntry<float> WeakspotDebugLineWidth;
        internal static ConfigEntry<float> WeakspotDebugDepthOffset;

        internal static ConfigEntry<bool> EnableAimbot;
        internal static ConfigEntry<KeyCode> HoldDisableKey;

        internal static ConfigEntry<float> HardLockMaxDistance;
        internal static ConfigEntry<float> HardLockMaxDistanceCap;
        internal static ConfigEntry<bool> HardLockPreferWeakspot;
        internal static ConfigEntry<float> HardLockWeakspotBias;
        internal static ConfigEntry<float> HardLockRotationSpeed;
        internal static ConfigEntry<float> HardLockRotationGain;
        internal static ConfigEntry<float> HardLockMaxDeltaPerFrame;
        internal static ConfigEntry<bool> HardLockRecoilCompensation;
        internal static ConfigEntry<bool> HardLockRequireLineOfSight;
        internal static ConfigEntry<bool> HardLockRequireVisibleTarget;
        internal static ConfigEntry<HardLockTargetPriority> HardLockTargetPriorityMode;
        internal static ConfigEntry<float> HardLockDistanceWeight;
        internal static ConfigEntry<float> HardLockLowHealthWeight;
        internal static ConfigEntry<float> HardLockWeakspotWeight;
        internal static ConfigEntry<float> HardLockStickyTargetBonus;

        internal static ConfigEntry<bool> EnableAutoFire;
        internal static ConfigEntry<bool> AutoFireOnlyInHardLock;
        internal static ConfigEntry<bool> AutoFireRequireAligned;
        internal static ConfigEntry<float> AutoFireMaxAngleDegrees;
        internal static ConfigEntry<bool> AutoFireRequireWeaponReady;
        internal static ConfigEntry<bool> AutoFireRespectSemiAuto;
        internal static ConfigEntry<float> AutoFirePulseSeconds;
        internal static ConfigEntry<float> AutoFireReleaseSeconds;

        private void Awake()
        {
            Log = Logger;
            BindConfig();

            harmony = new Harmony(PluginGuid);

            MethodInfo aimAssistLateUpdate = AccessTools.Method(typeof(AimAssist), "LateUpdate");
            if (aimAssistLateUpdate != null)
            {
                harmony.Patch(
                    aimAssistLateUpdate,
                    prefix: new HarmonyMethod(typeof(AimAssistLateUpdatePatch), nameof(AimAssistLateUpdatePatch.Prefix)),
                    postfix: new HarmonyMethod(typeof(AimAssistLateUpdatePatch), nameof(AimAssistLateUpdatePatch.Postfix))
                );

                Logger.LogInfo("Patched AimAssist.LateUpdate().");
            }
            else
            {
                Logger.LogError("Could not find AimAssist.LateUpdate().");
            }

            MethodInfo inputReaderUpdate = AccessTools.Method(typeof(InputReader), "Update");
            if (inputReaderUpdate != null)
            {
                harmony.Patch(
                    inputReaderUpdate,
                    prefix: new HarmonyMethod(typeof(InputReaderUpdatePatch), nameof(InputReaderUpdatePatch.Prefix)),
                    transpiler: new HarmonyMethod(typeof(InputReaderUpdatePatch), nameof(InputReaderUpdatePatch.Transpiler))
                );

                Logger.LogInfo("Patched InputReader.Update().");
            }
            else
            {
                Logger.LogError("Could not find InputReader.Update().");
            }

            MethodInfo weaponLateUpdate = AccessTools.Method(typeof(Weapon), "LateUpdate");
            if (weaponLateUpdate != null)
            {
                harmony.Patch(
                    weaponLateUpdate,
                    prefix: new HarmonyMethod(typeof(WeaponLateUpdatePatch), nameof(WeaponLateUpdatePatch.Prefix))
                );

                Logger.LogInfo("Patched Weapon.LateUpdate().");
            }
            else
            {
                Logger.LogWarning("Could not find Weapon.LateUpdate(). AutoFire will be unavailable.");
            }

            MethodInfo cameraRecoilLateUpdate = AccessTools.Method(typeof(CameraRecoil), "LateUpdate");
            if (cameraRecoilLateUpdate != null)
            {
                harmony.Patch(
                    cameraRecoilLateUpdate,
                    postfix: new HarmonyMethod(typeof(CameraRecoilLateUpdatePatch), nameof(CameraRecoilLateUpdatePatch.Postfix))
                );

                Logger.LogInfo("Patched CameraRecoil.LateUpdate().");
            }
            else
            {
                Logger.LogWarning("Could not find CameraRecoil.LateUpdate(). HardLock recoil compensation will be unavailable.");
            }

            CreateWeakspotDebugOverlay();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }


        internal static AssistMode ActiveMode()
        {
            return Mode != null ? Mode.Value : AssistMode.Magnet;
        }

        internal static AssistPreset ActivePreset()
        {
            if (ActiveMode() == AssistMode.Natural)
            {
                return NaturalPreset != null ? NaturalPreset.Value : AssistPreset.Medium;
            }

            return MagnetPreset != null ? MagnetPreset.Value : AssistPreset.High;
        }

        internal static bool UsingCustomPreset()
        {
            return ActivePreset() == AssistPreset.Custom;
        }

        internal static float EffectiveAssistCoefficient()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(AssistCoefficient, 1.4f);
            }

            if (ActiveMode() == AssistMode.Natural)
            {
                switch (ActivePreset())
                {
                    case AssistPreset.Low:
                        return 0.8f;
                    case AssistPreset.High:
                        return 2.0f;
                    default:
                        return 1.4f;
                }
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.65f;
                case AssistPreset.Medium:
                    return 0.95f;
                default:
                    return 1.4f;
            }
        }

        internal static float EffectiveRotationDeltaPerSecond()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(RotationDeltaPerSecond, 10f);
            }

            if (ActiveMode() == AssistMode.Natural)
            {
                switch (ActivePreset())
                {
                    case AssistPreset.Low:
                        return 6f;
                    case AssistPreset.High:
                        return 14f;
                    default:
                        return 10f;
                }
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 4.5f;
                case AssistPreset.Medium:
                    return 7f;
                default:
                    return 10f;
            }
        }

        internal static float EffectiveMaxRotationDeltaPerFrame()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MaxRotationDeltaPerFrame, 0.9f);
            }

            if (ActiveMode() == AssistMode.Natural)
            {
                switch (ActivePreset())
                {
                    case AssistPreset.Low:
                        return 0.45f;
                    case AssistPreset.High:
                        return 1.4f;
                    default:
                        return 0.9f;
                }
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.35f;
                case AssistPreset.Medium:
                    return 0.55f;
                default:
                    return 0.9f;
            }
        }

        internal static float EffectiveOuterBubbleAngleDegrees()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(OuterBubbleAngleDegrees, 34f);
            }

            if (ActiveMode() == AssistMode.Natural)
            {
                switch (ActivePreset())
                {
                    case AssistPreset.Low:
                        return 26f;
                    case AssistPreset.High:
                        return 42f;
                    default:
                        return 34f;
                }
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 24f;
                case AssistPreset.Medium:
                    return 28f;
                default:
                    return 34f;
            }
        }

        internal static float EffectiveInnerBubbleAngleDegrees()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(InnerBubbleAngleDegrees, 8f);
            }

            if (ActiveMode() == AssistMode.Natural)
            {
                switch (ActivePreset())
                {
                    case AssistPreset.Low:
                        return 6f;
                    case AssistPreset.High:
                        return 10f;
                    default:
                        return 8f;
                }
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 5f;
                case AssistPreset.Medium:
                    return 6f;
                default:
                    return 8f;
            }
        }

        internal static float EffectiveBubbleCurvePower()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(BubbleCurvePower, 0.65f);
            }

            if (ActiveMode() == AssistMode.Natural)
            {
                switch (ActivePreset())
                {
                    case AssistPreset.Low:
                        return 0.9f;
                    case AssistPreset.High:
                        return 0.5f;
                    default:
                        return 0.65f;
                }
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 1.1f;
                case AssistPreset.Medium:
                    return 0.9f;
                default:
                    return 0.65f;
            }
        }

        internal static float EffectiveDistanceFalloffPower()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(DistanceFalloffPower, 0.35f);
            }

            if (ActiveMode() == AssistMode.Natural)
            {
                switch (ActivePreset())
                {
                    case AssistPreset.Low:
                        return 0.55f;
                    case AssistPreset.High:
                        return 0.2f;
                    default:
                        return 0.35f;
                }
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.7f;
                case AssistPreset.Medium:
                    return 0.55f;
                default:
                    return 0.35f;
            }
        }

        internal static bool EffectiveMagnetPreferWeakspot()
        {
            return UsingCustomPreset() ? SafeBool(MagnetPreferWeakspot, true) : true;
        }

        internal static bool EffectiveMagnetWeakspotOverrideOfficialPull()
        {
            return UsingCustomPreset() ? SafeBool(MagnetWeakspotOverrideOfficialPull, true) : true;
        }

        internal static bool EffectiveMagnetAllowLookAwayEscape()
        {
            return UsingCustomPreset() ? SafeBool(MagnetAllowLookAwayEscape, true) : true;
        }

        internal static float EffectiveMagnetWeakspotBias()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetWeakspotBias, 0.85f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.55f;
                case AssistPreset.Medium:
                    return 0.70f;
                default:
                    return 0.85f;
            }
        }

        internal static float EffectiveMagnetCenterBias()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetCenterBias, 0.15f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.05f;
                case AssistPreset.Medium:
                    return 0.10f;
                default:
                    return 0.15f;
            }
        }

        internal static float EffectiveMagnetMovementOnlyScale()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetMovementOnlyScale, 0.35f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.20f;
                case AssistPreset.Medium:
                    return 0.28f;
                default:
                    return 0.35f;
            }
        }

        internal static float EffectiveMagnetSmoothing()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetSmoothing, 22f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 16f;
                case AssistPreset.Medium:
                    return 19f;
                default:
                    return 22f;
            }
        }

        internal static float EffectiveMagnetReleaseSmoothing()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetReleaseSmoothing, 12f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 8f;
                case AssistPreset.Medium:
                    return 10f;
                default:
                    return 12f;
            }
        }

        internal static float EffectiveMagnetEdgeSofteningPower()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetEdgeSofteningPower, 2.2f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 3.0f;
                case AssistPreset.Medium:
                    return 2.6f;
                default:
                    return 2.2f;
            }
        }

        internal static float EffectiveMagnetOuterEdgeStrength()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetOuterEdgeStrength, 0.08f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.03f;
                case AssistPreset.Medium:
                    return 0.05f;
                default:
                    return 0.08f;
            }
        }

        internal static float EffectiveMagnetSnapBackAnglePadding()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetSnapBackAnglePadding, 4f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 6f;
                case AssistPreset.Medium:
                    return 5f;
                default:
                    return 4f;
            }
        }

        internal static float EffectiveMagnetLookAwayThreshold()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetLookAwayThreshold, 0.10f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.05f;
                case AssistPreset.Medium:
                    return 0.08f;
                default:
                    return 0.10f;
            }
        }

        internal static float EffectiveMagnetLookAwayMinScale()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetLookAwayMinScale, 0.08f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.02f;
                case AssistPreset.Medium:
                    return 0.05f;
                default:
                    return 0.08f;
            }
        }

        internal static float EffectiveMagnetLookAwayCurvePower()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetLookAwayCurvePower, 1.2f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 1.0f;
                case AssistPreset.Medium:
                    return 1.1f;
                default:
                    return 1.2f;
            }
        }

        internal static float EffectiveMagnetLookAwayMovementScale()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(MagnetLookAwayMovementScale, 0.45f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.35f;
                case AssistPreset.Medium:
                    return 0.40f;
                default:
                    return 0.45f;
            }
        }

        internal static bool EffectiveNaturalKeepOfficialGamepadPull()
        {
            return UsingCustomPreset() ? SafeBool(NaturalKeepOfficialGamepadPull, true) : true;
        }

        internal static float EffectiveNaturalTowardAssist()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalTowardAssist, 0.12f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.05f;
                case AssistPreset.High:
                    return 0.20f;
                default:
                    return 0.12f;
            }
        }

        internal static float EffectiveNaturalSideAssist()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalSideAssist, 0.55f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.28f;
                case AssistPreset.High:
                    return 0.85f;
                default:
                    return 0.55f;
            }
        }

        internal static float EffectiveNaturalAwayDamping()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalAwayDamping, 1.45f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.75f;
                case AssistPreset.High:
                    return 2.2f;
                default:
                    return 1.45f;
            }
        }

        internal static float EffectiveNaturalMovementOnlyScale()
        {
            return UsingCustomPreset() ? SafeFloat(NaturalMovementOnlyScale, 0f) : 0f;
        }

        internal static float EffectiveNaturalMaxInputFraction()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalMaxInputFraction, 0.85f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.55f;
                case AssistPreset.High:
                    return 1.2f;
                default:
                    return 0.85f;
            }
        }

        internal static float EffectiveNaturalMinDeltaPerFrame()
        {
            return UsingCustomPreset() ? SafeFloat(NaturalMinDeltaPerFrame, 0.003f) : 0.003f;
        }

        internal static float EffectiveNaturalSmoothing()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalSmoothing, 30f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 24f;
                case AssistPreset.High:
                    return 38f;
                default:
                    return 30f;
            }
        }

        internal static float EffectiveNaturalReleaseSmoothing()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalReleaseSmoothing, 10f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 8f;
                case AssistPreset.High:
                    return 14f;
                default:
                    return 10f;
            }
        }

        internal static float EffectiveNaturalDirectionSmoothing()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalDirectionSmoothing, 18f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 14f;
                case AssistPreset.High:
                    return 24f;
                default:
                    return 18f;
            }
        }

        internal static float EffectiveNaturalMinLookInput()
        {
            return UsingCustomPreset() ? SafeFloat(NaturalMinLookInput, 0.001f) : 0.001f;
        }

        internal static float EffectiveNaturalReleaseCurvePower()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalReleaseCurvePower, 2.4f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 2.8f;
                case AssistPreset.High:
                    return 2.0f;
                default:
                    return 2.4f;
            }
        }

        internal static float EffectiveNaturalOuterEdgeStrength()
        {
            if (UsingCustomPreset())
            {
                return SafeFloat(NaturalOuterEdgeStrength, 0.02f);
            }

            switch (ActivePreset())
            {
                case AssistPreset.Low:
                    return 0.01f;
                case AssistPreset.High:
                    return 0.04f;
                default:
                    return 0.02f;
            }
        }

        internal static float SafeFloat(ConfigEntry<float> entry, float fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        internal static bool SafeBool(ConfigEntry<bool> entry, bool fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        internal static bool IsAimbotRuntimeEnabled()
        {
            if (EnableMod == null || !EnableMod.Value)
            {
                return false;
            }

            if (EnableAimbot != null && !EnableAimbot.Value)
            {
                return false;
            }

            KeyCode holdKey = HoldDisableKey != null ? HoldDisableKey.Value : KeyCode.LeftAlt;
            if (IsHoldDisableKeyPressed(holdKey))
            {
                return false;
            }

            return true;
        }

        private static bool IsHoldDisableKeyPressed(KeyCode keyCode)
        {
            if (keyCode == KeyCode.None)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            Key key;
            if (!TryConvertUnityKeyCode(keyCode, out key))
            {
                return false;
            }

            try
            {
                return keyboard[key].isPressed;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertUnityKeyCode(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.LeftAlt:
                    key = Key.LeftAlt;
                    return true;
                case KeyCode.RightAlt:
                    key = Key.RightAlt;
                    return true;
                case KeyCode.LeftControl:
                    key = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    key = Key.RightCtrl;
                    return true;
                case KeyCode.LeftShift:
                    key = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    key = Key.RightShift;
                    return true;
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                case KeyCode.Tab:
                    key = Key.Tab;
                    return true;
                case KeyCode.Return:
                    key = Key.Enter;
                    return true;
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                case KeyCode.Backspace:
                    key = Key.Backspace;
                    return true;
                case KeyCode.Delete:
                    key = Key.Delete;
                    return true;
                case KeyCode.Insert:
                    key = Key.Insert;
                    return true;
                case KeyCode.Home:
                    key = Key.Home;
                    return true;
                case KeyCode.End:
                    key = Key.End;
                    return true;
                case KeyCode.PageUp:
                    key = Key.PageUp;
                    return true;
                case KeyCode.PageDown:
                    key = Key.PageDown;
                    return true;
                case KeyCode.UpArrow:
                    key = Key.UpArrow;
                    return true;
                case KeyCode.DownArrow:
                    key = Key.DownArrow;
                    return true;
                case KeyCode.LeftArrow:
                    key = Key.LeftArrow;
                    return true;
                case KeyCode.RightArrow:
                    key = Key.RightArrow;
                    return true;
                case KeyCode.Alpha0:
                    key = Key.Digit0;
                    return true;
                case KeyCode.Alpha1:
                    key = Key.Digit1;
                    return true;
                case KeyCode.Alpha2:
                    key = Key.Digit2;
                    return true;
                case KeyCode.Alpha3:
                    key = Key.Digit3;
                    return true;
                case KeyCode.Alpha4:
                    key = Key.Digit4;
                    return true;
                case KeyCode.Alpha5:
                    key = Key.Digit5;
                    return true;
                case KeyCode.Alpha6:
                    key = Key.Digit6;
                    return true;
                case KeyCode.Alpha7:
                    key = Key.Digit7;
                    return true;
                case KeyCode.Alpha8:
                    key = Key.Digit8;
                    return true;
                case KeyCode.Alpha9:
                    key = Key.Digit9;
                    return true;
                case KeyCode.F1:
                    key = Key.F1;
                    return true;
                case KeyCode.F2:
                    key = Key.F2;
                    return true;
                case KeyCode.F3:
                    key = Key.F3;
                    return true;
                case KeyCode.F4:
                    key = Key.F4;
                    return true;
                case KeyCode.F5:
                    key = Key.F5;
                    return true;
                case KeyCode.F6:
                    key = Key.F6;
                    return true;
                case KeyCode.F7:
                    key = Key.F7;
                    return true;
                case KeyCode.F8:
                    key = Key.F8;
                    return true;
                case KeyCode.F9:
                    key = Key.F9;
                    return true;
                case KeyCode.F10:
                    key = Key.F10;
                    return true;
                case KeyCode.F11:
                    key = Key.F11;
                    return true;
                case KeyCode.F12:
                    key = Key.F12;
                    return true;
            }

            int keyCodeInt = (int)keyCode;
            if (keyCodeInt >= (int)KeyCode.A && keyCodeInt <= (int)KeyCode.Z)
            {
                key = Key.A + (keyCodeInt - (int)KeyCode.A);
                return true;
            }

            key = Key.None;
            return false;
        }

        internal static bool IsHardLockModeActive()
        {
            return IsAimbotRuntimeEnabled() && ActiveMode() == AssistMode.HardLock;
        }

        private void CreateWeakspotDebugOverlay()
        {
            if (weakspotDebugOverlayObject != null)
            {
                return;
            }

            weakspotDebugOverlayObject = new GameObject("Ryuka.WeakspotDebugOverlay");
            weakspotDebugOverlayObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(weakspotDebugOverlayObject);
            weakspotDebugOverlayObject.AddComponent<WeakspotDebugOverlayController>();
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();

            if (weakspotDebugOverlayObject != null)
            {
                Destroy(weakspotDebugOverlayObject);
                weakspotDebugOverlayObject = null;
            }
        }

        private void BindConfig()
        {
            EnableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enable this mod.");

            DebugLogging = Config.Bind(
                "General",
                "DebugLogging",
                false,
                "Print aim assist debug logs.");

            Mode = Config.Bind(
                "Assist Mode",
                "Mode",
                AssistMode.Magnet,
                "Magnet = magnetic pull. Natural = input-shaped sticky assist.");

            MagnetPreset = Config.Bind(
                "Preset",
                "MagnetPreset",
                AssistPreset.High,
                "Low / Medium / High / Custom for Magnet mode. Current tested Magnet settings are defined as High.");

            NaturalPreset = Config.Bind(
                "Preset",
                "NaturalPreset",
                AssistPreset.Medium,
                "Low / Medium / High / Custom for Natural mode. Current tested Natural settings are defined as Medium.");

            EnableMouseConsumesRotationPullDelta = Config.Bind(
                "Official Pipeline",
                "EnableMouseConsumesRotationPullDelta",
                true,
                "Allow mouse input to consume AimAssist.rotationPullDelta inside InputReader.Update().");

            EnableContinuousRotationalAssist = Config.Bind(
                "Official Pipeline",
                "EnableContinuousRotationalAssist",
                true,
                "Append continuous rotational assist to official AimAssist.rotationPullDelta.");

            RequirePlayerInput = Config.Bind(
                "Input",
                "RequirePlayerInput",
                true,
                "Only assist while player has movement or look input.");

            CountLookInput = Config.Bind(
                "Input",
                "CountLookInput",
                true,
                "Look input counts as active player input.");

            CountMoveInput = Config.Bind(
                "Input",
                "CountMoveInput",
                true,
                "Movement input counts as active player input.");

            LookInputThreshold = Config.Bind(
                "Input",
                "LookInputThreshold",
                0.001f,
                new ConfigDescription(
                    "Threshold for effective look input after sensitivity.",
                    new AcceptableValueRange<float>(0f, 10f)));

            MoveInputThreshold = Config.Bind(
                "Input",
                "MoveInputThreshold",
                0.05f,
                new ConfigDescription(
                    "Threshold for movement input.",
                    new AcceptableValueRange<float>(0f, 1f)));

            AssistCoefficient = Config.Bind(
                "Custom Common",
                "AssistCoefficient",
                1.4f,
                new ConfigDescription(
                    "Overall assist strength. 0.6 strong, 1.0 very strong, 2.0 extreme.",
                    new AcceptableValueRange<float>(0f, 5f)));

            RotationDeltaPerSecond = Config.Bind(
                "Custom Common",
                "RotationDeltaPerSecond",
                10.0f,
                new ConfigDescription(
                    "Base rotationPullDelta per second before AssistCoefficient scaling.",
                    new AcceptableValueRange<float>(0f, 100f)));

            MaxRotationDeltaPerFrame = Config.Bind(
                "Custom Common",
                "MaxRotationDeltaPerFrame",
                0.9f,
                new ConfigDescription(
                    "Hard cap for added rotationPullDelta per frame.",
                    new AcceptableValueRange<float>(0.001f, 10f)));

            MagnetCenterBias = Config.Bind(
                "Custom Magnet",
                "MagnetCenterBias",
                0.15f,
                new ConfigDescription(
                    "For Magnet mode only when weakspot targeting is not available. 0 = official target point, 1 = collider center. Keep low to avoid center locking.",
                    new AcceptableValueRange<float>(0f, 1f)));

            MagnetMovementOnlyScale = Config.Bind(
                "Custom Magnet",
                "MagnetMovementOnlyScale",
                0.35f,
                new ConfigDescription(
                    "For Magnet mode only. Strength scale when player is moving but not moving aim.",
                    new AcceptableValueRange<float>(0f, 1f)));

            MagnetSmoothing = Config.Bind(
                "Custom Magnet",
                "MagnetSmoothing",
                22f,
                new ConfigDescription(
                    "For Magnet mode only. Active pull smoothing speed. Higher = more responsive.",
                    new AcceptableValueRange<float>(0f, 80f)));

            MagnetReleaseSmoothing = Config.Bind(
                "Custom Magnet",
                "MagnetReleaseSmoothing",
                12f,
                new ConfigDescription(
                    "For Magnet mode only. Release smoothing speed. Lower = softer release.",
                    new AcceptableValueRange<float>(0f, 80f)));

            MagnetPreferWeakspot = Config.Bind(
                "Custom Magnet",
                "MagnetPreferWeakspot",
                true,
                "For Magnet mode only. Prefer the highest base Hitmesh.Data.GetShapeMultiplier() part when available.");

            MagnetWeakspotBias = Config.Bind(
                "Custom Magnet",
                "MagnetWeakspotBias",
                0.85f,
                new ConfigDescription(
                    "For MagnetPreferWeakspot. 0 = normal Magnet target, 1 = weakspot point.",
                    new AcceptableValueRange<float>(0f, 1f)));

            MagnetWeakspotOverrideOfficialPull = Config.Bind(
                "Custom Magnet",
                "MagnetWeakspotOverrideOfficialPull",
                true,
                "When Magnet weakspot targeting succeeds, clear the official center pull before adding the weakspot pull.");

            MagnetEdgeSofteningPower = Config.Bind(
                "Custom Magnet",
                "MagnetEdgeSofteningPower",
                2.2f,
                new ConfigDescription(
                    "For Magnet mode only. Extra falloff near the outer assist bubble edge. Higher = softer near edge.",
                    new AcceptableValueRange<float>(0.1f, 8f)));

            MagnetOuterEdgeStrength = Config.Bind(
                "Custom Magnet",
                "MagnetOuterEdgeStrength",
                0.08f,
                new ConfigDescription(
                    "For Magnet mode only. Minimum pull strength near the outer edge. Lower reduces snap-back jitter.",
                    new AcceptableValueRange<float>(0f, 0.5f)));

            MagnetSnapBackAnglePadding = Config.Bind(
                "Custom Magnet",
                "MagnetSnapBackAnglePadding",
                4f,
                new ConfigDescription(
                    "For Magnet mode only. Softly scales down pull during the last N degrees before leaving the bubble.",
                    new AcceptableValueRange<float>(0f, 30f)));

            MagnetAllowLookAwayEscape = Config.Bind(
                "Custom Magnet",
                "MagnetAllowLookAwayEscape",
                true,
                "For Magnet mode only. Reduce pull when player look input is clearly moving away from the target.");

            MagnetLookAwayThreshold = Config.Bind(
                "Custom Magnet",
                "MagnetLookAwayThreshold",
                0.10f,
                new ConfigDescription(
                    "For Magnet escape. Dot threshold for detecting look-away input. 0.10 means only clear away input reduces pull.",
                    new AcceptableValueRange<float>(0f, 1f)));

            MagnetLookAwayMinScale = Config.Bind(
                "Custom Magnet",
                "MagnetLookAwayMinScale",
                0.08f,
                new ConfigDescription(
                    "For Magnet escape. Minimum pull scale when player strongly looks away. Lower makes controller escape easier.",
                    new AcceptableValueRange<float>(0f, 1f)));

            MagnetLookAwayCurvePower = Config.Bind(
                "Custom Magnet",
                "MagnetLookAwayCurvePower",
                1.2f,
                new ConfigDescription(
                    "For Magnet escape. Higher makes pull reduction happen later; lower makes it more sensitive.",
                    new AcceptableValueRange<float>(0.1f, 5f)));

            MagnetLookAwayMovementScale = Config.Bind(
                "Custom Magnet",
                "MagnetLookAwayMovementScale",
                0.45f,
                new ConfigDescription(
                    "For Magnet escape. Extra scale when movement input is also active. Higher preserves sticky aim while strafing.",
                    new AcceptableValueRange<float>(0f, 1f)));

            TargetSwitchFadeSeconds = Config.Bind(
                "Custom Target Stability",
                "TargetSwitchFadeSeconds",
                0.12f,
                new ConfigDescription(
                    "Fade-in time after the official tracked target changes.",
                    new AcceptableValueRange<float>(0f, 1f)));

            TargetSwitchMinScale = Config.Bind(
                "Custom Target Stability",
                "TargetSwitchMinScale",
                0.25f,
                new ConfigDescription(
                    "Minimum assist scale immediately after target switch. 0 = fully fade from zero, 1 = no switch fade.",
                    new AcceptableValueRange<float>(0f, 1f)));


            OuterBubbleAngleDegrees = Config.Bind(
                "Custom Common",
                "OuterBubbleAngleDegrees",
                30f,
                new ConfigDescription(
                    "Target must be inside this angle from crosshair.",
                    new AcceptableValueRange<float>(1f, 89f)));

            InnerBubbleAngleDegrees = Config.Bind(
                "Custom Common",
                "InnerBubbleAngleDegrees",
                7f,
                new ConfigDescription(
                    "Inside this angle, assist reaches full strength.",
                    new AcceptableValueRange<float>(0.1f, 60f)));

            BubbleCurvePower = Config.Bind(
                "Custom Common",
                "BubbleCurvePower",
                0.8f,
                new ConfigDescription(
                    "Higher values make assist weaker near the edge.",
                    new AcceptableValueRange<float>(0.1f, 5f)));

            DistanceFalloffPower = Config.Bind(
                "Custom Common",
                "DistanceFalloffPower",
                0.5f,
                new ConfigDescription(
                    "Higher values make far targets weaker.",
                    new AcceptableValueRange<float>(0f, 5f)));

            NaturalTowardAssist = Config.Bind(
                "Custom Natural",
                "NaturalTowardAssist",
                0.12f,
                new ConfigDescription(
                    "Extra assist when player is already moving aim toward the target. Default 0 means no boost toward target.",
                    new AcceptableValueRange<float>(0f, 5f)));

            NaturalSideAssist = Config.Bind(
                "Custom Natural",
                "NaturalSideAssist",
                0.55f,
                new ConfigDescription(
                    "Small assist when player aim input is mostly perpendicular to target direction.",
                    new AcceptableValueRange<float>(0f, 5f)));

            NaturalAwayDamping = Config.Bind(
                "Custom Natural",
                "NaturalAwayDamping",
                1.45f,
                new ConfigDescription(
                    "Damping force when player aim input moves away from target.",
                    new AcceptableValueRange<float>(0f, 5f)));

            NaturalMovementOnlyScale = Config.Bind(
                "Custom Natural",
                "NaturalMovementOnlyScale",
                0.0f,
                new ConfigDescription(
                    "Assist when only movement input exists and no look input exists. Default 0 disables hands-free pull.",
                    new AcceptableValueRange<float>(0f, 1f)));

            NaturalMaxInputFraction = Config.Bind(
                "Custom Natural",
                "NaturalMaxInputFraction",
                0.85f,
                new ConfigDescription(
                    "Natural assist cap as a fraction of current player look input.",
                    new AcceptableValueRange<float>(0.01f, 2f)));

            NaturalMinDeltaPerFrame = Config.Bind(
                "Custom Natural",
                "NaturalMinDeltaPerFrame",
                0.003f,
                new ConfigDescription(
                    "Minimum allowed natural delta cap per frame.",
                    new AcceptableValueRange<float>(0f, 1f)));

            NaturalSmoothing = Config.Bind(
                "Custom Natural",
                "NaturalSmoothing",
                30f,
                new ConfigDescription(
                    "Smoothing speed for natural assist output. Higher is more responsive.",
                    new AcceptableValueRange<float>(0f, 60f)));

            NaturalReleaseSmoothing = Config.Bind(
                "Custom Natural",
                "NaturalReleaseSmoothing",
                10f,
                new ConfigDescription(
                    "Smoothing speed when Natural assist is releasing. Lower = softer release.",
                    new AcceptableValueRange<float>(0f, 60f)));

            NaturalDirectionSmoothing = Config.Bind(
                "Custom Natural",
                "NaturalDirectionSmoothing",
                18f,
                new ConfigDescription(
                    "Smoothing speed for target direction in Natural mode. Lower reduces jitter from target point changes.",
                    new AcceptableValueRange<float>(0f, 60f)));

            NaturalMinLookInput = Config.Bind(
                "Custom Natural",
                "NaturalMinLookInput",
                0.001f,
                new ConfigDescription(
                    "Minimum look input magnitude before natural aim shaping uses look direction.",
                    new AcceptableValueRange<float>(0f, 10f)));

            NaturalKeepOfficialGamepadPull = Config.Bind(
                "Custom Natural",
                "NaturalKeepOfficialGamepadPull",
                true,
                "In Natural mode, keep the game's original controller ADS pull. Mouse official ADS pull is still removed.");

            NaturalReleaseCurvePower = Config.Bind(
                "Custom Natural",
                "NaturalReleaseCurvePower",
                2.4f,
                new ConfigDescription(
                    "Extra falloff used only by Natural mode. Higher values make assist release more smoothly near the edge of the bubble.",
                    new AcceptableValueRange<float>(0.1f, 8f)));

            NaturalOuterEdgeStrength = Config.Bind(
                "Custom Natural",
                "NaturalOuterEdgeStrength",
                0.02f,
                new ConfigDescription(
                    "Minimum Natural strength near the outer bubble edge. Keep low to avoid a sudden breakaway feeling.",
                    new AcceptableValueRange<float>(0f, 0.5f)));

            OverrideOfficialScanner = Config.Bind(
                "System - Official Scanner",
                "OverrideOfficialScanner",
                true,
                "Tune official AimAssist scanner fields.");

            OfficialMaxAssistDistance = Config.Bind(
                "System - Official Scanner",
                "OfficialMaxAssistDistance",
                100f,
                new ConfigDescription(
                    "Official scanner max distance.",
                    new AcceptableValueRange<float>(5f, 250f)));

            OfficialScanConeDegrees = Config.Bind(
                "System - Official Scanner",
                "OfficialScanConeDegrees",
                75f,
                new ConfigDescription(
                    "Official scanner cone. Should be wider than OuterBubbleAngleDegrees.",
                    new AcceptableValueRange<float>(1f, 89f)));

            OfficialScoreThreshold = Config.Bind(
                "System - Official Scanner",
                "OfficialScoreThreshold",
                0f,
                new ConfigDescription(
                    "Official scanner score threshold.",
                    new AcceptableValueRange<float>(0f, 1f)));

            OfficialDistanceVsDotBlend = Config.Bind(
                "System - Official Scanner",
                "OfficialDistanceVsDotBlend",
                0.03f,
                new ConfigDescription(
                    "Official scanner scoring mix. 0 = aim angle, 1 = distance.",
                    new AcceptableValueRange<float>(0f, 1f)));

            OfficialScanIntervalFrames = Config.Bind(
                "System - Official Scanner",
                "OfficialScanIntervalFrames",
                1,
                new ConfigDescription(
                    "Frames between official target scans.",
                    new AcceptableValueRange<int>(1, 30)));

            ScannerKeepAliveStrength = Config.Bind(
                "System - Official Scanner",
                "ScannerKeepAliveStrength",
                0.001f,
                new ConfigDescription(
                    "Tiny internal friction strength to keep official scanner active when all official assist sliders are zero.",
                    new AcceptableValueRange<float>(0f, 0.5f)));

            InvertX = Config.Bind(
                "Troubleshooting",
                "InvertX",
                false,
                "Invert horizontal assist direction if it moves away from target.");

            InvertY = Config.Bind(
                "Troubleshooting",
                "InvertY",
                false,
                "Invert vertical assist direction if it moves away from target.");

            EnableWeakspotDebugOverlay = Config.Bind(
                "Debug - Weakspot Overlay",
                "EnableWeakspotDebugOverlay",
                false,
                "Enable a runtime overlay that draws actual weakspot polygons from Hitmesh / HitboxColliders data.");

            WeakspotDebugOnlyTrackedTarget = Config.Bind(
                "Debug - Weakspot Overlay",
                "WeakspotDebugOnlyTrackedTarget",
                true,
                "Only draw weakspot polygons for the current AimAssist tracked target.");

            WeakspotDebugShowAllPositiveShapes = Config.Bind(
                "Debug - Weakspot Overlay",
                "WeakspotDebugShowAllPositiveShapes",
                false,
                "Draw every non-invulnerable positive-multiplier shape. If false, only draw the highest-multiplier shape(s) per target.");

            WeakspotDebugMinimumMultiplier = Config.Bind(
                "Debug - Weakspot Overlay",
                "WeakspotDebugMinimumMultiplier",
                0.75f,
                new ConfigDescription(
                    "Minimum base GetShapeMultiplier() required for overlay drawing when WeakspotDebugShowAllPositiveShapes is true.",
                    new AcceptableValueRange<float>(0f, 2f)));

            WeakspotDebugLineWidth = Config.Bind(
                "Debug - Weakspot Overlay",
                "WeakspotDebugLineWidth",
                0.025f,
                new ConfigDescription(
                    "World-space line width for the weakspot overlay.",
                    new AcceptableValueRange<float>(0.001f, 0.2f)));

            WeakspotDebugDepthOffset = Config.Bind(
                "Debug - Weakspot Overlay",
                "WeakspotDebugDepthOffset",
                0.03f,
                new ConfigDescription(
                    "Move overlay vertices slightly toward the player camera to reduce z-fighting.",
                    new AcceptableValueRange<float>(0f, 0.25f)));

            EnableAimbot = Config.Bind(
                "Master Switch",
                "EnableAimbot",
                true,
                "Master switch for Deadeye Instinct aim assist and HardLock AutoFire.");

            HoldDisableKey = Config.Bind(
                "Master Switch",
                "HoldDisableKey",
                KeyCode.LeftAlt,
                "Hold this key to temporarily disable aim assist and release AutoFire trigger. Use None to disable this keybind.");

            HardLockMaxDistance = Config.Bind(
                "HardLock",
                "HardLockMaxDistance",
                20f,
                new ConfigDescription(
                    "Maximum configured distance for HardLock target search.",
                    new AcceptableValueRange<float>(5f, 1000f)));

            HardLockMaxDistanceCap = Config.Bind(
                "HardLock",
                "HardLockMaxDistanceCap",
                20f,
                new ConfigDescription(
                    "Safety cap applied on top of HardLockMaxDistance. This keeps old configs with very large distances from locking enemies too far away. Set to 0 to disable the cap.",
                    new AcceptableValueRange<float>(0f, 1000f)));

            HardLockPreferWeakspot = Config.Bind(
                "HardLock",
                "HardLockPreferWeakspot",
                true,
                "HardLock aims at the best weakspot center when available.");

            HardLockWeakspotBias = Config.Bind(
                "HardLock",
                "HardLockWeakspotBias",
                1f,
                new ConfigDescription(
                    "0 = target body point, 1 = weakspot center.",
                    new AcceptableValueRange<float>(0f, 1f)));

            HardLockRotationSpeed = Config.Bind(
                "HardLock",
                "HardLockRotationSpeed",
                420f,
                new ConfigDescription(
                    "Maximum HardLock correction speed per second. High values create near-instant lock.",
                    new AcceptableValueRange<float>(1f, 5000f)));

            HardLockRotationGain = Config.Bind(
                "HardLock",
                "HardLockRotationGain",
                0.18f,
                new ConfigDescription(
                    "Scales angular error into camera rotation delta. Higher values are more aggressive.",
                    new AcceptableValueRange<float>(0.01f, 10f)));

            HardLockMaxDeltaPerFrame = Config.Bind(
                "HardLock",
                "HardLockMaxDeltaPerFrame",
                12f,
                new ConfigDescription(
                    "Hard cap for HardLock rotation delta per frame.",
                    new AcceptableValueRange<float>(0.01f, 100f)));

            HardLockRecoilCompensation = Config.Bind(
                "HardLock",
                "HardLockRecoilCompensation",
                true,
                "Apply an extra HardLock correction after CameraRecoil.LateUpdate().");

            HardLockRequireLineOfSight = Config.Bind(
                "HardLock",
                "HardLockRequireLineOfSight",
                true,
                "Legacy line-of-sight switch. Kept for compatibility. HardLockRequireVisibleTarget is the safer release switch.");

            HardLockRequireVisibleTarget = Config.Bind(
                "HardLock",
                "HardLockRequireVisibleTarget",
                true,
                "Require official geometry-layer visibility before selecting a HardLock target. This prevents locking enemies behind walls or before the player can see them.");

            HardLockTargetPriorityMode = Config.Bind(
                "HardLock Target Priority",
                "HardLockTargetPriority",
                HardLockTargetPriority.ThreatWeighted,
                "Nearest / LowestHealth / WeakspotThenDistance / ThreatWeighted.");

            HardLockDistanceWeight = Config.Bind(
                "HardLock Target Priority",
                "DistanceWeight",
                0.75f,
                new ConfigDescription(
                    "ThreatWeighted score weight for nearby enemies. Default is 50% higher than v1.2.4.",
                    new AcceptableValueRange<float>(0f, 2f)));

            HardLockLowHealthWeight = Config.Bind(
                "HardLock Target Priority",
                "LowHealthWeight",
                0.25f,
                new ConfigDescription(
                    "ThreatWeighted score weight for low health enemies.",
                    new AcceptableValueRange<float>(0f, 2f)));

            HardLockWeakspotWeight = Config.Bind(
                "HardLock Target Priority",
                "WeakspotWeight",
                0.15f,
                new ConfigDescription(
                    "ThreatWeighted score weight for high-multiplier weakspots.",
                    new AcceptableValueRange<float>(0f, 2f)));

            HardLockStickyTargetBonus = Config.Bind(
                "HardLock Target Priority",
                "StickyTargetBonus",
                0.20f,
                new ConfigDescription(
                    "Extra score for the current HardLock target to prevent rapid target switching.",
                    new AcceptableValueRange<float>(0f, 2f)));

            EnableAutoFire = Config.Bind(
                "Auto Fire",
                "EnableAutoFire",
                false,
                "Automatically holds or pulses the weapon trigger when HardLock is aligned.");

            AutoFireOnlyInHardLock = Config.Bind(
                "Auto Fire",
                "AutoFireOnlyInHardLock",
                true,
                "Only allow AutoFire while Mode = HardLock.");

            AutoFireRequireAligned = Config.Bind(
                "Auto Fire",
                "AutoFireRequireAligned",
                true,
                "Only fire when HardLock target angle is within AutoFireMaxAngleDegrees.");

            AutoFireMaxAngleDegrees = Config.Bind(
                "Auto Fire",
                "AutoFireMaxAngleDegrees",
                1.0f,
                new ConfigDescription(
                    "Maximum angle error for AutoFire alignment.",
                    new AcceptableValueRange<float>(0.01f, 30f)));

            AutoFireRequireWeaponReady = Config.Bind(
                "Auto Fire",
                "AutoFireRequireWeaponReady",
                true,
                "Check ammo / reload / cooldown fields before pressing the trigger when available.");

            AutoFireRespectSemiAuto = Config.Bind(
                "Auto Fire",
                "AutoFireRespectSemiAuto",
                true,
                "Pulse semi-auto weapons instead of holding the trigger down.");

            AutoFirePulseSeconds = Config.Bind(
                "Auto Fire",
                "AutoFirePulseSeconds",
                0.04f,
                new ConfigDescription(
                    "Trigger-down duration for semi-auto AutoFire pulses.",
                    new AcceptableValueRange<float>(0.005f, 0.5f)));

            AutoFireReleaseSeconds = Config.Bind(
                "Auto Fire",
                "AutoFireReleaseSeconds",
                0.04f,
                new ConfigDescription(
                    "Trigger-up duration between semi-auto AutoFire pulses.",
                    new AcceptableValueRange<float>(0.005f, 0.5f)));
        }
    }

    internal static class InputReaderUpdatePatch
    {
        private static readonly FieldInfo FieldLookDelta =
            AccessTools.Field(typeof(InputReader), "lookDelta");

        private static readonly FieldInfo FieldLookDeltaGamepad =
            AccessTools.Field(typeof(InputReader), "lookDeltaGamepad");

        private static readonly MethodInfo GamepadUsedGetter =
            AccessTools.PropertyGetter(typeof(InputReader), nameof(InputReader.GamepadUsed));

        private static readonly FieldInfo AimAssistField =
            AccessTools.Field(typeof(InputReader), "aimAssist");

        private static readonly FieldInfo RotationPullDeltaField =
            AccessTools.Field(typeof(AimAssist), "rotationPullDelta");

        internal struct InputSnapshot
        {
            public bool PlayerInputActive;
            public bool LookInputActive;
            public bool MoveInputActive;
            public Vector2 EffectiveLookInput;
            public bool GamepadUsed;
        }

        internal static bool LastPlayerInputActive;
        internal static bool LastLookInputActive;
        internal static bool LastMoveInputActive;
        internal static Vector2 LastEffectiveLookInput;
        internal static bool LastGamepadUsed;

        private static readonly Dictionary<AimAssist, InputSnapshot> SnapshotsByAimAssist =
            new Dictionary<AimAssist, InputSnapshot>();

        public static void Prefix(InputReader __instance)
        {
            LastPlayerInputActive = false;
            LastLookInputActive = false;
            LastMoveInputActive = false;
            LastEffectiveLookInput = Vector2.zero;
            LastGamepadUsed = false;

            if (!DeadeyeInstinctPlugin.EnableMod.Value || __instance == null)
            {
                return;
            }

            Vector2 lookDelta = GetVector2(FieldLookDelta, __instance);
            Vector2 lookDeltaGamepad = GetVector2(FieldLookDeltaGamepad, __instance);
            LastGamepadUsed = __instance.GamepadUsed;

            LastEffectiveLookInput = __instance.GamepadUsed
                ? lookDeltaGamepad * Time.unscaledDeltaTime
                : lookDelta;

            bool lookActive =
                DeadeyeInstinctPlugin.CountLookInput.Value &&
                LastEffectiveLookInput.magnitude >= DeadeyeInstinctPlugin.LookInputThreshold.Value;

            bool moveActive = false;
            if (DeadeyeInstinctPlugin.CountMoveInput.Value)
            {
                try
                {
                    moveActive = __instance.GetRawMovementInput().magnitude >= DeadeyeInstinctPlugin.MoveInputThreshold.Value;
                }
                catch
                {
                    moveActive = false;
                }
            }

            LastLookInputActive = lookActive;
            LastMoveInputActive = moveActive;
            LastPlayerInputActive = lookActive || moveActive;

            if (__instance.aimAssist != null)
            {
                SnapshotsByAimAssist[__instance.aimAssist] = new InputSnapshot
                {
                    PlayerInputActive = LastPlayerInputActive,
                    LookInputActive = LastLookInputActive,
                    MoveInputActive = LastMoveInputActive,
                    EffectiveLookInput = LastEffectiveLookInput,
                    GamepadUsed = LastGamepadUsed
                };
            }
        }

        public static bool TryGetSnapshot(AimAssist aimAssist, out InputSnapshot snapshot)
        {
            if (aimAssist != null && SnapshotsByAimAssist.TryGetValue(aimAssist, out snapshot))
            {
                return true;
            }

            snapshot = new InputSnapshot
            {
                PlayerInputActive = LastPlayerInputActive,
                LookInputActive = LastLookInputActive,
                MoveInputActive = LastMoveInputActive,
                EffectiveLookInput = LastEffectiveLookInput,
                GamepadUsed = LastGamepadUsed
            };

            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            MethodInfo shouldConsumeMethod =
                AccessTools.Method(typeof(InputReaderUpdatePatch), nameof(ShouldConsumeRotationPullDelta));

            int patchCount = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instruction = codes[i];

                if (!instruction.Calls(GamepadUsedGetter))
                {
                    continue;
                }

                if (!IsRotationPullDeltaBranch(codes, i))
                {
                    continue;
                }

                codes[i] = new CodeInstruction(OpCodes.Call, shouldConsumeMethod);
                patchCount++;
            }

            DeadeyeInstinctPlugin.Log?.LogInfo($"InputReader.Update transpiler patched rotationPullDelta branch count={patchCount}.");
            return codes;
        }

        private static bool IsRotationPullDeltaBranch(List<CodeInstruction> codes, int index)
        {
            int end = Mathf.Min(codes.Count, index + 24);

            for (int i = index + 1; i < end; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo field)
                {
                    if (field == AimAssistField)
                    {
                        for (int j = i + 1; j < end; j++)
                        {
                            if (codes[j].opcode == OpCodes.Ldfld && codes[j].operand is FieldInfo field2)
                            {
                                if (field2 == RotationPullDeltaField)
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    if (field == RotationPullDeltaField)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ShouldConsumeRotationPullDelta(InputReader reader)
        {
            if (reader == null)
            {
                return false;
            }

            if (reader.GamepadUsed)
            {
                return true;
            }

            return DeadeyeInstinctPlugin.EnableMod.Value &&
                   DeadeyeInstinctPlugin.EnableMouseConsumesRotationPullDelta.Value;
        }

        private static Vector2 GetVector2(FieldInfo field, object target)
        {
            if (field == null || target == null)
            {
                return Vector2.zero;
            }

            try
            {
                object value = field.GetValue(target);
                return value is Vector2 vector ? vector : Vector2.zero;
            }
            catch
            {
                return Vector2.zero;
            }
        }
    }

    internal static class AimAssistLateUpdatePatch
    {
        private static readonly FieldInfo FieldScanIntervalFrames =
            AccessTools.Field(typeof(AimAssist), "scanIntervalFrames");

        private static readonly FieldInfo FieldMaxAssistDistance =
            AccessTools.Field(typeof(AimAssist), "maxAssistDistance");

        private static readonly FieldInfo FieldMinAimDot =
            AccessTools.Field(typeof(AimAssist), "minAimDot");

        private static readonly FieldInfo FieldDistanceVsDotBlend =
            AccessTools.Field(typeof(AimAssist), "distanceVsDotBlend");

        private static readonly FieldInfo FieldScoreThreshold =
            AccessTools.Field(typeof(AimAssist), "scoreThreshold");

        private static readonly FieldInfo FieldAdsDistanceMultiplier =
            AccessTools.Field(typeof(AimAssist), "adsDistanceMultiplier");

        private static readonly FieldInfo FieldFrictionStrength =
            AccessTools.Field(typeof(AimAssist), "frictionStrength");

        private static readonly FieldInfo FieldPlayerCamera =
            AccessTools.Field(typeof(AimAssist), "playerCamera");

        private static readonly FieldInfo FieldTrackedTarget =
            AccessTools.Field(typeof(AimAssist), "trackedTarget");

        private static readonly FieldInfo FieldTrackedTargetPoint =
            AccessTools.Field(typeof(AimAssist), "trackedTargetPoint");

        private static Vector2 smoothedNaturalDelta;
        private static Vector2 smoothedNaturalDirection;
        private static Vector2 smoothedMagnetDelta;
        private static Unit lastAssistTarget;
        private static float targetSwitchTimer;
        private static float nextDebugLogTime;

        internal static Unit CurrentTrackedTargetForDebug;
        internal static Camera CurrentPlayerCameraForDebug;

        public static void Prefix(AimAssist __instance)
        {
            if (!DeadeyeInstinctPlugin.EnableMod.Value || __instance == null)
            {
                return;
            }

            if (DeadeyeInstinctPlugin.OverrideOfficialScanner.Value)
            {
                Set(FieldScanIntervalFrames, __instance, DeadeyeInstinctPlugin.OfficialScanIntervalFrames.Value);
                Set(FieldMaxAssistDistance, __instance, DeadeyeInstinctPlugin.OfficialMaxAssistDistance.Value);

                float cone = Mathf.Clamp(DeadeyeInstinctPlugin.OfficialScanConeDegrees.Value, 1f, 89f);
                Set(FieldMinAimDot, __instance, Mathf.Cos(cone * Mathf.Deg2Rad));

                Set(FieldDistanceVsDotBlend, __instance, DeadeyeInstinctPlugin.OfficialDistanceVsDotBlend.Value);
                Set(FieldScoreThreshold, __instance, DeadeyeInstinctPlugin.OfficialScoreThreshold.Value);
                Set(FieldAdsDistanceMultiplier, __instance, 1f);
            }

            float keepAlive = Mathf.Clamp(DeadeyeInstinctPlugin.ScannerKeepAliveStrength.Value, 0f, 0.5f);
            object currentValue = Get(FieldFrictionStrength, __instance);
            float current = currentValue is float value ? value : 0f;

            if (current < keepAlive)
            {
                Set(FieldFrictionStrength, __instance, keepAlive);
            }
        }

        public static void Postfix(AimAssist __instance)
        {
            if (!DeadeyeInstinctPlugin.EnableMod.Value || __instance == null)
            {
                return;
            }

            bool naturalMode = DeadeyeInstinctPlugin.ActiveMode() == AssistMode.Natural;

            InputReaderUpdatePatch.InputSnapshot inputSnapshot;
            bool hasInputSnapshot = InputReaderUpdatePatch.TryGetSnapshot(__instance, out inputSnapshot);

            if (!DeadeyeInstinctPlugin.IsAimbotRuntimeEnabled())
            {
                __instance.rotationPullDelta = Vector2.zero;
                CurrentTrackedTargetForDebug = null;
                HardLockController.ClearRuntimeTarget();
                DecayNaturalDelta();
                return;
            }

            if (DeadeyeInstinctPlugin.ActiveMode() == AssistMode.HardLock)
            {
                Camera hardLockCamera = Get(FieldPlayerCamera, __instance) as Camera;
                CurrentPlayerCameraForDebug = hardLockCamera;

                // HardLock is intentionally independent from the normal rotationPullDelta pipeline.
                // rotationPullDelta is a small input-like value consumed by InputReader and then
                // multiplied by camera speed. Feeding yaw/pitch degrees into it causes over-rotation.
                __instance.rotationPullDelta = Vector2.zero;

                Unit hardLockTarget;
                Vector3 hardLockPoint;
                if (HardLockController.TryUpdateFromCamera(hardLockCamera, out hardLockTarget, out hardLockPoint))
                {
                    CurrentTrackedTargetForDebug = hardLockTarget;
                    UpdateTargetSwitchState(hardLockTarget);
                }
                else
                {
                    CurrentTrackedTargetForDebug = null;
                    ClearTargetState();
                }

                DecayNaturalDelta();
                return;
            }

            // Natural mode must not include the official mouse ADS snap/pull.
            // Keep controller official pull when configured, because controller is the original supported path.
            // Unity calls InputReader.Update before AimAssist.LateUpdate, so LastGamepadUsed represents
            // the current frame's input mode for the rotationPullDelta that will be consumed next frame.
            if (naturalMode)
            {
                bool shouldClearOfficialPull =
                    !inputSnapshot.GamepadUsed ||
                    !DeadeyeInstinctPlugin.EffectiveNaturalKeepOfficialGamepadPull();

                if (shouldClearOfficialPull)
                {
                    __instance.rotationPullDelta = Vector2.zero;
                }
            }

            if (!DeadeyeInstinctPlugin.EnableContinuousRotationalAssist.Value)
            {
                DecayNaturalDelta();
                return;
            }

            if (DeadeyeInstinctPlugin.RequirePlayerInput.Value &&
                !inputSnapshot.PlayerInputActive)
            {
                DecayNaturalDelta();
                return;
            }

            Unit target = Get(FieldTrackedTarget, __instance) as Unit;
            if (target == null || target.UnitState == UnitState.Dead || target.mainCollider == null)
            {
                CurrentTrackedTargetForDebug = null;
                ClearTargetState();
                DecayNaturalDelta();
                return;
            }

            UpdateTargetSwitchState(target);

            object pointObject = Get(FieldTrackedTargetPoint, __instance);
            if (!(pointObject is Vector3 targetPoint))
            {
                DecayNaturalDelta();
                return;
            }

            Camera camera = Get(FieldPlayerCamera, __instance) as Camera;
            if (camera == null)
            {
                CurrentPlayerCameraForDebug = null;
                CurrentTrackedTargetForDebug = target;
                DecayNaturalDelta();
                return;
            }

            CurrentPlayerCameraForDebug = camera;
            CurrentTrackedTargetForDebug = target;

            bool magnetWeakspotTargetUsed = false;
            if (DeadeyeInstinctPlugin.ActiveMode() == AssistMode.Magnet)
            {
                targetPoint = GetMagnetTargetPoint(target, targetPoint, out magnetWeakspotTargetUsed);

                if (magnetWeakspotTargetUsed &&
                    DeadeyeInstinctPlugin.EffectiveMagnetWeakspotOverrideOfficialPull())
                {
                    // Official AimAssist has already written a center-biased rotationPullDelta in LateUpdate().
                    // If we leave it there, our weakspot pull is only added on top and the final result still
                    // tends to the center. In weakspot Magnet mode, replace the official center pull with
                    // our weakspot-directed pull.
                    __instance.rotationPullDelta = Vector2.zero;
                }
            }

            Vector3 viewport = camera.WorldToViewportPoint(targetPoint);
            if (viewport.z <= 0f)
            {
                DecayNaturalDelta();
                return;
            }

            Vector2 offset = new Vector2(
                viewport.x - 0.5f,
                -(viewport.y - 0.5f)
            );

            if (DeadeyeInstinctPlugin.InvertX.Value)
            {
                offset.x = -offset.x;
            }

            if (DeadeyeInstinctPlugin.InvertY.Value)
            {
                offset.y = -offset.y;
            }

            if (offset.sqrMagnitude <= 0.000001f)
            {
                DecayNaturalDelta();
                return;
            }

            Vector3 toTarget = targetPoint - camera.transform.position;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f)
            {
                DecayNaturalDelta();
                return;
            }

            Vector3 direction = toTarget / distance;
            float dot = Mathf.Clamp(Vector3.Dot(camera.transform.forward, direction), -1f, 1f);
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

            float outer = Mathf.Clamp(DeadeyeInstinctPlugin.EffectiveOuterBubbleAngleDegrees(), 0.1f, 89f);
            float inner = Mathf.Clamp(DeadeyeInstinctPlugin.EffectiveInnerBubbleAngleDegrees(), 0.05f, outer);

            if (angle > outer)
            {
                DecayNaturalDelta();
                return;
            }

            float bubble = Mathf.InverseLerp(outer, inner, angle);
            bubble = Mathf.Clamp01(bubble);
            bubble = Mathf.Pow(bubble, Mathf.Max(0.01f, DeadeyeInstinctPlugin.EffectiveBubbleCurvePower()));

            float maxDistance = Mathf.Max(1f, DeadeyeInstinctPlugin.OfficialMaxAssistDistance.Value);
            float distanceStrength = 1f - Mathf.Clamp01(distance / maxDistance);
            distanceStrength = Mathf.Pow(
                distanceStrength,
                Mathf.Max(0.01f, DeadeyeInstinctPlugin.EffectiveDistanceFalloffPower())
            );

            float amount =
                Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveRotationDeltaPerSecond()) *
                Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveAssistCoefficient()) *
                bubble *
                distanceStrength *
                Time.deltaTime;

            amount *= GetTargetSwitchScale();

            if (DeadeyeInstinctPlugin.ActiveMode() == AssistMode.Magnet)
            {
                amount *= GetMagnetEdgeScale(angle, outer, bubble);
            }

            amount = Mathf.Min(
                amount,
                Mathf.Max(0.001f, DeadeyeInstinctPlugin.EffectiveMaxRotationDeltaPerFrame())
            );

            if (amount <= 0.000001f)
            {
                DecayNaturalDelta();
                return;
            }

            Vector2 addDelta = ComputeAssistDelta(offset, amount, bubble, inputSnapshot);

            if (addDelta.sqrMagnitude <= 0.0000001f)
            {
                DecayNaturalDelta();
                return;
            }

            __instance.rotationPullDelta += addDelta;

            if (DeadeyeInstinctPlugin.DebugLogging.Value && Time.unscaledTime >= nextDebugLogTime)
            {
                nextDebugLogTime = Time.unscaledTime + 0.5f;
                DeadeyeInstinctPlugin.Log.LogInfo(
                    $"Mode={DeadeyeInstinctPlugin.ActiveMode()}, target={target.name}, angle={angle:F2}, bubble={bubble:F3}, dist={distanceStrength:F3}, add={addDelta}, final={__instance.rotationPullDelta}"
                );
            }
        }

        private static Vector2 ComputeAssistDelta(Vector2 offset, float amount, float bubble, InputReaderUpdatePatch.InputSnapshot inputSnapshot)
        {
            if (offset.sqrMagnitude <= 0.000001f || amount <= 0.000001f)
            {
                return Vector2.zero;
            }

            Vector2 targetDirection = offset.normalized;

            switch (DeadeyeInstinctPlugin.ActiveMode())
            {
                case AssistMode.Magnet:
                    return ComputeMagnetDelta(targetDirection, amount, inputSnapshot);

                case AssistMode.Natural:
                    return ComputeNaturalDelta(targetDirection, amount, bubble, inputSnapshot);

                default:
                    return targetDirection * amount;
            }
        }

        private static Vector3 GetMagnetTargetPoint(Unit target, Vector3 officialTargetPoint, out bool weakspotTargetUsed)
        {
            weakspotTargetUsed = false;

            if (target == null)
            {
                return officialTargetPoint;
            }

            if (DeadeyeInstinctPlugin.EffectiveMagnetPreferWeakspot())
            {
                try
                {
                    Vector3 weakspotPoint;
                    float weakspotMultiplier;

                    // Use the official target point as the reference position.
                    // This keeps the "free movement inside target box" behavior instead of first biasing to center.
                    if (TryGetBestWeakspotPoint(target, officialTargetPoint, out weakspotPoint, out weakspotMultiplier))
                    {
                        weakspotTargetUsed = true;
                        float weakspotBias = Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetWeakspotBias());
                        return Vector3.Lerp(officialTargetPoint, weakspotPoint, weakspotBias);
                    }
                }
                catch
                {
                    // Weakspot targeting is optional. Fall through to normal stable target point.
                }
            }

            try
            {
                Collider collider = target.mainCollider;
                if (collider != null)
                {
                    Bounds bounds = collider.bounds;
                    float centerBias = Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetCenterBias());
                    return Vector3.Lerp(officialTargetPoint, bounds.center, centerBias);
                }
            }
            catch
            {
                // Fall back to official target point.
            }

            return officialTargetPoint;
        }


        private static bool TryGetBestWeakspotPoint(Unit target, Vector3 referenceWorldPoint, out Vector3 point, out float multiplier)
        {
            point = Vector3.zero;
            multiplier = 0f;

            if (target == null)
            {
                return false;
            }

            Hitmesh[] hitmeshes = null;
            try
            {
                hitmeshes = target.GetComponentsInChildren<Hitmesh>(true);
            }
            catch
            {
                return false;
            }

            if (hitmeshes == null || hitmeshes.Length == 0)
            {
                return false;
            }

            bool found = false;
            Vector3 bestPoint = Vector3.zero;
            float bestMultiplier = 0f;
            float bestDistanceSqr = float.MaxValue;
            int bestPriority = -1;

            for (int i = 0; i < hitmeshes.Length; i++)
            {
                Hitmesh hitmesh = hitmeshes[i];
                if (hitmesh == null || hitmesh.hitShapes == null || hitmesh.hitShapes.Length == 0)
                {
                    continue;
                }

                if (hitmesh.owner != null && hitmesh.owner != target)
                {
                    continue;
                }

                for (int j = 0; j < hitmesh.hitShapes.Length; j++)
                {
                    Hitmesh.Data data = hitmesh.hitShapes[j];

                    if (data.isInvulnerable)
                    {
                        continue;
                    }

                    float shapeMultiplier = data.GetShapeMultiplier();
                    if (shapeMultiplier <= 0f)
                    {
                        continue;
                    }

                    Vector3 candidatePoint;
                    float candidateDistanceSqr;
                    if (!TryGetShapeAimPoint(hitmesh, data.shapeId, referenceWorldPoint, out candidatePoint, out candidateDistanceSqr))
                    {
                        continue;
                    }

                    int priority = GetPartPriority(data.shapeId.part);

                    bool better =
                        !found ||
                        shapeMultiplier > bestMultiplier + 0.0001f ||
                        (Mathf.Abs(shapeMultiplier - bestMultiplier) <= 0.0001f && priority > bestPriority) ||
                        (Mathf.Abs(shapeMultiplier - bestMultiplier) <= 0.0001f && priority == bestPriority && candidateDistanceSqr < bestDistanceSqr);

                    if (!better)
                    {
                        continue;
                    }

                    found = true;
                    bestMultiplier = shapeMultiplier;
                    bestPriority = priority;
                    bestDistanceSqr = candidateDistanceSqr;
                    bestPoint = candidatePoint;
                    multiplier = shapeMultiplier;
                }
            }

            if (!found)
            {
                return false;
            }

            point = bestPoint;
            return true;
        }

        private static int GetPartPriority(HitboxColliders.Parts part)
        {
            if (part == HitboxColliders.Parts.Eye)
            {
                return 30;
            }

            if (part == HitboxColliders.Parts.Head)
            {
                return 20;
            }

            if (part == HitboxColliders.Parts.Thorax)
            {
                return 10;
            }

            return 0;
        }

        private static bool TryGetShapeAimPoint(
            Hitmesh hitmesh,
            HitboxColliders.ShapeId shapeId,
            Vector3 referenceWorldPoint,
            out Vector3 point,
            out float distanceSqr)
        {
            point = Vector3.zero;
            distanceSqr = float.MaxValue;

            if (hitmesh == null || shapeId.part == HitboxColliders.Parts.None)
            {
                return false;
            }

            try
            {
                HitboxColliders loadedHitboxColliders = StaticInstance<AsyncAssetLoading>.Instance.loadedHitboxColliders;
                if (loadedHitboxColliders == null ||
                    !loadedHitboxColliders.runtimeHitmeshData.IsCreated ||
                    !loadedHitboxColliders.runtimeVertexData.IsCreated)
                {
                    return false;
                }

                int frameIndex = hitmesh.hitboxFrameIndex;
                if (frameIndex < 0 || frameIndex >= loadedHitboxColliders.runtimeHitmeshData.Length)
                {
                    return false;
                }

                HitboxColliders.RuntimeHitboxData runtimeHitboxData =
                    loadedHitboxColliders.runtimeHitmeshData[frameIndex];

                Vector3 localReference3 = hitmesh.transform.InverseTransformPoint(referenceWorldPoint);
                Vector2 localReference = new Vector2(localReference3.x, localReference3.y);

                for (int i = 0; i < runtimeHitboxData.shapes.Length; i++)
                {
                    HitboxColliders.ShapeData shapeData = runtimeHitboxData.shapes[i];
                    if (shapeData.shapeId != shapeId)
                    {
                        continue;
                    }

                    int length = (int)shapeData.length;
                    if (length <= 1)
                    {
                        return false;
                    }

                    Vector2[] vertices = new Vector2[length];
                    for (int j = 0; j < length; j++)
                    {
                        int vertexIndex = shapeData.index + j;
                        if (vertexIndex < 0 || vertexIndex >= loadedHitboxColliders.runtimeVertexData.Length)
                        {
                            return false;
                        }

                        Unity.Mathematics.float2 vertex =
                            loadedHitboxColliders.runtimeVertexData[vertexIndex];

                        vertices[j] = new Vector2(vertex.x, vertex.y);
                    }

                    Vector2 localAimPoint;

                    if (IsPointInsidePolygon(localReference, vertices))
                    {
                        // Already inside the weakspot polygon:
                        // keep the current official point so Magnet does not force the aim to the center.
                        localAimPoint = localReference;
                        distanceSqr = 0f;
                    }
                    else
                    {
                        localAimPoint = GetClosestPointOnPolygon(localReference, vertices, out distanceSqr);
                    }

                    point = hitmesh.transform.TransformPoint(
                        new Vector3(localAimPoint.x, localAimPoint.y, localReference3.z)
                    );

                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
            {
                return false;
            }

            bool inside = false;
            int j = polygon.Length - 1;

            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];

                bool intersects =
                    ((pi.y > point.y) != (pj.y > point.y)) &&
                    (point.x < (pj.x - pi.x) * (point.y - pi.y) / Mathf.Max(0.000001f, pj.y - pi.y) + pi.x);

                if (intersects)
                {
                    inside = !inside;
                }

                j = i;
            }

            return inside;
        }

        private static Vector2 GetClosestPointOnPolygon(Vector2 point, Vector2[] polygon, out float bestDistanceSqr)
        {
            bestDistanceSqr = float.MaxValue;
            Vector2 bestPoint = point;

            if (polygon == null || polygon.Length == 0)
            {
                return bestPoint;
            }

            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];

                Vector2 projected = ProjectPointOnSegment(point, a, b);
                float distanceSqr = (projected - point).sqrMagnitude;

                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestPoint = projected;
                }
            }

            return bestPoint;
        }

        private static Vector2 ProjectPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denom = Vector2.Dot(ab, ab);

            if (denom <= 0.000001f)
            {
                return a;
            }

            float t = Vector2.Dot(point - a, ab) / denom;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }

        private static Vector2 ComputeMagnetDelta(Vector2 targetDirection, float amount, InputReaderUpdatePatch.InputSnapshot inputSnapshot)
        {
            float scale = 1f;

            if (!inputSnapshot.LookInputActive && inputSnapshot.MoveInputActive)
            {
                scale *= Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetMovementOnlyScale());
            }

            scale *= GetMagnetLookAwayEscapeScale(targetDirection, inputSnapshot);

            Vector2 rawDelta = targetDirection * amount * scale;

            smoothedMagnetDelta = Vector2.Lerp(
                smoothedMagnetDelta,
                rawDelta,
                GetMagnetSmoothingT()
            );

            return smoothedMagnetDelta;
        }

        private static float GetMagnetLookAwayEscapeScale(Vector2 targetDirection, InputReaderUpdatePatch.InputSnapshot inputSnapshot)
        {
            if (!DeadeyeInstinctPlugin.EffectiveMagnetAllowLookAwayEscape())
            {
                return 1f;
            }

            if (!inputSnapshot.LookInputActive)
            {
                return 1f;
            }

            Vector2 lookInput = inputSnapshot.EffectiveLookInput;
            float lookMagnitude = lookInput.magnitude;
            if (lookMagnitude <= 0.000001f || targetDirection.sqrMagnitude <= 0.000001f)
            {
                return 1f;
            }

            Vector2 lookDirection = lookInput / lookMagnitude;
            float alignment = Vector2.Dot(lookDirection, targetDirection);

            float threshold = Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetLookAwayThreshold());

            // alignment < -threshold means the player is pushing the camera away from the target.
            if (alignment >= -threshold)
            {
                return 1f;
            }

            float away01 = Mathf.InverseLerp(threshold, 1f, -alignment);
            away01 = Mathf.Clamp01(away01);
            away01 = Mathf.Pow(
                away01,
                Mathf.Max(0.01f, DeadeyeInstinctPlugin.EffectiveMagnetLookAwayCurvePower())
            );

            float minScale = Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetLookAwayMinScale());
            float scale = Mathf.Lerp(1f, minScale, away01);

            if (inputSnapshot.MoveInputActive)
            {
                float movementScale = Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetLookAwayMovementScale());
                scale = Mathf.Lerp(scale, 1f, movementScale);
            }

            return Mathf.Clamp01(scale);
        }

        private static Vector2 ComputeNaturalDelta(Vector2 targetDirection, float amount, float bubble, InputReaderUpdatePatch.InputSnapshot inputSnapshot)
        {
            targetDirection = SmoothNaturalDirection(targetDirection);

            Vector2 playerLook = inputSnapshot.EffectiveLookInput;
            float lookMagnitude = playerLook.magnitude;

            bool hasLookInput =
                inputSnapshot.LookInputActive &&
                lookMagnitude >= DeadeyeInstinctPlugin.EffectiveNaturalMinLookInput();

            float releaseT = Mathf.Clamp01(bubble);
            releaseT = Mathf.Pow(
                releaseT,
                Mathf.Max(0.01f, DeadeyeInstinctPlugin.EffectiveNaturalReleaseCurvePower())
            );

            releaseT = Mathf.Lerp(
                Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveNaturalOuterEdgeStrength()),
                1f,
                releaseT
            );

            amount *= releaseT;

            if (amount <= 0.000001f)
            {
                DecayNaturalDelta();
                return smoothedNaturalDelta;
            }

            if (!hasLookInput)
            {
                if (!inputSnapshot.MoveInputActive)
                {
                    DecayNaturalDelta();
                    return smoothedNaturalDelta;
                }

                Vector2 movementOnlyDelta =
                    targetDirection *
                    amount *
                    Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveNaturalMovementOnlyScale());

                smoothedNaturalDelta = Vector2.Lerp(
                    smoothedNaturalDelta,
                    movementOnlyDelta,
                    GetSmoothingT()
                );

                return smoothedNaturalDelta;
            }

            Vector2 lookDirection = playerLook / lookMagnitude;
            float alignment = Vector2.Dot(lookDirection, targetDirection);

            // Positive alignment means the player is already moving aim toward the target.
            // Natural mode should usually not boost that, so NaturalTowardAssist defaults to 0.
            float toward = Mathf.Clamp01(alignment);

            // Negative alignment means the player is moving aim away from the target.
            // Add a small opposite force, which feels like sticky resistance rather than snapping.
            float away = Mathf.Clamp01(-alignment);

            // Side movement gets a small configurable bend so sweeping across targets is easier.
            float side = Mathf.Clamp01(1f - Mathf.Abs(alignment));

            float naturalFactor =
                DeadeyeInstinctPlugin.EffectiveNaturalTowardAssist() * toward +
                DeadeyeInstinctPlugin.EffectiveNaturalSideAssist() * side +
                DeadeyeInstinctPlugin.EffectiveNaturalAwayDamping() * away;

            if (naturalFactor <= 0.000001f)
            {
                DecayNaturalDelta();
                return smoothedNaturalDelta;
            }

            Vector2 rawDelta = targetDirection * amount * naturalFactor;

            float capFromInput =
                Mathf.Max(
                    DeadeyeInstinctPlugin.EffectiveNaturalMinDeltaPerFrame(),
                    lookMagnitude * Mathf.Max(0.01f, DeadeyeInstinctPlugin.EffectiveNaturalMaxInputFraction())
                );

            rawDelta = Vector2.ClampMagnitude(rawDelta, capFromInput);

            smoothedNaturalDelta = Vector2.Lerp(
                smoothedNaturalDelta,
                rawDelta,
                GetSmoothingT()
            );

            return smoothedNaturalDelta;
        }

        private static Vector2 SmoothNaturalDirection(Vector2 targetDirection)
        {
            if (targetDirection.sqrMagnitude <= 0.000001f)
            {
                return targetDirection;
            }

            if (smoothedNaturalDirection.sqrMagnitude <= 0.000001f)
            {
                smoothedNaturalDirection = targetDirection;
                return targetDirection;
            }

            float t = GetNaturalDirectionSmoothingT();
            smoothedNaturalDirection = Vector2.Lerp(smoothedNaturalDirection, targetDirection, t);

            if (smoothedNaturalDirection.sqrMagnitude <= 0.000001f)
            {
                smoothedNaturalDirection = targetDirection;
            }
            else
            {
                smoothedNaturalDirection.Normalize();
            }

            return smoothedNaturalDirection;
        }

        private static void UpdateTargetSwitchState(Unit target)
        {
            if (target == null)
            {
                ClearTargetState();
                return;
            }

            if (lastAssistTarget != target)
            {
                lastAssistTarget = target;
                targetSwitchTimer = Mathf.Max(0f, SafeValue(DeadeyeInstinctPlugin.TargetSwitchFadeSeconds, 0.12f));
                smoothedNaturalDirection = Vector2.zero;
            }
            else if (targetSwitchTimer > 0f)
            {
                targetSwitchTimer -= Time.unscaledDeltaTime;
                if (targetSwitchTimer < 0f)
                {
                    targetSwitchTimer = 0f;
                }
            }
        }

        private static void ClearTargetState()
        {
            lastAssistTarget = null;
            targetSwitchTimer = 0f;
            smoothedNaturalDirection = Vector2.zero;
        }

        private static float GetMagnetEdgeScale(float angle, float outerAngle, float bubble)
        {
            float edgeScale = Mathf.Clamp01(bubble);
            edgeScale = Mathf.Pow(
                edgeScale,
                Mathf.Max(0.01f, DeadeyeInstinctPlugin.EffectiveMagnetEdgeSofteningPower())
            );

            edgeScale = Mathf.Lerp(
                Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetOuterEdgeStrength()),
                1f,
                edgeScale
            );

            float padding = Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveMagnetSnapBackAnglePadding());
            if (padding > 0f)
            {
                float fromOuter = outerAngle - angle;
                float paddingScale = Mathf.Clamp01(fromOuter / padding);
                paddingScale = Mathf.SmoothStep(0f, 1f, paddingScale);

                edgeScale *= Mathf.Lerp(
                    Mathf.Clamp01(DeadeyeInstinctPlugin.EffectiveMagnetOuterEdgeStrength()),
                    1f,
                    paddingScale
                );
            }

            return Mathf.Clamp01(edgeScale);
        }

        private static float GetTargetSwitchScale()
        {
            float fadeSeconds = Mathf.Max(0f, SafeValue(DeadeyeInstinctPlugin.TargetSwitchFadeSeconds, 0.12f));
            if (fadeSeconds <= 0f || targetSwitchTimer <= 0f)
            {
                return 1f;
            }

            float elapsed01 = 1f - Mathf.Clamp01(targetSwitchTimer / fadeSeconds);
            float minScale = Mathf.Clamp01(SafeValue(DeadeyeInstinctPlugin.TargetSwitchMinScale, 0.25f));

            return Mathf.Lerp(minScale, 1f, elapsed01);
        }

        private static void DecayNaturalDelta()
        {
            if (smoothedNaturalDelta.sqrMagnitude <= 0.0000001f)
            {
                smoothedNaturalDelta = Vector2.zero;
            }
            else
            {
                smoothedNaturalDelta = Vector2.Lerp(
                    smoothedNaturalDelta,
                    Vector2.zero,
                    GetNaturalReleaseSmoothingT()
                );
            }

            if (smoothedMagnetDelta.sqrMagnitude <= 0.0000001f)
            {
                smoothedMagnetDelta = Vector2.zero;
            }
            else
            {
                smoothedMagnetDelta = Vector2.Lerp(
                    smoothedMagnetDelta,
                    Vector2.zero,
                    GetMagnetReleaseSmoothingT()
                );
            }
        }

        private static float GetNaturalDirectionSmoothingT()
        {
            float smoothing = Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveNaturalDirectionSmoothing());

            if (smoothing <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        }

        private static float GetNaturalReleaseSmoothingT()
        {
            float smoothing = Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveNaturalReleaseSmoothing());

            if (smoothing <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        }

        private static float GetMagnetSmoothingT()
        {
            float smoothing = Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveMagnetSmoothing());

            if (smoothing <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        }

        private static float GetMagnetReleaseSmoothingT()
        {
            float smoothing = Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveMagnetReleaseSmoothing());

            if (smoothing <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        }

        private static float GetSmoothingT()
        {
            float smoothing = Mathf.Max(0f, DeadeyeInstinctPlugin.EffectiveNaturalSmoothing());

            if (smoothing <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        }

        private static float SafeValue(ConfigEntry<float> entry, float fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        private static bool SafeValue(ConfigEntry<bool> entry, bool fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        private static void Set(FieldInfo field, object target, object value)
        {
            if (field == null || target == null)
            {
                return;
            }

            try
            {
                field.SetValue(target, value);
            }
            catch (Exception ex)
            {
                DeadeyeInstinctPlugin.Log?.LogWarning($"Failed to set {field.Name}: {ex.Message}");
            }
        }

        private static object Get(FieldInfo field, object target)
        {
            if (field == null || target == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target);
            }
            catch
            {
                return null;
            }
        }
    }



    internal static class HardLockController
    {
        internal struct HardLockTargetState
        {
            public bool HasTarget;
            public bool IsAligned;
            public Unit Target;
            public Vector3 TargetPoint;
            public float AngleDegrees;
            public float Distance;
            public float WeakspotMultiplier;
        }

        private static HardLockTargetState currentState;
        private static readonly MethodInfo MethodIsHostileTo = AccessTools.Method(typeof(Npc), "IsHostileTo", new[] { typeof(FactionIds) });
        private static readonly MethodInfo MethodGetPositionToAimAt = AccessTools.Method(typeof(Unit), "GetPositionToAimAt");

        internal static HardLockTargetState CurrentState => currentState;

        internal static void ClearRuntimeTarget()
        {
            currentState = new HardLockTargetState();
        }

        internal static bool TryUpdateFromCamera(Camera camera, out Unit target, out Vector3 targetPoint)
        {
            target = null;
            targetPoint = Vector3.zero;

            if (camera == null || !DeadeyeInstinctPlugin.IsHardLockModeActive())
            {
                ClearRuntimeTarget();
                return false;
            }

            HardLockTargetState state;
            if (!TrySelectTarget(camera, out state))
            {
                ClearRuntimeTarget();
                return false;
            }

            state.AngleDegrees = ComputeHardLockAngle(camera, state.TargetPoint);

            object controller = CameraRecoilLateUpdatePatch.GetExtendedCameraController(camera);
            if (controller != null)
            {
                CameraRecoilLateUpdatePatch.RotateTowardPosition(controller, state.TargetPoint);
                state.AngleDegrees = ComputeHardLockAngle(camera, state.TargetPoint);
            }

            state.IsAligned =
                !float.IsNaN(state.AngleDegrees) &&
                !float.IsInfinity(state.AngleDegrees) &&
                state.AngleDegrees <= Mathf.Max(0.01f, DeadeyeInstinctPlugin.AutoFireMaxAngleDegrees.Value);

            currentState = state;
            target = state.Target;
            targetPoint = state.TargetPoint;
            return true;
        }

        internal static void ApplyImmediateRecoilCompensation(CameraRecoil recoil)
        {
            if (!DeadeyeInstinctPlugin.IsHardLockModeActive() || !DeadeyeInstinctPlugin.HardLockRecoilCompensation.Value)
            {
                return;
            }

            if (!currentState.HasTarget || currentState.Target == null ||
                !IsValidHardLockTarget(currentState.Target as Npc) ||
                !IsFiniteVector(currentState.TargetPoint) ||
                !IsPointNearTargetBounds(currentState.Target, currentState.TargetPoint, 3.5f))
            {
                ClearRuntimeTarget();
                return;
            }

            Camera camera = AimAssistLateUpdatePatch.CurrentPlayerCameraForDebug;
            if (camera == null)
            {
                return;
            }

            object controller = CameraRecoilLateUpdatePatch.GetExtendedCameraController(recoil);
            if (controller == null)
            {
                return;
            }

            CameraRecoilLateUpdatePatch.RotateTowardPosition(controller, currentState.TargetPoint);
            currentState.AngleDegrees = ComputeHardLockAngle(camera, currentState.TargetPoint);
            currentState.IsAligned = currentState.AngleDegrees <= Mathf.Max(0.01f, DeadeyeInstinctPlugin.AutoFireMaxAngleDegrees.Value);
        }

        private static float ComputeHardLockAngle(Camera camera, Vector3 point)
        {
            if (camera == null)
            {
                return 180f;
            }

            Vector3 toTarget = point - camera.transform.position;
            if (toTarget.sqrMagnitude <= 0.000001f)
            {
                return 0f;
            }

            return Vector3.Angle(camera.transform.forward, toTarget.normalized);
        }

        private static bool TrySelectTarget(Camera camera, out HardLockTargetState bestState)
        {
            bestState = new HardLockTargetState();

            List<Npc> aliveNpcs = GetAliveNpcs();
            if (aliveNpcs == null)
            {
                return false;
            }

            float maxDistance = GetEffectiveHardLockMaxDistance();
            Unit previousTarget = currentState.Target;
            bool found = false;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < aliveNpcs.Count; i++)
            {
                Npc npc = aliveNpcs[i];
                if (!IsValidHardLockTarget(npc))
                {
                    continue;
                }

                Vector3 targetPoint;
                float weakspotMultiplier;
                if (!TryResolveHardLockPoint(npc, camera, out targetPoint, out weakspotMultiplier))
                {
                    continue;
                }

                float distance = Vector3.Distance(camera.transform.position, targetPoint);
                if (distance > maxDistance)
                {
                    continue;
                }

                bool requireVisible =
                    (DeadeyeInstinctPlugin.HardLockRequireVisibleTarget != null && DeadeyeInstinctPlugin.HardLockRequireVisibleTarget.Value) ||
                    (DeadeyeInstinctPlugin.HardLockRequireLineOfSight != null && DeadeyeInstinctPlugin.HardLockRequireLineOfSight.Value);

                if (requireVisible && !HasLineOfSight(camera, npc, targetPoint, distance))
                {
                    continue;
                }

                float score = ScoreTarget(npc, distance, maxDistance, weakspotMultiplier, previousTarget);
                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestState = new HardLockTargetState
                    {
                        HasTarget = true,
                        Target = npc,
                        TargetPoint = targetPoint,
                        Distance = distance,
                        WeakspotMultiplier = weakspotMultiplier,
                        IsAligned = false,
                        AngleDegrees = 180f
                    };
                }
            }

            return found;
        }

        private static float GetEffectiveHardLockMaxDistance()
        {
            float configured = Mathf.Max(1f, DeadeyeInstinctPlugin.HardLockMaxDistance.Value);
            float cap = DeadeyeInstinctPlugin.HardLockMaxDistanceCap != null
                ? DeadeyeInstinctPlugin.HardLockMaxDistanceCap.Value
                : 90f;

            if (cap > 0f)
            {
                configured = Mathf.Min(configured, Mathf.Max(1f, cap));
            }

            return configured;
        }

        private static List<Npc> GetAliveNpcs()
        {
            try
            {
                UnitManager unitManager = StaticInstance<UnitManager>.Instance;
                if (unitManager == null)
                {
                    return null;
                }

                Npc[] npcs = unitManager.GetAllNpcs(false);
                if (npcs == null || npcs.Length == 0)
                {
                    return null;
                }

                return new List<Npc>(npcs);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsHostileToPlayer(Npc npc)
        {
            if (npc == null || MethodIsHostileTo == null)
            {
                return false;
            }

            try
            {
                object value = MethodIsHostileTo.Invoke(npc, new object[] { FactionIds.Player });
                return value is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidHardLockTarget(Npc npc)
        {
            if (npc == null || npc.transform == null || !npc.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (npc.UnitState == UnitState.Dead || npc.IsProtectedNpc || npc.IsPlayerFaction || npc.mainCollider == null)
            {
                return false;
            }

            return IsHostileToPlayer(npc);
        }

        private static float ScoreTarget(Npc npc, float distance, float maxDistance, float weakspotMultiplier, Unit previousTarget)
        {
            float distanceScore = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, maxDistance));
            float lowHealthScore = GetLowHealthScore(npc);
            float weakspotScore = Mathf.Clamp01(weakspotMultiplier / 1.5f);
            float stickyBonus = (previousTarget != null && npc == previousTarget)
                ? Mathf.Max(0f, DeadeyeInstinctPlugin.HardLockStickyTargetBonus.Value)
                : 0f;

            switch (DeadeyeInstinctPlugin.HardLockTargetPriorityMode.Value)
            {
                case HardLockTargetPriority.Nearest:
                    return distanceScore + stickyBonus;

                case HardLockTargetPriority.LowestHealth:
                    return lowHealthScore + distanceScore * 0.375f + stickyBonus;

                case HardLockTargetPriority.WeakspotThenDistance:
                    return weakspotScore + distanceScore * 0.75f + stickyBonus;

                default:
                    return
                        distanceScore * Mathf.Max(0f, DeadeyeInstinctPlugin.HardLockDistanceWeight.Value) +
                        lowHealthScore * Mathf.Max(0f, DeadeyeInstinctPlugin.HardLockLowHealthWeight.Value) +
                        weakspotScore * Mathf.Max(0f, DeadeyeInstinctPlugin.HardLockWeakspotWeight.Value) +
                        stickyBonus;
            }
        }

        private static float GetLowHealthScore(Unit unit)
        {
            if (unit == null || unit.Stats == null)
            {
                return 0f;
            }

            try
            {
                float current = Mathf.Max(0f, unit.GetCurrentHealth());
                float max = unit.Stats.GetAttribute(EntityAttributes.Stat_MaxHealth);
                if (max <= 0.001f)
                {
                    return 0f;
                }

                return 1f - Mathf.Clamp01(current / max);
            }
            catch
            {
                return 0f;
            }
        }

        private static bool TryResolveHardLockPoint(Unit target, Camera camera, out Vector3 point, out float weakspotMultiplier)
        {
            point = Vector3.zero;
            weakspotMultiplier = 0.5f;

            if (target == null)
            {
                return false;
            }

            Vector3 basePoint = GetFallbackTargetPoint(target);
            if (!IsFiniteVector(basePoint) || !IsPointNearTargetBounds(target, basePoint, 3.5f))
            {
                return false;
            }

            point = basePoint;

            if (!DeadeyeInstinctPlugin.HardLockPreferWeakspot.Value)
            {
                return true;
            }

            Vector3 weakspotPoint;
            float multiplier;
            if (TryGetBestWeakspotCenterPoint(target, basePoint, out weakspotPoint, out multiplier) &&
                IsFiniteVector(weakspotPoint) &&
                IsPointNearTargetBounds(target, weakspotPoint, 3.5f))
            {
                weakspotMultiplier = multiplier;
                float bias = Mathf.Clamp01(DeadeyeInstinctPlugin.HardLockWeakspotBias.Value);
                Vector3 blended = Vector3.Lerp(basePoint, weakspotPoint, bias);
                if (IsFiniteVector(blended) && IsPointNearTargetBounds(target, blended, 3.5f))
                {
                    point = blended;
                }
            }

            return true;
        }

        private static Vector3 GetFallbackTargetPoint(Unit target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            try
            {
                if (MethodGetPositionToAimAt != null)
                {
                    object value = MethodGetPositionToAimAt.Invoke(target, null);
                    if (value is Vector3 aimPosition && IsFiniteVector(aimPosition) && IsPointNearTargetBounds(target, aimPosition, 2.5f))
                    {
                        return aimPosition;
                    }
                }
            }
            catch
            {
                // Fall through to collider center.
            }

            try
            {
                if (target.mainCollider != null)
                {
                    return target.mainCollider.bounds.center;
                }
            }
            catch
            {
                // Fall through to transform position.
            }

            return target.transform.position + Vector3.up;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return
                !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsPointNearTargetBounds(Unit target, Vector3 point, float padding)
        {
            if (target == null || !IsFiniteVector(point))
            {
                return false;
            }

            try
            {
                if (target.mainCollider != null)
                {
                    Bounds bounds = target.mainCollider.bounds;
                    bounds.Expand(Mathf.Max(0.1f, padding));
                    return bounds.Contains(point);
                }
            }
            catch
            {
                // Fall through to transform-distance fallback.
            }

            try
            {
                return Vector3.Distance(target.transform.position + Vector3.up, point) <= Mathf.Max(2f, padding);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetBestWeakspotCenterPoint(Unit target, Vector3 referenceWorldPoint, out Vector3 point, out float multiplier)
        {
            point = Vector3.zero;
            multiplier = 0f;

            Hitmesh[] hitmeshes = null;
            try
            {
                hitmeshes = target.GetComponentsInChildren<Hitmesh>(true);
            }
            catch
            {
                return false;
            }

            if (hitmeshes == null || hitmeshes.Length == 0)
            {
                return false;
            }

            bool found = false;
            Vector3 bestPoint = Vector3.zero;
            float bestMultiplier = 0f;
            float bestDistanceSqr = float.MaxValue;
            int bestPriority = -1;

            for (int i = 0; i < hitmeshes.Length; i++)
            {
                Hitmesh hitmesh = hitmeshes[i];
                if (hitmesh == null || hitmesh.hitShapes == null || hitmesh.hitShapes.Length == 0)
                {
                    continue;
                }

                if (hitmesh.owner != null && hitmesh.owner != target)
                {
                    continue;
                }

                for (int j = 0; j < hitmesh.hitShapes.Length; j++)
                {
                    Hitmesh.Data data = hitmesh.hitShapes[j];
                    if (data.isInvulnerable)
                    {
                        continue;
                    }

                    float shapeMultiplier = data.GetShapeMultiplier();
                    if (shapeMultiplier <= 0f)
                    {
                        continue;
                    }

                    Vector3 candidatePoint;
                    float distanceSqr;
                    if (!TryGetShapeCenterPoint(hitmesh, data.shapeId, referenceWorldPoint, out candidatePoint, out distanceSqr))
                    {
                        continue;
                    }

                    int priority = GetPartPriority(data.shapeId.part);
                    bool better =
                        !found ||
                        shapeMultiplier > bestMultiplier + 0.0001f ||
                        (Mathf.Abs(shapeMultiplier - bestMultiplier) <= 0.0001f && priority > bestPriority) ||
                        (Mathf.Abs(shapeMultiplier - bestMultiplier) <= 0.0001f && priority == bestPriority && distanceSqr < bestDistanceSqr);

                    if (!better)
                    {
                        continue;
                    }

                    found = true;
                    bestMultiplier = shapeMultiplier;
                    bestPriority = priority;
                    bestDistanceSqr = distanceSqr;
                    bestPoint = candidatePoint;
                    multiplier = shapeMultiplier;
                }
            }

            if (!found)
            {
                return false;
            }

            point = bestPoint;
            return true;
        }

        private static bool TryGetShapeCenterPoint(Hitmesh hitmesh, HitboxColliders.ShapeId shapeId, Vector3 referenceWorldPoint, out Vector3 point, out float distanceSqr)
        {
            point = Vector3.zero;
            distanceSqr = float.MaxValue;

            if (hitmesh == null || shapeId.part == HitboxColliders.Parts.None)
            {
                return false;
            }

            try
            {
                HitboxColliders loadedHitboxColliders = StaticInstance<AsyncAssetLoading>.Instance.loadedHitboxColliders;
                if (loadedHitboxColliders == null ||
                    !loadedHitboxColliders.runtimeHitmeshData.IsCreated ||
                    !loadedHitboxColliders.runtimeVertexData.IsCreated)
                {
                    return false;
                }

                int frameIndex = hitmesh.hitboxFrameIndex;
                if (frameIndex < 0 || frameIndex >= loadedHitboxColliders.runtimeHitmeshData.Length)
                {
                    return false;
                }

                HitboxColliders.RuntimeHitboxData runtimeHitboxData =
                    loadedHitboxColliders.runtimeHitmeshData[frameIndex];

                Vector3 localReference3 = hitmesh.transform.InverseTransformPoint(referenceWorldPoint);

                for (int i = 0; i < runtimeHitboxData.shapes.Length; i++)
                {
                    HitboxColliders.ShapeData shapeData = runtimeHitboxData.shapes[i];
                    if (shapeData.shapeId != shapeId)
                    {
                        continue;
                    }

                    int length = (int)shapeData.length;
                    if (length <= 1)
                    {
                        return false;
                    }

                    Vector2 sum = Vector2.zero;
                    int count = 0;
                    for (int j = 0; j < length; j++)
                    {
                        int vertexIndex = shapeData.index + j;
                        if (vertexIndex < 0 || vertexIndex >= loadedHitboxColliders.runtimeVertexData.Length)
                        {
                            return false;
                        }

                        Unity.Mathematics.float2 vertex = loadedHitboxColliders.runtimeVertexData[vertexIndex];
                        sum += new Vector2(vertex.x, vertex.y);
                        count++;
                    }

                    if (count <= 0)
                    {
                        return false;
                    }

                    Vector2 center = sum / count;
                    Vector3 localPoint = new Vector3(center.x, center.y, localReference3.z);
                    point = hitmesh.transform.TransformPoint(localPoint);
                    if (!IsFiniteVector(point))
                    {
                        return false;
                    }

                    distanceSqr = (point - referenceWorldPoint).sqrMagnitude;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static int GetPartPriority(HitboxColliders.Parts part)
        {
            if (part == HitboxColliders.Parts.Eye)
            {
                return 30;
            }

            if (part == HitboxColliders.Parts.Head)
            {
                return 20;
            }

            if (part == HitboxColliders.Parts.Thorax)
            {
                return 10;
            }

            return 0;
        }

        private static bool HasLineOfSight(Camera camera, Unit target, Vector3 point, float distance)
        {
            if (camera == null || target == null)
            {
                return false;
            }

            Vector3 origin = camera.transform.position;
            Vector3 direction = point - origin;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            try
            {
                float maxDistance = Mathf.Max(0.01f, distance);
                return !Physics.Raycast(origin, direction.normalized, maxDistance, StaticInstance<GameManager>.Instance.geometryLayer);
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class WeaponLateUpdatePatch
    {
        private sealed class AutoFireState
        {
            public bool Controlled;
            public bool PulseDown;
            public float PulseEndTime;
            public float NextPulseTime;
        }

        private static readonly Dictionary<Weapon, AutoFireState> AutoFireStates = new Dictionary<Weapon, AutoFireState>();
        private static readonly FieldInfo FieldBOwnerIsNpc = AccessTools.Field(typeof(Holdable), "bOwnerIsNpc");
        private static readonly FieldInfo FieldIsReloading = AccessTools.Field(typeof(Weapon), "isReloading");
        private static readonly FieldInfo FieldBIsOnCooldown = AccessTools.Field(typeof(Weapon), "bIsOnCooldown");
        private static readonly PropertyInfo PropertyIsFullAutoEnabled = AccessTools.Property(typeof(Weapon), "IsFullAutoEnabled");

        public static void Prefix(Weapon __instance)
        {
            if (__instance == null)
            {
                return;
            }

            if (!ShouldAutoFire(__instance))
            {
                ReleaseTrigger(__instance);
                return;
            }

            bool isFullAuto = IsFullAuto(__instance);
            if (isFullAuto || !DeadeyeInstinctPlugin.AutoFireRespectSemiAuto.Value)
            {
                MarkControlled(__instance);
                __instance.SetTrigger(true);
                return;
            }

            PulseSemiAuto(__instance);
        }

        private static bool ShouldAutoFire(Weapon weapon)
        {
            if (!DeadeyeInstinctPlugin.EnableMod.Value || !DeadeyeInstinctPlugin.EnableAutoFire.Value)
            {
                return false;
            }

            if (!DeadeyeInstinctPlugin.IsAimbotRuntimeEnabled())
            {
                return false;
            }

            if (DeadeyeInstinctPlugin.AutoFireOnlyInHardLock.Value && DeadeyeInstinctPlugin.ActiveMode() != AssistMode.HardLock)
            {
                return false;
            }

            if (IsNpcWeapon(weapon))
            {
                return false;
            }

            HardLockController.HardLockTargetState state = HardLockController.CurrentState;
            if (!state.HasTarget || state.Target == null)
            {
                return false;
            }

            if (DeadeyeInstinctPlugin.AutoFireRequireAligned.Value && !state.IsAligned)
            {
                return false;
            }

            if (DeadeyeInstinctPlugin.AutoFireRequireWeaponReady.Value && !IsWeaponReady(weapon))
            {
                return false;
            }

            return true;
        }

        private static bool IsNpcWeapon(Weapon weapon)
        {
            try
            {
                object value = FieldBOwnerIsNpc?.GetValue(weapon);
                return value is bool b && b;
            }
            catch
            {
                return true;
            }
        }

        private static bool IsWeaponReady(Weapon weapon)
        {
            try
            {
                if (FieldIsReloading != null && FieldIsReloading.GetValue(weapon) is bool reloading && reloading)
                {
                    return false;
                }

                if (FieldBIsOnCooldown != null && FieldBIsOnCooldown.GetValue(weapon) is bool cooldown && cooldown)
                {
                    return false;
                }

                // Do not reject empty magazines here.
                // Weapon.AttemptShoot() owns empty-click and auto-reload behavior.
                // If AutoFire stops before AttemptShoot(), auto-reload can never trigger.
            }
            catch
            {
                return true;
            }

            return true;
        }

        private static bool IsFullAuto(Weapon weapon)
        {
            try
            {
                if (PropertyIsFullAutoEnabled != null)
                {
                    object value = PropertyIsFullAutoEnabled.GetValue(weapon, null);
                    if (value is bool b)
                    {
                        return b;
                    }
                }
            }
            catch
            {
                // Fall back to semi-auto pulse.
            }

            return false;
        }

        private static void PulseSemiAuto(Weapon weapon)
        {
            AutoFireState state;
            if (!AutoFireStates.TryGetValue(weapon, out state))
            {
                state = new AutoFireState();
                AutoFireStates[weapon] = state;
            }

            state.Controlled = true;
            float now = Time.unscaledTime;

            if (state.PulseDown)
            {
                if (now >= state.PulseEndTime)
                {
                    weapon.SetTrigger(false);
                    state.PulseDown = false;
                    state.NextPulseTime = now + Mathf.Max(0.005f, DeadeyeInstinctPlugin.AutoFireReleaseSeconds.Value);
                }
                else
                {
                    weapon.SetTrigger(true);
                }

                return;
            }

            if (now < state.NextPulseTime)
            {
                weapon.SetTrigger(false);
                return;
            }

            weapon.SetTrigger(true);
            state.PulseDown = true;
            state.PulseEndTime = now + Mathf.Max(0.005f, DeadeyeInstinctPlugin.AutoFirePulseSeconds.Value);
        }

        private static void MarkControlled(Weapon weapon)
        {
            AutoFireState state;
            if (!AutoFireStates.TryGetValue(weapon, out state))
            {
                state = new AutoFireState();
                AutoFireStates[weapon] = state;
            }

            state.Controlled = true;
        }

        private static void ReleaseTrigger(Weapon weapon)
        {
            AutoFireState state;
            if (!AutoFireStates.TryGetValue(weapon, out state) || !state.Controlled)
            {
                return;
            }

            try
            {
                weapon.SetTrigger(false);
            }
            catch
            {
                // Ignore release failures.
            }

            state.Controlled = false;
            state.PulseDown = false;
            state.PulseEndTime = 0f;
            state.NextPulseTime = Time.unscaledTime;
        }
    }

    internal static class CameraRecoilLateUpdatePatch
    {
        private static readonly FieldInfo FieldExtendedCameraController =
            AccessTools.Field(typeof(CameraRecoil), "extendedCameraController");

        private static readonly Dictionary<Type, MethodInfo> InsertRotationMethods = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> RotateTowardPositionMethods = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> RotateTowardDirectionMethods = new Dictionary<Type, MethodInfo>();

        public static void Postfix(CameraRecoil __instance)
        {
            HardLockController.ApplyImmediateRecoilCompensation(__instance);
        }

        internal static object GetExtendedCameraController(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            try
            {
                ExtendedCameraController controller = camera.GetComponentInParent<ExtendedCameraController>();
                if (controller != null)
                {
                    return controller;
                }
            }
            catch
            {
                // Fall through.
            }

            return null;
        }

        internal static object GetExtendedCameraController(CameraRecoil recoil)
        {
            if (recoil == null || FieldExtendedCameraController == null)
            {
                return null;
            }

            try
            {
                return FieldExtendedCameraController.GetValue(recoil);
            }
            catch
            {
                return null;
            }
        }

        internal static void RotateTowardPosition(object cameraController, Vector3 targetPoint)
        {
            if (cameraController == null)
            {
                return;
            }

            Vector3 direction = targetPoint - ((Component)cameraController).transform.position;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Type type = cameraController.GetType();
            MethodInfo method;
            if (!RotateTowardPositionMethods.TryGetValue(type, out method))
            {
                method = AccessTools.Method(type, "RotateTowardPosition", new[] { typeof(Vector3), typeof(float) });
                RotateTowardPositionMethods[type] = method;
            }

            try
            {
                if (method != null)
                {
                    method.Invoke(cameraController, new object[] { targetPoint, 0f });
                    return;
                }
            }
            catch
            {
                // Try direction fallback below.
            }

            MethodInfo directionMethod;
            if (!RotateTowardDirectionMethods.TryGetValue(type, out directionMethod))
            {
                directionMethod = AccessTools.Method(type, "RotateTowardDirection", new[] { typeof(Vector3), typeof(float) });
                RotateTowardDirectionMethods[type] = directionMethod;
            }

            try
            {
                if (directionMethod != null)
                {
                    directionMethod.Invoke(cameraController, new object[] { direction.normalized, 0f });
                }
            }
            catch
            {
                // Keep HardLock correction optional and silent.
            }
        }

        internal static void InsertRotationValue(object cameraController, Vector2 delta)
        {
            if (cameraController == null || delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Type type = cameraController.GetType();
            MethodInfo method;
            if (!InsertRotationMethods.TryGetValue(type, out method))
            {
                method = AccessTools.Method(type, "InsertRotationValue", new[] { typeof(Vector2) });
                InsertRotationMethods[type] = method;
            }

            if (method == null)
            {
                return;
            }

            try
            {
                method.Invoke(cameraController, new object[] { delta });
            }
            catch
            {
                // Keep recoil compensation optional and silent.
            }
        }
    }

    internal sealed class WeakspotDebugOverlayController : MonoBehaviour
    {
        private sealed class OverlayLine
        {
            public LineRenderer Renderer;
            public int LastSeenFrame;
        }

        private struct WeakspotShapePolygon
        {
            public string Key;
            public float Multiplier;
            public Vector3[] Vertices;
        }

        private readonly Dictionary<string, OverlayLine> overlays = new Dictionary<string, OverlayLine>();
        private readonly List<WeakspotShapePolygon> workingShapes = new List<WeakspotShapePolygon>();
        private Material lineMaterial;

        private void LateUpdate()
        {
            if (!IsEnabled())
            {
                ClearAll();
                return;
            }

            EnsureMaterial();
            workingShapes.Clear();

            if (SafeBool(DeadeyeInstinctPlugin.WeakspotDebugOnlyTrackedTarget, true))
            {
                Unit trackedTarget = AimAssistLateUpdatePatch.CurrentTrackedTargetForDebug;
                if (trackedTarget != null)
                {
                    CollectTargetShapes(trackedTarget, workingShapes);
                }
            }
            else
            {
                Unit[] allUnits = FindObjectsOfType<Unit>();
                for (int i = 0; i < allUnits.Length; i++)
                {
                    Unit unit = allUnits[i];
                    if (unit == null || unit.UnitState == UnitState.Dead)
                    {
                        continue;
                    }

                    CollectTargetShapes(unit, workingShapes);
                }
            }

            int frame = Time.frameCount;
            for (int i = 0; i < workingShapes.Count; i++)
            {
                WeakspotShapePolygon shape = workingShapes[i];
                OverlayLine overlay = GetOrCreateOverlay(shape.Key);
                overlay.LastSeenFrame = frame;
                ApplyShapeToRenderer(overlay.Renderer, shape);
            }

            CleanupStale(frame);
        }

        private bool IsEnabled()
        {
            return SafeBool(DeadeyeInstinctPlugin.EnableWeakspotDebugOverlay, false);
        }

        private void EnsureMaterial()
        {
            if (lineMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private OverlayLine GetOrCreateOverlay(string key)
        {
            OverlayLine overlay;
            if (overlays.TryGetValue(key, out overlay) && overlay.Renderer != null)
            {
                return overlay;
            }

            GameObject go = new GameObject("WeakspotOverlay_" + key);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            line.material = lineMaterial;
            line.sortingOrder = short.MaxValue;

            overlay = new OverlayLine { Renderer = line };
            overlays[key] = overlay;
            return overlay;
        }

        private void ApplyShapeToRenderer(LineRenderer line, WeakspotShapePolygon shape)
        {
            if (line == null || shape.Vertices == null || shape.Vertices.Length < 2)
            {
                return;
            }

            float width = SafeFloat(DeadeyeInstinctPlugin.WeakspotDebugLineWidth, 0.025f);
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = GetColorForMultiplier(shape.Multiplier);
            line.endColor = line.startColor;

            int count = shape.Vertices.Length;
            line.positionCount = count + 1;

            Camera camera = AimAssistLateUpdatePatch.CurrentPlayerCameraForDebug != null
                ? AimAssistLateUpdatePatch.CurrentPlayerCameraForDebug
                : Camera.main;

            float depthOffset = SafeFloat(DeadeyeInstinctPlugin.WeakspotDebugDepthOffset, 0.03f);

            for (int i = 0; i < count; i++)
            {
                Vector3 point = shape.Vertices[i];
                if (camera != null && depthOffset > 0f)
                {
                    Vector3 towardCamera = (camera.transform.position - point).normalized;
                    point += towardCamera * depthOffset;
                }

                line.SetPosition(i, point);
            }

            Vector3 closePoint = shape.Vertices[0];
            if (camera != null && depthOffset > 0f)
            {
                Vector3 towardCamera = (camera.transform.position - closePoint).normalized;
                closePoint += towardCamera * depthOffset;
            }

            line.SetPosition(count, closePoint);
            line.enabled = true;
        }

        private void CollectTargetShapes(Unit target, List<WeakspotShapePolygon> results)
        {
            if (target == null)
            {
                return;
            }

            Hitmesh[] hitmeshes;
            try
            {
                hitmeshes = target.GetComponentsInChildren<Hitmesh>(true);
            }
            catch
            {
                return;
            }

            if (hitmeshes == null || hitmeshes.Length == 0)
            {
                return;
            }

            bool showAll = SafeBool(DeadeyeInstinctPlugin.WeakspotDebugShowAllPositiveShapes, false);
            float minMultiplier = SafeFloat(DeadeyeInstinctPlugin.WeakspotDebugMinimumMultiplier, 0.75f);

            List<WeakspotShapePolygon> candidates = new List<WeakspotShapePolygon>();
            float bestMultiplier = 0f;

            for (int i = 0; i < hitmeshes.Length; i++)
            {
                Hitmesh hitmesh = hitmeshes[i];
                if (hitmesh == null || hitmesh.hitShapes == null || hitmesh.hitShapes.Length == 0)
                {
                    continue;
                }

                if (hitmesh.owner != null && hitmesh.owner != target)
                {
                    continue;
                }

                for (int j = 0; j < hitmesh.hitShapes.Length; j++)
                {
                    Hitmesh.Data data = hitmesh.hitShapes[j];

                    if (data.isInvulnerable)
                    {
                        continue;
                    }

                    float multiplier = data.GetShapeMultiplier();
                    if (multiplier <= 0f)
                    {
                        continue;
                    }

                    if (showAll && multiplier < minMultiplier)
                    {
                        continue;
                    }

                    Vector3[] vertices;
                    if (!TryGetShapeWorldPolygon(hitmesh, data.shapeId, out vertices))
                    {
                        continue;
                    }

                    if (vertices == null || vertices.Length < 2)
                    {
                        continue;
                    }

                    candidates.Add(new WeakspotShapePolygon
                    {
                        Key = target.GetInstanceID() + "_" + hitmesh.GetInstanceID() + "_" + j,
                        Multiplier = multiplier,
                        Vertices = vertices
                    });

                    if (multiplier > bestMultiplier)
                    {
                        bestMultiplier = multiplier;
                    }
                }
            }

            if (showAll)
            {
                results.AddRange(candidates);
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (Mathf.Abs(candidates[i].Multiplier - bestMultiplier) <= 0.0001f)
                {
                    results.Add(candidates[i]);
                }
            }
        }

        private bool TryGetShapeWorldPolygon(Hitmesh hitmesh, HitboxColliders.ShapeId shapeId, out Vector3[] worldVertices)
        {
            worldVertices = null;

            if (hitmesh == null || shapeId.part == HitboxColliders.Parts.None)
            {
                return false;
            }

            try
            {
                AsyncAssetLoading asyncAssetLoading = StaticInstance<AsyncAssetLoading>.Instance;
                if (asyncAssetLoading == null)
                {
                    return false;
                }

                HitboxColliders loadedHitboxColliders = asyncAssetLoading.loadedHitboxColliders;
                if (loadedHitboxColliders == null ||
                    !loadedHitboxColliders.runtimeHitmeshData.IsCreated ||
                    !loadedHitboxColliders.runtimeVertexData.IsCreated)
                {
                    return false;
                }

                int frameIndex = hitmesh.hitboxFrameIndex;
                if (frameIndex < 0 || frameIndex >= loadedHitboxColliders.runtimeHitmeshData.Length)
                {
                    return false;
                }

                HitboxColliders.RuntimeHitboxData runtimeHitboxData =
                    loadedHitboxColliders.runtimeHitmeshData[frameIndex];

                for (int i = 0; i < runtimeHitboxData.shapes.Length; i++)
                {
                    HitboxColliders.ShapeData shapeData = runtimeHitboxData.shapes[i];
                    if (shapeData.shapeId != shapeId)
                    {
                        continue;
                    }

                    int length = (int)shapeData.length;
                    if (length <= 1)
                    {
                        return false;
                    }

                    worldVertices = new Vector3[length];
                    for (int j = 0; j < length; j++)
                    {
                        int vertexIndex = shapeData.index + j;
                        if (vertexIndex < 0 || vertexIndex >= loadedHitboxColliders.runtimeVertexData.Length)
                        {
                            return false;
                        }

                        Unity.Mathematics.float2 vertex = loadedHitboxColliders.runtimeVertexData[vertexIndex];
                        worldVertices[j] = hitmesh.transform.TransformPoint(new Vector3(vertex.x, vertex.y, 0f));
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private Color GetColorForMultiplier(float multiplier)
        {
            if (multiplier >= 1.5f)
            {
                return Color.red;
            }

            if (multiplier >= 1f)
            {
                return new Color(1f, 0.5f, 0f, 1f);
            }

            if (multiplier >= 0.75f)
            {
                return Color.yellow;
            }

            return Color.white;
        }

        private void CleanupStale(int frame)
        {
            List<string> toRemove = null;

            foreach (KeyValuePair<string, OverlayLine> pair in overlays)
            {
                OverlayLine overlay = pair.Value;
                if (overlay == null || overlay.Renderer == null)
                {
                    if (toRemove == null)
                    {
                        toRemove = new List<string>();
                    }

                    toRemove.Add(pair.Key);
                    continue;
                }

                if (overlay.LastSeenFrame != frame)
                {
                    if (overlay.Renderer != null)
                    {
                        Destroy(overlay.Renderer.gameObject);
                    }

                    if (toRemove == null)
                    {
                        toRemove = new List<string>();
                    }

                    toRemove.Add(pair.Key);
                }
            }

            if (toRemove == null)
            {
                return;
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                overlays.Remove(toRemove[i]);
            }
        }

        private void ClearAll()
        {
            foreach (KeyValuePair<string, OverlayLine> pair in overlays)
            {
                if (pair.Value != null && pair.Value.Renderer != null)
                {
                    Destroy(pair.Value.Renderer.gameObject);
                }
            }

            overlays.Clear();
        }

        private static bool SafeBool(ConfigEntry<bool> entry, bool fallback)
        {
            return entry != null ? entry.Value : fallback;
        }

        private static float SafeFloat(ConfigEntry<float> entry, float fallback)
        {
            return entry != null ? entry.Value : fallback;
        }
    }

}
