using System;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEditor;

using NodeEditorFramework;
using NodeEditorFramework.Utilities;

namespace NodeEditorFramework.Standard
{
	public class NodeEditorWindow : EditorWindow 
	{
		// Information about current instance
		private static NodeEditorWindow _editor;
		public static NodeEditorWindow editor { get { AssureEditor(); return _editor; } }
		public static void AssureEditor() { if (_editor == null) OpenNodeEditor(); }

		// Opened Canvas
		public static NodeEditorUserCache canvasCache;

		// GUI
		private int toolbarHeight = 17;

		public Rect canvasWindowRect { get { return new Rect (0, toolbarHeight, position.width, position.height - toolbarHeight); } }

		#region General 

		/// <summary>
		/// Opens the Node Editor window and loads the last session
		/// </summary>
		[MenuItem("Window/UI Framework")]
		public static NodeEditorWindow OpenNodeEditor () 
		{
			_editor = GetWindow<NodeEditorWindow>();
			_editor.minSize = new Vector2(800, 600);
			NodeEditor.ReInit (false);

			Texture iconTexture = ResourceManager.LoadTexture (EditorGUIUtility.isProSkin? "Textures/Icon_Dark.png" : "Textures/Icon_Light.png");
			_editor.titleContent = new GUIContent ("Node Editor", iconTexture);

			return _editor;
		}
		
		[UnityEditor.Callbacks.OnOpenAsset(1)]
		private static bool AutoOpenCanvas(int instanceID, int line)
		{
			if (Selection.activeObject != null && Selection.activeObject is NodeCanvas)
			{
				string NodeCanvasPath = AssetDatabase.GetAssetPath(instanceID);
				NodeEditorWindow.OpenNodeEditor();
				canvasCache.LoadNodeCanvas(NodeCanvasPath);
				return true;
			}
			return false;
		}

		private void OnEnable()
		{            
			_editor = this;
			NodeEditor.checkInit(false);

			NodeEditor.ClientRepaints -= Repaint;
			NodeEditor.ClientRepaints += Repaint;

			EditorLoadingControl.justLeftPlayMode -= NormalReInit;
			EditorLoadingControl.justLeftPlayMode += NormalReInit;
			// Here, both justLeftPlayMode and justOpenedNewScene have to act because of timing
			EditorLoadingControl.justOpenedNewScene -= NormalReInit;
			EditorLoadingControl.justOpenedNewScene += NormalReInit;

			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			SceneView.onSceneGUIDelegate += OnSceneGUI;

			// Setup Cache
			//canvasCache = new NodeEditorUserCache(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject (this))));
			canvasCache = new NodeEditorUserCache();
			canvasCache.SetupCacheEvents();
		}

	    private void NormalReInit()
		{
			NodeEditor.ReInit(false);
		}

		private void OnDestroy()
		{
			EditorUtility.SetDirty(canvasCache.nodeCanvas);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			NodeEditor.ClientRepaints -= Repaint;

			EditorLoadingControl.justLeftPlayMode -= NormalReInit;
			EditorLoadingControl.justOpenedNewScene -= NormalReInit;

			SceneView.onSceneGUIDelegate -= OnSceneGUI;

			// Clear Cache
			canvasCache.ClearCacheEvents ();
		}

        #endregion

        #region GUI

        private void OnSceneGUI(SceneView sceneview)
        {
            DrawSceneGUI();
        }

	    private void DrawSceneGUI()
	    {
			if (canvasCache.editorState != null && canvasCache.editorState.selectedNode != null)
				canvasCache.editorState.selectedNode.OnSceneGUI();
            SceneView.lastActiveSceneView.Repaint();
        }

