using UnityEditor;
using UnityEngine;

namespace Raven12345.SpineAssetStudio {
	/// <summary>Shown while the optional spine-unity dependency is unavailable.</summary>
	public sealed class SpineAssetStudioMissingSpineWindow : EditorWindow {
		[MenuItem("Spine/Asset Studio")]
		static void Open () => GetWindow<SpineAssetStudioMissingSpineWindow>("Spine Asset Studio");

		void OnGUI () {
			EditorGUILayout.Space(12);
			EditorGUILayout.LabelField("SPINE ASSET STUDIO", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Spine Asset Studio is installed, but spine-unity has not been detected. " +
				"Import a compatible spine-unity runtime, then ensure the SPINE_UNITY scripting define is enabled. " +
				"Unity will compile the full editor automatically after the next domain reload.",
				MessageType.Info);
			if (GUILayout.Button("Open Player Settings")) SettingsService.OpenProjectSettings("Project/Player");
		}
	}
}
