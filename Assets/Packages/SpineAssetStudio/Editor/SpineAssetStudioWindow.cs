using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
#if SPINE_UNITY
using Spine;
using Spine.Unity;
#endif
using UnityEditor;
using UnityEngine;

#if SPINE_UNITY
/// <summary>Edits event timelines in a Spine JSON export and previews it in an isolated editor preview scene.</summary>
public class SpineAssetStudioWindow : EditorWindow {
	SkeletonDataAsset skeletonDataAsset;
	readonly List<SkeletonDataAsset> projectSelection = new List<SkeletonDataAsset>();
	int projectSelectionIndex;
	int animationIndex;
	int skinIndex;
	float previewTime;
	bool playing;
	Vector2 scroll;
	double lastTime;
	PreviewRenderUtility preview;
	GameObject previewObject;
	Dictionary<string, object> sourceRoot;
	string previewDiagnostics = "Preview has not been initialized.";
	int selectedEventIndex = -1;
	int draggingEventIndex = -1;
	string eventSearch = "";
	bool sortByTime = true;
	float snapStep = .01f;
	bool showAnimationManager;
	bool showSkinManager;
	string animationManagerName = "";
	string skinManagerName = "";
	readonly Stack<string> undoSnapshots = new Stack<string>();
	readonly Stack<string> redoSnapshots = new Stack<string>();

	[MenuItem("Spine/Asset Studio")]
	static void Open () => GetWindow<SpineAssetStudioWindow>("Spine Asset Studio");
	[MenuItem("Assets/Open in Spine Asset Studio", false, 2000)]
	static void OpenProjectSelection () { SpineAssetStudioWindow window = GetWindow<SpineAssetStudioWindow>("Spine Asset Studio"); window.LoadProjectSelection(); window.Focus(); }
	[MenuItem("Assets/Open in Spine Asset Studio", true)]
	static bool ValidateOpenProjectSelection () => Selection.objects.Any(item => item is SkeletonDataAsset || item is TextAsset);
	void OnEnable () { EditorApplication.update += Tick; lastTime = EditorApplication.timeSinceStartup; }
	void OnDisable () { EditorApplication.update -= Tick; DisposePreview(); }
	void Tick () {
		if (!playing || Duration <= 0f) { lastTime = EditorApplication.timeSinceStartup; return; }
		previewTime = Mathf.Repeat(previewTime + (float)(EditorApplication.timeSinceStartup - lastTime), Duration);
		lastTime = EditorApplication.timeSinceStartup; ApplyPreviewPose(); Repaint();
	}

