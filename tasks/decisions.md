# Decisions — Boxyboksers

## VR Player Rig (2026-09-21)

- **Punch detection = physics-based knockback**, not trigger-only hit registration.
  Gloves are kinematic `Rigidbody`s with real (non-trigger) colliders, driven via
  `Rigidbody.MovePosition`/`MoveRotation` in `FixedUpdate` (not plain transform
  parenting). Kinematic bodies only report a real velocity to PhysX when moved
  via `MovePosition`/`MoveRotation` — a plain parented collider would report
  ~zero velocity and targets wouldn't get knocked back correctly.

- **Locomotion = basic continuous move + snap turn**, using XR Interaction
  Toolkit's stock action-based providers (left stick move / right stick snap
  turn), via the Starter Assets sample's premade rig rather than hand-wiring
  the locomotion providers — the premade rig already has the fiddly
  cross-references (mediator/body transformer, input action bindings) set up
  correctly by the package authors, and those wiring details vary between XRI
  versions.

- **No XR Plug-in Management / OpenXR loader enabled yet.** Unity's XR Device
  Simulator works entirely through the Input System and doesn't need an active
  XR loader. We have no headset yet ("next time"), so enabling OpenXR and
  picking a feature group is deferred until hardware is in hand — avoids
  Project Settings churn we'd likely have to redo once we know the target
  headset.

- **Scope for the initial rig task = rig only.** No punch targets, scoring, or
  hit-registration logic yet — just the HMD+hands rig, gloves that are
  physics-ready for knockback once targets exist, and locomotion.
