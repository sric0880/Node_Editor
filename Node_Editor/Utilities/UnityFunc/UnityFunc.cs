using UnityEngine;
using UnityEngine.Events;
using System;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

using Object = UnityEngine.Object;

namespace NodeEditorFramework.Utilities
{
	/// <summary>
	/// Base for all UnityFuncs. Includes all serialization stuff of the delegates. Can also be used as an anonymous serialized delegate
	/// </summary>
	[Serializable]
	public class UnityFuncBase
	{
		// Following are the serialized method details
		[SerializeField]
		private FuncSerializedType _targetType = null;
		[SerializeField]
		private Object _targetObject = null;
		[SerializeField]
		private string _methodName;
		[SerializeField]
		private FuncSerializedType _returnType;
		[SerializeField]
		private FuncSerializedType[] _argumentTypes;

		public UnityEventCallState callState = UnityEventCallState.EditorAndRuntime;

		// Accessors
		public bool isDefinitionComplete { get { return !String.IsNullOrEmpty (_methodName); } }
		public bool isInstanceMethod { get { return _targetObject != null; } }
		public Type TargetType { get { return _targetType != null && _targetType.Validate ()? _targetType.GetRuntimeType () : null; } }
		public Object TargetObject { get { return _targetObject; } }
		public string MethodName { get { return _methodName; } }
		public Type ReturnType { get { return _returnType.GetRuntimeType (); }}
		public Type[] ArgumentTypes { get { return _argumentTypes.Select ((FuncSerializedType argType) => argType.GetRuntimeType ()).ToArray (); } }
		public BindingFlags AllFunctionBindingFlags { get { return BindingFlags.Public | BindingFlags.NonPublic | (isInstanceMethod? BindingFlags.Instance : BindingFlags.Static); } }

		// Runtime deserialized data
		[NonSerialized]
		protected Delegate runtimeDelegate;
		[NonSerialized]
		private MethodInfo method;

		/// <summary>
		/// Dynamically invokes the func represented by this UnityFunc.
		/// </summary>
		public object DynamicInvoke (params object[] parameter) 
		{
			if (runtimeDelegate == null)
				runtimeDelegate = CreateDelegate ();
			if (runtimeDelegate != null)
				return runtimeDelegate.DynamicInvoke (parameter);
			return null;
		}

		#region Constructors

		/// <summary>
		/// Creates a serializeable UnityFunc from the passed delegate
		/// </summary>
		public UnityFuncBase (Delegate func) 
		{
			if (func.Method == null)
				throw new ArgumentException ("Func " + func + " is anonymous!");
			if (func.Target != null && !(func.Target is Object))
				throw new ArgumentException ("Target of func " + func + " is not serializeable, it has to inherit from UnityEngine.Object!");

			Type[] argumentTypes = func.Method.GetParameters ().Select ((ParameterInfo param) => param.ParameterType).ToArray ();
			InternalSetup (func.Method.DeclaringType, (Object)func.Target, func.Method.Name, func.Method.ReturnType, argumentTypes);
		}

		/// <summary>
		/// Creates a serializeable UnityFunc from the passed method on the targetObject
		/// </summary>
		public UnityFuncBase (Object targetObject, MethodInfo methodInfo) 
		{
			Type[] argumentTypes = methodInfo.GetParameters ().Select ((ParameterInfo param) => param.ParameterType).ToArray ();
			InternalSetup (methodInfo.DeclaringType, targetObject, methodInfo.Name, methodInfo.ReturnType, argumentTypes);
		}

		/// <summary>
		/// Creates a serializeable UnityFunc from the passed method on the targetType.
		/// When the method with the specified types could not be found, it'll throw.
		/// If targetObject is specified, the method is assumed to be an instance method, else a static function
		/// </summary>
		public UnityFuncBase (Type targetType, Object targetObject, string methodName, Type returnType, Type[] argumentTypes) 
		{
			InternalSetup (targetType, targetObject, methodName, returnType, argumentTypes);
		}

		/// <summary>
		/// Creates an empty serializeable Delegate
		/// </summary>
		public UnityFuncBase () 
		{
			_targetObject = null;
			_methodName = "";
			_returnType = null;
			_argumentTypes = new FuncSerializedType[0];
		}

		/// <summary>
		/// Setup of the UnityFunc. Internal.
		/// </summary>
		private void InternalSetup (Type targetType, Object targetObject, string methodName, Type returnType, Type[] argumentTypes) 
		{
			_targetType = new FuncSerializedType (targetType);
			_targetObject = targetObject;
			_methodName = methodName;
			_returnType = new FuncSerializedType (returnType);

			_argumentTypes = new FuncSerializedType[argumentTypes.Length];
			for (int argCnt = 0; argCnt < argumentTypes.Length; argCnt++)
				_argumentTypes[argCnt] = new FuncSerializedType (argumentTypes[argCnt]);
			
			CreateDelegate ();
		}