	void OnGUI () {
		EditorGUILayout.Space(8);
		EditorGUILayout.LabelField("SPINE ASSET STUDIO", EditorStyles.boldLabel);
		EditorGUILayout.LabelField("Direct JSON editing • isolated preview", EditorStyles.miniLabel);
		EditorGUILayout.BeginHorizontal();
		EditorGUI.BeginChangeCheck();
		SkeletonDataAsset pickedAsset = (SkeletonDataAsset)EditorGUILayout.ObjectField("Skeleton Data", skeletonDataAsset, typeof(SkeletonDataAsset), false);
		if (EditorGUI.EndChangeCheck()) SetSkeletonDataAsset(pickedAsset);
		if (GUILayout.Button($"Use Project Selection ({Selection.objects.Length})", GUILayout.Width(170))) LoadProjectSelection();
		EditorGUILayout.EndHorizontal();
		if (projectSelection.Count > 1) {
			string[] selectedNames = projectSelection.Select((asset, i) => $"{i + 1}. {asset.name}").ToArray();
			EditorGUI.BeginChangeCheck();
			projectSelectionIndex = EditorGUILayout.Popup(new GUIContent("Active file", "Only the active file is edited. Other selected files remain unchanged."), projectSelectionIndex, selectedNames);
			if (EditorGUI.EndChangeCheck()) SetSkeletonDataAsset(projectSelection[projectSelectionIndex], false);
			EditorGUILayout.HelpBox($"{projectSelection.Count} files selected. Only the Active file will be modified.", MessageType.Info);
		}
		if (skeletonDataAsset == null) { EditorGUILayout.HelpBox("Select a SkeletonDataAsset that uses a Spine JSON export.", MessageType.Info); return; }
		if (!IsJsonSource) { EditorGUILayout.HelpBox("This asset uses a binary .skel export. Direct editing is supported only for Spine JSON exports.", MessageType.Error); return; }
		string[] names = AnimationNames;
		if (names.Length == 0) { EditorGUILayout.HelpBox("No animations found in this export.", MessageType.Warning); return; }
		animationIndex = Mathf.Clamp(animationIndex, 0, names.Length - 1);
		EditorGUI.BeginChangeCheck(); animationIndex = EditorGUILayout.Popup("Animation", animationIndex, names);
		if (EditorGUI.EndChangeCheck()) { previewTime = 0; selectedEventIndex = -1; animationManagerName = ""; ApplyPreviewPose(); }
		string[] skinNames = SkinNames;
		if (skinNames.Length > 0) {
			skinIndex = Mathf.Clamp(skinIndex, 0, skinNames.Length - 1);
			EditorGUI.BeginChangeCheck();
			skinIndex = EditorGUILayout.Popup(new GUIContent("Preview Skin", "Changes only the skin shown in this preview."), skinIndex, skinNames);
			if (EditorGUI.EndChangeCheck()) { skinManagerName = ""; ApplyPreviewPose(); }
		} else {
			EditorGUILayout.LabelField("Preview Skin", "No skins available");
		}
		DrawAnimationManager();
		DrawSkinManager();
		DrawPreview(); DrawTransport();
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("Log Preview Diagnostics", GUILayout.Width(160))) LogPreviewDiagnostics();
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.LabelField(previewDiagnostics, EditorStyles.wordWrappedMiniLabel);
		EditorGUILayout.Space(7);
		EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("EVENT TIMELINE", EditorStyles.boldLabel); GUILayout.FlexibleSpace();
		GUI.enabled = undoSnapshots.Count > 0; if (GUILayout.Button("Undo", GUILayout.Width(48))) Undo(); GUI.enabled = redoSnapshots.Count > 0; if (GUILayout.Button("Redo", GUILayout.Width(48))) Redo(); GUI.enabled = true;
		if (GUILayout.Button("+ Add at Playhead", GUILayout.Width(142))) AddEvent(); EditorGUILayout.EndHorizontal();
		DrawTimeline(); DrawEvents();
		EditorGUILayout.HelpBox("Changes are written directly to the assigned Spine JSON file and reimported immediately. The active Scene is never used for preview.", MessageType.None);
	}

	void DrawPreview () {
		Rect r = GUILayoutUtility.GetRect(10, 220, GUILayout.ExpandWidth(true));
		if (preview == null || previewObject == null) RebuildPreview();
		if (preview == null) { EditorGUI.DrawRect(r, new Color(.12f, .12f, .12f)); GUI.Label(r, "Preview unavailable", EditorStyles.centeredGreyMiniLabel); return; }
		preview.BeginPreview(r, GUIStyle.none);
		preview.camera.backgroundColor = new Color(.14f, .15f, .18f);
		// Draw explicitly: PreviewRenderUtility does not reliably run the normal
		// renderer culling path for a Spine mesh rebuilt outside the player loop.
		foreach (MeshFilter filter in previewObject.GetComponentsInChildren<MeshFilter>()) {
			MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
			if (filter.sharedMesh == null || renderer == null) continue;
			Material[] materials = renderer.sharedMaterials;
			for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount && submesh < materials.Length; submesh++)
				if (materials[submesh] != null) preview.DrawMesh(filter.sharedMesh, filter.transform.localToWorldMatrix, materials[submesh], submesh);
		}
		preview.camera.Render();
		GUI.DrawTexture(r, preview.EndPreview(), ScaleMode.StretchToFill, false);
	}

	void DrawTransport () {
		EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
		if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(55))) { playing = !playing; lastTime = EditorApplication.timeSinceStartup; }
		if (GUILayout.Button("|<", GUILayout.Width(32))) { previewTime = 0; ApplyPreviewPose(); }
		EditorGUI.BeginChangeCheck(); previewTime = EditorGUILayout.Slider(previewTime, 0, Mathf.Max(.01f, Duration));
		if (EditorGUI.EndChangeCheck()) ApplyPreviewPose();
		EditorGUILayout.LabelField($"{previewTime:0.000}s / {Duration:0.000}s", GUILayout.Width(115)); EditorGUILayout.EndHorizontal();
	}

	void DrawTimeline () {
		Rect r = GUILayoutUtility.GetRect(10, 46, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(r, new Color(.10f, .12f, .16f));
		float duration = Mathf.Max(.01f, Duration);
		for (int i = 0; i < RawEventKeys.Count; i++) { Dictionary<string, object> e = (Dictionary<string, object>)RawEventKeys[i]; float x = r.x + Mathf.Clamp01(Number(e, "time") / duration) * r.width; Color color = i == selectedEventIndex ? new Color(1f, .47f, .1f) : new Color(.25f, .82f, 1f); EditorGUI.DrawRect(new Rect(x - 3, r.y + 4, 6, r.height - 8), color); }
		float h = r.x + Mathf.Clamp01(previewTime / duration) * r.width; EditorGUI.DrawRect(new Rect(h - 1, r.y, 2, r.height), new Color(1, .72f, .18f));
		UnityEngine.Event input = UnityEngine.Event.current;
		if (input.type == EventType.MouseDown && r.Contains(input.mousePosition)) { int hit = FindMarkerAt(r, input.mousePosition.x); if (hit >= 0) { selectedEventIndex = hit; draggingEventIndex = hit; RecordUndo(); } else previewTime = Mathf.Clamp01((input.mousePosition.x - r.x) / r.width) * duration; ApplyPreviewPose(); input.Use(); }
		if (input.type == EventType.MouseDrag && draggingEventIndex >= 0) { float time = Snap(Mathf.Clamp01((input.mousePosition.x - r.x) / r.width) * duration); ((Dictionary<string, object>)RawEventKeys[draggingEventIndex])["time"] = time; previewTime = time; ApplyPreviewPose(); input.Use(); Repaint(); }
		if (input.type == EventType.MouseUp && draggingEventIndex >= 0) { Save(false); draggingEventIndex = -1; input.Use(); }
	}

	void DrawEvents () {
		EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
		eventSearch = GUILayout.TextField(eventSearch, GUI.skin.FindStyle("ToolbarSearchTextField"));
		if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(22))) eventSearch = "";
		sortByTime = GUILayout.Toggle(sortByTime, "Time", EditorStyles.toolbarButton, GUILayout.Width(50));
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
		EditorGUILayout.LabelField("Snap interval (seconds)", GUILayout.Width(145));
		GUI.enabled = snapStep > 0f;
		float interval = EditorGUILayout.FloatField(snapStep, GUILayout.Width(80));
		GUI.enabled = true;
		if (GUILayout.Button(snapStep > 0f ? "Disable Snap" : "Enable Snap", GUILayout.Width(96))) snapStep = snapStep > 0f ? 0f : .01f;
		if (snapStep > 0f) snapStep = Mathf.Max(.0001f, interval);
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.LabelField(snapStep > 0f
			? $"Snap rounds event times to multiples of {snapStep:0.###} seconds when dragging, adding, or duplicating events. Set it to 0 to disable."
			: "Snap is disabled: events can be placed at any precise time.", EditorStyles.wordWrappedMiniLabel);
		if (selectedEventIndex >= 0 && selectedEventIndex < RawEventKeys.Count)
			EditorGUILayout.LabelField($"Selected: {Text((Dictionary<string, object>)RawEventKeys[selectedEventIndex], "name")} at {Number((Dictionary<string, object>)RawEventKeys[selectedEventIndex], "time"):0.000}s", EditorStyles.miniBoldLabel);
		scroll = EditorGUILayout.BeginScrollView(scroll);
		IEnumerable<int> indices = Enumerable.Range(0, RawEventKeys.Count).Where(i => string.IsNullOrEmpty(eventSearch) || Text((Dictionary<string, object>)RawEventKeys[i], "name").IndexOf(eventSearch, StringComparison.OrdinalIgnoreCase) >= 0);
		if (sortByTime) indices = indices.OrderBy(i => Number((Dictionary<string, object>)RawEventKeys[i], "time"));
		foreach (int index in indices.ToList()) {
			Dictionary<string, object> key = (Dictionary<string, object>)RawEventKeys[index];
			EditorGUILayout.BeginVertical(index == selectedEventIndex ? "SelectionRect" : EditorStyles.helpBox);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button($"{Number(key, "time"):0.000}", GUILayout.Width(58))) SelectEvent(index);
			if (GUILayout.Button(Text(key, "name"), EditorStyles.label, GUILayout.MinWidth(110))) SelectEvent(index);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Duplicate", GUILayout.Width(66))) DuplicateEvent(index);
			if (GUILayout.Button("Delete", GUILayout.Width(52))) { RecordUndo(); RawEventKeys.RemoveAt(index); if (selectedEventIndex == index) selectedEventIndex = -1; Save(false); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
			EditorGUILayout.EndHorizontal();
			if (index != selectedEventIndex) { EditorGUILayout.EndVertical(); continue; }
			EditorGUI.BeginChangeCheck();
			float time = EditorGUILayout.Slider("Time", Number(key, "time"), 0, Mathf.Max(.01f, Duration));
			string name = EditorGUILayout.TextField("Name", Text(key, "name"));
			int intValue = EditorGUILayout.IntField("Int", (int)Number(key, "int"));
			float floatValue = EditorGUILayout.FloatField("Float", Number(key, "float"));
			string stringValue = EditorGUILayout.TextField("String", Text(key, "string"));
			float volume = EditorGUILayout.Slider("Volume", Number(key, "volume", 1), 0, 1);
			float balance = EditorGUILayout.Slider("Balance", Number(key, "balance"), -1, 1);
			if (EditorGUI.EndChangeCheck()) { RecordUndo(); key["time"] = Snap(time); key["name"] = name; key["int"] = intValue; key["float"] = floatValue; key["string"] = stringValue; key["volume"] = volume; key["balance"] = balance; EnsureDefinition(name); Save(false); }
			EditorGUILayout.EndVertical();
		}
		if (EventKeys.Count == 0) EditorGUILayout.LabelField("No event keys in this animation.", EditorStyles.centeredGreyMiniLabel);
		EditorGUILayout.EndScrollView();
	}

	void AddEvent () { RecordUndo(); Dictionary<string, object> e = new Dictionary<string, object> { { "time", Snap(previewTime) }, { "name", "new-event" } }; EnsureDefinition("new-event"); List<object> events = GetRawEventKeys(true); events.Add(e); selectedEventIndex = events.Count - 1; Save(false); }
	void DuplicateEvent (int index) { RecordUndo(); Dictionary<string, object> source = (Dictionary<string, object>)RawEventKeys[index]; Dictionary<string, object> copy = new Dictionary<string, object>(source) { ["time"] = Snap(Number(source, "time") + Mathf.Max(snapStep, .01f)) }; RawEventKeys.Insert(index + 1, copy); selectedEventIndex = index + 1; Save(false); }
	void SelectEvent (int index) {
		if (selectedEventIndex == index) {
			selectedEventIndex = -1;
		} else {
			selectedEventIndex = index;
			previewTime = Number((Dictionary<string, object>)RawEventKeys[index], "time");
			ApplyPreviewPose();
		}
		Repaint();
	}
	int FindMarkerAt (Rect rect, float mouseX) { float duration = Mathf.Max(.01f, Duration); for (int i = 0; i < RawEventKeys.Count; i++) { float x = rect.x + Mathf.Clamp01(Number((Dictionary<string, object>)RawEventKeys[i], "time") / duration) * rect.width; if (Mathf.Abs(mouseX - x) <= 7f) return i; } return -1; }
	float Snap (float value) => snapStep <= 0f ? value : Mathf.Round(value / snapStep) * snapStep;
	void DrawAnimationManager () {
		showAnimationManager = EditorGUILayout.Foldout(showAnimationManager, "Animation Manager", true);
		if (!showAnimationManager || AnimationNames.Length == 0) return;
		string currentName = AnimationNames[Mathf.Clamp(animationIndex, 0, AnimationNames.Length - 1)];
		if (string.IsNullOrEmpty(animationManagerName)) animationManagerName = currentName;
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		animationManagerName = EditorGUILayout.TextField("Name", animationManagerName);
		EditorGUILayout.BeginHorizontal();
		GUI.enabled = animationManagerName != currentName && IsValidUniqueName(animationManagerName, AnimationNames);
		if (GUILayout.Button("Rename")) RenameAnimation(currentName, animationManagerName);
		GUI.enabled = IsValidUniqueName(animationManagerName, AnimationNames);
		if (GUILayout.Button("Duplicate")) DuplicateAnimation(currentName, animationManagerName);
		GUI.enabled = AnimationNames.Length > 1;
		if (GUILayout.Button("Delete")) DeleteAnimation(currentName);
		GUI.enabled = true;
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.LabelField("Rename updates this SkeletonDataAsset's mix settings and matching AnimationReferenceAssets.", EditorStyles.wordWrappedMiniLabel);
		EditorGUILayout.EndVertical();
	}
	void DrawSkinManager () {
		showSkinManager = EditorGUILayout.Foldout(showSkinManager, "Skin Manager", true);
		string[] names = SkinNames;
		if (!showSkinManager || names.Length == 0) return;
		string currentName = names[Mathf.Clamp(skinIndex, 0, names.Length - 1)];
		if (string.IsNullOrEmpty(skinManagerName)) skinManagerName = currentName;
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		skinManagerName = EditorGUILayout.TextField("Name", skinManagerName);
		EditorGUILayout.BeginHorizontal();
		GUI.enabled = skinManagerName != currentName && IsValidUniqueName(skinManagerName, names);
		if (GUILayout.Button("Rename")) RenameSkin(currentName, skinManagerName);
		GUI.enabled = IsValidUniqueName(skinManagerName, names);
		if (GUILayout.Button("Duplicate")) DuplicateSkin(currentName, skinManagerName);
		GUI.enabled = names.Length > 1;
		if (GUILayout.Button("Delete")) DeleteSkin(currentName);
		GUI.enabled = true;
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.LabelField("Rename updates linked-skin references in the JSON and loaded Spine components using this asset.", EditorStyles.wordWrappedMiniLabel);
		EditorGUILayout.EndVertical();
	}
	static bool IsValidUniqueName (string name, IEnumerable<string> existing) => !string.IsNullOrWhiteSpace(name) && !existing.Contains(name);
	void RenameAnimation (string oldName, string newName) {
		RecordUndo(); object animation = Animations[oldName]; Animations.Remove(oldName); Animations[newName] = animation;
		UpdateAnimationUnityReferences(oldName, newName); animationIndex = Array.IndexOf(AnimationNames, newName); animationManagerName = newName; Save(false);
	}
	void DuplicateAnimation (string sourceName, string newName) {
		RecordUndo(); Animations[newName] = CloneJson(Animations[sourceName]); animationIndex = Array.IndexOf(AnimationNames, newName); animationManagerName = newName; Save(false);
	}
	void DeleteAnimation (string name) {
		if (!EditorUtility.DisplayDialog("Delete Animation", $"Delete animation '{name}' from the Spine JSON?", "Delete", "Cancel")) return;
		RecordUndo(); Animations.Remove(name); animationIndex = Mathf.Clamp(animationIndex, 0, Animations.Count - 1); animationManagerName = ""; selectedEventIndex = -1; Save(false);
	}
	void RenameSkin (string oldName, string newName) {
		RecordUndo();
		Dictionary<string, object> skin = FindSkinJson(oldName);
		skin["name"] = newName;
		ReplaceJsonStringProperty(Root, "skin", oldName, newName);
		RenameAnimationAttachmentSkinKeys(oldName, newName);
		UpdateLoadedSkinReferences(oldName, newName);
		skinManagerName = newName;
		Save(false);
		skinIndex = Array.IndexOf(SkinNames, newName);
	}
	void DuplicateSkin (string sourceName, string newName) {
		RecordUndo(); Dictionary<string, object> copy = (Dictionary<string, object>)CloneJson(FindSkinJson(sourceName)); copy["name"] = newName; SkinJsonList.Add(copy); skinManagerName = newName; Save(false); skinIndex = Array.IndexOf(SkinNames, newName);
	}
	void DeleteSkin (string name) {
		if (!EditorUtility.DisplayDialog("Delete Skin", $"Delete skin '{name}' from the Spine JSON? Linked meshes referencing it may become invalid.", "Delete", "Cancel")) return;
		RecordUndo(); Dictionary<string, object> skin = FindSkinJson(name); SkinJsonList.Remove(skin); skinIndex = Mathf.Clamp(skinIndex, 0, SkinJsonList.Count - 1); skinManagerName = ""; Save(false);
	}
	List<object> SkinJsonList => Root.TryGetValue("skins", out object skins) ? (List<object>)skins : new List<object>();
	Dictionary<string, object> FindSkinJson (string name) => SkinJsonList.Cast<Dictionary<string, object>>().First(skin => Text(skin, "name") == name);
	static object CloneJson (object value) => Spine.Json.Deserialize(new StringReader(Encode(value)));
	static void ReplaceJsonStringProperty (object node, string property, string oldValue, string newValue) {
		if (node is IDictionary<string, object> dictionary) {
			if (dictionary.TryGetValue(property, out object value) && value is string text && text == oldValue) dictionary[property] = newValue;
			foreach (object child in dictionary.Values.ToList()) ReplaceJsonStringProperty(child, property, oldValue, newValue);
		} else if (node is IList list) foreach (object child in list) ReplaceJsonStringProperty(child, property, oldValue, newValue);
	}
	void RenameAnimationAttachmentSkinKeys (string oldName, string newName) {
		foreach (object value in Animations.Values) {
			Dictionary<string, object> animation = (Dictionary<string, object>)value;
			RenameDictionaryKey(animation, "attachments", oldName, newName);
			// Spine 4.1 and older exports used "deform" or "ffd" at animation root.
			RenameDictionaryKey(animation, "deform", oldName, newName);
			RenameDictionaryKey(animation, "ffd", oldName, newName);
		}
	}
	static void RenameDictionaryKey (Dictionary<string, object> owner, string collectionName, string oldName, string newName) {
		if (!owner.TryGetValue(collectionName, out object raw) || !(raw is Dictionary<string, object> collection) || !collection.TryGetValue(oldName, out object value)) return;
		collection.Remove(oldName); collection[newName] = value;
	}
	void UpdateAnimationUnityReferences (string oldName, string newName) {
		for (int i = 0; i < skeletonDataAsset.fromAnimation.Length; i++) if (skeletonDataAsset.fromAnimation[i] == oldName) skeletonDataAsset.fromAnimation[i] = newName;
		for (int i = 0; i < skeletonDataAsset.toAnimation.Length; i++) if (skeletonDataAsset.toAnimation[i] == oldName) skeletonDataAsset.toAnimation[i] = newName;
		EditorUtility.SetDirty(skeletonDataAsset);
		foreach (string guid in AssetDatabase.FindAssets("t:AnimationReferenceAsset")) {
			AnimationReferenceAsset reference = AssetDatabase.LoadAssetAtPath<AnimationReferenceAsset>(AssetDatabase.GUIDToAssetPath(guid));
			if (reference == null || reference.SkeletonDataAsset != skeletonDataAsset) continue;
			SerializedObject serialized = new SerializedObject(reference);
			SerializedProperty name = serialized.FindProperty("animationName");
			if (name != null && name.stringValue == oldName) { name.stringValue = newName; serialized.ApplyModifiedPropertiesWithoutUndo(); reference.Clear(); EditorUtility.SetDirty(reference); }
		}
		AssetDatabase.SaveAssets();
	}
	void UpdateLoadedSkinReferences (string oldName, string newName) {
		foreach (SkeletonRenderer renderer in Resources.FindObjectsOfTypeAll<SkeletonRenderer>()) {
			if (renderer.SkeletonDataAsset != skeletonDataAsset || renderer.initialSkinName != oldName) continue;
			UnityEditor.Undo.RecordObject(renderer, "Rename Spine Skin Reference"); renderer.initialSkinName = newName; EditorUtility.SetDirty(renderer);
		}
		foreach (SkeletonGraphic graphic in Resources.FindObjectsOfTypeAll<SkeletonGraphic>()) {
			if (graphic.SkeletonDataAsset != skeletonDataAsset || graphic.initialSkinName != oldName) continue;
			UnityEditor.Undo.RecordObject(graphic, "Rename Spine Skin Reference"); graphic.initialSkinName = newName; EditorUtility.SetDirty(graphic);
		}
	}
	void SetSkeletonDataAsset (SkeletonDataAsset asset, bool clearProjectSelection = true) {
		skeletonDataAsset = asset;
		if (clearProjectSelection) { projectSelection.Clear(); projectSelectionIndex = 0; }
		animationIndex = 0; skinIndex = 0; animationManagerName = ""; skinManagerName = ""; previewTime = 0; selectedEventIndex = -1; sourceRoot = null; undoSnapshots.Clear(); redoSnapshots.Clear(); RebuildPreview();
	}
	void LoadProjectSelection () {
		List<SkeletonDataAsset> found = new List<SkeletonDataAsset>();
		foreach (UnityEngine.Object selected in Selection.objects) {
			if (selected is SkeletonDataAsset dataAsset) { found.Add(dataAsset); continue; }
			if (selected is TextAsset textAsset) {
				foreach (string guid in AssetDatabase.FindAssets("t:SkeletonDataAsset")) {
					SkeletonDataAsset candidate = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(guid));
					if (candidate != null && candidate.skeletonJSON == textAsset) found.Add(candidate);
				}
			}
		}
		found = found.Where(asset => asset != null).Distinct().OrderBy(asset => AssetDatabase.GetAssetPath(asset)).ToList();
		if (found.Count == 0) { ShowNotification(new GUIContent("Selection contains no usable Spine JSON/SkeletonDataAsset.")); return; }
		projectSelection.Clear(); projectSelection.AddRange(found); projectSelectionIndex = 0; SetSkeletonDataAsset(projectSelection[0], false);
	}
	bool IsJsonSource => skeletonDataAsset != null && skeletonDataAsset.skeletonJSON != null && !skeletonDataAsset.skeletonJSON.name.ToLowerInvariant().Contains(".skel");
	string[] AnimationNames => Root == null ? Array.Empty<string>() : Animations.Keys.ToArray();
	string[] SkinNames { get { SkeletonData data = skeletonDataAsset == null ? null : skeletonDataAsset.GetSkeletonData(true); return data == null ? Array.Empty<string>() : data.Skins.Items.Take(data.Skins.Count).Select(skin => skin.Name).ToArray(); } }
	float Duration { get { if (skeletonDataAsset == null || AnimationNames.Length == 0) return 0; SkeletonData data = skeletonDataAsset.GetSkeletonData(true); Spine.Animation animation = data == null ? null : data.FindAnimation(AnimationNames[animationIndex]); return animation == null ? 0 : animation.Duration; } }
	Dictionary<string, object> Root { get { if (sourceRoot != null) return sourceRoot; try { sourceRoot = Spine.Json.Deserialize(new StringReader(skeletonDataAsset.skeletonJSON.text)) as Dictionary<string, object>; } catch { sourceRoot = null; } return sourceRoot; } }
	Dictionary<string, object> Animations => (Dictionary<string, object>)Root["animations"];
	List<object> RawEventKeys => GetRawEventKeys(false);
	List<object> GetRawEventKeys (bool create) {
		Dictionary<string, object> animation = (Dictionary<string, object>)Animations[AnimationNames[animationIndex]];
		if (animation.TryGetValue("events", out object raw)) return (List<object>)raw;
		if (!create) return new List<object>();
		List<object> events = new List<object>(); animation["events"] = events; return events;
	}
	List<Dictionary<string, object>> EventKeys => RawEventKeys.Cast<Dictionary<string, object>>().ToList();
	void EnsureDefinition (string name) { Dictionary<string, object> root = Root; if (!root.TryGetValue("events", out object raw)) { raw = new Dictionary<string, object>(); root["events"] = raw; } Dictionary<string, object> definitions = (Dictionary<string, object>)raw; if (!definitions.ContainsKey(name)) definitions[name] = new Dictionary<string, object>(); }
	static float Number (Dictionary<string, object> map, string key, float fallback = 0) => map.TryGetValue(key, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : fallback;
	static string Text (Dictionary<string, object> map, string key) => map.TryGetValue(key, out object value) ? value?.ToString() ?? "" : "";

	void RecordUndo () { undoSnapshots.Push(Encode(Root)); redoSnapshots.Clear(); }
	void Undo () { if (undoSnapshots.Count == 0) return; redoSnapshots.Push(Encode(Root)); Restore(undoSnapshots.Pop()); }
	void Redo () { if (redoSnapshots.Count == 0) return; undoSnapshots.Push(Encode(Root)); Restore(redoSnapshots.Pop()); }
	void Restore (string json) { string path = AssetDatabase.GetAssetPath(skeletonDataAsset.skeletonJSON); File.WriteAllText(path, json, new UTF8Encoding(false)); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); skeletonDataAsset.Clear(); sourceRoot = null; selectedEventIndex = -1; RebuildPreview(); Repaint(); }
	void Save (bool recordUndo = false) { if (recordUndo) RecordUndo(); RemoveEmptyEventTimelines(); string path = AssetDatabase.GetAssetPath(skeletonDataAsset.skeletonJSON); File.WriteAllText(path, Encode(Root), new UTF8Encoding(false)); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); skeletonDataAsset.Clear(); sourceRoot = null; RebuildPreview(); Repaint(); }
	void RemoveEmptyEventTimelines () { foreach (object value in Animations.Values) { Dictionary<string, object> animation = (Dictionary<string, object>)value; if (animation.TryGetValue("events", out object events) && events is IList list && list.Count == 0) animation.Remove("events"); } }
	void RebuildPreview () {
		DisposePreview(); if (skeletonDataAsset == null || !IsJsonSource) return;
		preview = new PreviewRenderUtility(); preview.camera.orthographic = true; preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 1000f; preview.camera.transform.position = new Vector3(0, 0, -10); preview.camera.transform.rotation = Quaternion.identity;
		try {
			previewObject = new GameObject("Spine Event Preview");
			SkeletonAnimation animation = previewObject.AddComponent<SkeletonAnimation>();
			animation.skeletonDataAsset = skeletonDataAsset;
			// Preview should not emit material-import warnings every time it is rebuilt.
			// The original asset inspector remains responsible for reporting setup issues.
			animation.Initialize(true, true);
			preview.AddSingleGO(previewObject);
			ApplyPreviewPose();
			FramePreviewCamera();
			previewObject.hideFlags = HideFlags.HideAndDontSave;
			UpdatePreviewDiagnostics();
		} catch (Exception exception) {
			previewDiagnostics = "Preview initialization exception: " + exception.GetType().Name + " — " + exception.Message;
			Debug.LogException(exception);
		}
	}
	void ApplyPreviewPose () {
		if (previewObject == null || AnimationNames.Length == 0) return;
		SkeletonAnimation animation = previewObject.GetComponent<SkeletonAnimation>();
		if (!animation.valid) return;
		// Apply the pose directly, avoiding AnimationState mix timing in the editor-only preview.
		// This guarantees slots receive their setup attachments before MeshGenerator runs.
		Skeleton skeleton = animation.Skeleton;
		Skin previewSkin = null;
		string[] skinNames = SkinNames;
		if (skinNames.Length > 0) previewSkin = skeleton.Data.FindSkin(skinNames[Mathf.Clamp(skinIndex, 0, skinNames.Length - 1)]);
		if (previewSkin == null) previewSkin = skeleton.Data.DefaultSkin;
		if (previewSkin == null && skeleton.Data.Skins.Count > 0) previewSkin = skeleton.Data.Skins.Items[0];
		if (previewSkin != null) skeleton.SetSkin(previewSkin);
		skeleton.SetToSetupPose();
		Spine.Animation clip = skeleton.Data.FindAnimation(AnimationNames[animationIndex]);
		clip.Apply(skeleton, -1f, previewTime, false, null, 1f, MixBlend.Replace, MixDirection.In);
		skeleton.UpdateWorldTransform(Skeleton.Physics.Pose);
		animation.LateUpdateMesh();
		FramePreviewCamera();
	}
	void FramePreviewCamera () {
		if (previewObject == null || preview == null) return;
		Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
		if (renderers.Length == 0) { preview.camera.orthographicSize = 4; return; }
		Bounds bounds = renderers[0].bounds;
		for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
		preview.camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
		preview.camera.transform.rotation = Quaternion.identity;
		preview.camera.orthographicSize = Mathf.Max(.1f, bounds.extents.y * 1.25f, bounds.extents.x * .75f);
	}
	void UpdatePreviewDiagnostics () {
		if (previewObject == null) { previewDiagnostics = "No preview GameObject was created."; return; }
		SkeletonAnimation animation = previewObject.GetComponent<SkeletonAnimation>();
		MeshFilter[] filters = previewObject.GetComponentsInChildren<MeshFilter>();
		int meshCount = filters.Count(f => f.sharedMesh != null);
		int vertices = filters.Where(f => f.sharedMesh != null).Sum(f => f.sharedMesh.vertexCount);
		int materials = previewObject.GetComponentsInChildren<MeshRenderer>().Sum(r => r.sharedMaterials == null ? 0 : r.sharedMaterials.Count(m => m != null));
		previewDiagnostics = $"Spine valid: {animation != null && animation.valid} | Meshes: {meshCount}/{filters.Length} | Vertices: {vertices} | Materials: {materials}";
	}
	void LogPreviewDiagnostics () {
		UpdatePreviewDiagnostics();
		StringBuilder report = new StringBuilder("[Spine Asset Studio Preview Diagnostics]\n");
		report.AppendLine(previewDiagnostics);
		report.AppendLine("Preview scene valid: " + (preview != null));
		if (preview != null) report.AppendLine($"Camera: position={preview.camera.transform.position}, rotation={preview.camera.transform.rotation.eulerAngles}, orthoSize={preview.camera.orthographicSize}, near={preview.camera.nearClipPlane}, far={preview.camera.farClipPlane}");
		if (previewObject != null) {
			report.AppendLine("Preview object scene: " + previewObject.scene.name + " (loaded=" + previewObject.scene.isLoaded + ")");
			foreach (MeshFilter filter in previewObject.GetComponentsInChildren<MeshFilter>()) {
				MeshRenderer renderer = filter.GetComponent<MeshRenderer>(); Mesh mesh = filter.sharedMesh;
				report.AppendLine($"MeshFilter '{filter.name}': mesh={(mesh == null ? "null" : mesh.name)}, vertices={(mesh == null ? 0 : mesh.vertexCount)}, submeshes={(mesh == null ? 0 : mesh.subMeshCount)}, bounds={(mesh == null ? "n/a" : mesh.bounds.ToString())}, materials={(renderer == null || renderer.sharedMaterials == null ? 0 : renderer.sharedMaterials.Length)}");
			}
		}
		Debug.Log(report.ToString());
	}
	void DisposePreview () { if (preview != null) { preview.Cleanup(); preview = null; } previewObject = null; }

	static string Encode (object value) { StringBuilder s = new StringBuilder(); WriteJson(s, value, 0); return s.ToString(); }
	static void WriteJson (StringBuilder s, object value, int depth) {
		if (value is IDictionary dictionary) { s.Append("{"); bool first = true; foreach (DictionaryEntry e in dictionary) { if (!first) s.Append(','); AppendIndent(s, depth + 1); s.Append('"').Append(Escape(e.Key.ToString())).Append("\": "); WriteJson(s, e.Value, depth + 1); first = false; } if (!first) AppendIndent(s, depth); s.Append('}'); return; }
		if (value is IList list) { s.Append('['); for (int i = 0; i < list.Count; i++) { if (i > 0) s.Append(','); AppendIndent(s, depth + 1); WriteJson(s, list[i], depth + 1); } if (list.Count > 0) AppendIndent(s, depth); s.Append(']'); return; }
		if (value is string text) { s.Append('"').Append(Escape(text)).Append('"'); return; }
		if (value is bool boolean) { s.Append(boolean ? "true" : "false"); return; }
		if (value == null) { s.Append("null"); return; }
		s.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
	}
	static void AppendIndent (StringBuilder s, int depth) { s.Append('\n'); for (int i = 0; i < depth; i++) s.Append('\t'); }
	static string Escape (string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
#else
/// <summary>Shown while the optional spine-unity dependency is unavailable.</summary>
public sealed class SpineAssetStudioWindow : EditorWindow {
	[MenuItem("Spine/Asset Studio")]
	static void Open () => GetWindow<SpineAssetStudioWindow>("Spine Asset Studio");

	void OnGUI () {
		EditorGUILayout.Space(12);
		EditorGUILayout.LabelField("SPINE ASSET STUDIO", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox(
			"Spine Asset Studio is installed, but spine-unity has not been detected. " +
			"Import a compatible spine-unity runtime, then ensure the SPINE_UNITY scripting define is enabled.",
			MessageType.Info);
		if (GUILayout.Button("Detect Spine and Enable SPINE_UNITY")) EnableSpineUnityDefine();
	}

	static void EnableSpineUnityDefine () {
		bool spineDetected = AppDomain.CurrentDomain.GetAssemblies()
			.Any(assembly => assembly.GetType("Spine.Unity.SkeletonDataAsset", false) != null);
		if (!spineDetected) {
			EditorUtility.DisplayDialog(
				"spine-unity not found",
				"Import a compatible spine-unity runtime first. SPINE_UNITY was not added because it would cause the full tool to compile without Spine.",
				"OK");
			return;
		}

		#if UNITY_2021_2_OR_NEWER
		UnityEditor.Build.NamedBuildTarget target = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
		string defines = PlayerSettings.GetScriptingDefineSymbols(target);
		#else
		BuildTargetGroup target = EditorUserBuildSettings.selectedBuildTargetGroup;
		string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
		#endif

		List<string> symbols = defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
		if (!symbols.Contains("SPINE_UNITY")) symbols.Add("SPINE_UNITY");

		#if UNITY_2021_2_OR_NEWER
		PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
		#else
		PlayerSettings.SetScriptingDefineSymbolsForGroup(target, string.Join(";", symbols));
		#endif

		EditorUtility.DisplayDialog("SPINE_UNITY enabled", "The SPINE_UNITY define was added for the current build target. Unity will now reload scripts and enable Spine Asset Studio.", "OK");
	}
}
#endif
