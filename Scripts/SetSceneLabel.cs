using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class SetSceneLabel : MonoBehaviour
{

	// [SerializeField]
	// private TextMeshPro textMeshPro;

	void Start()
	{
		// This conflicts with Kirk's SceneManager script
		// Scene scene = SceneManager.GetActiveScene();
		// Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
		// textMeshPro.text = scene.name;
		// Debug.Log("Active Scene is '" + scene.name + "'.");
		transform.GetComponent<TextMeshPro>().text = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
	}
}