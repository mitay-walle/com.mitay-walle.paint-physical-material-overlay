using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace LevelDesign
{
    [CreateAssetMenu]
    public class PaintPhysicsMaterialOverlayPalette : ScriptableObject
    {
#if ODIN_INSPECTOR
        [DrawWithUnity]
#endif
        public List<Entry2D> Entries2D = new();
#if ODIN_INSPECTOR
        [DrawWithUnity]
#endif
        public List<Entry3D> Entries3D = new();

        [Serializable]
        public abstract class EntryBase
        {
            public Color Color = Color.white;
            public abstract Object Value { get; }
            public string Name => Value ? Value.name : "null";
        }

        [Serializable]
        public sealed class Entry3D : EntryBase
        {
            public PhysicsMaterial Material;
            public override Object Value => Material;
        }

        [Serializable]
        public sealed class Entry2D : EntryBase
        {
            public PhysicsMaterial2D Material;
            public override Object Value => Material;
        }
    }

    /// <summary>
    /// Allow to display or paint Physical Material by mouse pointer and hotkeys
    /// </summary>
    [Serializable]
    [Overlay(typeof(SceneView), "Paint Physics Material")]
    public sealed class PaintPhysicsMaterialOverlay : Overlay
    {
        private static GUIContent _iconGUIContent = EditorGUIUtility.IconContent("d_PhysicMaterial Icon");

        [SerializeField] private float _maxDistance = 50;
        [SerializeField] private float _alpha = .25f;
        [SerializeField] private bool _drawAll;
        [SerializeField] private bool _useLeftClick = true;
        [SerializeField] private KeyCode _paintHotKey = KeyCode.V;
        private PaintPhysicsMaterialOverlayPalette.EntryBase _current;
        [SerializeField] private PaintPhysicsMaterialOverlayPalette _palette;
        //[SerializeField] private Vector2 WindowSize = new Vector2(200, 460);
        //private Vector2 scroll;
        private Material _wireMat;
        private GUIContent _guiContent;
        private Shader _meshColliderShader;

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public override VisualElement CreatePanelContent()
        {
            VisualElement root = new VisualElement
            {
                name = nameof(PaintPhysicsMaterialOverlay)
            };
            root.Add(new IMGUIContainer(OnWindowGUI));

            _guiContent = _iconGUIContent;
            _guiContent.text = "Paint Physical Material";
            _guiContent.tooltip = "Allow to display or paint Physical Material by mouse pointer and hotkeys";

            SceneView.duringSceneGui -= OnToolGUI;
            SceneView.duringSceneGui += OnToolGUI;

            return root;
        }

        private void OnToolGUI(EditorWindow sceneWindow)
        {
            if (!(sceneWindow is SceneView sceneView))
            {
                return;
            }

            if (!displayed)
            {
                return;
            }

            if (collapsed)
            {
                return;
            }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            Scene scene = stage ? stage.scene : SceneManager.GetActiveScene();

            if (Event.current.type == EventType.Repaint)
            {
                if (_drawAll)
                {
                    DrawAllColliders(scene, sceneView);
                }

                DrawColliderCurrent(scene);
                sceneView.Repaint();
            }

            bool isClicked = _useLeftClick && Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                             Event.current.isMouse;

            bool isHotkey = _paintHotKey != KeyCode.None && Event.current.type == EventType.KeyDown &&
                            Event.current.keyCode == _paintHotKey &&
                            Event.current.isKey;

            if (isHotkey || isClicked)
            {
                if (isClicked)
                {
                    GUIUtility.hotControl = -1;
                    Event.current.Use();
                }

                SwapMaterialUnderCursor(scene);
            }
        }

        private void OnWindowGUI()
        {
            float lastLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 100;

            _maxDistance = EditorGUILayout.Slider("Max Distance", _maxDistance, 0, 1000);
            _alpha = EditorGUILayout.Slider(nameof(_alpha), _alpha, 0, 1);
            _drawAll = EditorGUILayout.Toggle("Draw All", _drawAll);
            _useLeftClick = EditorGUILayout.Toggle("Use Left Click", _useLeftClick);

            EditorGUI.BeginChangeCheck();

            KeyCode newKey = (KeyCode)EditorGUILayout.EnumPopup("Hotkey", _paintHotKey);

            if (EditorGUI.EndChangeCheck())
            {
                //Undo.RecordObject(this, "paintHotKey");
                _paintHotKey = newKey;
            }

            GUI.enabled = false;
            EditorGUILayout.ObjectField(_current?.Value, typeof(Object), false);
            GUI.enabled = true;

            _palette = (PaintPhysicsMaterialOverlayPalette)EditorGUILayout.ObjectField(_palette, typeof(PaintPhysicsMaterialOverlayPalette), false);

            //scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(WindowSize.y - 60));

            if (_palette)
            {
                foreach (PaintPhysicsMaterialOverlayPalette.Entry3D entry in _palette.Entries3D)
                {
                    DrawMaterialToggle(entry);
                }
            }

            //GUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = lastLabelWidth;
        }

        private void DrawAllColliders(Scene scene, SceneView sceneView)
        {
            if (_alpha == 0)
            {
                return;
            }

            if (!_palette)
            {
                return;
            }

            Vector3 cameraPosition = sceneView.camera.transform.position;

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(sceneView.camera);

            Collider[] colliders = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Collider>())
                                        .ToArray();

            foreach (Collider collider in colliders)
            {
                if ((collider.bounds.center - cameraPosition).sqrMagnitude < _maxDistance * _maxDistance &&
                    GeometryUtility.TestPlanesAABB(planes, collider.bounds))
                {
                    DrawCollider(collider, _alpha);
                }
            }
        }

        private void DrawColliderCurrent(Scene scene)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            bool result = scene.GetPhysicsScene().Raycast(ray.origin, ray.direction, out RaycastHit hit, 1000);

            if (!result)
            {
                return;
            }

            if (!_palette)
            {
                return;
            }

            DrawCollider(hit.collider, 1);
        }

        private void DrawCollider(Collider collider, float alpha)
        {
            if (collider.isTrigger)
            {
                return;
            }

            if (!collider.enabled)
            {
                return;
            }

            PaintPhysicsMaterialOverlayPalette.Entry3D entry = _palette.Entries3D.FirstOrDefault(e => e.Value == collider.sharedMaterial);

            if (entry == null)
            {
                return;
            }

            Color color = entry.Color.SetAlpha(entry.Color.a * alpha);

            switch (collider)
            {
                case BoxCollider box:
                {
                    DrawCube(box, color);
                    break;
                }

                case CapsuleCollider capsule:
                {
                    DrawCapsule(capsule, color);
                    break;
                }

                case SphereCollider sphere:
                {
                    DrawSphere(sphere, color);
                    break;
                }

                case MeshCollider mesh:
                {
                    DrawMesh(mesh, color);
                    break;
                }
            }
        }

        private void SwapMaterialUnderCursor(Scene scene)
        {
            Debug.Log($"click Swap material");

            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            bool result = scene.GetPhysicsScene().Raycast(ray.origin, ray.direction, out RaycastHit hit, 1000);

            if (result)
            {
                Debug.Log($"Swap material '{hit.collider.name}'", hit.collider);
                SwapMaterial(hit.collider);
            }
        }

        void SwapMaterial(Collider collider)
        {
            if (_current is PaintPhysicsMaterialOverlayPalette.Entry3D entry3D)
            {
                Undo.RecordObject(collider, $"swap material {_current.Name}");
                collider.sharedMaterial = entry3D.Material;
            }
        }

        void DrawMaterialToggle(PaintPhysicsMaterialOverlayPalette.EntryBase entry)
        {
            if (entry == null)
            {
                return;
            }

            GUI.color = entry.Color.SetAlpha(1);

            bool newResult = GUILayout.Toggle(entry == _current, entry.Name, "button");
            GUI.color = Color.white;

            if (newResult)
            {
                _current = entry;
            }
        }

        private void DrawCube(BoxCollider box, Color color)
        {
            Matrix4x4 old = Handles.matrix;
            Handles.color = color;
            Handles.matrix = box.transform.localToWorldMatrix;
            Handles.DrawWireCube(box.center, box.size);
            Handles.matrix = old;
        }

        private void DrawCapsule(CapsuleCollider capsule, Color color)
        {
            Handles.color = color;
            // calculate top and bottom center sphere locations.
            float offset = capsule.height / 2;
            Vector3 top, bottom = top = capsule.center;
            float radius = capsule.radius;
#if (UNITY_2017_2_OR_NEWER)
            Vector3 scale = capsule.transform.localToWorldMatrix.lossyScale;
#else
      Vector3 scale = CurrentAttachTo.transform.lossyScale;
#endif
            switch (capsule.direction)
            {
                case 0: //x axis
                    //adjust radius by the bigger scale.
                    radius *= scale.y > scale.z ? scale.y : scale.z;
                    // adjust the offset to top and bottom mid points for spheres based on radius / scale in that direction
                    offset -= radius / scale.x;
                    // offset top and bottom points.
                    top.x += offset;
                    bottom.x -= offset;
                    break;

                case 1:
                    radius *= scale.x > scale.z ? scale.x : scale.z;
                    offset -= radius / scale.y;
                    top.y += offset;
                    bottom.y -= offset;
                    break;

                case 2:
                    radius *= scale.x > scale.y ? scale.x : scale.y;
                    offset -= radius / scale.z;
                    top.z += offset;
                    bottom.z -= offset;
                    break;
            }

            if (capsule.height < capsule.radius * 2)
            {
                // draw just the sphere if the radius and the height will make a sphere.
                Vector3 worldCenter = capsule.transform.localToWorldMatrix.MultiplyPoint(capsule.center);
                Handles.DrawWireDisc(worldCenter, Vector3.forward, radius);
                Handles.DrawWireDisc(worldCenter, Vector3.right, radius);
                Handles.DrawWireDisc(worldCenter, Vector3.up, radius);
                return;
            }

            Vector3 worldTop = capsule.transform.localToWorldMatrix.MultiplyPoint3x4(top);
            Vector3 worldBottom = capsule.transform.localToWorldMatrix.MultiplyPoint3x4(bottom);
            Vector3 up = worldTop - worldBottom;
            Vector3 cross1 = Vector3.up;

            // dont want to cross if in same direction, forward works in this case as the first cross
            if (up.normalized == cross1 || up.normalized == -cross1)
            {
                cross1 = Vector3.forward;
            }

            Vector3 right = Vector3.Cross(up, -cross1).normalized;
            Vector3 forward = Vector3.Cross(up, -right).normalized;
            // full circles at top and bottom
            Handles.DrawWireDisc(worldTop, up, radius);
            Handles.DrawWireDisc(worldBottom, up, radius);
            // half arcs at top and bottom
            Handles.DrawWireArc(worldTop, forward, right, 180f, radius);
            Handles.DrawWireArc(worldTop, -right, forward, 180f, radius);
            Handles.DrawWireArc(worldBottom, -forward, right, 180f, radius);
            Handles.DrawWireArc(worldBottom, right, forward, 180f, radius);
            // connect bottom and top side points
            Handles.DrawLine(worldTop + right * radius, worldBottom + right * radius);
            Handles.DrawLine(worldTop - right * radius, worldBottom - right * radius);
            Handles.DrawLine(worldTop + forward * radius, worldBottom + forward * radius);
            Handles.DrawLine(worldTop - forward * radius, worldBottom - forward * radius);
        }

        private void DrawSphere(SphereCollider sphere, Color color)
        {
            Handles.color = color;
            Vector3 worldCenter = sphere.transform.localToWorldMatrix.MultiplyPoint3x4(sphere.center);
            // Draw all normal axis' rings at the world center location for both perspective and isometric/orthographic
            float radius = sphere.radius;
#if (UNITY_2017_2_OR_NEWER)
            Vector3 scale = sphere.transform.localToWorldMatrix.lossyScale;
#else
      Vector3 scale = CurrentAttachTo.transform.lossyScale;
#endif
            float largestScale = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z);
            radius *= largestScale;
            Handles.DrawWireDisc(worldCenter, Vector3.forward, radius);
            Handles.DrawWireDisc(worldCenter, Vector3.right, radius);
            Handles.DrawWireDisc(worldCenter, Vector3.up, radius);

            // orthographic camera
            if (Camera.current != null)
            {
                if (Camera.current.orthographic)
                {
                    // simple, use cameras forward in orthographic
                    Handles.DrawWireDisc(worldCenter, Camera.current.transform.forward, radius);
                }
                else
                {
                    // draw a circle facing the camera covering all the radius in prespective mode
                    Vector3 normal = worldCenter - Handles.inverseMatrix.MultiplyPoint(Camera.current.transform.position);
                    float sqrMagnitude = normal.sqrMagnitude;
                    float r2 = radius * radius;
                    float r4M = r2 * r2 / sqrMagnitude;
                    float newRadius = Mathf.Sqrt(r2 - r4M);
                    Handles.DrawWireDisc(worldCenter - r2 * normal / sqrMagnitude, normal, newRadius);
                }
            }
        }

        private void DrawMesh(MeshCollider mesh, Color color)
        {
            // try to find mesh shader
            if (_meshColliderShader == null)
            {
                _meshColliderShader = Shader.Find("Unlit/Color");
            }

            if (!_meshColliderShader)
            {
                return;
            }

            if (!_wireMat)
            {
                _wireMat = new Material(_meshColliderShader);
            }

            if (_meshColliderShader == null || mesh.sharedMesh == null)
            {
                return;
            }

            _wireMat.SetColor(ColorProperty, color);
            _wireMat.SetPass(0);
            GL.wireframe = true;
            Graphics.DrawMeshNow(mesh.sharedMesh, mesh.transform.localToWorldMatrix);
            GL.wireframe = false;
            // Graphics.DrawMeshNow(mesh.sharedMesh, mesh.transform.localToWorldMatrix);
        }
    }

    public static class Utility
    {
        public static Color SetAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
