using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

[ExecuteInEditMode]
public class SceneResolution : MonoBehaviour
{
	public int ResolutionPresetIndex;
	
	void Start()
	{
		SetResolution(ResolutionPresetIndex);
	}
	
	void SetResolution(int index)
	{
		Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
		PropertyInfo selectedSizeIndex = gameView.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		EditorWindow window = EditorWindow.GetWindow(gameView);
		selectedSizeIndex.SetValue(window, index, null);
	}
	
	// More advanced option would be to search for a matching string, which would have some possibility of working on multiple systems
	// https://forum.unity.com/threads/how-to-change-game-window-resoltuion-width-height-in-editor-mode-programmatically.1193257/
}

// Resources:
// https://forum.unity.com/threads/add-game-view-resolution-programatically-old-solution-doesnt-work.860563/
