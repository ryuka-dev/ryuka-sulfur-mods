using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoForestAmbush
{
    [BepInPlugin("kumo.sulfur.no_forest_ambush", "No Forest Ambush", "1.3.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private ConfigEntry<bool> logActions;
        private ConfigEntry<float> sceneSignatureCheckInterval;
        private ConfigEntry<int> cleanupBurstIterations;
        private ConfigEntry<float> cleanupBurstInterval;

        private int lastSceneSignature;
        private bool cleanupBurstRunning;

        private readonly HashSet<int> neutralizedAmbushIds = new HashSet<int>();

        private void Awake()
        {
            Log = Logger;

            logActions = Config.Bind(
                "General",
                "LogActions",
                false,
                "Log neutralized Forest Ambush objects."
            );

            sceneSignatureCheckInterval = Config.Bind(
                "Performance",
                "SceneSignatureCheckInterval",
                1.0f,
                "How often to cheaply check whether the loaded scene hierarchy changed."
            );

            cleanupBurstIterations = Config.Bind(
                "Performance",
                "CleanupBurstIterations",
                24,
                "How many cleanup passes to run after a scene hierarchy change."
            );

            cleanupBurstInterval = Config.Bind(
                "Performance",
                "CleanupBurstInterval",
                0.2f,
                "Delay between cleanup passes during a cleanup burst."
            );

            SceneManager.sceneLoaded += OnSceneLoaded;

            lastSceneSignature = ComputeSceneSignature();

            StartCleanupBurst("startup");
            StartCoroutine(SceneSignatureWatchLoop());

            Logger.LogInfo("No Forest Ambush 1.3.0 loaded.");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            neutralizedAmbushIds.Clear();
            lastSceneSignature = ComputeSceneSignature();
            StartCleanupBurst("scene loaded");
        }

        private IEnumerator SceneSignatureWatchLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Mathf.Max(0.25f, sceneSignatureCheckInterval.Value));

                int currentSignature = ComputeSceneSignature();

                if (currentSignature != lastSceneSignature)
                {
                    lastSceneSignature = currentSignature;
                    StartCleanupBurst("scene hierarchy changed");
                }
            }
        }

        private int ComputeSceneSignature()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + SceneManager.sceneCount;

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);

                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;

                    hash = hash * 31 + scene.name.GetHashCode();
                    hash = hash * 31 + scene.rootCount;
                }

                return hash;
            }
        }

        private void StartCleanupBurst(string reason)
        {
            if (cleanupBurstRunning)
                return;

            StartCoroutine(CleanupBurst(reason));
        }

        private IEnumerator CleanupBurst(string reason)
        {
            cleanupBurstRunning = true;

            if (logActions.Value)
                Logger.LogInfo("Starting Forest Ambush cleanup burst: " + reason);

            int iterations = Mathf.Max(1, cleanupBurstIterations.Value);
            float interval = Mathf.Max(0.05f, cleanupBurstInterval.Value);

            for (int i = 0; i < iterations; i++)
            {
                int neutralized = NeutralizeForestAmbushObjectsInLoadedScenes();

                if (logActions.Value && neutralized > 0)
                    Logger.LogInfo("Cleanup pass neutralized Forest Ambush objects: " + neutralized);

                yield return new WaitForSeconds(interval);
            }

            cleanupBurstRunning = false;

            if (logActions.Value)
                Logger.LogInfo("Finished Forest Ambush cleanup burst: " + reason);
        }

        private int NeutralizeForestAmbushObjectsInLoadedScenes()
        {
            int neutralized = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();

                foreach (GameObject root in roots)
                {
                    neutralized += ScanTransformForForestAmbush(root.transform);
                }
            }

            return neutralized;
        }

        private int ScanTransformForForestAmbush(Transform transform)
        {
            if (transform == null)
                return 0;

            GameObject go = transform.gameObject;

            if (go == null)
                return 0;

            if (IsForestAmbushRootName(go.name))
            {
                int instanceId = go.GetInstanceID();

                if (neutralizedAmbushIds.Contains(instanceId))
                    return 0;

                neutralizedAmbushIds.Add(instanceId);

                NeutralizeForestAmbushRoot(go);
                return 1;
            }

            int neutralized = 0;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                neutralized += ScanTransformForForestAmbush(transform.GetChild(i));
            }

            return neutralized;
        }

        private void NeutralizeForestAmbushRoot(GameObject ambushRoot)
        {
            if (ambushRoot == null)
                return;

            Transform contents = ambushRoot.transform.Find("Contents");

            if (contents == null)
            {
                if (logActions.Value)
                    Logger.LogInfo("Forest Ambush has no Contents: " + GetPath(ambushRoot));

                return;
            }

            GameObject contentsObject = contents.gameObject;

            // The falling-tree scare is driven from Contents:
            // Contents has the ForestAmbush Animator and AudioSource.
            DisableBehaviourComponentByTypeName(contentsObject, "Animator");
            DisableBehaviourComponentByTypeName(contentsObject, "AudioSource");

            // Do NOT disable these:
            // - Trigger
            // - TriggerSpawner
            // - NavMeshAnchor
            //
            // They may be required for the encounter / spawn logic.
            for (int i = contents.childCount - 1; i >= 0; i--)
            {
                Transform child = contents.GetChild(i);

                if (child == null)
                    continue;

                GameObject childObject = child.gameObject;
                string childName = childObject.name;

                if (ShouldDisableAmbushVisualObject(childName))
                {
                    if (logActions.Value)
                        Logger.LogInfo("Disabled Forest Ambush visual object: " + GetPath(childObject));

                    childObject.SetActive(false);
                }
            }

            if (logActions.Value)
                Logger.LogInfo("Neutralized Forest Ambush visuals only: " + GetPath(ambushRoot));
        }

        private static bool ShouldDisableAmbushVisualObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            // From debug log:
            // Contents/Tree1, Tree2, Tree4, Tree5, Tree6, Tree8, Tree9
            if (objectName.StartsWith("Tree", StringComparison.OrdinalIgnoreCase))
                return true;

            // From debug log:
            // Contents/Particles
            if (objectName.IndexOf("Particles", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static bool IsForestAmbushRootName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return
                objectName.IndexOf("Event_Forest_Ambush", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Forest_Ambush", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DisableBehaviourComponentByTypeName(GameObject go, string typeName)
        {
            if (go == null || string.IsNullOrEmpty(typeName))
                return;

            Component[] components = go.GetComponents<Component>();

            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                Type componentType = component.GetType();

                if (!string.Equals(componentType.Name, typeName, StringComparison.OrdinalIgnoreCase))
                    continue;

                Behaviour behaviour = component as Behaviour;

                if (behaviour != null)
                    behaviour.enabled = false;
            }
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
    }
}