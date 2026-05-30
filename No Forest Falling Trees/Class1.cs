using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoForestFallingTrees
{
    [BepInPlugin("kumo.sulfur.no_forest_falling_trees", "No Forest Falling Trees", "1.3.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private ConfigEntry<bool> logActions;
        private ConfigEntry<bool> muteAmbushContentAudio;
        private ConfigEntry<bool> hideTreeRenderers;
        private ConfigEntry<bool> disableTreeColliders;
        private ConfigEntry<bool> disableTreeShakeable;
        private ConfigEntry<bool> stopParticles;

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
                "Log neutralized Forest Ambush visual objects."
            );

            muteAmbushContentAudio = Config.Bind(
                "General",
                "MuteAmbushContentAudio",
                true,
                "Mute the AudioSource on Forest Ambush Contents without disabling the component."
            );

            hideTreeRenderers = Config.Bind(
                "General",
                "HideTreeRenderers",
                true,
                "Hide Forest Ambush tree renderers."
            );

            disableTreeColliders = Config.Bind(
                "General",
                "DisableTreeColliders",
                true,
                "Disable Forest Ambush tree colliders."
            );

            disableTreeShakeable = Config.Bind(
                "General",
                "DisableTreeShakeable",
                true,
                "Disable TreeShakeable components only on Forest Ambush tree objects."
            );

            stopParticles = Config.Bind(
                "General",
                "StopParticles",
                true,
                "Stop and hide Forest Ambush particle effects."
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

            Logger.LogInfo("No Forest Falling Trees 1.3.1 loaded.");
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
                Logger.LogInfo("Starting Forest Falling Trees cleanup burst: " + reason);

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
                Logger.LogInfo("Finished Forest Falling Trees cleanup burst: " + reason);
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

            // Important:
            // Do NOT disable the Contents Animator.
            // Do NOT disable Trigger / TriggerSpawner / NavMeshAnchor.
            // The Animator may drive animation events required for Ghost / enemy spawning.

            if (muteAmbushContentAudio.Value)
                MuteAudioSourcesWithoutDisabling(contentsObject);

            for (int i = contents.childCount - 1; i >= 0; i--)
            {
                Transform child = contents.GetChild(i);

                if (child == null)
                    continue;

                GameObject childObject = child.gameObject;
                string childName = childObject.name;

                if (IsAmbushTreeObject(childName))
                {
                    NeutralizeTreeVisualObject(childObject);

                    if (logActions.Value)
                        Logger.LogInfo("Neutralized Forest Ambush tree visual object: " + GetPath(childObject));

                    continue;
                }

                if (IsAmbushParticleObject(childName))
                {
                    NeutralizeParticleVisualObject(childObject);

                    if (logActions.Value)
                        Logger.LogInfo("Neutralized Forest Ambush particle object: " + GetPath(childObject));
                }
            }

            if (logActions.Value)
                Logger.LogInfo("Neutralized Forest Ambush falling-tree presentation only: " + GetPath(ambushRoot));
        }

        private void NeutralizeTreeVisualObject(GameObject go)
        {
            if (go == null)
                return;

            // Keep the GameObject active so the ForestAmbush Animator timeline remains intact.
            // Only hide visuals and remove physical collision.

            if (hideTreeRenderers.Value)
                DisableRenderers(go);

            if (disableTreeColliders.Value)
                DisableColliders(go);

            if (disableTreeShakeable.Value)
                DisableBehaviourComponentByTypeNameRecursive(go, "TreeShakeable");
        }

        private void NeutralizeParticleVisualObject(GameObject go)
        {
            if (go == null)
                return;

            // Keep the GameObject active.
            // Hide particle renderers and stop already-started particle systems.

            DisableRenderers(go);

            if (stopParticles.Value)
                StopParticleSystems(go);
        }

        private void MuteAudioSourcesWithoutDisabling(GameObject go)
        {
            if (go == null)
                return;

            Component[] components = go.GetComponents<Component>();

            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                if (!string.Equals(component.GetType().Name, "AudioSource", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    AudioSource audioSource = component as AudioSource;

                    if (audioSource != null)
                    {
                        audioSource.volume = 0f;
                        audioSource.mute = true;
                    }
                }
                catch (Exception ex)
                {
                    if (logActions.Value)
                        Logger.LogWarning("Failed to mute AudioSource: " + ex.Message);
                }
            }
        }

        private static void DisableRenderers(GameObject go)
        {
            if (go == null)
                return;

            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                    renderer.enabled = false;
            }
        }

        private static void DisableColliders(GameObject go)
        {
            if (go == null)
                return;

            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
            {
                if (collider != null)
                    collider.enabled = false;
            }
        }

        private static void StopParticleSystems(GameObject go)
        {
            if (go == null)
                return;

            ParticleSystem[] particleSystems = go.GetComponentsInChildren<ParticleSystem>(true);

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void DisableBehaviourComponentByTypeNameRecursive(GameObject go, string typeName)
        {
            if (go == null || string.IsNullOrEmpty(typeName))
                return;

            Component[] components = go.GetComponentsInChildren<Component>(true);

            foreach (Component component in components)
            {
                if (component == null)
                    continue;

                if (!string.Equals(component.GetType().Name, typeName, StringComparison.OrdinalIgnoreCase))
                    continue;

                Behaviour behaviour = component as Behaviour;

                if (behaviour != null)
                    behaviour.enabled = false;
            }
        }

        private static bool IsAmbushTreeObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return objectName.StartsWith("Tree", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAmbushParticleObject(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return objectName.IndexOf("Particles", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsForestAmbushRootName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return
                objectName.IndexOf("Event_Forest_Ambush", StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Forest_Ambush", StringComparison.OrdinalIgnoreCase) >= 0;
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