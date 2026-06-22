# Paint PhysicMaterial Overlay
Unity3d overlay window to easily paint or preview PhysicalMaterial at SceneView

![PaintPhysicalMaterial_Demo](https://github.com/user-attachments/assets/adc1c272-088e-4acd-9f27-3f7b1daa3094)

# Features
- palettes
- draw all by distance
- 3D colliders support (Box, Capsule, Sphere, MeshCollider)
- colored wireframe by physical material
- set by mouse click in scene
- set under cursor by hotkey
- samples scene
- paint in isolated prefab scene support
- undo/redo
- collapse window to disable
- Unity 2021+ supported

# Installation
- add script `PaintPhysicsMaterialOverlayPalette.cs` in `Project/Assets/.../Editor/..`

# Usage
- create palette: `Assets/Create/Paint Physics Material Palette`
- fill palette with `PhysicsMaterial` set `Color` for them
- add `null` material, to control it's color
- open overlay: press `~` uncollapse `Custom`, enable `Paint Physics Material`
- select material, then press mouse left button or hotkey in scene object to draw materials. Colors will change
<img width="271" height="454" alt="{C8FD8867-BFE9-483D-91BE-E11FA1C3F2E9}" src="https://github.com/user-attachments/assets/29a54e1f-dd55-40a9-a4d5-8cbc3fa0f204" />


# Work In Progress
- Collider2D support
- Physics modules optional (with version defines)
- single file / package branches