		#endregion

		#region Reassignements

		/// <summary>
		/// Reassigns the target object of this UnityFunc. If needed it reassigns the type, too.
		/// It is possible to switch from static to instance and vice-versa this way.
		/// </summary>
		public void ReassignTargetObject (Object newTargetObject) 
		{
			Debug.Log ("Reassigning target object to " + (newTargetObject == null? "null" : newTargetObject.name));
			if (newTargetObject != null)
				ReassignTargetType (newTargetObject.GetType ());

			_targetObject = newTargetObject;
			DeserializeMethod (false);
			if (method == null)
			{
				Debug.Log ("Failed to create method with new target object!");
				_methodName = "";
				_returnType = null;
				_argumentTypes = null;
			}
		}

		/// <summary>
		/// Reassigns the target type of this UnityFunc. Has to be of the same type or assigneable from the type of this UnityFunc.
		/// It is possible to switch from static to instance provided that the method exists.
		/// </summary>
		public void ReassignTargetType (Type newTargetType) 
		{
			Debug.Log ("Reassigning target type to " + newTargetType.Name);
			if (_targetType == null || !_targetType.Validate  () || _targetType.GetRuntimeType ().IsAssignableFrom (newTargetType))
			{ // Have to clear this delegate
				Debug.Log ("Had to wipe method data for new target type!");
				_targetObject = null;
				_methodName = "";
				_returnType = null;
				_argumentTypes = null;
			}
			_targetType = new FuncSerializedType (newTargetType);
		}

		/// <summary>
		/// Reassigns the target type of this UnityFunc. Has to be of the same type or assigneable from the type of this UnityFunc.
		/// It is possible to switch from static to instance provided that the method exists.
		/// </summary>
		public void ReassignMethod (string newMethodName, Type returnType, Type[] argumentTypes) 
		{
			if (_methodName != newMethodName)
			{
				_methodName = newMethodName;
				_returnType = new FuncSerializedType (returnType);

				_argumentTypes = new FuncSerializedType[argumentTypes.Length];
				for (int argCnt = 0; argCnt < argumentTypes.Length; argCnt++)
					_argumentTypes[argCnt] = new FuncSerializedType (argumentTypes[argCnt]);

				DeserializeMethod (false);
				if (method == null)
				{
					_methodName = "";
					_returnType = null;
					_argumentTypes = null;
					throw new UnityException ("Invalid method data on UnityFunc " + _methodName  + "!");
				}
			}
		}

		private bool hasFullMethodData () 
		{
			return !String.IsNullOrEmpty (_methodName) && _returnType != null && _returnType.Validate () &&_argumentTypes != null;
		}

		#endregion

		#region Deserialization

		/// <summary>
		/// Returns the runtime delegate this UnityFunc representates, a System.Func with of apropriate type depending on return and argument types boxed as a delegate.
		/// Handled and stored on initialisation by children func variations.
		/// </summary>
		protected Delegate CreateDelegate ()
		{
			if (callState == UnityEventCallState.Off || (callState == UnityEventCallState.RuntimeOnly && !Application.isPlaying))
				return null;

			// Make sure method is deserialized
			if (method == null)
				DeserializeMethod (true);
			// Create a delegate from the method
			return runtimeDelegate = (isInstanceMethod? Delegate.CreateDelegate (typeof (Func<>), _targetObject, method) : Delegate.CreateDelegate (typeof (Func<>), method));
		}

		/// <summary>
		/// Fetches the methodInfo this UnityFunc representates. Call only once on initialisation!
		/// </summary>
		private void DeserializeMethod (bool throwOnBindFailure)
		{
			if (!hasFullMethodData ())
			{
				if (throwOnBindFailure)
					throw new UnityException ("Invalid method data on UnityFunc " + _methodName  + "!");
				return;
			}
			// Get the argument types
			Type[] runtimeArgumentTypes = new Type[_argumentTypes.Length];
			for (int argCnt = 0; argCnt < _argumentTypes.Length; argCnt++) 
				runtimeArgumentTypes[argCnt] = _argumentTypes[argCnt].GetRuntimeType ();
			// Get the method Info that this UnityFunc representates
			method = GetValidMethodInfo (_targetType.GetRuntimeType (), _methodName, _returnType.GetRuntimeType (), runtimeArgumentTypes, AllFunctionBindingFlags);
			if (method == null && throwOnBindFailure)
				throw new UnityException ("Invalid method data on UnityFunc " + _methodName  + "!");
		}

