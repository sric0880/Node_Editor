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

[Node (false, "Action Node")]
public class ActionNode : Node 
{
	public const string ID = "actionNode";
	public override string GetID { get { return ID; } }

	[SerializeField]
	public List<UnityFuncBase> actionCommands = null;
	[NonSerialized]
	private GenericMenu functionSelectionMenu;

	public string typeName = "";

	public override Node Create (Vector2 pos) 
	{
		ActionNode node = CreateInstance<ActionNode> ();
		node.rect = new Rect (pos.x, pos.y, 300, 100);
		node.name = "Action Node";
		actionCommands = new List<UnityFuncBase> { new UnityFuncBase () };
		return node;
	}

	protected internal override void NodeGUI () 
	{
		if (actionCommands == null)
			actionCommands = new List<UnityFuncBase> { new UnityFuncBase () };
		if (actionCommands.Count == 0)
			actionCommands.Add (new UnityFuncBase ());
		UnityFuncBase startFunc = actionCommands[0];

		// Function Source selection
		GUILayout.BeginHorizontal ();
		// Target Object
		Object editedTargetObject = RTEditorGUI.ObjectField<Object> (startFunc.TargetObject, false);
		if (startFunc.TargetObject != editedTargetObject)
		{
			startFunc.ReassignTargetObject (editedTargetObject);
			NodeEditor.RepaintClients ();
			functionSelectionMenu = null;
		}
		// if null, choose static type
		if (startFunc.TargetObject == null)
		{ // Type dropdown
			string typeNameEdited = GUILayout.TextField (typeName);
			if (typeName != typeNameEdited) 
			{
				typeName = typeNameEdited;
				Type type = Type.GetType (typeName);
				if (type != null) 
				{
					startFunc.ReassignTargetType (type);
					functionSelectionMenu = null;
				}
			}
		}
		else
			typeName = startFunc.TargetType.Name;
		GUILayout.EndHorizontal ();

		// Function selection
		if (startFunc.TargetType != null && startFunc.TargetType != typeof(void))
		{
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Pick Function"))
			{
				if (functionSelectionMenu == null)
					functionSelectionMenu = FunctionSelector.BuildCommandExecutionSelection (startFunc.TargetType, BindingFlags.Public | (startFunc.isInstanceMethod? BindingFlags.Instance : BindingFlags.Static), TransformSelectedFunction, 3);
				functionSelectionMenu.ShowAsContext ();
			}
			GUILayout.EndHorizontal ();
		}
	}

	private void TransformSelectedFunction (object selectorData) 
	{
		if (selectorData == null)
			throw new UnityException ("Function selection is null!");
		List<FunctionSelector.Command> commands = selectorData as List<FunctionSelector.Command>;
		if (commands == null || commands.Count == 0)
			throw new UnityException ("Invalid function selection " + selectorData.ToString () + "!");

		// TODO: Transform command list to UnityFunc list (actionCommands)
		// So the command list is serialized. When executing, command after command gets executed and the next command gets called on the result
		// Also, need to create inputs/outputs based on first / last command here
	}

	private object ExecuteCommands () 
	{
		// TODO: Execute the commands of the ActionNode
		// Need to retarget each command with the result of the previous one
		// return the final return value, and if necessary pass the input values as parameter to the last command
		return null;
	}

	public override bool Calculate () 
	{
		if (actionCommands != null && actionCommands.Count != 0)
		{
			if (!allInputsReady ())
				return false;
			Outputs[0].SetValue (ExecuteCommands ());
		}
		return true;
	}
}
