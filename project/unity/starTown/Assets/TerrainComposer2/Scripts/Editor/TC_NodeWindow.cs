﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace TerrainComposer2
{
    public class TC_NodeWindow : EditorWindow
    {
        static public TC_NodeWindow window;
        static Event eventCurrent; 

        bool onFocus;
        
        TC_TerrainLayer terrainLayer;
                
        [MenuItem("Window/Power of Nature Software/TerrainComposer2")]
        public static void ShowWindow()
        {
            window = GetWindow(typeof(TC_NodeWindow)) as TC_NodeWindow;
            window.titleContent = new GUIContent("TC2");
        }

        void OnEnable()
        {
            autoRepaintOnSceneChange = true;
            TD.scale = 2;
            
            Undo.undoRedoPerformed += UndoRedoPerformed;

            // Debug.Log("Node Window OnEnable");
            TC.RefreshOutputReferences(6);
        }

        void OnFocus()
        {
            onFocus = true;
        }

        void OnLostFocus()
        {
            onFocus = false;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoRedoPerformed;
        }

        void OnDestroy()
        {
            Undo.undoRedoPerformed -= UndoRedoPerformed;
        }

        void UndoRedoPerformed()
        {
            // Debug.Log("Perform undo");
            if (TD.SelectionContainsItemBehaviour())
            {
                TC.refreshOutputReferences = TC.allOutput;
                TC.AutoGenerate();
            }
        }

        Vector2 mousePositionOld;

        void OnInspectorUpdate()
        {
            if (!TD.SelectionContainsItemBehaviour()) Repaint();
        }

        void Update()
        {
            if (TC.repaintNodeWindow)
            {
                TC.repaintNodeWindow = false;
                Repaint();
            }
        }

        void CaptureWindowEvents()
        {
            if (!TD.rectWindow.Contains(eventCurrent.mousePosition))
            {
                TD.selectedOpacityItem = null;
                TD.posClickMouseDown = new Vector2(-1000, -1000);
            }

            if (eventCurrent.type == EventType.mouseDown) { TD.posClickMouseDown = eventCurrent.mousePosition; TD.mouseDownButton = eventCurrent.button; }
            else if (eventCurrent.type == EventType.MouseUp) { TD.posClickMouseDown = new Vector2(-1000, -1000); TD.mouseDownButton = -1; }
        }

        bool correctSetup = true;
        TC_Settings settings;

        void OnGUI()
        {
            ShowMessages();

            if (!correctSetup)
            {
                TC.AddMessage("Can't load default project.\nThis file is needed -> TerrainComposer2/Defaults/TerrainComposer2.prefab.\n\n Please try to close and re-open the TerrainComposer window.");
                return;
            }

            if (TC_Settings.instance == null)
            {
                GameObject go = new GameObject();
                go.AddComponent<TC_Settings>();

                TC.GetInstallPath();
                GameObject.DestroyImmediate(go);
            }
            else TC.GetInstallPath();

            if (TC_Settings.instance == null)
            {
                if (!LoadDefault()) { correctSetup = false; return; }
            }
            if (TC_Settings.instance.global == null)
            {
                if (!LoadDefault()) { correctSetup = false; return; }
            }

            if (!TD.Init()) return;

            settings = TC_Settings.instance;
            TC_Reporter.BenchmarkStart();

            TD.countDrawNode = TD.countDrawNodeCulled = 0;

            TD.hoverItem = null;

            TD.rectWindow = new Rect(0, 0, Screen.width, Screen.height);

            settings.selectionOld = Selection.activeTransform;

            if (terrainLayer == null) GetLayerLevel();

            Keys();
            ScrollInterface();

            TD.eventCurrent = eventCurrent;

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), TD.texShelfBackground1);

            settings.HasMasterTerrain();

            TD.showSelectRect = false;

            CaptureWindowEvents();
             
            TD.scrollMax = Vector2.zero;

            TC_TerrainLayerGUI.Draw(terrainLayer);
            DrawCommand.DrawCommandLists();

            TC_ItemBehaviourEditor.CheckKeyLockOnSelection(eventCurrent);

            TD.DrawCenter(Color.red, 21, 1);
            TD.DrawCenter(Color.red, 11, 3);
             
            DrawMenu();

            DropDownMenuMain();

            if (settings.showFps)
            {
                if (!EditorGUIUtility.isProSkin)
                {
                    GUI.color = new Color(1, 1, 1, 0.5f);
                    GUI.DrawTexture(new Rect(180, 0, 250, 17), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                string fps = TC_Reporter.BenchmarkStop("| fps ", false);
                EditorGUI.LabelField(new Rect(180, 0, 250, 17), "Node Draw " + TD.countDrawNode + "| Nodes Culled " + TD.countDrawNodeCulled + fps);
            }

            if (onFocus) Repaint();
        }

        void ShowMessages()
        {
            if (TC.messages.Count > 0)
            {
                string message = "";
                bool first = true;
                for (int i = 0; i < TC.messages.Count; i++)
                {
                    TC.MessageCommand messageCommand = TC.messages[i];
                    if (Time.realtimeSinceStartup > messageCommand.startTime + messageCommand.delay)
                    {
                        if (!first) message += "\n\n"; else first = false;
                        message += messageCommand.message;
                        if (Time.realtimeSinceStartup > messageCommand.startTime + messageCommand.duration) { TC.messages.RemoveAt(i); --i; }
                    }
                }
                if (message.Length != 0) ShowNotification(new GUIContent(message));
            }
        }

        static public string test3 = "Unlocked";
        void DrawProgressbar()
        {
            if (TC_Area2D.current != null)
            {
                if (TC_Area2D.current.showProgressBar)
                    EditorGUI.ProgressBar(new Rect(0, Screen.height - 40, Screen.width - 82, 20), TC_Area2D.current.progress, TC_Area2D.current.currentTerrain.name + "    " + (TC_Area2D.current.progress * 100).ToString("F1") + "%");
            }
        }

        static public void DeleteKey()
        {
            if (Selection.transforms != null)
            {
                for (int i = 0; i < Selection.transforms.Length; i++)
                {
                    Transform t = Selection.transforms[i];

                    TC_LayerGroup layerGroup = t.GetComponent<TC_LayerGroup>();
                    TC_LayerGroupResult groupResult = t.GetComponent<TC_LayerGroupResult>();
                    TC_SelectItemGroup selectItemGroup = t.GetComponent<TC_SelectItemGroup>();
                    TC_NodeGroup nodeGroup = t.GetComponent<TC_NodeGroup>();
                    TC_Node node = t.GetComponent<TC_Node>();
                    
                    if (node != null)
                    {
                        if (node.nodeType == NodeGroupType.Select)
                        {
                            TC_NodeGroup nodeGroupParent = (TC_NodeGroup)node.parentItem;
                            if (nodeGroupParent.itemList.Count == 1 && node.outputId != TC.heightOutput) { TC.AddMessage("Select node cannot be deleted as there is always minimum one needed in a Layer."); continue; }
                        }
                    }
                    else if (layerGroup != null)
                    {
                        if (layerGroup.level == 0) { TC.AddMessage("The main " + TC.outputNames[layerGroup.outputId] + " Output Layer Group cannot be deleted. \n\nRight click to clear it."); continue; }
                    }
                    else if (groupResult != null) { TC.AddMessage("A Result Node cannot be deleted. The Layer Group itself needs to be deleted."); continue; }
                    else if (selectItemGroup != null) { selectItemGroup.Clear(true); continue; }
                    else if (nodeGroup != null) { nodeGroup.Clear(true); continue; }
                    
                    Undo.DestroyObjectImmediate(Selection.gameObjects[i]);
                    --i;
                }
            }
        }

        static public void DuplicateKey()
        {
            Transform[] transforms = (Transform[])Selection.transforms.Clone();
            
            if (transforms != null)
            {
                List<GameObject> gos = new List<GameObject>();

                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];

                    TC_LayerGroup layerGroup = t.GetComponent<TC_LayerGroup>();
                    TC_LayerGroupResult groupResult = t.GetComponent<TC_LayerGroupResult>();
                    TC_SelectItemGroup selectItemGroup = t.GetComponent<TC_SelectItemGroup>();
                    TC_NodeGroup nodeGroup = t.GetComponent<TC_NodeGroup>();

                    if (layerGroup != null)
                    {
                        if (layerGroup.level == 0) { TC.AddMessage("The main " + TC.outputNames[layerGroup.outputId] + " Output Layer Group cannot be duplicated."); continue; }
                    }
                    else if (groupResult != null) { TC.AddMessage("A Result Node cannot be duplicated."); continue; }
                    else if (selectItemGroup != null) { TC.AddMessage("A " + TC.outputNames[selectItemGroup.outputId] + " Group cannot be directly duplicated.\n\nDrag & drop works with it, while dropping hold the alt key to duplicate it."); continue; }
                    else if (nodeGroup != null) { TC.AddMessage("A " + nodeGroup.type.ToString() + " Group cannot be directly duplicated.\n\nDrag & drop works with it, while dropping hold the alt key to duplicate it."); continue; }

                    TC_ItemBehaviour item = t.GetComponent<TC_ItemBehaviour>();
                    if (item != null) gos.Add(item.Duplicate(item.transform.parent).gameObject);
                }
                if (gos.Count > 0) Selection.objects = gos.ToArray();
            }
        }

        static public bool keyDown;

        static public void Keys()
        {
            eventCurrent = Event.current;

            if (eventCurrent.type == EventType.keyUp)
            {
                if (eventCurrent.keyCode == KeyCode.D && eventCurrent.control) DuplicateKey();
                if (eventCurrent.keyCode == KeyCode.Delete) DeleteKey();
                keyDown = false;
            }
            else if (eventCurrent.type == EventType.keyDown)
            { 
                if (eventCurrent.control && eventCurrent.shift)
                {
                    if (eventCurrent.keyCode == KeyCode.D) TC_Settings.instance.drawDefaultInspector = !TC_Settings.instance.drawDefaultInspector;
                    else if (eventCurrent.keyCode == KeyCode.H)
                    {
                        TC_Settings.instance.debugMode = !TC_Settings.instance.debugMode;
                        DebugMode();
                    }
                }
            }
        }

        void ScrollInterface()
        {
            TC_GlobalSettings g = TC_Settings.instance.global;

            TD.scrollOffset = settings.scrollOffset + settings.scrollAdd;
            TD.scale = settings.scale;

            if (TD.scrollOffset.x < 0) TD.scrollOffset.x = 0; 
            else if (TD.scrollOffset.x > TD.scrollMax.x + 1000 && TD.scrollMax.x != 0) TD.scrollOffset.x = TD.scrollMax.x + 1000;

            if (TD.scrollOffset.y > 0) TD.scrollOffset.y = 0;
            else if (TD.scrollOffset.y < TD.scrollMax.y && TD.scrollMax.y != 0) TD.scrollOffset.y = TD.scrollMax.y;
            // Vector2 deltaMouse = key.mousePosition-mousePositionOld;

            bool doScroll = false;
            if (eventCurrent.alt && eventCurrent.control && eventCurrent.type == EventType.MouseDrag && eventCurrent.button == 0) doScroll = true;
            else if (eventCurrent.type == EventType.MouseDrag && eventCurrent.button == 2) doScroll = true;
            
            if (doScroll)
            {
                TD.scrollOffset += (eventCurrent.delta / TD.scale) / TD.mouseSensivity;
                TC.repaintNodeWindow = true;
                eventCurrent.Use();
            }

            if (eventCurrent.alt && eventCurrent.control && eventCurrent.type == EventType.mouseDown) eventCurrent.Use();
            
            // Debug.Log(TD.scrollOffset.y + " " + TD.scrollMax.y);
            
            Vector2 deltaMouse = eventCurrent.mousePosition - new Vector2(Screen.width / 2, Screen.height / 2);

            if (eventCurrent.type == EventType.ScrollWheel)
            {
                if (eventCurrent.delta.y > 0)
                {
                    // TD.scrollOffset -= deltaMouse / (TD.scale);
                    TD.scale /= 1 + (eventCurrent.delta.y / (20f * TD.mouseScrollWheelSensivity));
                    // TD.scrollOffset += deltaMouse / (TD.scale);
                }
                else
                {
                    TD.scrollOffset -= deltaMouse / (TD.scale);
                    TD.scale *= 1 + (-eventCurrent.delta.y / (20f * TD.mouseScrollWheelSensivity));
                    TD.scrollOffset += deltaMouse / (TD.scale);
                }
                
                if (TD.scale > 2.4f) TD.scale = 2.4f;
                else if (TD.scale < 0.05f) TD.scale = 0.05f;

                TC.repaintNodeWindow = true;
            }

            if (eventCurrent.type == EventType.keyDown)
            {
                if (eventCurrent.keyCode == g.keyZoomIn) TD.scale *= 1.5f;
                else if (eventCurrent.keyCode == g.keyZoomOut) TD.scale /= 1.5f;
            }
            
            if (eventCurrent.button == 2 && eventCurrent.clickCount == 2) TD.scale = 1;
            
            settings.scrollOffset = TD.scrollOffset - settings.scrollAdd;  
            settings.scale = TD.scale;
            // mousePositionOld = key.mousePosition;
            // if (TC_Settings.instance.global.scrollOffset.x * (TD.scale * 1.5f) < 680) TC_Settings.instance.global.scrollOffset.x = 680 / (TD.scale * 1.5f);
            // Debug.Log(GlobalManager.singleton.scrollOffset.x * (TD.scale * 1));
        }

        void DrawMenu()
        {
            TC_Settings settings = TC_Settings.instance;
            float width = 55;

            EditorGUILayout.BeginHorizontal();
            if (!TC_Settings.instance.hideMenuBar)
            {
                GUILayout.Space(2);
                if (GUILayout.Button("File", EditorStyles.miniButtonMid, GUILayout.Width(width)))
                {
                    GenericMenu menu = new GenericMenu();
                    DrawFile(menu, false);
                    menu.DropDown(new Rect(1, 17, 1, 1));
                }
                if (GUILayout.Button("Options", EditorStyles.miniButtonMid, GUILayout.Width(width)))
                {
                    GenericMenu menu = new GenericMenu();
                    DrawOptions(menu, false);
                    menu.DropDown(new Rect(1 + width, 17, 1, 1));
                }
                if (GUILayout.Button("Help", EditorStyles.miniButtonMid, GUILayout.Width(width)))
                {
                    GenericMenu menu = new GenericMenu();
                    DrawHelp(menu, false);
                    menu.DropDown(new Rect(1 + width * 2, 17, 1, 1));
                }

                GUILayout.Space(Screen.width - 540 - (width + 5));
            }
            else GUILayout.Space(Screen.width - 536 - (width + 5));

            GUI.changed = false;
            float labelWidthOld = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 45;
            settings.seed = EditorGUILayout.FloatField("Seed", settings.seed, GUILayout.Width(100));
            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
                TC.AutoGenerate();
            }
            EditorGUIUtility.labelWidth = labelWidthOld;

            for (int i = 0; i < 6; i++)
            {
                TC_LayerGroup layerGroup = terrainLayer.layerGroups[i];
                if (layerGroup == null) break;
                if (layerGroup.visible)
                {
                    if (layerGroup.active) GUI.backgroundColor = Color.green; else GUI.backgroundColor = Color.red;
                }
                if (GUILayout.Button(TC.outputNames[i][0].ToString(), EditorStyles.miniButtonMid, GUILayout.Width(25)))
                {
                    if (eventCurrent.control)
                    {
                        settings.scrollOffset = -layerGroup.nodePos;
                    }
                    else TD.ClickOutputButton(layerGroup);
                }
                GUI.backgroundColor = Color.white;
            }
            GUILayout.Space(5);

            if (GUILayout.Button("Generate", EditorStyles.miniButtonMid, GUILayout.Width(width + 5))) ClickMenuMain("Generate");
            if (TC_Generate.instance.autoGenerate) GUI.backgroundColor = Color.green;
            else if (TC.autoGenerateCallTimeStart + 0.05f > Time.realtimeSinceStartup) GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Auto", EditorStyles.miniButtonMid, GUILayout.Width(width))) ClickMenuMain("Auto Generate");
            GUI.backgroundColor = Color.white;
            
            if (GUILayout.Button("Refresh", EditorStyles.miniButtonMid, GUILayout.Width(width))) ClickMenuMain("Refresh");

            EditorGUILayout.EndHorizontal();
        }

        void DropDownMenuMain()
        {
            float x = (Screen.width / 2) + TD.scrollOffset.x * TD.scale;
            float width = Screen.width - x;
            
            if (TD.ClickRect(new Rect(x, 0, width, Screen.height)) != 1) return;
            
            GenericMenu menu = new GenericMenu();

            DrawFile(menu, true);
            menu.AddSeparator("");
            DrawOptions(menu, true);
            DrawHelp(menu, true);

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Auto Generate"), TC_Generate.instance.autoGenerate, ClickMenuMain, "Auto Generate");
            menu.AddItem(new GUIContent("Refresh"), false, ClickMenuMain, "Refresh");
            menu.AddItem(new GUIContent("Reset Textures"), false, ClickMenuMain, "ResetTextures");

            menu.ShowAsContext();
        }

        void DrawFile(GenericMenu menu, bool showPath)
        {
            string path = showPath ? "File/" : "";
            menu.AddItem(new GUIContent(path + "New"), false, ClickMenuMain, "New");
            menu.AddSeparator(path);
            menu.AddItem(new GUIContent(path + "Open"), false, ClickMenuMain, "Open");
            menu.AddItem(new GUIContent(path + "Save"), false, ClickMenuMain, "Save");
        }

        void DrawOptions(GenericMenu menu, bool showPath)
        {
            string path = showPath ? "Options/" : "";
            menu.AddItem(new GUIContent(path + "Show Window Fps"), TC_Settings.instance.showFps, ClickMenuMain, "Show Fps");
            menu.AddItem(new GUIContent(path + "Hide Menu bar"), TC_Settings.instance.hideMenuBar, ClickMenuMain, "Hide Menu Bar");
            menu.AddSeparator(path);
            menu.AddItem(new GUIContent(path + "Settings"), false, ClickMenuMain, "Settings");
        }

        void DrawHelp(GenericMenu menu, bool showPath)
        {
            string path = showPath ? "Help/" : "";
            menu.AddItem(new GUIContent(path + "Documentation"), TC_Settings.instance.showFps, ClickMenuMain, "Documentation");
            menu.AddSeparator("Help/");
            menu.AddItem(new GUIContent(path + "About..."), false, ClickMenuMain, "About...");
        }

        void New()
        {
            if (EditorUtility.DisplayDialog("New TerrainComposer Project", "Are you sure you want to start a new TerrainComposer project?", "Yes", "Cancel"))
            {
                TC_Area2D.current.terrainLayer.New(true);
            }
        }

        void ClickMenuMain(object obj)
        {
            string cmd = obj.ToString();

            if (cmd == "New") New();
            if (cmd == "Open") Open();
            if (cmd == "Save") Save();
            else if (cmd == "Settings") Selection.activeTransform = TC_Settings.instance.transform;
            else if (cmd == "Generate") TC_Generate.instance.Generate(false);
            else if (cmd == "Auto Generate") TC_Generate.instance.autoGenerate = !TC_Generate.instance.autoGenerate;
            else if (cmd == "Show Fps") TC_Settings.instance.showFps = !TC_Settings.instance.showFps;
            else if (cmd == "Hide Menu Bar")
            {
                TC_Settings.instance.hideMenuBar = !TC_Settings.instance.hideMenuBar;
                if (!TC_Settings.instance.hideMenuBar) TC.AddMessage("The menu bar can be unhided by the popup menu, this can be shown by right clicking in the empty area where the Height/Splat/Color... buttons are.");
            }
            else if (cmd == "Refresh")
            {
                TC_Generate.instance.RefreshOutputReferences(6, true); 
                if (TC_Generate.instance.autoGenerate) TC_Generate.instance.Generate(false);
            }
            else if (cmd == "ResetTextures") TC.refreshOutputReferences = 7;
            else if (cmd == "Documentation") Application.OpenURL("http://www.terraincomposer.com/terraincomposer2-documentation/");
            else if (cmd == "About...") TC.AddMessage("TerrainComposer 2 Beta 7d", 0, 4);
        }

        void Open()
        {
            TC_Settings settings = TC_Settings.instance;

            string folder = settings.lastPath;
            if (!folder.Contains(Application.dataPath)) folder = "";
            if (folder == "") folder = Application.dataPath + TC.installPath.Replace("Assets","") + "/Examples/Presets/TerrainLayer";

            string path = EditorUtility.OpenFilePanel("Open TerrainComposer2 project", folder, "prefab");
            if (path.Length != 0)
            {
                settings.lastPath = TC.GetPath(path);

                string filePath = TC.GetAssetDatabasePath(path);

                if (TC.FileExists(filePath))
                {
                    GameObject go = Instantiate(AssetDatabase.LoadAssetAtPath(filePath, typeof(GameObject))) as GameObject;
                    if (go != null)
                    {
                        go.transform.hideFlags = HideFlags.HideInInspector;
                        TC_TerrainLayer terrainLayer = go.GetComponent<TC_TerrainLayer>();
                        if (terrainLayer != null)
                        {
                            go.transform.parent = TC_Generate.instance.transform.parent;
                            DestroyImmediate(TC_Area2D.current.terrainLayer.gameObject);
                            TC_Area2D.current.terrainLayer = terrainLayer;
                            DebugMode();
                            TC.AddMessage(TC.GetFileName(path) + " is loaded succesfully.");
                        }
                        else
                        {
                            TC.AddMessage("This is not a TerrainLayer prefab.");
                            DestroyImmediate(go);
                        }
                    }
                }
                else TC.AddMessage("Can't find path.");
            }
        }

        void Save()
        {
            TC_Settings settings = TC_Settings.instance;

            string folder = settings.lastPath;
            if (!folder.Contains(Application.dataPath)) folder = "";
            if (folder == "") folder = Application.dataPath + (TC.installPath.Replace("/Assets", "/")) + "/Examples/Presets/TerrainLayer";
            
            string path = EditorUtility.SaveFilePanel("Save TerrainComposer2 project", folder, "TerrainLayer", "prefab");
            if (path.Length != 0)
            {
                settings.lastPath = TC.GetPath(path);

                string filePath = TC.GetAssetDatabasePath(path); 

                if (filePath != "")
                {
                    PrefabUtility.CreatePrefab(filePath, TC_Area2D.current.terrainLayer.gameObject); ;
                }
                else TC.AddMessage("Can't find path.");
            }
        }

        public void GetLayerLevel()
        {
            // GameObject go = GameObject.Find("LayerLevel");
            // if (go != null) layerLevel = go.GetComponent<TCLayerLevel>(); else Debug.Log("Cant find layerLevelGroup"); 
            terrainLayer = TC_Area2D.current.terrainLayer;
            // layerLevel = Area2D.current.layerLevel;
        }

        static public void DebugMode()
        {
            TC_Settings settings = TC_Settings.instance;

            if (settings.debugMode)
            {
                TC_Reporter.instance.gameObject.hideFlags = HideFlags.None;
                TC_Generate.instance.gameObject.hideFlags = HideFlags.None;
                TC_Area2D.current.terrainLayer.gameObject.hideFlags = HideFlags.None;
            }
            else
            {
                TC_Reporter.instance.gameObject.hideFlags = HideFlags.HideInHierarchy;
                TC_Generate.instance.gameObject.hideFlags = HideFlags.HideInHierarchy;
                if (settings.hideTerrainGroup) TC_Area2D.current.terrainLayer.gameObject.hideFlags = HideFlags.HideInHierarchy;
                else TC_Area2D.current.terrainLayer.gameObject.hideFlags = HideFlags.None;
            }

            EditorApplication.RepaintHierarchyWindow();
        }

        static bool LoadDefault()
        {
            GameObject defaultGo = AssetDatabase.LoadAssetAtPath(TC.installPath + "/Defaults/TerrainComposer2.prefab", typeof(GameObject)) as GameObject;

            if (defaultGo != null)
            {
                GameObject go = Instantiate(defaultGo, Vector3.zero, Quaternion.identity) as GameObject;
                
                if (go == null) return false;
                else
                {
                    go.transform.hideFlags = HideFlags.HideInInspector;
                    go.name = go.name.Replace("(Clone)", "");
                    go.transform.SetAsFirstSibling();
                    DebugMode();

                    if (!TC.LoadGlobalSettings()) return false;

                    Transform generateT = go.transform.Find("Generate");
                    if (generateT != null)
                    {
                        TC_Compute compute = generateT.GetComponent<TC_Compute>();

                        if (compute != null) compute.enabled = true;
                        else return false;
                    }
                    else return false;
                }
            }
            else return false;

            if (GameObject.Find("Terrain Area") == null)
            {
                GameObject terrainAreaGo = AssetDatabase.LoadAssetAtPath(TC.installPath + "/Defaults/Terrain Area.prefab", typeof(GameObject)) as GameObject;
                if (terrainAreaGo != null)
                {
                    GameObject go = Instantiate(terrainAreaGo, Vector3.zero, Quaternion.identity) as GameObject;

                    TC_TerrainLayer terrainLayer = TC_Generate.instance.area2D.terrainLayer;

                    terrainLayer.GetItems(false, true, false);
                    
                    // Detect prefab import issue
                    if (terrainLayer.layerGroups[TC.splatOutput].visible)
                    {
                        TC.AddMessage("TerrainComposer2 prefabs did not import correctly.\n\nYou need to be reimport TerrainComposer2 again (So import it twice).\n\nThis happens if ‘Asset Serialization’ is set to ‘Force Text’ in Unity Menu -> Project Settings -> Editor. It is a Unity import bug that can happen on any Asset. If that does not fix it, the ‘Asset Serialization’ can be set to ‘Mixed’ and then reimport TC2 again, then after importing the ‘Asset Serialization’ can be set to ‘Force Text’ again.", 0, 60);
                    }
                    else if (go == null)
                    {
                        TC.AddMessage("Default Terrain Area prefab is missing. Please reimport this prefab.");
                    }
                    go.name = go.name.Replace("(Clone)", "");
                }
                else TC.AddMessage("Default Terrain Area prefab is missing. Please reimport this prefab.");
            }

            TC.AddMessage("Loading default TerrainComposer project.");

            return true;
        }
    }
}