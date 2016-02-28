using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

using NodeEditorFramework;
using NodeEditorFramework.Utilities;

using Object = UnityEngine.Object;
#if UNITY_EDITOR
using GenericMenu = UnityEditor.GenericMenu;
#endif

[Node (false, "Utility/Action Node")]
public class ActionNode : Node 
{
	public const string ID = "actionNode";
	public override string GetID { get { return ID; } }

	// Action command chain
	[SerializeField]
	private List<UnityFuncBase> actionCommands = null;
	[SerializeField]
	private string actionChainLabel = "No command";

	// Action definition cache
	[NonSerialized]
	private Type targetType;
	[NonSerialized]
	private UnityEngine.Object targetObject;


	// Editor cache
	[NonSerialized]
	private GenericMenu functionSelectionMenu;
	[NonSerialized]
	private GenericMenu typeSelectionMenu;

	public override Node Create (Vector2 pos) 
	{
		ActionNode node = CreateInstance<ActionNode> ();
		node.rect = new Rect (pos.x, pos.y, 300, 200);
		node.name = "Action Node";
		node.CreateInput ("Target Object", typeof(Object).AssemblyQualifiedName);
		actionCommands = new List<UnityFuncBase> ();
		return node;
	}

	protected internal override void NodeGUI () 
	{
		if (actionCommands == null)
			actionCommands = new List<UnityFuncBase> { };
		if (Inputs == null || Inputs.Count < 1)
			setupNodeKnobs ();
		if (targetType == null)
		{ // make sure target type is correct
			UnityFuncBase firstFunc	= actionCommands.Count != 0? actionCommands[actionCommands.Count-1] : null;
			if (firstFunc != null)
				targetType = firstFunc.TargetType;
			else
				targetType = typeof(UnityEngine.Object);
		}

		#region TargetObject

		// Object selection an input field
		UnityEngine.Object selectedTargetObject;
		if (Inputs[0].connection == null)
		{
			selectedTargetObject = RTEditorGUI.ObjectField<UnityEngine.Object> (new GUIContent ("Target Object"), targetObject, NodeEditor.curNodeCanvas.livesInScene);
			Inputs[0].SetPosition ();
		}
		else 
		{
			selectedTargetObject = (Object)Inputs[0].GetValue (typeof(UnityEngine.Object));
			Inputs[0].DisplayLayout ();
		}

		if (selectedTargetObject != targetObject)
		{ // Set the new targetObject
			bool switchedType = false;
			if (targetObject != null)
			{ // Switch to the new type
				switchedType = !targetType.IsAssignableFrom (targetObject.GetType ());
				targetType = targetObject.GetType ();
			}

			if (switchedType) // if the old type is not assignable from the new targetObject, clear the function
				ReassignSelectedFunction (null);

			NodeEditor.RepaintClients ();
		}

		#endregion

		#region Alt: TargetType

		if (targetObject == null)
		{ // If no targetObject is specified, show a type dropdown for static functions
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Select Type"))
			{
				if (typeSelectionMenu == null)
				{ // Create type selection menu if not existant
					typeSelectionMenu = TypeSelector.BuildTypeSelection (ReassignSelectedTargetType);
				}
				typeSelectionMenu.ShowAsContext ();
			}
			GUILayout.Label (targetType == null? "No target type" : targetType.FullName);
			GUILayout.EndHorizontal ();
		}

		#endregion

		#region Function Selection

		if (targetType != null && targetType != typeof(void))
		{
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Select Command"))
			{
				if (functionSelectionMenu == null) 
				{ // Create function selection menuif not existant
					functionSelectionMenu = CommandSelector.BuildCommandExecutionSelection (targetType, 
						BindingFlags.Public | (targetObject != null? BindingFlags.Instance : BindingFlags.Static), 
						ReassignSelectedFunction, 3, false, true);
				}
				functionSelectionMenu.ShowAsContext ();
			}
			GUILayout.Label (actionChainLabel);
			GUILayout.EndHorizontal ();
		}

		#endregion

		#region parameters

		if (actionCommands != null && actionCommands.Count > 0)
		{
			for (int inCnt = 1; inCnt < Inputs.Count; inCnt++)
				Inputs[inCnt].DisplayLayout ();

			for (int outCnt = 0; outCnt < Outputs.Count; outCnt++)
				Outputs[outCnt].DisplayLayout ();
		}

		#endregion

		if (GUI.changed)
			NodeEditor.RecalculateFrom (this);
	}

