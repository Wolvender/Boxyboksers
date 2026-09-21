# Todo — VR Player Rig

See `tasks/decisions.md` for the locked decisions behind this setup.

## Plan
- [x] Bootstrap `tasks/` folder
- [x] Write `Assets/Scripts/Player/GloveFollower.cs`
- [x] Write `Assets/Scripts/Editor/PlayerRigInstall.cs` (Step 1: install package)
- [x] Write `Assets/Scripts/Editor/PlayerRigSetup.cs` (Step 2: build the rig)
- [ ] Run `Tools/Senna/VR Rig/Step 1 - Install XR Interaction Toolkit` in the Unity Editor
- [ ] Run `Tools/Senna/VR Rig/Step 2 - Setup VR Player Rig` in the Unity Editor (with `Senna.unity` open)
- [ ] Verify in Play mode via the XR Device Simulator (see checklist below)

## Verification checklist (manual PlayMode repro)
Scene: `Assets/Scenes/Senna.unity`
1. Enter Play mode.
2. Use XR Device Simulator keybinds (Tab to cycle HMD/Left/Right control
   target, WASD+mouse to move/look, mouse buttons for trigger/grip) to move
   the simulated HMD and both hands.
3. Confirm both `Glove` objects (children of Left/Right Controller) visibly
   track the simulated controllers with no lag/drift.
4. Confirm continuous move (stick simulation) and snap turn work.
5. Check the Console — zero new errors or warnings introduced by the setup.

## Still open / follow-up
- Punch targets + scoring/points+time system (explicitly out of scope for
  this task).
- Enabling an XR loader (OpenXR) once a real headset is available.
