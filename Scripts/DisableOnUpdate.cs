using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableOnUpdate : MonoBehaviour
{
	public GameObject item;
	bool active = false;
	
	void Update()
	{
		if (active)
		{
			item.SetActive(false);
			active = false;
		}
		else if (item.activeSelf)
		{
			active = true;
		}
	}
}