	/// <summary>
	/// Switches the nodes commands to the passed List of commands. Used as selection menu callback.
	/// </summary>
	/// <param name="selectorData">Selector data (of type List<FunctionSelector.Command>)</param>
	private void ReassignSelectedFunction (object selectorData) 
	{
		if (selectorData == null)
		{
			functionSelectionMenu = null;
			actionChainLabel = "No command";
			actionCommands.Clear ();
			setupNodeKnobs ();
			return;
		}

		List<Command> commands = selectorData as List<Command>;
		if (commands == null || commands.Count == 0)
			throw new UnityException ("Invalid function selection " + selectorData.ToString () + "!");

		actionChainLabel = "";
		actionCommands = new List<UnityFuncBase> (commands.Count);
		for (int comCnt = 0; comCnt < commands.Count; comCnt++) 
		{
			Command command = commands[comCnt];
			actionChainLabel += command.GetRepresentationName () + (comCnt < commands.Count-1? "::" : "");

			UnityFuncBase unityFunc = null;
			if (command.isMethod)
				unityFunc = new UnityFuncBase (null, command.method);
			else
				unityFunc = new UnityFuncBase (null, command.field, true);
			
			actionCommands.Add (unityFunc);
		}

		setupNodeKnobs ();

		NodeEditor.RecalculateFrom (this);

		// TODO: Transform command list to UnityFunc list (actionCommands)
		// So the command list is serialized. When executing, command after command gets executed and the next command gets called on the result
		// Also, need to create inputs/outputs based on first / last command here
	}

	/// <summary>
	/// Switches the nodes target type to the passed one. Used as selection menu callback.
	/// </summary>
	/// <param name="selectorData">Selector data (of type System.Type)</param>
	private void ReassignSelectedTargetType (object selectorData) 
	{
		if (selectorData == null)
			throw new UnityException ("Type selection is null!");
		Type selectedType = selectorData as Type;
		if (selectedType == null)
			throw new UnityException ("Invalid type selection " + selectorData.ToString () + ", it is of type " + selectorData.GetType ().FullName + "!");
		
		if (!targetType.IsAssignableFrom (selectedType))
			ReassignSelectedFunction (null);
		targetType = selectedType;
		NodeEditor.RecalculateFrom (this);
	}

	private void setupNodeKnobs () 
	{
		while (Inputs.Count > 0)
			Inputs[0].Delete ();
		while (Outputs.Count > 0)
			Outputs[0].Delete ();

		NodeEditorCallbacks.IssueOnAddNodeKnob (CreateInput ("Target Object", typeof(Object).AssemblyQualifiedName));

		if (actionCommands == null || actionCommands.Count == 0)
			return;
		
		UnityFuncBase firstFunc = actionCommands[0];
		UnityFuncBase lastFunc = actionCommands[actionCommands.Count-1];

		if (lastFunc.isMethod)
		{
			MethodInfo lastMethod = lastFunc.RuntimeMethod;
			if (lastMethod != null)
			{
				ParameterInfo[] paramInfos = lastMethod.GetParameters ();
				foreach (ParameterInfo param in paramInfos) 
				{
					Type paramType = param.ParameterType;
					if (!param.IsOut)
					{
						if (paramType.IsByRef)
							NodeEditorCallbacks.IssueOnAddNodeKnob (CreateOutput ("ref " + paramType.Name + " " + param.Name, paramType.AssemblyQualifiedName));
						else
							NodeEditorCallbacks.IssueOnAddNodeKnob (CreateInput (paramType.Name + " " + param.Name, paramType.AssemblyQualifiedName));
					}
					else
						NodeEditorCallbacks.IssueOnAddNodeKnob (CreateOutput ("out " + paramType.Name + " " + param.Name, paramType.AssemblyQualifiedName));
					
				}
			}
		}

		if (lastFunc.ReturnType != null && lastFunc.ReturnType != typeof(void))
			NodeEditorCallbacks.IssueOnAddNodeKnob (CreateOutput ("return " + lastFunc.ReturnType.Name, lastFunc.ReturnType.AssemblyQualifiedName));
	}