		/// <summary>
		/// Gets the valid MethodInfo of the method on targetObj called functionName with the specified returnType (may be null incase of void) and argumentTypes
		/// </summary>
		private static MethodInfo GetValidMethodInfo (Type targetType, string methodName, Type returnType, Type[] argumentTypes, BindingFlags flags)
		{
			if (returnType == null) // Account for void return type, too
				returnType = typeof(void);
			while (targetType != null && targetType != typeof(object) && targetType != typeof(void)) 
			{ // Search targetObj's type hierarchy for the functionName until we hit the object base type or found it (incase the function is inherited)
				MethodInfo method = targetType.GetMethod (methodName, flags, null, argumentTypes, null);
				if (method != null && method.ReturnType == returnType) 
				{ // This type contains a method with the specified name, arguments and return type
					ParameterInfo[] parameters = method.GetParameters ();
					bool flag = true;
					for (int paramCnt = 0; paramCnt < parameters.Length; paramCnt++) 
					{ // Check whether the arguments match in that they are primitives (isn't this already sorted out in getMethod?)
						if (!(flag = (argumentTypes [paramCnt].IsPrimitive == parameters [paramCnt].ParameterType.IsPrimitive)))
							break; // Else, this is not the right method
					}
					if (flag) // We found the method!
						return method;
				}
				// Move up in the type hierarchy (function was inherited)
				targetType = targetType.BaseType;
			}
			return null; // No valid method found on targetObj that has this functionName
		}

		#endregion

		#region Serialized Type

		[Serializable]
		private class FuncSerializedType : ISerializationCallbackReceiver
		{
			[SerializeField]
			public string argAssemblyTypeName;

			[NonSerialized]
			private Type runtimeType;

			public FuncSerializedType (Type type)
			{
				Debug.Log ("Creating serializedtype " + type.Name + " with tidied name out of " + type.AssemblyQualifiedName + "!");
				SetType (type);
			}

			public void SetType (Type type)
			{
				runtimeType = type;
				argAssemblyTypeName = type.AssemblyQualifiedName;
				if (String.IsNullOrEmpty (argAssemblyTypeName))
					throw new UnityException ("Could not setup type as it does not contain serializeable data!");
				Debug.Log ("Creating serializedtype " + runtimeType.Name + " with tidied name out of " + runtimeType.AssemblyQualifiedName + "!");
				TidyAssemblyTypeName ();
				Debug.Log ("Created serializedtype " + runtimeType.Name + " with tidied name " + argAssemblyTypeName + " out of " + runtimeType.AssemblyQualifiedName);
			}

			public void OnAfterDeserialize ()
			{
				TidyAssemblyTypeName ();
			}

			public void OnBeforeSerialize ()
			{
				TidyAssemblyTypeName ();
			}

			private void TidyAssemblyTypeName ()
			{
				if (!Validate ())
					return;
				argAssemblyTypeName = argAssemblyTypeName.Split (',')[0];
//				argAssemblyTypeName = Regex.Replace (argAssemblyTypeName, @", Version=\d+.\d+.\d+.\d+", String.Empty);
//				argAssemblyTypeName = Regex.Replace (argAssemblyTypeName, @", Culture=\w+", String.Empty);
//				argAssemblyTypeName = Regex.Replace (argAssemblyTypeName, @", PublicKeyToken=\w+", String.Empty);
			}

			public bool Validate () 
			{
				return !String.IsNullOrEmpty (argAssemblyTypeName);
			}

			public Type GetRuntimeType () 
			{
				if (string.IsNullOrEmpty (argAssemblyTypeName))
					throw new UnityException ("Could not deserialize type as it does not contain serialized data!");
				return runtimeType ?? (runtimeType = Type.GetType (argAssemblyTypeName, false) ?? typeof(Object));
			}
		}

		#endregion
	}

	#region Parameter Variations

	[Serializable]
	public class UnityFunc<T1, T2, T3, T4, TR> : UnityFuncBase
	{
		[NonSerialized]
		private Func<T1, T2, T3, T4, TR> runtimeFunc;

		// Retarget constructors to base
		public UnityFunc (Object targetObject, MethodInfo methodInfo) : base (targetObject, methodInfo) {}
		public UnityFunc (Delegate func) : base (func) {}

