using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// REQUIRES InspectorButton.cs
// https://www.reddit.com/r/Unity3D/comments/1s6czv/inspectorbutton_add_a_custom_button_to_your/

public class GetMicrophoneInput : MonoBehaviour
{
	// Print list of web cameras to the console
	[InspectorButton("PrintAudioList")]
	public bool PrintList;
	
	public string device = "Scarlett 2i2 USB";
	public int rate = 48000;
	
	private void PrintAudioList()
	{
		foreach (var device in Microphone.devices)
		{
			int min = 0;
			int max = 0;
			Microphone.GetDeviceCaps(device, out min, out max);
			print("Audio Device Name: " + device + " MIN: " + min + " MAX: " + max);
		}
	}

	void Start()
	{
		AudioSource audioSource = GetComponent<AudioSource>();
		audioSource.clip = Microphone.Start(device, true, 10, rate); // 44100 48000 96000
//		audioSource.loop = true;
		audioSource.Play();
	}
}
