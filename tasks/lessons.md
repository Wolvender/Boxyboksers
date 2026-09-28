# Lessons — Boxyboksers

## `UnityEditor.PackageManager.PackageInfo` vs `UnityEditor.PackageInfo` (2026-09-21)
- What happened: wrote `using UnityEditor;` and `using UnityEditor.PackageManager;`
  together, then referenced bare `PackageInfo`. Both namespaces define a type with
  that name, so it's `CS0104: ambiguous reference` — a hard compile error that
  blocked the entire project (no `Tools` menu showed up at all, not even unrelated
  scripts' menu items).
- Why it matters here: any Editor script touching Package Manager APIs needs the
  fully-qualified `UnityEditor.PackageManager.PackageInfo`, never the bare name.
- Rule: when adding `using UnityEditor.PackageManager;` alongside `using UnityEditor;`,
  fully-qualify `PackageInfo` every time.

## Asset name searches need exact match, not "contains" (2026-09-21)
- What happened: searched for the XR Device Simulator prefab with
  `AssetDatabase.FindAssets("XR Device Simulator t:Prefab", ...)`. That also
  matched `XR Device Simulator UI.prefab` (a separate, optional on-screen panel)
  and grabbed it instead of the real `XR Device Simulator.prefab` — so only the
  UI showed up in-scene, with no actual input-simulation logic. Looked plausible
  in the Console but silently did the wrong thing (WASD/mouse did nothing).
- Rule: when picking one specific sample/package asset by name, compare
  `Path.GetFileNameWithoutExtension(path) == exactName`, not a substring/contains
  search — XRI sample prefabs commonly share name prefixes.
