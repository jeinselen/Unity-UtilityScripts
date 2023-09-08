// Script source: forum.unity.com/threads/apply-changes-to-prefab-keyboard-shortcut.29251/#post-2538088
using UnityEngine;
using UnityEditor;

public class ApplyPrefabChanges : MonoBehaviour
{
	// % = command
	// & = option
	// # = shift
	// all of the simple combinations for "p" are already used
	[MenuItem("Tools/Apply Prefab Changes &s")]
	static public void applyPrefabChanges()
	{
		var obj = Selection.activeGameObject;
		if(obj!=null) {
			var prefab_root = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
			var prefab_src = PrefabUtility.GetCorrespondingObjectFromSource(prefab_root);
			if (prefab_src!=null) {
				// docs.unity3d.com/ScriptReference/PrefabUtility.ApplyPrefabInstance.html
				PrefabUtility.ApplyPrefabInstance(prefab_root, InteractionMode.UserAction);
				Debug.Log("Updating prefab: "+AssetDatabase.GetAssetPath(prefab_src));
			} else {
				Debug.Log("Selected has no prefab");
			}
		} else {
			Debug.Log("Nothing selected");
		}
	}
}
