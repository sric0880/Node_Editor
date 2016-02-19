using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NodeEditorFramework.Utilities
{
	#if UNITY_EDITOR
	using GenericMenuRepresentation = UnityEditor.GenericMenu;
	using MenuCallbackData = UnityEditor.GenericMenu.MenuFunction2;
	#else
	using GenericMenuRepresentation = NodeEditorFramework.Utilities.GenericMenu;
	using MenuCallbackData = NodeEditorFramework.Utilities.PopupMenu.MenuFunctionData;
	#endif

	public static class FunctionSelector 
	{
		private static List<ObjectCommands> cachedCommands = new List<ObjectCommands> ();

		/// <summary>
		/// Builds a GenericMenu with a hierarchial command selection of the specified type and bindingFlags.
		/// executionSelectionCallback is the contextMenu callback that will receive a list of all commands that should be executed one after another
		/// </summary>
		/// <param name="objectType">The type to show all the commands for.</param>
		/// <param name="bindingFlags">The BindingFlag criteria for the commands of the objectType to account for.</param>
		/// <param name="executionSelectionCallback">The menu item callback receiving the selected command list to execute the commands.</param>
		/// <param name="levels">How much levels to represent. When this value reaches 0, no more extra layers get added.</param>
		public static GenericMenuRepresentation BuildCommandExecutionSelection (Type objectType, BindingFlags bindingFlags, MenuCallbackData executionSelectionCallback, int levels) 
		{
			GenericMenuRepresentation menu = new GenericMenuRepresentation ();
			FillCommandExectutionSelectionMenu (menu, "", new List<Command> (), objectType, bindingFlags, executionSelectionCallback, levels);
			return menu;
		}

		/// <summary>
		/// Fills a layer in the GenericMenu at the passedPath with all commands on the passed objectType and creates sub paths
		/// </summary>
		/// <param name="pathString">The path to the layer of the GenericMenu which will be filled with the commands.</param>
		/// <param name="parentCommands">A list of commands that will be executed before this. Should be parallel to the path.</param>
		/// <param name="objectType">The type for which all the commands are searched and filled. SHould be the return type of the last command.</param>
		/// <param name="bindingFlags">The BindingFlag criteria for the commands of the objectType to account for.</param>
		/// <param name="executionSelectionCallback">The menu item callback receiving the selected command list to execute.</param>
		/// <param name="levels">How much levels to still go down, When this value reaches 0, no more extralayers get added.</param>
		private static void FillCommandExectutionSelectionMenu (GenericMenuRepresentation menu, string pathString, List<Command> parentCommands, Type objectType, BindingFlags bindingFlags, MenuCallbackData executionSelectionCallback, int levels) 
		{
			if (levels < 1)
				return;
			// Fetch all commands on this type
			ObjectCommands objCommands = cachedCommands.Find ((ObjectCommands objCmds) => objCmds.type == objectType && objCmds.flags == bindingFlags);
			if (objCommands == null)
			{
				objCommands = new ObjectCommands (objectType, bindingFlags);
				cachedCommands.Add (objCommands);
			}
			// Iterate through the commands and add them to the menuItem
			foreach (Command command in objCommands.commands) 
			{
				// Create new command list
				List<Command> allCommands = new List<Command> (parentCommands);
				allCommands.Add (command);
				// Add the entry to the genericMenu and pass the existing command list
				string itemString = pathString + command.GetRepresentationName ();
				menu.AddItem (new GUIContent (itemString), true, executionSelectionCallback, allCommands);
				// If this command is implicit (without parameters) and we should go further down, add an extra slection layer
				if (command.isImplicitCall && levels > 1)
					FillCommandExectutionSelectionMenu (menu, itemString + "/", allCommands, command.returnType, bindingFlags, executionSelectionCallback, levels-1);
			}
		}

//		/// <summary>
//		/// Generates the exectution item function.
//		/// </summary>
//		/// <returns>The exectution item function.</returns>
//		/// <param name="allCommands">All commands.</param>
//		private static Func<object, object[], object> GenerateExectutionItemFunction (List<Command> allCommands) 
//		{
//			return (object obj, object[] finalParams) => 
//			{
//				for (int comCnt = 0; comCnt < allCommands.Count; comCnt++) 
//				{
//					Command command = allCommands[comCnt];
//					if (command.isImplicitCall)
//						obj = command.InvokeImplicit (obj);
//					else
//					{
//						if (comCnt < allCommands.Count-1)
//							throw new UnityException ("Cannot implicitly call methods with parameters!");
//						else
//							obj = command.Invoke (obj, finalParams); 
//					}
//				}
//				return obj;
//			};
//		}

		/// <summary>
		/// Holds commands (Fields and methods) of a type
		/// </summary>
		public class ObjectCommands
		{
			public Type type { get; private set; }
			public BindingFlags flags { get; private set; }
			public List<Command> commands { get; private set; }

			public ObjectCommands (Type objectType, BindingFlags bindingFlags) 
			{
				type = objectType;
				flags = bindingFlags;

				commands = new List<Command> ();

				MethodInfo[] objectMethods = objectType.GetMethods (flags);
				foreach (MethodInfo method in objectMethods) 
				{
					commands.Add (new Command (objectType, method, flags));
				}

				FieldInfo[] objectFields = objectType.GetFields (flags);
				foreach (FieldInfo field in objectFields) 
				{
					commands.Add (new Command (objectType, field, flags));
				}

				Debug.Log ("---------------------- Commands for type: " + type.Name);
//				foreach (Command command in commands)
//					Debug.Log (command.GetRepresentationName ());
			}
		}

		/// <summary>
		/// Represents any command (field or method) on baseType matching with flags. 
		/// Implicit means it needs no parameters or is a field. This helps for the creation of a command selection hierarchy.
		/// Provides functions to create a hierarchy of implicitly callable commands.
		/// </summary>
		public struct Command
		{
			private MethodInfo method;
			private FieldInfo field;

			public Type baseType { get; private set; }
			public BindingFlags flags { get; private set; }
			public bool isMethod { get; private set; }
			public bool isImplicitCall { get { return (!isMethod || method.GetParameters ().Length == 0) && returnType != typeof(void); } }
			public Type returnType { get { return isMethod? method.ReturnType : field.FieldType; } }

			public Command (Type objectType, MethodInfo commandMethod, BindingFlags bindingFlags) 
			{
				baseType = objectType;
				flags = bindingFlags;
				method = commandMethod;
				field = null;
				isMethod = true;
			}

			public Command (Type objectType, FieldInfo commandField, BindingFlags bindingFlags) 
			{
				baseType = objectType;
				flags = bindingFlags;
				field = commandField;
				method = null;
				isMethod = false;
			}

			/// <summary>
			/// Returns the child commands of the return type of this command
			/// </summary>
			public ObjectCommands GetChildCommands () 
			{
				if (!isImplicitCall)
					throw new UnityException ("Cannot implicitly call methods with parameters!");
				return new ObjectCommands (returnType, flags);
			}

			/// <summary>
			/// Invokes this implicit command and returns the result. If it is not implicit, it'll throw an error.
			/// </summary>
			public object InvokeImplicit (object instanceObject) 
			{
				if (!isImplicitCall) 
					throw new UnityException ("Cannot implicitly call methods with parameters!");
				return isMethod? method.Invoke (instanceObject, new object[0]) : field.GetValue (instanceObject);
			}

			/// <summary>
			/// Invokes this command with the given parameters and returns the result. If it is implicit, it'll throw an error.
			/// </summary>
			public object Invoke (object targetObject, object[] parameter) 
			{
				if (isImplicitCall) 
					throw new UnityException ("Cannot call implicit commands with parameters!");
				return method.Invoke (targetObject, parameter);
			}

			/// <summary>
			/// Gets the name of the Command with the type prefixed
			/// </summary>
			public string GetRepresentationName () 
			{
				string name = isMethod? method.Name : field.Name;
				if (isMethod && (name.StartsWith ("get_") || name.StartsWith ("set_")))
					name = name.Substring (4);
				return returnType.Name + " " + name;
			}
		}
	}
}
