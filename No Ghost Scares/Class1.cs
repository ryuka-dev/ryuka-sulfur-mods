using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoGhostScares
{
    [BepInPlugin("kumo.sulfur.no_ghost_scares", "No Ghost Scares", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private ConfigEntry<bool> enableBlocking;
        private ConfigEntry<bool> showDebugOverlay;
        private ConfigEntry<bool> logActions;
        private ConfigEntry<bool> detectBroadGhostNames;

        private ConfigEntry<float> scanInterval;
        private ConfigEntry<float> overlayKeepSeconds;
        private ConfigEntry<int> maxOverlayItems;

        private readonly HashSet<int> processedStrongGhostScareIds = new HashSet<int>();
        private readonly HashSet<int> reportedBroadGhostIds = new HashSet<int>();
        private readonly List<GhostRecord> recentRecords = new List<GhostRecord>();

        private float lastRecordTime = -9999f;

        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallTextStyle;
        private GUIStyle boxStyle;

        private static readonly string[] BroadGhostKeywords =
        {
            "Ghost",
            "Wraith",
            "Poltergeist",
            "Apparition",
            "Scare",
            "Haunt",
            "Specter",
            "Spectre"
        };

        private void Awake()
        {
            Log = Logger;

            enableBlocking = Config.Bind(
                "General",
                "EnableBlocking",
                true,
                "If true, disables Ability_Ghost_*_Scare objects. If false, only detects them."
            );

            showDebugOverlay = Config.Bind(
                "Debug",
                "ShowDebugOverlay",
                false,
                "Show detected or blocked ghost scare objects on screen."
            );

            logActions = Config.Bind(
                "Debug",
                "LogActions",
                false,
                "Write detected or blocked ghost scare objects to BepInEx/LogOutput.log."
            );

            detectBroadGhostNames = Config.Bind(
                "Debug",
                "DetectBroadGhostNames",
                false,
                "Debug only. Also show broad ghost-related names like GhostParticles or Unit_Enemy_Ghost. These are never blocked."
            );

            scanInterval = Config.Bind(
                "Performance",
                "ScanInterval",
                1.0f,
                "How often to scan loaded scenes for ghost scare triggers."
            );

            overlayKeepSeconds = Config.Bind(
                "Overlay",
                "OverlayKeepSeconds",
                12.0f,
                "How long the on-screen overlay remains visible after the latest detection or block."
            );

            maxOverlayItems = Config.Bind(
                "Overlay",
                "MaxOverlayItems",
                8,
                "Maximum number of recent records shown on screen."
            );

            StartCoroutine(ScanLoop());

            Logger.LogInfo("No Ghost Scares 1.0.0 loaded.");
        }

        private IEnumerator ScanLoop()
        {
            while (true)
            {
                ScanLoadedScenes();
                yield return new WaitForSeconds(Mathf.Max(0.2f, scanInterval.Value));
            }
        }

        private void ScanLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();

                foreach (GameObject root in roots)
                {
                    ScanTransform(root.transform);
                }
            }
        }

        private void ScanTransform(Transform transform)
        {
            if (transform == null)
                return;

            GameObject go = transform.gameObject;

            if (go == null)
                return;

            string objectName = go.name;

            if (IsStrongGhostScareObject(objectName))
            {
                ProcessStrongGhostScare(go);
                return;
            }

            if (detectBroadGhostNames.Value && IsBroadGhostRelatedObject(objectName))
            {
                ReportBroadGhostRelatedObject(go);
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                ScanTransform(transform.GetChild(i));
            }
        }

        private void ProcessStrongGhostScare(GameObject go)
        {
            if (go == null)
                return;

            int instanceId = go.GetInstanceID();

            if (processedStrongGhostScareIds.Contains(instanceId))
                return;

            processedStrongGhostScareIds.Add(instanceId);

            if (enableBlocking.Value)
            {
                AddRecord(
                    "BLOCKED",
                    "StrongGhostScare",
                    go.name,
                    GetPath(go),
                    go.activeSelf,
                    go.activeInHierarchy
                );

                if (logActions.Value)
                    Logger.LogInfo("Blocked ghost scare object: " + GetPath(go));

                go.SetActive(false);
                UnityEngine.Object.Destroy(go);
            }
            else
            {
                AddRecord(
                    "DETECTED_ONLY",
                    "StrongGhostScare",
                    go.name,
                    GetPath(go),
                    go.activeSelf,
                    go.activeInHierarchy
                );

                if (logActions.Value)
                    Logger.LogInfo("Detected ghost scare object, blocking disabled: " + GetPath(go));
            }
        }

        private void ReportBroadGhostRelatedObject(GameObject go)
        {
            if (go == null)
                return;

            int instanceId = go.GetInstanceID();

            if (reportedBroadGhostIds.Contains(instanceId))
                return;

            reportedBroadGhostIds.Add(instanceId);

            AddRecord(
                "DEBUG_ONLY",
                "BroadGhostRelated",
                go.name,
                GetPath(go),
                go.activeSelf,
                go.activeInHierarchy
            );

            if (logActions.Value)
                Logger.LogInfo("Debug-only broad ghost related object: " + GetPath(go));
        }

        private void AddRecord(
            string action,
            string category,
            string objectName,
            string path,
            bool activeSelf,
            bool activeInHierarchy
        )
        {
            GhostRecord record = new GhostRecord
            {
                TimeText = DateTime.Now.ToString("HH:mm:ss"),
                Action = action,
                Category = category,
                ObjectName = objectName,
                Path = path,
                ActiveSelf = activeSelf,
                ActiveInHierarchy = activeInHierarchy
            };

            recentRecords.Insert(0, record);

            int maxKeep = Mathf.Max(12, maxOverlayItems.Value * 3);

            while (recentRecords.Count > maxKeep)
            {
                recentRecords.RemoveAt(recentRecords.Count - 1);
            }

            lastRecordTime = Time.realtimeSinceStartup;
        }

        private static bool IsStrongGhostScareObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return
                objectName.IndexOf("Ability_Ghost_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                objectName.IndexOf("_Scare", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBroadGhostRelatedObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            foreach (string keyword in BroadGhostKeywords)
            {
                if (objectName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay.Value)
                return;

            if (recentRecords.Count == 0)
                return;

            float elapsed = Time.realtimeSinceStartup - lastRecordTime;

            if (elapsed > overlayKeepSeconds.Value)
                return;

            EnsureGuiStyles();

            int itemsToShow = Mathf.Min(Mathf.Max(1, maxOverlayItems.Value), recentRecords.Count);

            float width = 820f;
            float lineHeight = 22f;
            float height = 112f + itemsToShow * 58f;

            Rect boxRect = new Rect(20f, 20f, width, height);
            GUI.Box(boxRect, GUIContent.none, boxStyle);

            float x = boxRect.x + 14f;
            float y = boxRect.y + 12f;

            string mode = enableBlocking.Value ? "BLOCKING ON" : "BLOCKING OFF / DETECT ONLY";

            GUI.Label(
                new Rect(x, y, width - 28f, 26f),
                "NO GHOST SCARES DEBUG - " + mode,
                titleStyle
            );

            y += 28f;

            string helpText = enableBlocking.Value
                ? "Detected Ability_Ghost_*_Scare objects are being blocked."
                : "Detected Ability_Ghost_*_Scare objects are only shown. They are not blocked.";

            GUI.Label(
                new Rect(x, y, width - 28f, 22f),
                helpText,
                smallTextStyle
            );

            y += 28f;

            GUI.Label(
                new Rect(x, y, width - 28f, 22f),
                "Recent records: " + recentRecords.Count + " | Showing: " + itemsToShow,
                textStyle
            );

            y += 28f;

            for (int i = 0; i < itemsToShow; i++)
            {
                GhostRecord record = recentRecords[i];

                string statusLine =
                    "[" +
                    record.TimeText +
                    "] " +
                    record.Action +
                    " | " +
                    record.Category +
                    " | " +
                    record.ObjectName +
                    " | active=" +
                    record.ActiveInHierarchy;

                string pathLine = ShortenPath(record.Path, 120);

                GUI.Label(new Rect(x, y, width - 28f, lineHeight), statusLine, textStyle);
                y += lineHeight;

                GUI.Label(new Rect(x, y, width - 28f, 34f), pathLine, smallTextStyle);
                y += 36f;
            }
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 18;
            titleStyle.normal.textColor = Color.white;

            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 14;
            textStyle.normal.textColor = Color.white;

            smallTextStyle = new GUIStyle(GUI.skin.label);
            smallTextStyle.fontSize = 12;
            smallTextStyle.wordWrap = true;
            smallTextStyle.normal.textColor = Color.white;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = Color.white;
        }

        private static string ShortenPath(string path, int maxLength)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            if (path.Length <= maxLength)
                return path;

            int keepStart = maxLength / 2 - 4;
            int keepEnd = maxLength - keepStart - 6;

            if (keepStart <= 0 || keepEnd <= 0)
                return path.Substring(0, maxLength);

            return path.Substring(0, keepStart) + " ... " + path.Substring(path.Length - keepEnd);
        }

        private static string GetPath(GameObject go)
        {
            if (go == null)
                return string.Empty;

            string path = go.name;
            Transform current = go.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private sealed class GhostRecord
        {
            public string TimeText;
            public string Action;
            public string Category;
            public string ObjectName;
            public string Path;
            public bool ActiveSelf;
            public bool ActiveInHierarchy;
        }
    }
}