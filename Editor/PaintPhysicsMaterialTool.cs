using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesign
{
	[EditorTool("Paint Physics Material")]
	public sealed class PaintPhysicsMaterialTool : EditorTool
	{
		private static GUIContent _iconContent;

		private readonly int _toolHash = nameof(PaintPhysicsMaterialTool).GetHashCode();
		private Collider _lastPaintedCollider;
		private Material _wireMaterial;
		private Shader _meshColliderShader;

		private static readonly int ColorProperty = Shader.PropertyToID("_Color");

		public override GUIContent toolbarIcon
		{
			get
			{
				_iconContent ??= EditorGUIUtility.IconContent("d_PhysicMaterial Icon", "Paint Physics Material");
				return _iconContent;
			}
		}

		public override void OnToolGUI(EditorWindow window)
		{
			if (!PaintPhysicsMaterialOverlayState.ShouldUseTool)
			{
				PaintPhysicsMaterialOverlayState.RestoreAutoActivatedTool();
				return;
			}

			if (window is not SceneView sceneView)
			{
				return;
			}

			Scene scene = PaintPhysicsMaterialOverlayState.CurrentScene;
			Event currentEvent = Event.current;

			if (currentEvent.type == EventType.Repaint)
			{
				DrawScenePreview(scene, sceneView);
			}

			HandleInput(scene, currentEvent);
		}

		private void HandleInput(Scene scene, Event currentEvent)
		{
			if (!PaintPhysicsMaterialOverlayState.CanPaint)
			{
				return;
			}

			int controlId = GUIUtility.GetControlID(_toolHash, FocusType.Passive);
			if (currentEvent.type == EventType.Layout && PaintPhysicsMaterialOverlayState.UseLeftClick)
			{
				HandleUtility.AddDefaultControl(controlId);
			}

			if (IsPaintHotkey(currentEvent))
			{
				PaintUnderCursor(scene);
				currentEvent.Use();
				return;
			}

			if (!PaintPhysicsMaterialOverlayState.UseLeftClick || !IsPlainLeftMouseEvent(currentEvent))
			{
				return;
			}

			switch (currentEvent.type)
			{
				case EventType.MouseDown:
					GUIUtility.hotControl = controlId;
					_lastPaintedCollider = null;
					PaintUnderCursor(scene);
					currentEvent.Use();
					break;

				case EventType.MouseDrag when GUIUtility.hotControl == controlId:
					PaintUnderCursor(scene);
					currentEvent.Use();
					break;

				case EventType.MouseUp when GUIUtility.hotControl == controlId:
					GUIUtility.hotControl = 0;
					_lastPaintedCollider = null;
					currentEvent.Use();
					break;
			}
		}

		private static bool IsPaintHotkey(Event currentEvent)
		{
			return PaintPhysicsMaterialOverlayState.PaintHotKey != KeyCode.None &&
			       currentEvent.type == EventType.KeyDown &&
			       currentEvent.keyCode == PaintPhysicsMaterialOverlayState.PaintHotKey &&
			       !currentEvent.alt;
		}

		private static bool IsPlainLeftMouseEvent(Event currentEvent)
		{
			return currentEvent.button == 0 &&
			       !currentEvent.alt &&
			       !currentEvent.control &&
			       !currentEvent.command;
		}

		private void PaintUnderCursor(Scene scene)
		{
			if (!PaintPhysicsMaterialOverlayState.TryGetColliderUnderCursor(scene, out Collider collider))
			{
				return;
			}

			if (collider == _lastPaintedCollider)
			{
				return;
			}

			if (PaintPhysicsMaterialOverlayState.Paint(collider))
			{
				_lastPaintedCollider = collider;
			}
		}

		private void DrawScenePreview(Scene scene, SceneView sceneView)
		{
			if (!PaintPhysicsMaterialOverlayState.Enabled || !PaintPhysicsMaterialOverlayState.Palette)
			{
				return;
			}

			if (PaintPhysicsMaterialOverlayState.DrawAll)
			{
				DrawAllColliders(scene, sceneView);
			}

			DrawColliderUnderCursor(scene);
			sceneView.Repaint();
		}

		private void DrawAllColliders(Scene scene, SceneView sceneView)
		{
			if (PaintPhysicsMaterialOverlayState.Alpha == 0)
			{
				return;
			}

			Vector3 cameraPosition = sceneView.camera.transform.position;
			Plane[] planes = GeometryUtility.CalculateFrustumPlanes(sceneView.camera);
			Collider[] colliders = scene.GetRootGameObjects()
				.SelectMany(gameObject => gameObject.GetComponentsInChildren<Collider>())
				.ToArray();

			foreach (Collider collider in colliders)
			{
				if ((collider.bounds.center - cameraPosition).sqrMagnitude < PaintPhysicsMaterialOverlayState.MaxDistance * PaintPhysicsMaterialOverlayState.MaxDistance &&
				    GeometryUtility.TestPlanesAABB(planes, collider.bounds))
				{
					DrawCollider(collider, PaintPhysicsMaterialOverlayState.Alpha);
				}
			}
		}

		private void DrawColliderUnderCursor(Scene scene)
		{
			if (!PaintPhysicsMaterialOverlayState.TryGetColliderUnderCursor(scene, out Collider collider))
			{
				return;
			}

			DrawCollider(collider, 1);
		}

		private void DrawCollider(Collider collider, float alpha)
		{
			if (collider.isTrigger || !collider.enabled)
			{
				return;
			}

			PaintPhysicsMaterialOverlayPalette.Entry3D entry = PaintPhysicsMaterialOverlayState.Palette.Entries3D.FirstOrDefault(e => e.Value == collider.sharedMaterial);
			if (entry == null)
			{
				return;
			}

			Color color = entry.Color.SetAlpha(entry.Color.a * alpha);

			switch (collider)
			{
				case BoxCollider box:
					DrawCube(box, color);
					break;

				case CapsuleCollider capsule:
					DrawCapsule(capsule, color);
					break;

				case SphereCollider sphere:
					DrawSphere(sphere, color);
					break;

				case MeshCollider mesh:
					DrawMesh(mesh, color);
					break;
			}
		}

		private static void DrawCube(BoxCollider box, Color color)
		{
			Matrix4x4 old = Handles.matrix;
			Handles.color = color;
			Handles.matrix = box.transform.localToWorldMatrix;
			Handles.DrawWireCube(box.center, box.size);
			Handles.matrix = old;
		}

		private static void DrawCapsule(CapsuleCollider capsule, Color color)
		{
			Handles.color = color;
			float offset = capsule.height / 2;
			Vector3 top = capsule.center;
			Vector3 bottom = top;
			float radius = capsule.radius;
			Vector3 scale = capsule.transform.localToWorldMatrix.lossyScale;

			switch (capsule.direction)
			{
				case 0:
					radius *= scale.y > scale.z ? scale.y : scale.z;
					offset -= radius / scale.x;
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
				Vector3 worldCenter = capsule.transform.localToWorldMatrix.MultiplyPoint(capsule.center);
				Handles.DrawWireDisc(worldCenter, Vector3.forward, radius);
				Handles.DrawWireDisc(worldCenter, Vector3.right, radius);
				Handles.DrawWireDisc(worldCenter, Vector3.up, radius);
				return;
			}

			Vector3 worldTop = capsule.transform.localToWorldMatrix.MultiplyPoint3x4(top);
			Vector3 worldBottom = capsule.transform.localToWorldMatrix.MultiplyPoint3x4(bottom);
			Vector3 up = worldTop - worldBottom;
			Vector3 cross = Vector3.up;

			if (up.normalized == cross || up.normalized == -cross)
			{
				cross = Vector3.forward;
			}

			Vector3 right = Vector3.Cross(up, -cross).normalized;
			Vector3 forward = Vector3.Cross(up, -right).normalized;
			Handles.DrawWireDisc(worldTop, up, radius);
			Handles.DrawWireDisc(worldBottom, up, radius);
			Handles.DrawWireArc(worldTop, forward, right, 180f, radius);
			Handles.DrawWireArc(worldTop, -right, forward, 180f, radius);
			Handles.DrawWireArc(worldBottom, -forward, right, 180f, radius);
			Handles.DrawWireArc(worldBottom, right, forward, 180f, radius);
			Handles.DrawLine(worldTop + right * radius, worldBottom + right * radius);
			Handles.DrawLine(worldTop - right * radius, worldBottom - right * radius);
			Handles.DrawLine(worldTop + forward * radius, worldBottom + forward * radius);
			Handles.DrawLine(worldTop - forward * radius, worldBottom - forward * radius);
		}

		private static void DrawSphere(SphereCollider sphere, Color color)
		{
			Handles.color = color;
			Vector3 worldCenter = sphere.transform.localToWorldMatrix.MultiplyPoint3x4(sphere.center);
			float radius = sphere.radius;
			Vector3 scale = sphere.transform.localToWorldMatrix.lossyScale;
			float largestScale = Mathf.Max(Mathf.Max(scale.x, scale.y), scale.z);
			radius *= largestScale;
			Handles.DrawWireDisc(worldCenter, Vector3.forward, radius);
			Handles.DrawWireDisc(worldCenter, Vector3.right, radius);
			Handles.DrawWireDisc(worldCenter, Vector3.up, radius);

			if (Camera.current == null)
			{
				return;
			}

			if (Camera.current.orthographic)
			{
				Handles.DrawWireDisc(worldCenter, Camera.current.transform.forward, radius);
				return;
			}

			Vector3 normal = worldCenter - Handles.inverseMatrix.MultiplyPoint(Camera.current.transform.position);
			float sqrMagnitude = normal.sqrMagnitude;
			float radiusSquared = radius * radius;
			float radiusFourthByMagnitude = radiusSquared * radiusSquared / sqrMagnitude;
			float newRadius = Mathf.Sqrt(radiusSquared - radiusFourthByMagnitude);
			Handles.DrawWireDisc(worldCenter - radiusSquared * normal / sqrMagnitude, normal, newRadius);
		}

		private void DrawMesh(MeshCollider mesh, Color color)
		{
			_meshColliderShader ??= Shader.Find("Unlit/Color");
			if (!_meshColliderShader || mesh.sharedMesh == null)
			{
				return;
			}

			_wireMaterial ??= new Material(_meshColliderShader);
			_wireMaterial.SetColor(ColorProperty, color);
			_wireMaterial.SetPass(0);
			GL.wireframe = true;
			Graphics.DrawMeshNow(mesh.sharedMesh, mesh.transform.localToWorldMatrix);
			GL.wireframe = false;
		}
	}
}