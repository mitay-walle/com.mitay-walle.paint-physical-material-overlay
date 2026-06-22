using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelDesign
{
	/// <summary>
	/// SceneView overlay for configuring the Paint Physics Material tool.
	/// </summary>
	[Overlay(typeof(SceneView), "Paint Physics Material")]
	public sealed class PaintPhysicsMaterialOverlay : Overlay
	{
		private static readonly GUIContent IconGUIContent = EditorGUIUtility.IconContent("d_PhysicMaterial Icon");

		private GUIContent _guiContent;

		public override void OnCreated()
		{
			displayedChanged += OnDisplayedChanged;
			collapsedChanged += OnCollapsedChanged;
			UpdateEnabledState();
		}

		public override void OnWillBeDestroyed()
		{
			displayedChanged -= OnDisplayedChanged;
			collapsedChanged -= OnCollapsedChanged;
			PaintPhysicsMaterialOverlayState.Enabled = false;
			PaintPhysicsMaterialOverlayState.UpdateToolActivation();
		}

		public override VisualElement CreatePanelContent()
		{
			VisualElement root = new VisualElement
			{
				name = nameof(PaintPhysicsMaterialOverlay)
			};
			root.Add(new IMGUIContainer(OnWindowGUI));

			_guiContent = IconGUIContent;
			_guiContent.text = "Paint Physical Material";
			_guiContent.tooltip = "Configure the SceneView physics material paint tool";

			return root;
		}

		private void OnWindowGUI()
		{
			UpdateEnabledState();

			float lastLabelWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 100;

			PaintPhysicsMaterialOverlayState.MaxDistance = EditorGUILayout.Slider(
				"Max Distance",
				PaintPhysicsMaterialOverlayState.MaxDistance,
				0,
				1000);
			PaintPhysicsMaterialOverlayState.Alpha = EditorGUILayout.Slider(
				"Alpha",
				PaintPhysicsMaterialOverlayState.Alpha,
				0,
				1);
			PaintPhysicsMaterialOverlayState.DrawAll = EditorGUILayout.Toggle("Draw All", PaintPhysicsMaterialOverlayState.DrawAll);
			PaintPhysicsMaterialOverlayState.UseLeftClick = EditorGUILayout.Toggle("Use Left Click", PaintPhysicsMaterialOverlayState.UseLeftClick);
			PaintPhysicsMaterialOverlayState.PaintHotKey = (KeyCode)EditorGUILayout.EnumPopup("Hotkey", PaintPhysicsMaterialOverlayState.PaintHotKey);

			GUI.enabled = false;
			EditorGUILayout.ObjectField(PaintPhysicsMaterialOverlayState.Current?.Value, typeof(Object), false);
			GUI.enabled = true;

			PaintPhysicsMaterialOverlayState.Palette = (PaintPhysicsMaterialOverlayPalette)EditorGUILayout.ObjectField(
				PaintPhysicsMaterialOverlayState.Palette,
				typeof(PaintPhysicsMaterialOverlayPalette),
				false);
			PaintPhysicsMaterialOverlayState.NormalizeSelection();

			DrawPaletteEntries();
			PaintPhysicsMaterialOverlayState.UpdateToolActivation();
			EditorGUIUtility.labelWidth = lastLabelWidth;
		}

		private void OnDisplayedChanged(bool isDisplayed)
		{
			UpdateEnabledState();
			PaintPhysicsMaterialOverlayState.UpdateToolActivation();
		}

		private void OnCollapsedChanged(bool isCollapsed)
		{
			UpdateEnabledState();
			PaintPhysicsMaterialOverlayState.UpdateToolActivation();
		}

		private void UpdateEnabledState()
		{
			PaintPhysicsMaterialOverlayState.Enabled = displayed && !collapsed;
		}

		private static void DrawPaletteEntries()
		{
			PaintPhysicsMaterialOverlayPalette palette = PaintPhysicsMaterialOverlayState.Palette;
			if (!palette)
			{
				return;
			}

			foreach (PaintPhysicsMaterialOverlayPalette.Entry3D entry in palette.Entries3D)
			{
				DrawMaterialToggle(entry);
			}
		}

		private static void DrawMaterialToggle(PaintPhysicsMaterialOverlayPalette.EntryBase entry)
		{
			if (entry == null)
			{
				return;
			}

			Color lastColor = GUI.color;
			GUI.color = entry.Color.SetAlpha(1);
			bool selected = GUILayout.Toggle(entry == PaintPhysicsMaterialOverlayState.Current, entry.Name, "button");
			GUI.color = lastColor;

			if (selected)
			{
				PaintPhysicsMaterialOverlayState.Current = entry;
			}
		}
	}
}