        private void OnGUI()
		{            
			// Initiation
			NodeEditor.checkInit(true);
			if (NodeEditor.InitiationError)
			{
				GUILayout.Label("Node Editor Initiation failed! Check console for more information!");
				return;
			}
			AssureEditor ();
			canvasCache.AssureCanvas ();

			// Specify the Canvas rect in the EditorState
			canvasCache.editorState.canvasRect = canvasWindowRect;
			// If you want to use GetRect:
//			Rect canvasRect = GUILayoutUtility.GetRect (600, 600);
//			if (Event.current.type != EventType.Layout)
//				mainEditorState.canvasRect = canvasRect;
			NodeEditorGUI.StartNodeGUI ();

			DrawMenuToolbar();

			// Perform drawing with error-handling
			try
			{
				NodeEditor.DrawCanvas (canvasCache.nodeCanvas, canvasCache.editorState);
			}
			catch (UnityException e)
			{ // on exceptions in drawing flush the canvas to avoid locking the ui.
				canvasCache.NewNodeCanvas ();
				NodeEditor.ReInit (true);
				Debug.LogError ("Unloaded Canvas due to an exception during the drawing phase!");
				Debug.LogException (e);
			}

			DrawCanvasTitle();

			NodeEditorGUI.EndNodeGUI();
		}

		private void DrawCanvasTitle()
		{
			string _title = canvasCache.nodeCanvas.name + "(" + canvasCache.openedCanvasPath + ")";
			var content = new GUIContent(_title);
			var size = EditorStyles.whiteLargeLabel.CalcSize(content);
			Rect titleRect = new Rect(10, 20, size.x + 10, size.y + 10);
			GUI.BeginGroup(titleRect, EditorStyles.textArea);
			titleRect.x = 5;
			titleRect.y = 5;
			GUI.Label (titleRect, content, EditorStyles.whiteLargeLabel);
			GUI.EndGroup();
		}

		private void DrawMenuToolbar()
		{
			GUILayout.BeginHorizontal();

			//			EditorGUILayout.ObjectField ("Loaded Canvas", canvasCache.nodeCanvas, typeof(NodeCanvas), false);
			//			EditorGUILayout.ObjectField ("Loaded State", canvasCache.editorState, typeof(NodeEditorState), false);
			GUILayout.Space(6);

			if (GUILayout.Button(new GUIContent("New Canvas", "Loads an Specified Empty CanvasType"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				canvasCache.NewNodeCanvas();
			}

			if (GUILayout.Button(new GUIContent("Load Canvas", "Loads the Canvas from a Canvas Save File in the Assets Folder"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				string path = EditorUtility.OpenFilePanel("Load Node Canvas", NodeEditor.editorPath + "Resources/Saves/", "asset");
				if (!path.Contains(Application.dataPath))
				{
					if (!string.IsNullOrEmpty(path))
						ShowNotification(new GUIContent("You should select an asset inside your project folder!"));
				}
				else
					canvasCache.LoadNodeCanvas(path);
			}

			if (GUILayout.Button(new GUIContent("Save Canvas", "Saves the Canvas to a Canvas Save File in the Assets Folder"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				string path = canvasCache.openedCanvasPath;
				if (string.IsNullOrEmpty(path))
				{
					path = EditorUtility.SaveFilePanelInProject("Save Node Canvas", "Node Canvas", "asset", "", NodeEditor.editorPath + "Resources/Saves/");
				}
				if (!string.IsNullOrEmpty (path))
					canvasCache.SaveNodeCanvas (path);
			}

			GUILayout.Space(6);

			if (GUILayout.Button(new GUIContent("Export Lua", "Export canvas data to lua userdata file for runtime game use"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
			{
				//
			}

			GUILayout.FlexibleSpace();

			//if (GUILayout.Button (new GUIContent ("Recalculate All", "Initiates complete recalculate. Usually does not need to be triggered manually."), EditorStyles.toolbarButton))
			//	NodeEditor.RecalculateAll (canvasCache.nodeCanvas);

			//if (GUILayout.Button ("Force Re-Init", EditorStyles.toolbarButton))
			//	NodeEditor.ReInit (true);

			//NodeEditorGUI.knobSize = EditorGUILayout.IntSlider (new GUIContent ("Handle Size", "The size of the Node Input/Output handles"), NodeEditorGUI.knobSize, 12, 20);
			canvasCache.editorState.zoom = GUILayout.HorizontalSlider(canvasCache.editorState.zoom, 0.6f, 2f, GUILayout.MinWidth(100));

			if (canvasCache.editorState.selectedNode != null && Event.current.type != EventType.Ignore)
				canvasCache.editorState.selectedNode.DrawNodePropertyEditor();

			GUILayout.EndHorizontal();
		}

		#endregion
	}
}