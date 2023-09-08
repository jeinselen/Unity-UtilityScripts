using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// REQUIRES InspectorButton.cs
// https://www.reddit.com/r/Unity3D/comments/1s6czv/inspectorbutton_add_a_custom_button_to_your/

public class DisplayWebCamUI : MonoBehaviour
{
	// Print list of web cameras to the console
	[InspectorButton("PrintWebcamList")]
	public bool PrintList;
	
	// Name of the camera to use
	public string CameraName = "FaceTime";
	
	// UI image to be replaced
	[SerializeField]
	private RawImage _rawImage;
	
	// Index of the camera
	private int Webcam = 0;
	
	private void PrintWebcamList()
	{
		WebCamDevice[] devices = WebCamTexture.devices;
		for (int i = 0; i < devices.Length; i++)
		{
			print("Webcam " + i + ":   " + devices[i].name);
		}
	}
	
	void Start()
	{
		WebCamDevice[] devices = WebCamTexture.devices;
		
		for (int i = 0; i < devices.Length; i++)
		{
			if (devices[i].name.Contains(CameraName))
			{
				Webcam = i;
				break; // Found the web cam so let's leave the loop.
			}
			else
			{
				print("Webcam name not found");
			}
		}
		
		// Out of range safety net
		if (Webcam >= devices.Length)
		{
			Webcam = 0;
			print("Webcam index reset to zero");
		}
		
		WebCamTexture tex = new WebCamTexture(devices[Webcam].name);
		
		RawImage m_RawImage;
		m_RawImage = GetComponent<RawImage>();
		m_RawImage.texture = tex;
		tex.Play();
	}
}

// Resources:
// https://stackoverflow.com/questions/19482481/display-live-camera-feed-in-unity
