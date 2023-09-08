using UnityEngine;

public class EnableObjects : MonoBehaviour
{
	public GameObject[] gameObjects;
	
	void Start()
	{
		EnableForEach();
	}
	
	public void EnableForEach()
	{
		foreach (GameObject item in gameObjects)
		{
			if (item) item.SetActive(true);
		}
	}
}