	private object ExecuteCommands () 
	{
		if (actionCommands == null || actionCommands.Count == 0)
		{
			Debug.LogWarning ("No commands selected for Action Node!");
			return null; // No commands
		}
		UnityFuncBase firstCommand = actionCommands[0];
		if ((targetObject == null) != (firstCommand.isStatic)) 
		{
			Debug.LogWarning ("Could not execute first command " + firstCommand.CommandName + " because of invalid targetObject!");
			return null; // Invalid targetObject
		}

		Debug.Log ("Executing Action node " + firstCommand.CommandName);

		UnityFuncBase lastCommand = actionCommands[actionCommands.Count-1];
		if (Inputs.Count-1 <= lastCommand.ArgumentTypes.Length)
		{ // Check if node knobs were setup correctly
			setupNodeKnobs ();
			if (Inputs.Count-1 < lastCommand.ArgumentTypes.Length)
			{
				Debug.LogError ("Inputs (" + Inputs.Count + "-1) could not be setup correctly according to the argument count(" + lastCommand.ArgumentTypes.Length + ")!");
				return true;
			}
		}

		Type[] argumentTypes = lastCommand.ArgumentTypes;
		object[] lastCommandArguments = new object[argumentTypes.Length];
		for (int argCnt = 0; argCnt < lastCommandArguments.Length; argCnt++)		
		{ // Get the arguments inputted to the node
			Type argType = argumentTypes[argCnt];
			lastCommandArguments[argCnt] = Inputs[argCnt+1].GetValue (argType);
			Debug.Log ("Setting argument " + argType.FullName + " for command " + lastCommand.CommandName);
		}

		Object nextTargetObject = targetObject;
		for (int comCnt = 0; comCnt < actionCommands.Count; comCnt++)
		{
			UnityFuncBase nextCommand = actionCommands[comCnt];
			if ((nextTargetObject == null) != (nextCommand.isStatic))
			{ // Invalid targetObject or result from previous command
				Debug.LogWarning ("Aborted action execution at command " + nextCommand.CommandName + " because of invalid targetObject or result from previous command!");
				break;
			}
			if (comCnt != actionCommands.Count-1 && nextCommand.ArgumentTypes.Length != 0)
			{ // Invalid function in the middle of the chain accepting arguments (-> non-implicit)
				Debug.LogError ("Aborted action execution at command " + nextCommand.CommandName + " because it is not implicit but within the chain!");
				break;
			}
			// Retarget and invoke command
			nextCommand.ReassignTargetObject (nextTargetObject);
			if (comCnt == actionCommands.Count-1 && lastCommandArguments.Length > 0)
				nextTargetObject = (Object)nextCommand.DynamicInvoke (lastCommandArguments);
			else
				nextTargetObject = (Object)nextCommand.DynamicInvoke ();
			Debug.Log ("Invoked method " + nextCommand.CommandName + "!");
		}

		return nextTargetObject;
	}

	public override bool Calculate () 
	{
		targetObject = Inputs[0].GetValue () as Object;
		if (actionCommands != null && actionCommands.Count != 0)
		{
			UnityFuncBase lastCommand = actionCommands[actionCommands.Count-1];
			object result = ExecuteCommands ();
			if (!lastCommand.isStatic)
				Outputs[0].SetValue (result);
		}
		return true;
	}
}
