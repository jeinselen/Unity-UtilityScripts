using UnityEngine;
using DG.Tweening;

// Requires DoTween

public class AnimateColor : MonoBehaviour
{
	public string targetName;
	public float duration = 4f;
	private GameObject[] objects;
	
	void Start()
	{
		objects = GameObject.FindGameObjectsWithTag(targetName);
//		print(objects.Length + " elements found with the tag \"" + targetName + "\"");
	}
	
	public void Animate(string hex)
	{
		Color targetColor;
		if (ColorUtility.TryParseHtmlString(hex, out targetColor))
		{
			foreach (GameObject obj in objects)
			{
				Material material = obj.GetComponent<Renderer>().material;
				material.DOColor(targetColor, "_BaseColor", duration).SetEase(Ease.Linear);
			}
		}
		else
		{
			print("color conversion failed");
		}
	}
}
