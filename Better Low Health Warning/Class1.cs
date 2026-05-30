using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace BetterLowHealthWarning
{
    [BepInPlugin("kumo.sulfur.better_low_health_warning", "Better Low Health Warning", "1.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> EnableMod;
        internal static ConfigEntry<bool> HideWhenHudHidden;

        internal static ConfigEntry<float> WarningHealthPercent;
        internal static ConfigEntry<float> CriticalHealthPercent;

        internal static ConfigEntry<int> BorderThickness;
        internal static ConfigEntry<float> MinOpacity;
        internal static ConfigEntry<float> MaxOpacity;

        internal static ConfigEntry<bool> EnablePulse;
        internal static ConfigEntry<float> PulseSpeed;

        internal static ConfigEntry<float> FadeSpeed;
        internal static ConfigEntry<float> FullscreenTintOpacity;

        internal static ConfigEntry<bool> LogErrors;

        internal static BetterLowHealthWarningOverlay Overlay;

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(
                "General",
                "EnableMod",
                true,
                "Enable better low health warning."
            );

            HideWhenHudHidden = Config.Bind(
                "General",
                "HideWhenHudHidden",
                true,
                "Hide the warning when the game HUD is hidden."
            );

            WarningHealthPercent = Config.Bind(
                "Visual",
                "WarningHealthPercent",
                0.35f,
                new ConfigDescription(
                    "Show warning below this health percentage. 0.35 = 35%.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );

            CriticalHealthPercent = Config.Bind(
                "Visual",
                "CriticalHealthPercent",
                0.15f,
                new ConfigDescription(
                    "Use stronger warning below this health percentage. 0.15 = 15%.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );

            BorderThickness = Config.Bind(
                "Visual",
                "BorderThickness",
                160,
                new ConfigDescription(
                    "Soft red vignette thickness in pixels.",
                    new AcceptableValueRange<int>(1, 700)
                )
            );

            MinOpacity = Config.Bind(
                "Visual",
                "MinOpacity",
                0.08f,
                new ConfigDescription(
                    "Warning opacity at the warning threshold.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );

            MaxOpacity = Config.Bind(
                "Visual",
                "MaxOpacity",
                0.42f,
                new ConfigDescription(
                    "Warning opacity at or below the critical threshold.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );

            EnablePulse = Config.Bind(
                "Visual",
                "EnablePulse",
                true,
                "Pulse the warning when health is low."
            );

            PulseSpeed = Config.Bind(
                "Visual",
                "PulseSpeed",
                2.2f,
                new ConfigDescription(
                    "Pulse speed. Higher values pulse faster.",
                    new AcceptableValueRange<float>(0.1f, 20f)
                )
            );

            FadeSpeed = Config.Bind(
                "Visual",
                "FadeSpeed",
                10f,
                new ConfigDescription(
                    "Fade in/out speed for the warning.",
                    new AcceptableValueRange<float>(0.1f, 50f)
                )
            );

            FullscreenTintOpacity = Config.Bind(
                "Visual",
                "FullscreenTintOpacity",
                0.035f,
                new ConfigDescription(
                    "Very subtle full-screen red tint multiplier. Set to 0 to disable.",
                    new AcceptableValueRange<float>(0f, 0.2f)
                )
            );

            LogErrors = Config.Bind(
                "Debug",
                "LogErrors",
                false,
                "Log patch errors. Keep false for normal gameplay."
            );

            GameObject overlayObject = new GameObject("BetterLowHealthWarningOverlay");
            DontDestroyOnLoad(overlayObject);
            overlayObject.hideFlags = HideFlags.HideAndDontSave;
            Overlay = overlayObject.AddComponent<BetterLowHealthWarningOverlay>();

            harmony = new Harmony("kumo.sulfur.better_low_health_warning");

            Type playerHudType = AccessTools.TypeByName("PerfectRandom.Sulfur.Gameplay.PlayerHUD");

            if (playerHudType == null)
            {
                Logger.LogError("Could not find PlayerHUD type.");
                return;
            }

            MethodInfo updateMethod = AccessTools.Method(playerHudType, "Update");

            if (updateMethod == null)
            {
                Logger.LogError("Could not find PlayerHUD.Update.");
                return;
            }

            HarmonyMethod postfix = new HarmonyMethod(
                typeof(Plugin).GetMethod(
                    nameof(PlayerHudUpdatePostfix),
                    BindingFlags.Static | BindingFlags.NonPublic
                )
            );

            harmony.Patch(updateMethod, postfix: postfix);

            Logger.LogInfo("Better Low Health Warning 1.1.0 loaded. Patched PlayerHUD.Update.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();

            if (Overlay != null)
            {
                Destroy(Overlay.gameObject);
                Overlay = null;
            }
        }

        private static bool loggedError;

        private static void PlayerHudUpdatePostfix(object __instance)
        {
            if (Overlay == null)
                return;

            if (!EnableMod.Value)
            {
                Overlay.ClearHealth();
                return;
            }

            try
            {
                float normalizedHealth;

                if (!TryGetNormalizedHealthFromPlayerHud(__instance, out normalizedHealth))
                {
                    Overlay.ClearHealth();
                    return;
                }

                bool hudVisible = TryGetHudVisible(__instance);
                Overlay.SetHealth(normalizedHealth, hudVisible);
            }
            catch (Exception ex)
            {
                Overlay.ClearHealth();

                if (LogErrors.Value && !loggedError)
                {
                    loggedError = true;
                    Log?.LogWarning("Better Low Health Warning patch error: " + ex);
                }
            }
        }

        private static bool TryGetNormalizedHealthFromPlayerHud(object playerHud, out float normalizedHealth)
        {
            normalizedHealth = 1f;

            if (playerHud == null)
                return false;

            object player = GetFieldValue(playerHud, "player");

            if (player == null)
                return false;

            object playerUnit = GetFieldValue(player, "playerUnit");

            if (playerUnit == null)
                return false;

            MethodInfo method = playerUnit.GetType().GetMethod(
                "GetNormalizedHealth",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (method == null)
                return false;

            object result = method.Invoke(playerUnit, null);

            if (!(result is float))
                return false;

            normalizedHealth = Mathf.Clamp01((float)result);
            return true;
        }

        private static bool TryGetHudVisible(object playerHud)
        {
            if (playerHud == null)
                return true;

            object canvasGroup = GetFieldValue(playerHud, "canvasGroup");

            if (canvasGroup == null)
                return true;

            PropertyInfo alphaProperty = canvasGroup.GetType().GetProperty(
                "alpha",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (alphaProperty == null)
                return true;

            object value = alphaProperty.GetValue(canvasGroup, null);

            if (!(value is float))
                return true;

            return (float)value > 0.01f;
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            if (instance == null || string.IsNullOrEmpty(fieldName))
                return null;

            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (field == null)
                return null;

            return field.GetValue(instance);
        }
    }

    internal sealed class BetterLowHealthWarningOverlay : MonoBehaviour
    {
        private float normalizedHealth = 1f;
        private bool hasValidHealth;
        private bool hudVisible = true;

        private float displayedOpacity;

        public void SetHealth(float value, bool isHudVisible)
        {
            normalizedHealth = Mathf.Clamp01(value);
            hudVisible = isHudVisible;
            hasValidHealth = true;
        }

        public void ClearHealth()
        {
            hasValidHealth = false;
            normalizedHealth = 1f;
            hudVisible = true;
        }

        private void OnGUI()
        {
            float targetOpacity = 0f;

            bool canShow =
                Plugin.EnableMod.Value &&
                hasValidHealth &&
                (!Plugin.HideWhenHudHidden.Value || hudVisible);

            if (canShow)
            {
                float warningThreshold = Mathf.Clamp01(Plugin.WarningHealthPercent.Value);
                float criticalThreshold = Mathf.Clamp01(Plugin.CriticalHealthPercent.Value);

                float highThreshold = Mathf.Max(warningThreshold, criticalThreshold);
                float lowThreshold = Mathf.Min(warningThreshold, criticalThreshold);

                if (normalizedHealth <= highThreshold)
                {
                    float denominator = Mathf.Max(0.001f, highThreshold - lowThreshold);
                    float severity = Mathf.Clamp01((highThreshold - normalizedHealth) / denominator);

                    float minOpacity = Mathf.Clamp01(Plugin.MinOpacity.Value);
                    float maxOpacity = Mathf.Clamp01(Plugin.MaxOpacity.Value);

                    if (maxOpacity < minOpacity)
                    {
                        float temp = minOpacity;
                        minOpacity = maxOpacity;
                        maxOpacity = temp;
                    }

                    targetOpacity = Mathf.Lerp(minOpacity, maxOpacity, severity);

                    if (Plugin.EnablePulse.Value)
                    {
                        float pulseSpeed = Mathf.Max(0.1f, Plugin.PulseSpeed.Value);
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);

                        float pulseStrength = Mathf.Lerp(0.05f, 0.35f, severity);
                        targetOpacity *= Mathf.Lerp(1f - pulseStrength, 1f, pulse);
                    }
                }
            }

            float fadeSpeed = Mathf.Max(0.1f, Plugin.FadeSpeed.Value);

            displayedOpacity = Mathf.Lerp(
                displayedOpacity,
                targetOpacity,
                1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime)
            );

            if (displayedOpacity <= 0.005f)
                return;

            DrawSoftVignette(displayedOpacity);
        }

        private static void DrawSoftVignette(float opacity)
        {
            int thickness = Mathf.Clamp(Plugin.BorderThickness.Value, 1, 700);
            int steps = 14;

            Texture2D texture = Texture2D.whiteTexture;
            Color oldColor = GUI.color;

            float width = Screen.width;
            float height = Screen.height;

            float tintOpacity = Mathf.Clamp(Plugin.FullscreenTintOpacity.Value, 0f, 0.2f);

            if (tintOpacity > 0f)
            {
                GUI.color = new Color(0.45f, 0f, 0f, Mathf.Clamp01(opacity * tintOpacity));
                GUI.DrawTexture(new Rect(0f, 0f, width, height), texture);
            }

            float stepSize = Mathf.Max(1f, thickness / (float)steps);

            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);

                float alpha = opacity * Mathf.Pow(1f - t, 1.85f);

                GUI.color = new Color(0.95f, 0.02f, 0.015f, Mathf.Clamp01(alpha));

                float offset = i * stepSize;
                float band = Mathf.Ceil(stepSize);

                GUI.DrawTexture(
                    new Rect(offset, offset, width - offset * 2f, band),
                    texture
                );

                GUI.DrawTexture(
                    new Rect(offset, height - offset - band, width - offset * 2f, band),
                    texture
                );

                GUI.DrawTexture(
                    new Rect(offset, offset + band, band, height - offset * 2f - band * 2f),
                    texture
                );

                GUI.DrawTexture(
                    new Rect(width - offset - band, offset + band, band, height - offset * 2f - band * 2f),
                    texture
                );
            }

            GUI.color = oldColor;
        }
    }
}