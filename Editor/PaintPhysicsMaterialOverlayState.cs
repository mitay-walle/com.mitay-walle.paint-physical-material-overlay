using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesign
{
	internal static class PaintPhysicsMaterialOverlayState
	{
		public static bool Enabled = true;
		public static float MaxDistance = 50;
		public static float Alpha = .25f;
		public static bool DrawAll;
		public static bool UseLeftClick = true;
		public static KeyCode PaintHotKey = KeyCode.V;
		public static PaintPhysicsMaterialOverlayPalette.EntryBase Current;
		public static PaintPhysicsMaterialOverlayPalette Palette;

		private static bool _activatedTool;

		public static bool CanPaint => Enabled && Palette && Current is PaintPhysicsMaterialOverlayPalette.Entry3D;
		public static bool ShouldUseTool => CanPaint && UseLeftClick;

		public static Scene CurrentScene
		{
			get
			{
				PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
				return stage != null ? stage.scene : SceneManager.GetActiveScene();
			}
		}

		public static void NormalizeSelection()
		{
			if (!Palette)
			{
				Current = null;
				return;
			}

			if (Current is PaintPhysicsMaterialOverlayPalette.Entry3D currentEntry && Palette.Entries3D.Contains(currentEntry))
			{
				return;
			}

			Current = null;
		}

		public static void UpdateToolActivation()
		{
			bool active = ToolManager.activeToolType == typeof(PaintPhysicsMaterialTool);
			if (ShouldUseTool)
			{
				if (!active)
				{
					ToolManager.SetActiveTool<PaintPhysicsMaterialTool>();
					_activatedTool = true;
				}

				return;
			}

			RestoreAutoActivatedTool();
		}

		public static void RestoreAutoActivatedTool()
		{
			if (!_activatedTool || ToolManager.activeToolType != typeof(PaintPhysicsMaterialTool))
			{
				_activatedTool = false;
				return;
			}

			_activatedTool = false;
			ToolManager.RestorePreviousTool();
		}

		public static bool TryGetColliderUnderCursor(Scene scene, out Collider collider)
		{
			Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
			bool result = scene.GetPhysicsScene().Raycast(ray.origin, ray.direction, out RaycastHit hit, 1000);
			collider = result ? hit.collider : null;
			return collider != null;
		}

		public static bool Paint(Collider collider)
		{
			if (collider == null || Current is not PaintPhysicsMaterialOverlayPalette.Entry3D entry)
			{
				return false;
			}

			if (collider.sharedMaterial == entry.Material)
			{
				return false;
			}

			Undo.RecordObject(collider, $"Paint Physics Material {Current.Name}");
			collider.sharedMaterial = entry.Material;
			EditorUtility.SetDirty(collider);
			return true;
		}
	}
}