using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Boxyboksers.EditorTools
{
    // Split from PlayerRigSetup: that script will reference XR Interaction Toolkit
    // types, which don't exist until this install step has finished and recompiled.
    internal static class PlayerRigInstall
    {
        private const string PackageName = "com.unity.xr.interaction.toolkit";

        private static AddRequest _request;

        [MenuItem("Tools/Senna/VR Rig/Step 1 - Install XR Interaction Toolkit")]
        private static void InstallXrInteractionToolkit()
        {
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PackageName}") != null)
            {
                Debug.Log("[PlayerRigInstall] XR Interaction Toolkit is already installed. Run 'Tools/Senna/VR Rig/Step 2 - Setup VR Player Rig' next.");
                return;
            }

            _request = Client.Add(PackageName);
            EditorApplication.update += Poll;
            Debug.Log("[PlayerRigInstall] Installing XR Interaction Toolkit...");
        }

        private static void Poll()
        {
            if (_request == null || !_request.IsCompleted) return;

            if (_request.Status == StatusCode.Success)
                Debug.Log($"[PlayerRigInstall] Installed {_request.Result.packageId}. Wait for scripts to recompile, then run 'Tools/Senna/VR Rig/Step 2 - Setup VR Player Rig'.");
            else if (_request.Status >= StatusCode.Failure)
                Debug.LogError($"[PlayerRigInstall] Package install failed: {_request.Error.message}");

            EditorApplication.update -= Poll;
            _request = null;
        }
    }
}