		public TR Invoke (T1 arg1, T2 arg2, T3 arg3, T4 arg4) 
		{
			if (runtimeFunc == null)
			{
				if (runtimeDelegate == null)
					runtimeDelegate = CreateDelegate ();
				runtimeFunc = runtimeDelegate as Func<T1, T2, T3, T4, TR>;
			}
			if (runtimeFunc != null)
				return runtimeFunc.Invoke (arg1, arg2, arg3, arg4);
			return default(TR);
		}
	}

	/// <summary>
	/// UnityFunc with three paramteters. Extend this and use that class to make it serializeable
	/// </summary>
	[Serializable]
	public class UnityFunc<T1, T2, T3, TR> : UnityFuncBase
	{
		[NonSerialized]
		private Func<T1, T2, T3, TR> runtimeFunc;

		// Retarget constructors to base
		public UnityFunc (Delegate func) : base (func) {}
		public UnityFunc (Object targetObject, MethodInfo methodInfo) : base (targetObject, methodInfo) {}
		public UnityFunc (Type targetType, Object targetObject, string methodName, Type returnType, Type[] argumentTypes) : base (targetType, targetObject, methodName, returnType, argumentTypes) {}

		public TR Invoke (T1 arg1, T2 arg2, T3 arg3) 
		{
			if (runtimeFunc == null)
			{
				if (runtimeDelegate == null)
					runtimeDelegate = CreateDelegate ();
				runtimeFunc = runtimeDelegate as Func<T1, T2, T3, TR>;
			}
			if (runtimeFunc != null)
				return runtimeFunc.Invoke (arg1, arg2, arg3);
			return default(TR);
		}
	}

	/// <summary>
	/// UnityFunc with two paramteters. Extend this and use that class to make it serializeable
	/// </summary>
	[Serializable]
	public class UnityFunc<T1, T2, TR> : UnityFuncBase
	{
		[NonSerialized]
		private Func<T1, T2, TR> runtimeFunc;

		// Retarget constructors to base
		public UnityFunc (Delegate func) : base (func) {}
		public UnityFunc (Object targetObject, MethodInfo methodInfo) : base (targetObject, methodInfo) {}
		public UnityFunc (Type targetType, Object targetObject, string methodName, Type returnType, Type[] argumentTypes) : base (targetType, targetObject, methodName, returnType, argumentTypes) {}

		public TR Invoke (T1 arg1, T2 arg2) 
		{
			if (runtimeFunc == null)
			{
				if (runtimeDelegate == null)
					runtimeDelegate = CreateDelegate ();
				runtimeFunc = runtimeDelegate as Func<T1, T2, TR>;
			}
			if (runtimeFunc != null)
				return runtimeFunc.Invoke (arg1, arg2);
			return default(TR);
		}
	}

	/// <summary>
	/// UnityFunc with one paramteter. Extend this and use that class to make it serializeable
	/// </summary>
	[Serializable]
	public class UnityFunc<T1, TR> : UnityFuncBase
	{
		[NonSerialized]
		private Func<T1, TR> runtimeFunc;

		// Retarget constructors to base
		public UnityFunc (Delegate func) : base (func) {}
		public UnityFunc (Object targetObject, MethodInfo methodInfo) : base (targetObject, methodInfo) {}
		public UnityFunc (Type targetType, Object targetObject, string methodName, Type returnType, Type[] argumentTypes) : base (targetType, targetObject, methodName, returnType, argumentTypes) {}

		public TR Invoke (T1 arg) 
		{
			if (runtimeFunc == null)
			{
				if (runtimeDelegate == null)
					runtimeDelegate = CreateDelegate ();
				runtimeFunc = runtimeDelegate as Func<T1, TR>;
			}
			if (runtimeFunc != null)
				return runtimeFunc.Invoke (arg);
			return default(TR);
		}
	}

	/// <summary>
	/// UnityFunc with no paramteters. Extend this and use that class to make it serializeable
	/// </summary>
	[Serializable]
	public class UnityFunc<TR> : UnityFuncBase
	{
		[NonSerialized]
		private Func<TR> runtimeFunc;

		// Retarget constructors to base
		public UnityFunc (Delegate func) : base (func) {}
		public UnityFunc (Object targetObject, MethodInfo methodInfo) : base (targetObject, methodInfo) {}
		public UnityFunc (Type targetType, Object targetObject, string methodName, Type returnType, Type[] argumentTypes) : base (targetType, targetObject, methodName, returnType, argumentTypes) {}

		public TR Invoke () 
		{
			if (runtimeFunc == null)
			{
				if (runtimeDelegate == null)
					runtimeDelegate = CreateDelegate ();
				runtimeFunc = runtimeDelegate as Func<TR>;
			}
			if (runtimeFunc != null)
				return runtimeFunc.Invoke ();
			return default(TR);
		}
	}

	#endregion
}