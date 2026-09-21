using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Boxyboksers.Player;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Boxyboksers.EditorTools
{
    // Run after PlayerRigInstall has finished and scripts have recompiled.
    // Deliberately avoids hard-coding XR Interaction Toolkit component types for
    // anything but the glove (which only needs UnityEngine.Rigidbody/Collider) —
    // locomotion wiring is version-specific enough that we reuse the package's own
    // premade Starter Assets rig instead of hand-assembling it.
    internal static class PlayerRigSetup
    {
        private const string PackageName = "com.unity.xr.interaction.toolkit";
        private static readonly string[] SampleNames = { "Starter Assets", "XR Device Simulator" };
        // Exact names, not a substring search — "XR Device Simulator" alone would also
        // match "XR Device Simulator UI", which is a separate, optional on-screen panel.
        private static readonly string[] SimulatorPrefabNames = { "XR Device Simulator", "XR Device Simulator UI" };
        private const float GloveRadius = 0.05f;
        private const float GloveHeight = 0.15f;

        [MenuItem("Tools/Senna/VR Rig/Step 2 - Setup VR Player Rig")]
        private static void Setup()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PackageName}");
            if (packageInfo == null)
            {
                Debug.LogError("[PlayerRigSetup] XR Interaction Toolkit isn't installed. Run 'Tools/Senna/VR Rig/Step 1 - Install XR Interaction Toolkit' first and wait for it to finish.");
                return;
            }

            ImportSamples(packageInfo.version);
            AssetDatabase.Refresh();

            GameObject rig = FindOrCreateRig();
            if (rig == null)
            {
                Debug.LogError("[PlayerRigSetup] Could not find or create an XR Origin rig — see warnings above.");
                return;
            }

            InstantiateDeviceSimulator();
            AddGloves(rig);
            DisableRayInteractors(rig);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[PlayerRigSetup] Done and saved. Enter Play mode and use the XR Device Simulator (Tab cycles HMD/Left/Right, WASD+mouse to move/look) to test.");
        }

        private static void ImportSamples(string packageVersion)
        {
            // XR Interaction Toolkit 3.x ships the Device Simulator as its own sample,
            // separate from "Starter Assets" (they used to be bundled together) — import both.
            List<Sample> samples = Sample.FindByPackage(PackageName, packageVersion)?.ToList() ?? new List<Sample>();

            foreach (string sampleName in SampleNames)
            {
                Sample sample = samples.FirstOrDefault(s => s.displayName == sampleName);
                if (string.IsNullOrEmpty(sample.displayName))
                {
                    Debug.LogWarning($"[PlayerRigSetup] No '{sampleName}' sample found for package version {packageVersion} — skipping.");
                    continue;
                }

                if (!sample.isImported)
                    sample.Import();
            }
        }

        private static GameObject FindOrCreateRig()
        {
            GameObject existingLeftController = GameObject.Find("Left Controller");
            if (existingLeftController != null)
            {
                Debug.Log("[PlayerRigSetup] Rig already present in the scene — reusing it instead of adding another.");
                return existingLeftController.transform.root.gameObject;
            }

            List<string> candidates = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Samples" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    return name.Contains("XR Origin") && (name.Contains("Rig") || name.Contains("Hands") || name.Contains("Set Up"));
                })
                .ToList();

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[PlayerRigSetup] No premade rig prefab found in the imported sample — falling back to the base 'GameObject > XR > XR Origin (VR)' rig. Locomotion (move/turn) will need to be wired manually.");
                EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
                GameObject fallback = GameObject.Find("XR Origin");
                if (fallback == null)
                    Debug.LogError("[PlayerRigSetup] 'GameObject > XR > XR Origin (VR)' didn't produce an 'XR Origin' object — create the rig manually via the GameObject > XR menu and re-run this step.");
                return fallback;
            }

            if (candidates.Count > 1)
                Debug.LogWarning("[PlayerRigSetup] Multiple candidate rig prefabs found, using the first: " + string.Join(", ", candidates));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(candidates[0]);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add VR Player Rig");
            return instance;
        }

        private static void InstantiateDeviceSimulator()
        {
            List<string> allPrefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Samples" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            foreach (string prefabName in SimulatorPrefabNames)
            {
                if (GameObject.Find(prefabName) != null) continue;

                string path = allPrefabPaths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == prefabName);
                if (path == null)
                {
                    Debug.LogWarning($"[PlayerRigSetup] '{prefabName}' prefab not found in imported samples.");
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, $"Add {prefabName}");
            }
        }

        private static void AddGloves(GameObject rig)
        {
            AddGlove(rig.transform, "Left Controller");
            AddGlove(rig.transform, "Right Controller");
        }

        private static void AddGlove(Transform rigRoot, string handName)
        {
            Transform hand = FindDeepChild(rigRoot, handName);
            if (hand == null)
            {
                Debug.LogWarning($"[PlayerRigSetup] Couldn't find '{handName}' under the rig — skipping glove.");
                return;
            }

            Transform existingGlove = hand.Find("Glove");
            GameObject glove;
            if (existingGlove != null)
            {
                glove = existingGlove.gameObject;
            }
            else
            {
                glove = new GameObject("Glove");
                Undo.RegisterCreatedObjectUndo(glove, "Add Glove");
                glove.transform.SetParent(hand, false);

                var collider = glove.AddComponent<CapsuleCollider>();
                collider.radius = GloveRadius;
                collider.height = GloveHeight;

                var rb = glove.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                var follower = glove.AddComponent<GloveFollower>();
                var serialized = new SerializedObject(follower);
                serialized.FindProperty("trackedHand").objectReferenceValue = hand;
                serialized.ApplyModifiedProperties();
            }

            EnsureGloveVisual(glove, handName);
        }

        // Separate child object so scaling the placeholder mesh never affects the
        // Glove's own collider size (which must stay physically accurate for punches).
        private static void EnsureGloveVisual(GameObject glove, string handName)
        {
            if (glove.transform.Find("Visual") != null) return;

            var visual = new GameObject("Visual");
            Undo.RegisterCreatedObjectUndo(visual, "Add Glove Visual");
            visual.transform.SetParent(glove.transform, false);
            visual.transform.localScale = new Vector3(GloveRadius / 0.5f, GloveHeight / 2f, GloveRadius / 0.5f);

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Mesh capsuleMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temp);

            visual.AddComponent<MeshFilter>().sharedMesh = capsuleMesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = handName.Contains("Left") ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.2f, 0.4f, 0.9f)
            };
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void DisableRayInteractors(GameObject rig)
        {
            string[] typeNameMarkers = { "RayInteractor", "InteractorLineVisual", "UIInteractor" };
            Behaviour[] behaviours = rig.GetComponentsInChildren<Behaviour>(true);

            foreach (Behaviour behaviour in behaviours)
            {
                string typeName = behaviour.GetType().Name;
                if (Array.Exists(typeNameMarkers, marker => typeName.Contains(marker)))
                {
                    behaviour.enabled = false;
                    Debug.Log($"[PlayerRigSetup] Disabled {typeName} on {behaviour.gameObject.name} (not needed for boxing).");
                }
            }
        }
    }
}
