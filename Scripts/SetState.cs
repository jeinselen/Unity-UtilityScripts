using UnityEngine;
using System.Linq;

/// <summary>
/// StateMachineBehaviour that sets an Animator int parameter (default "Scene") when the state is entered.
/// Optional: set it every frame while the state is active to guard against other scripts overwriting it.
/// </summary>
public class SetState : StateMachineBehaviour
{
	[Tooltip("Animator int parameter to set.")]
	public string parameterName = "Scene";
	
	[Tooltip("Value to assign to the Animator int parameter when this state is entered.")]
	public int sceneIndex = 0;
	
	[Tooltip("If true, also set the parameter every frame while this state is active.")]
	public bool setEveryFrame = false;
	
	private int _paramHash;
	private bool _paramFound;
	
	// Called when a transition starts and the state machine starts to evaluate this state
	public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		if (animator == null) return;
		
		_paramHash = Animator.StringToHash(parameterName);
		_paramFound = HasIntParameter(animator, _paramHash);
		
		if (!_paramFound)
		{
			Debug.LogWarning($"[SetState] Animator '{animator.name}' does not have an int parameter named '{parameterName}'.", animator);
			return;
		}
		
		int before = animator.GetInteger(_paramHash);
		animator.SetInteger(_paramHash, sceneIndex);
		int after = animator.GetInteger(_paramHash);
//		Debug.Log($"[SetState] {animator.name}: '{parameterName}' {before} -> {after} on enter.", animator);
	}
	
	// Optionally reinforce every frame while the state is playing
	public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		if (!setEveryFrame || animator == null || !_paramFound) return;
		animator.SetInteger(_paramHash, sceneIndex);
	}
	
	private static bool HasIntParameter(Animator animator, int hash)
	{
		foreach (var p in animator.parameters)
		{
			if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Int)
			return true;
		}
		return false;
	}
}
