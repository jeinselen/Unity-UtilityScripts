using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections;

public class SpatialUITrigger : MonoBehaviour
{
	[Tooltip("Name of the Animator trigger on Scene Root, e.g. 'Button0'")]
	public string triggerName = "Button0";
	
	[Tooltip("Keyboard key to simulate this button in Editor/visionOS with a keyboard")]
	public Key keyboardKey = Key.Digit1; // KeypadDigit1 also works if you prefer
	
	[SerializeField]
	protected UnityEvent m_PressStart;
	
	[SerializeField]
	protected UnityEvent m_PressEnd;
	
	public UnityEvent PressEndEvent => m_PressEnd;
	
	Animator sceneRootAnimator;
	
	void Awake()
	{
		sceneRootAnimator = GetComponentInParent<Animator>();
	}
	
	void Update()
	{
		if (Keyboard.current != null && Keyboard.current[keyboardKey].wasPressedThisFrame)
		{
//			Debug.Log("Press Start");
			m_PressStart.Invoke();
		}
		if (Keyboard.current != null && Keyboard.current[keyboardKey].wasReleasedThisFrame)
		{
//			Debug.Log("Press Start");
			m_PressEnd.Invoke();
			TriggerEvent();
		}
	}
	
	public virtual void PressStart()
	{
//		Debug.Log("Press Start");
		m_PressStart.Invoke();
	}
	
	public virtual void PressEnd()
	{
//		Debug.Log("Press End");
		m_PressEnd.Invoke();
		TriggerEvent();
	}
	
	public void TriggerEvent()
	{
		if (sceneRootAnimator && !string.IsNullOrEmpty(triggerName))
		{
			sceneRootAnimator.SetTrigger(triggerName);
			Debug.Log("Trigger Animation Event: " + triggerName);
		}
	}
}
