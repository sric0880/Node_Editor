using UnityEngine;
using System;
using System.Collections.Generic;
using NodeEditorFramework.Utilities;

namespace NodeEditorFramework 
{
	/// <summary>
	/// Node output accepts multiple connections to NodeInputs by default
	/// </summary>
	public class NodeOutput : NodeKnob
	{
		// NodeKnob Members
		protected override NodeSide defaultSide { get { return NodeSide.Right; } }
		private static GUIStyle _defaultStyle;
		protected override GUIStyle defaultLabelStyle { get { if (_defaultStyle == null) { _defaultStyle = new GUIStyle (GUI.skin.label); _defaultStyle.alignment = TextAnchor.MiddleRight; } return _defaultStyle; } }

		// NodeInput Members
		public List<NodeInput> connections = new List<NodeInput> ();
		public string typeID;
		private TypeData _typeData;
		internal TypeData typeData { get { CheckType (); return _typeData; } }
		[System.NonSerialized]
		private object value = null;

		#region General

		/// <summary>
		/// Creates a new NodeOutput in NodeBody of specified type
		/// </summary>
		public static NodeOutput Create (Node nodeBody, string outputName, string outputType) 
		{
			return Create (nodeBody, outputName, outputType, NodeSide.Right, 20);
		}

		/// <summary>
		/// Creates a new NodeOutput in NodeBody of specified type
		/// </summary>
		public static NodeOutput Create (Node nodeBody, string outputName, string outputType, NodeSide nodeSide) 
		{
			return Create (nodeBody, outputName, outputType, nodeSide, 20);
		}

		/// <summary>
		/// Creates a new NodeOutput in NodeBody of specified type at the specified Node Side
		/// </summary>
		public static NodeOutput Create (Node nodeBody, string outputName, string outputType, NodeSide nodeSide, float sidePosition) 
		{
			NodeOutput output = CreateInstance <NodeOutput> ();
			output.typeID = outputType;
			output.InitBase (nodeBody, nodeSide, sidePosition, outputName);
			nodeBody.Outputs.Add (output);
			return output;
		}

		public override void Delete () 
		{
			while (connections.Count > 0)
				connections[0].RemoveConnection ();
			body.Outputs.Remove (this);
			base.Delete ();
		}

		#endregion

		#region Additional Serialization

		protected internal override void CopyScriptableObjects (System.Func<ScriptableObject, ScriptableObject> replaceSerializableObject) 
		{
			for (int conCnt = 0; conCnt < connections.Count; conCnt++) 
				connections[conCnt] = replaceSerializableObject.Invoke (connections[conCnt]) as NodeInput;
		}

		#endregion

		#region KnobType

		protected override void ReloadTexture () 
		{
			CheckType ();
			knobTexture = typeData.OutputKnob;
		}

		private void CheckType () 
		{
			if (_typeData == null || !_typeData.isValid ()) 
				_typeData = ConnectionTypes.GetTypeData (typeID);
		}

		#endregion

		#region Value
		
		public bool IsValueNull { get { return value == null; } }

		/// <summary>
		/// Gets the output value anonymously. Not advised as it may lead to unwanted behaviour!
		/// </summary>
		public object GetValue ()
		{
			return value;
		}

		/// <summary>
		/// Gets the output value if the type matches or null. If possible, use strongly typed version instead.
		/// </summary>
		public object GetValue (Type type)
		{
			if (type == null)
				throw new UnityException ("Trying to get value of " + name + " with null type!");
			CheckType ();
			if (type.IsAssignableFrom (typeData.Type))
				return value;
			Debug.LogError ("Trying to GetValue<" + type.FullName + "> for Output Type: " + typeData.Type.FullName);
			return null;
		}

		/// <summary>
		/// Sets the output value if the type matches. If possible, use strongly typed version instead.
		/// </summary>
		public void SetValue (object Value)
		{
			CheckType ();
			if (Value == null || typeData.Type.IsAssignableFrom (Value.GetType ()))
				value = Value;
			else
				Debug.LogError ("Trying to SetValue of type " + Value.GetType ().FullName + " for Output Type: " + typeData.Type.FullName);
		}

		/// <summary>
		/// Gets the output value if the type matches
		/// </summary>
		/// <returns>Value, if null default(T) (-> For reference types, null. For value types, default value)</returns>
		public T GetValue<T> ()
		{
			CheckType ();
			if (typeof(T).IsAssignableFrom (typeData.Type))
				return (T)(value?? (value = TypeSelector.GetDefault<T> ()));
			Debug.LogError ("Trying to GetValue<" + typeof(T).FullName + "> for Output Type: " + typeData.Type.FullName);
			return TypeSelector.GetDefault<T> ();
		}
		
		/// <summary>
		/// Sets the output value if the type matches
		/// </summary>
		public void SetValue<T> (T Value)
		{
			CheckType ();
			if (typeData.Type.IsAssignableFrom (typeof(T)))
				value = Value;
			else
				Debug.LogError ("Trying to SetValue<" + typeof(T).FullName + "> for Output Type: " + typeData.Type.FullName);
		}
		
		/// <summary>
		/// Resets the output value to null.
		/// </summary>
		public void ResetValue () 
		{
			value = null;
		}
		


		#endregion
	}
}