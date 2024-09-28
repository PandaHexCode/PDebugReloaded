using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading;
using System.IO;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using Microsoft.Win32;

namespace PandaHexCode.PDebug{

    public class PDebugReloaded : MonoBehaviour{

        public bool isMonoMod = true; /*If a game is mono or IL2CPP*/

        #region Values only for Mono games (Not IL2CPP)
        public static PDebugReloaded instance = null;
        #endregion

        private string NAME = "PDebugReloaded";

        #region Settings values
        public bool is3D = true;/*This Script uses a other Ray for 3D Games!*/
        private bool logExceptions = false;/*True needs lot more Performance sometimes!*/

        private KeyCode shortcutMainKey = KeyCode.F1;
        private bool onlyUseVarCopy = false /*If true, it always use CopiedVar for copy button*/;
        private bool viewCursor = true;
        #endregion

        #region Window render values
        private bool showMenu = true;
        private bool windowTrans = false;

        public Color backgroundColor = new Color(63f, 63, 116f, 225f); 
        private GUIStyle transStyle = null;
        private bool[] windowRegionEnabled = new bool[7];

        private string[] editValueInput = new string[15] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" };

        private Vector2[] scrollPosition = new Vector2[11];/*For the OnGUI windows*/
        private Rect[] windowRects = new Rect[40];
        #endregion

        #region Target values
        private GameObject targetObject = null;/*Current GameObject in Objects Tab*/
        [System.NonSerialized] public Camera targetCamera = null;/*Camera that calculating the Ray*/
        private UnityEngine.Component targetComponent = null;
        private MethodInfo targetMethod;
        private PropertyInfo targetPropertyInfo;
        private FieldInfo targetFieldInfo;
        private bool isTargetField = false;
        private int targetCopiedIndex = 0;
        #endregion

        private static AppDomain customCodeAppDomain = null;/*For executing code at runtime*/

        private List<LogEntry> logs = new List<LogEntry>();/*For the console window*/

        private void Awake(){
            this.targetCamera = Camera.main;
            Application.logMessageReceived += HandleLog;/*Register Console Log*/

            if (!this.isMonoMod)
                return;

            /*Check if an instance allready exits and if exits destroy this, else create a new gameobject with the instance*/
            if (PDebugReloaded.instance == null){
                if (this.gameObject.name != this.NAME + "-INSTANCE"){
                    GameObject gm = new GameObject(this.NAME + "-INSTANCE");
                    PDebugReloaded.instance = gm.AddComponent<PDebugReloaded>();
                    PDebugReloaded.instance.logExceptions = this.logExceptions;
                    PDebugReloaded.instance.backgroundColor = this.backgroundColor;
#if UNITY_2018_1_OR_NEWER
                    PDebugReloaded.customCodeAppDomain = AppDomain.CreateDomain("CustomCodeAppDomain");
#endif
                    DontDestroyOnLoad(instance);
                    Destroy(this);
                }
            }else if (PDebugReloaded.instance != this)
                Destroy(this);       
        }

        #region Window render main methods
         private void OnGUI(){/*Draw the current window*/
            GUI.backgroundColor = this.backgroundColor;
            if(this.transStyle == null){
                this.transStyle = new GUIStyle(GUI.skin.window);
                this.transStyle.normal.background = MakeStyleTex(1, 1, this.backgroundColor);
            }

            GUI.Label(new Rect(Screen.width - 218, 0, 500, 50), "PDebugReloaded by PandaHexCode");

            if (!this.showMenu)
                return;

            DrawMainButtons();

            if (this.windowRegionEnabled[0])
                    DrawWindow(0, new Rect(10, 40, 300, 200), ConsoleWindow, "Console", 0);
            if (this.windowRegionEnabled[1]) {
                DrawWindow(1, new Rect(10, 40, 380, 160), ObjectsWindow, "GameObjects", 1);

                if (this.objectPosRotScaEditWindowEnable)
                    DrawWindow(2, new Rect(395, 40, 275, 180), ObjectPosRotScaEditWindow, "Edit");

                float objectComponentsWindowsAndOtherHeight = 140;
                if (!this.viewEnumValuesWindowEnable)
                    objectComponentsWindowsAndOtherHeight = 270;

                if (this.objectComponentsWindowEnable)
                    DrawWindow(3, new Rect(10, 205, 380, objectComponentsWindowsAndOtherHeight), ObjectComponentsWindow, "Components");

                if (this.objectComponentValuesWindowEnable != 0){
                    int xPos = 10;
                    if (this.objectComponentsWindowEnable)
                        xPos = 395;

                    if (this.objectComponentValuesWindowEnable == 1)
                        DrawWindow(4, new Rect(xPos, 205, 380, objectComponentsWindowsAndOtherHeight), ObjectComponentValuesWindow, "Values");
                    else{
                        string valueName = string.Empty;
                        if (this.isTargetField)
                            valueName = this.targetFieldInfo.Name;
                        else
                            valueName = this.targetPropertyInfo.Name;

                        DrawWindow(5, new Rect(xPos, 205, 380, objectComponentsWindowsAndOtherHeight), EditValueWindow, "EditValue - " + valueName);
                    }
                }else if (this.objectComponentMethodesWindowEnable != 0){
                    int xPos = 10;
                    if (this.objectComponentsWindowEnable)
                        xPos = 395;

                    if (this.objectComponentMethodesWindowEnable == 1)
                        DrawWindow(6, new Rect(xPos, 205, 380, objectComponentsWindowsAndOtherHeight), ObjectComponentMethodesWindow, "Methodes");
                    else
                        DrawWindow(7, new Rect(xPos, 205, 380, objectComponentsWindowsAndOtherHeight), EditMethodInvokeWindow, "EditInvoke - " + this.targetMethod.Name);
                }

                if (this.objectChildWindowEnable){
                    if (!this.objectPosRotScaEditWindowEnable)
                        DrawWindow(8, new Rect(395, 40, 245, 180), ObjectChildrenWindow, "Children");
                    else
                        DrawWindow(9, new Rect(645, 40, 245, 180), ObjectChildrenWindow, "Children");
                }

                if (this.targetCameraWindowEnable){
                    if (!this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable)
                        DrawWindow(10, new Rect(395, 40, 245, 180), TargetCameraWindow, "TargetCamera");
                    else if ((this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable) | (this.objectChildWindowEnable && !this.objectPosRotScaEditWindowEnable))
                        DrawWindow(11, new Rect(645, 40, 245, 180), TargetCameraWindow, "TargetCamera");
                    else
                        DrawWindow(12, new Rect(895, 40, 245, 180), TargetCameraWindow, "TargetCamera");
                }else if (this.objectLayerWindowEnable){
                    if (!this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable)
                        DrawWindow(13, new Rect(395, 40, 245, 180), ObjectLayerWindow, "Layer");
                    else if ((this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable) | (this.objectChildWindowEnable && !this.objectPosRotScaEditWindowEnable))
                        DrawWindow(14, new Rect(645, 40, 245, 180), ObjectLayerWindow, "Layer");
                    else
                        DrawWindow(15, new Rect(895, 40, 245, 180), ObjectLayerWindow, "Layer");
                }else if (this.objectGetObjectByNameWindowEnable){
                    if (!this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable)
                        DrawWindow(16, new Rect(395, 40, 345, 280), ObjectGetObjectByNameWindow, "FindObject");
                    else if ((this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable) | (this.objectChildWindowEnable && !this.objectPosRotScaEditWindowEnable))
                        DrawWindow(17, new Rect(645, 40, 345, 280), ObjectGetObjectByNameWindow, "FindObject");
                    else
                        DrawWindow(18, new Rect(895, 40, 345, 280), ObjectGetObjectByNameWindow, "FindObject");
                }

                if (this.viewEnumValuesWindowEnable){
                    if (this.objectComponentsWindowEnable | this.objectComponentValuesWindowEnable != 0 | this.objectComponentMethodesWindowEnable != 0)
                        DrawWindow(19, new Rect(10, 350, 245, 180), ViewEnumValuesWindow, "Enum - " + (Type)this.targetEnum);
                    else
                        DrawWindow(20, new Rect(10, 205, 245, 180), ViewEnumValuesWindow, "Enum - " + (Type)this.targetEnum);
                }
            }

            if (this.windowRegionEnabled[2])
                    DrawWindow(21, new Rect(10, 40, 380, 150), TimeWindow, "Time", 2);
            if (this.windowRegionEnabled[3])
                DrawWindow(22, new Rect(10, 40, 380, 150), SceneWindow, "Scene", 3);
            if (this.windowRegionEnabled[4])
                DrawWindow(23, new Rect(10, 40, 380, 160), ApplicationWindow, "Application", 4);
            if (this.windowRegionEnabled[5]){
                DrawWindow(24, new Rect(10, 40, 380, 150), OtherWindow, "Other", 5);

                if (this.otherResourcesWindowEnable)
                    DrawWindow(25, new Rect(395, 40, 245, 180), ResourcesWindow, "Resources");

                if (this.otherPhysicsWindowEnable > 0){
                    int yW = 280;
                    if (this.assemblyWindowEnable > 0 | this.materialsWindowEnable > 0)
                        yW = 140;

                    if (this.otherPhysicsWindowEnable == 1)
                        DrawWindow(26, new Rect(10, 205, 500, yW), PhysicsWindow, "Physics");
                    else
                        DrawWindow(27, new Rect(10, 205, 500, yW), EditValueWindow, this.targetPropertyInfo.Name + " - Edit");
                }

                if (this.otherRenderWindowEnable){
                    if (this.otherPhysicsWindowEnable > 0)
                        DrawWindow(28, new Rect(515, 205, 430, 140), RenderWindow, "Render\nTargetCamera: " + this.targetCamera.name);
                    else
                        DrawWindow(29, new Rect(10, 205, 430, 140), RenderWindow, "Render\nTargetCamera: " + this.targetCamera.name);
                }

                if (this.assemblyWindowEnable > 0){
                    int yPos = 205;
                    if (this.otherRenderWindowEnable | this.otherPhysicsWindowEnable > 0)
                        yPos = 350;

                    if (this.assemblyWindowEnable == 1)
                        DrawWindow(30, new Rect(10, yPos, 405, 280), AssemblyWindow, "Assemblies");
                    else if (this.assemblyWindowEnable == 2)
                        DrawWindow(31, new Rect(10, yPos, 405, 280), AssemblyTypesWindow, this.targetAssembly.GetName().Name + " - Types");
                    else
                        DrawWindow(32, new Rect(10, yPos, 405, 280), AssemblyTypeInstancesWindow, this.targetType.Name + " - Instances");
                }

                if (this.materialsWindowEnable > 0){
                    int yPos = 205;
                    int xPos = 10;
                    if (this.otherRenderWindowEnable | this.otherPhysicsWindowEnable > 0)
                        yPos = 350;
                    if (this.assemblyWindowEnable > 0)
                        xPos = 420;

                    if (this.materialsWindowEnable == 1)
                        DrawWindow(33, new Rect(xPos, yPos, 405, 280), MaterialsWindow, "Loaded Materials");
                    else
                        DrawWindow(34, new Rect(xPos, yPos, 405, 280), MaterialEditWindow, this.targetMaterial.name + " - Edit");
                }
            }

            if (this.windowRegionEnabled[6])
                DrawWindow(35, new Rect(10, 40, 380, 150), CustomWindow, "Custom", 6);

            if (this.experimentWindowEnabled)
                DrawWindow(36, new Rect(10, 40, 280, 350), ExperimentWindow, "Experiment", 6);
        }

        private void DrawWindow(int id, Rect standartRect, GUI.WindowFunction func, string name, int region = -1){
            if (this.windowRects[id] == Rect.zero)
                this.windowRects[id] = standartRect;
            if (this.windowTrans)
                this.windowRects[id] = GUI.Window(id, this.windowRects[id], func, name, this.transStyle);
            else
                this.windowRects[id] = GUI.Window(id, this.windowRects[id], func, name);
        }

        private void DrawCloseButton(int region, int id){
            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.windowRegionEnabled[region] = false;
        }

        private Texture2D MakeStyleTex(int width, int height, Color color){
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        #endregion

        private void Update(){/*For TargetGameObject find*/
            if (this.viewCursor){
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }else{
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            if (this.freeCam != null)
                this.freeCam.Update();

            if (Input.GetKey(this.shortcutMainKey)){
                if (Input.GetKeyDown(KeyCode.C))
                    this.viewCursor = !this.viewCursor;

                if (Input.GetKeyDown(KeyCode.M))
                    this.showMenu = !this.showMenu;

                if (Input.GetKeyDown(KeyCode.P)){
                    if (Time.timeScale == 0)
                        Time.timeScale = 1;
                    else
                        Time.timeScale = 0;
                }

                if (Input.GetKeyDown("1"))
                    this.targetCopiedIndex = 0;
                else if (Input.GetKeyDown("2"))
                    this.targetCopiedIndex = 1;
                else if (Input.GetKeyDown("3"))
                    this.targetCopiedIndex = 2;
                else if (Input.GetKeyDown("4"))
                    this.targetCopiedIndex = 3;
                else if (Input.GetKeyDown("5"))
                    this.targetCopiedIndex = 4;
            }

            if (this.windowRegionEnabled[1]) {
                if (Input.GetKeyDown(KeyCode.I)) {/*Try to get a TargetObject for the Objects Tab*/
                    if (this.targetCamera == null)
                        this.targetCamera = Camera.main;

                    Ray ray = this.targetCamera.ScreenPointToRay(Input.mousePosition);
                    if (!this.is3D) {/*2D*/
                        RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity);
                        if (hit.collider != null)
                            this.targetObject = hit.collider.gameObject;
                    }else {/*3D*/
                        RaycastHit hit;
                        bool old = Physics.queriesHitTriggers;
                        Physics.queriesHitTriggers = false;
                        if (Physics.Raycast(ray, out hit)) {
                            if (hit.collider != null)
                                this.targetObject = hit.collider.gameObject;
                        }

                        Physics.queriesHitTriggers = old;
                    }
                }
            }
        }

        private void DrawMainButtons(){
            if (GUI.Button(new Rect(10f, 10f, 70f, 20f), "Console"))
                this.windowRegionEnabled[0] = !this.windowRegionEnabled[0];

            if (GUI.Button(new Rect(85f, 10f, 70f, 20f), "Objects")){
                if (this.objectComponentValuesWindowEnable > 0)
                    this.objectComponentValuesWindowEnable = 1;
                this.windowRegionEnabled[1] = !this.windowRegionEnabled[1];
            }
            if (GUI.Button(new Rect(160f, 10f, 70f, 20f), "Time"))
                this.windowRegionEnabled[2] = !this.windowRegionEnabled[2];

            if (GUI.Button(new Rect(235f, 10f, 70f, 20f), "Scene"))
                this.windowRegionEnabled[3] = !this.windowRegionEnabled[3];

            if (GUI.Button(new Rect(310f, 10f, 80f, 20f), "Application"))
                this.windowRegionEnabled[4] = !this.windowRegionEnabled[4];

            if (GUI.Button(new Rect(395f, 10f, 70f, 20f), "Other")){
                if(this.otherPhysicsWindowEnable > 0)
                    this.otherPhysicsWindowEnable = 1;
                this.windowRegionEnabled[5] = !this.windowRegionEnabled[5];
            }
            if (GUI.Button(new Rect(470f, 10f, 100f, 20f), "CopyIndex: " + (this.targetCopiedIndex + 1))){
                if (this.targetCopiedIndex < 4)
                    this.targetCopiedIndex++;
                else
                    this.targetCopiedIndex = 0;
            }

            if (GUI.Button(new Rect(575f, 10f, 120f, 20f), this.onlyUseVarCopy ? "OnlyVarCopy: On" : "OnlyVarCopy: Off"))
                this.onlyUseVarCopy = !this.onlyUseVarCopy;

            this.windowTrans = GUI.Toggle(new Rect(700, 10, 120, 20), this.windowTrans, "Transparent");
        }

        private void ConsoleWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(0, 0);

            this.scrollPosition[0] = GUILayout.BeginScrollView(this.scrollPosition[0]);

            for (int i = 0; i < this.logs.Count; i++){
                switch (this.logs[i].type){
                    case UnityEngine.LogType.Warning:
                        GUI.contentColor = Color.yellow;
                        break;
                    case UnityEngine.LogType.Error:
                    case UnityEngine.LogType.Exception:
                        GUI.contentColor = Color.red;
                        break;
                    default:
                        GUI.contentColor = Color.white;
                        break;
                }
                GUILayout.Label(this.logs[i].logString);
            }

            GUILayout.EndScrollView();

            GUI.contentColor = Color.white;
            if (GUILayout.Button("Clear", GUILayout.Width(50))){
                this.logs.Clear();
            }

            GUI.DragWindow();
        }

        #region ObjetWindow
        private GameObject[] copiedGameObject = new GameObject[5];
        private void ObjectsWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(1, 1);

            if (GUI.Button(new Rect(0f, 0f, 100f, 20f), "Target Camera")){
                this.targetCameraWindowEnable = !this.targetCameraWindowEnable;
                this.objectLayerWindowEnable = false;
                this.objectGetObjectByNameWindowEnable = false;
            }

            if(this.targetObject == null){
                GUI.Label(new Rect(10f, 30f, 500f, 100f), "Please click on an GameObject and press \"i\"!");
                GUI.DragWindow();
                return;
            }

            GUI.Label(new Rect(10f, 30f, 200f, 50f), "Target GameObject: " + this.targetObject.name + "\nTransform instance id: " + this.targetObject.transform.GetInstanceID());

            if (GUI.Button(new Rect(10f, 100f, 70f, 20f), "Destroy"))
                Destroy(this.targetObject);

            if (GUI.Button(new Rect(10f, 75f, 40f, 20f), "Copy")){
                if (this.onlyUseVarCopy)
                    this.copiedVar[this.targetCopiedIndex] = this.targetObject;
                else
                    this.copiedGameObject[this.targetCopiedIndex] = this.targetObject;
            }

            if (this.copiedGameObject[this.targetCopiedIndex] != null && GUI.Button(new Rect(55f, 75f, 25f, 20f), "P")){
                GameObject obj = Instantiate(this.copiedGameObject[this.targetCopiedIndex]);
                obj.transform.position = this.targetObject.transform.position;
            }

            if (GUI.Button(new Rect(10f, 125f, 70f, 20f), this.targetObject.active ? "Disable" : "Enable"))
                this.targetObject.SetActive(!this.targetObject.active);

            if (GUI.Button(new Rect(90f, 125f, 85f, 20f), "Components"))
                this.objectComponentsWindowEnable = !this.objectComponentsWindowEnable;

            if (GUI.Button(new Rect(180f, 125f, 55f, 20f), "Layer")){
                this.objectLayerWindowEnable = !this.objectLayerWindowEnable;
                this.targetCameraWindowEnable = false;
                this.objectGetObjectByNameWindowEnable = false;
            }

            if(GUI.Button(new Rect(180f, 100f, 55f, 20f), "Find")){
                this.objectGetObjectByNameWindowEnable = !this.objectGetObjectByNameWindowEnable;
                this.objectLayerWindowEnable = false;
                this.targetCameraWindowEnable = false; 
            }

            if (this.targetObject.transform.parent != null){
                if (GUI.Button(new Rect(90f, 100f, 85f, 20f), "Parent"))
                    this.targetObject = targetObject.transform.parent.gameObject;
            }

            if (this.targetObject.transform.childCount > 0){
                if (GUI.Button(new Rect(90f, 75f, 85f, 20f), "Childrens"))
                    this.objectChildWindowEnable = !this.objectChildWindowEnable;
            }

            GUI.Label(new Rect(250f, 30f, 200f, 100f), "Position\n" + "X:" + this.targetObject.transform.position.x + " Y:" + this.targetObject.transform.position.y + " Z:" + this.targetObject.transform.position.z);
            GUI.Label(new Rect(250f, 60f, 200f, 100f), "Rotation\n" + "X:" + this.targetObject.transform.eulerAngles.x + " Y:" + this.targetObject.transform.eulerAngles.y + " Z:" + this.targetObject.transform.eulerAngles.z);
            GUI.Label(new Rect(250f, 90f, 200f, 100f), "Scale\n" + "X:" + this.targetObject.transform.localScale.x + " Y:" + this.targetObject.transform.localScale.y + " Z:" + this.targetObject.transform.localScale.z);
            
            if (GUI.Button(new Rect(250f, 125f, 70f, 20f), "Edit"))
                this.objectPosRotScaEditWindowEnable = !this.objectPosRotScaEditWindowEnable;

            GUI.DragWindow();
        }

        private Vector3 posInput;
        private Vector3 rotInput;
        private Vector3 scaleInput;
        private bool objectPosRotScaEditWindowEnable = false;
        private void ObjectPosRotScaEditWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectPosRotScaEditWindowEnable = false;

            if (this.targetObject == null)
                return;

            /*Position*/
            GUI.Label(new Rect(10f, 30f, 100f, 50f), "Postion");
            if (GUI.Button(new Rect(60f, 33f, 60f, 15f), "Get"))
                this.posInput = this.targetObject.transform.position;
            if (GUI.Button(new Rect(130f, 33f, 60f, 15f), "Set"))
                this.targetObject.transform.position = this.posInput;
            if (GUI.Button(new Rect(200f, 33f, 60f, 15f), "Add"))
                this.targetObject.transform.Translate(this.posInput.x * Time.timeScale, this.posInput.y * Time.timeScale, this.posInput.z * Time.timeScale);

            this.posInput.Set(StringToFloat(GUI.TextField(new Rect(10f, 55f, 70, 18), this.posInput.x.ToString())), this.posInput.y, this.posInput.z);
            this.posInput.Set(this.posInput.x, StringToFloat(GUI.TextField(new Rect(85f, 55f, 70, 18), this.posInput.y.ToString())), this.posInput.z);
            this.posInput.Set(this.posInput.x, this.posInput.y, StringToFloat(GUI.TextField(new Rect(160f, 55f, 70, 18), this.posInput.z.ToString())));

            /*Scale*/
            GUI.Label(new Rect(10f, 80f, 100f, 50f), "Scale");
            if (GUI.Button(new Rect(60f, 83f, 60f, 15f), "Get"))
                this.scaleInput = this.targetObject.transform.localScale;
            if (GUI.Button(new Rect(130f, 83f, 60f, 15f), "Set"))
                this.targetObject.transform.localScale = this.scaleInput;

            this.scaleInput.Set(StringToFloat(GUI.TextField(new Rect(10f, 105f, 70, 18), this.scaleInput.x.ToString())), this.scaleInput.y, this.scaleInput.z);
            this.scaleInput.Set(this.scaleInput.x, StringToFloat(GUI.TextField(new Rect(85f, 105f, 70, 18), this.scaleInput.y.ToString())), this.scaleInput.z);
            this.scaleInput.Set(this.scaleInput.x, this.scaleInput.y, StringToFloat(GUI.TextField(new Rect(160f, 105f, 70, 18), this.scaleInput.z.ToString())));

            /*Rotation*/
            GUI.Label(new Rect(10f, 130f, 100f, 50f), "Rotation");
            if (GUI.Button(new Rect(60f, 133f, 60f, 15f), "Get"))
                this.rotInput = this.targetObject.transform.eulerAngles;
            if (GUI.Button(new Rect(130f, 133f, 60f, 15f), "Set"))
                this.targetObject.transform.rotation = Quaternion.Euler(this.rotInput.x, this.rotInput.y, this.rotInput.z);

            this.rotInput.Set(StringToFloat(GUI.TextField(new Rect(10f, 155f, 70, 18), this.rotInput.x.ToString())), this.rotInput.y, this.rotInput.z);
            this.rotInput.Set(this.rotInput.x, StringToFloat(GUI.TextField(new Rect(85f, 155f, 70, 18), this.rotInput.y.ToString())), this.rotInput.z);
            this.rotInput.Set(this.rotInput.x, this.rotInput.y, StringToFloat(GUI.TextField(new Rect(160f, 155f, 70, 18), this.rotInput.z.ToString())));
            GUI.DragWindow();
        }

        private bool objectComponentsWindowEnable = false;
        private Type[] copiedComponent = new Type[5];
        private void ObjectComponentsWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectComponentsWindowEnable = false;

            if (this.targetObject == null)
                return;

            this.scrollPosition[1] = GUILayout.BeginScrollView(this.scrollPosition[1]);

            if (this.copiedComponent[this.targetCopiedIndex] != null && GUILayout.Button("Paste"))
                this.targetObject.AddComponent(this.copiedComponent[this.targetCopiedIndex]);

            foreach (Component comp in this.targetObject.GetComponents(typeof(UnityEngine.Component))){
                string compName = comp.ToString();
                compName = compName.Replace("UnityEngine.", "").Replace(this.targetObject.name, "");
                Behaviour behaviour = null;

                GUILayout.Label(compName);

                try{
                    behaviour = (Behaviour)comp;
                }catch (Exception e){
                    if (this.logExceptions)
                        Debug.Log(e.Message + "|" + e.StackTrace);
                }

                GUILayout.BeginHorizontal();

                if (behaviour != null && GUILayout.Button(behaviour.enabled ? "Disable" : "Enable"))
                    behaviour.enabled = !behaviour.enabled;

                if (GUILayout.Button("Values")){
                    this.targetComponent = comp;
                    this.objectComponentMethodesWindowEnable = 0;
                    if(this.objectComponentValuesWindowEnable == 0)
                        this.objectComponentValuesWindowEnable = 1;
                    else
                        this.objectComponentValuesWindowEnable = 0;
                }

                if (GUILayout.Button("Methodes")){
                    this.targetComponent = comp;
                    this.objectComponentValuesWindowEnable = 0;
                    if (this.objectComponentMethodesWindowEnable == 0)
                        this.objectComponentMethodesWindowEnable = 1;
                    else
                        this.objectComponentMethodesWindowEnable = 0;
                }

                if (GUILayout.Button("Copy")){
                    if (this.onlyUseVarCopy)
                        this.copiedVar[this.targetCopiedIndex] = comp;
                    else
                        this.copiedComponent[this.targetCopiedIndex] = comp.GetType();
                }

                if (GUILayout.Button("Destroy"))
                    Destroy(comp);

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private int objectComponentValuesWindowEnable = 0 /*0 = false, 1 = NormalValuesWindow, 2 = EditValuesWindow, Good cast = 3, bad cast = 4*/;
        private void ObjectComponentValuesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectComponentValuesWindowEnable = 0;

            if (this.targetComponent == null)
                return;

            GUILayout.Label("Search:");
            this.searchQueryValues = GUILayout.TextField(this.searchQueryValues);

            this.scrollPosition[4] = GUILayout.BeginScrollView(this.scrollPosition[4]);

            foreach(FieldInfo field in this.targetComponent.GetType().GetFields(BindingFlags.Static | BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.ExactBinding | BindingFlags.GetField | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.SetField | BindingFlags.SetProperty)){
                try{
                    if (field.Name.Equals("rigidbody") | field.Name.Equals("rigidbody2D") | field.Name.Equals("camera"))
                        continue;

                    var value = field.GetValue(this.targetComponent);

                    if (string.IsNullOrEmpty(this.searchQueryValues) || field.Name.ToLower().Contains(this.searchQueryValues.ToLower())){
                        GUILayout.BeginHorizontal();

                        GUILayout.Label(field.Name + " - " + value);

                        if (GUILayout.Button("Edit")){
                            this.targetFieldInfo = field;
                            this.editValueInput[0] = value.ToString().Replace("(", "").Replace(")", "");
                            this.objectComponentValuesWindowEnable = 2;
                            this.isTargetField = true;
                            this.editPhysicsValue = false;
                        }

                        GUILayout.EndHorizontal();
                    }
                }catch (Exception e){
                    if (this.logExceptions)
                        Debug.Log(e.Message + "|" + e.StackTrace);
                    continue;
                } 
            }

            GUILayout.Label("Properties");
            
            foreach(PropertyInfo prop in this.targetComponent.GetType().GetProperties()){
                try{
                    if (prop.Name.Equals("rigidbody") | prop.Name.Equals("rigidbody2D") | prop.Name.Equals("camera"))
                        continue;

                    var value = prop.GetValue(this.targetComponent, null);

                    if (string.IsNullOrEmpty(this.searchQueryValues) || prop.Name.ToLower().Contains(this.searchQueryValues.ToLower())){

                        GUILayout.BeginHorizontal();

                        GUILayout.Label(prop.Name + " - " + value);

                        if (GUILayout.Button("Edit")){
                            this.targetPropertyInfo = prop;
                            this.editValueInput[0] = value.ToString().Replace("(", "").Replace(")", "");
                            this.objectComponentValuesWindowEnable = 2;
                            this.isTargetField = false;
                            this.editPhysicsValue = false;
                        }

                        GUILayout.EndHorizontal();
                    }

                }catch (Exception e){
                    if (this.logExceptions)
                        Debug.Log(e.Message + "|" + e.StackTrace);
                    continue;
                } 
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private object[] copiedVar = new object[5];
        private bool editPhysicsValue = false;
        private string searchQueryValues = "";
        private void EditValueWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if ((!this.editPhysicsValue && this.targetComponent == null) | (this.targetPropertyInfo == null && this.targetFieldInfo == null))
                return;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Back")){
                SetValueWindowValue(1);
                return;
            }

            object target = null;
            if (!this.editPhysicsValue)
                target = this.targetComponent;

            if (GUILayout.Button("CopyVar")){
                if(this.isTargetField)
                    this.copiedVar[this.targetCopiedIndex] = this.targetFieldInfo.GetValue(target);
                else
                    this.copiedVar[this.targetCopiedIndex] = this.targetPropertyInfo.GetValue(target, null);
            }

            if (this.copiedVar[this.targetCopiedIndex] != null && GUILayout.Button("PasteVar")){
                if((!this.isTargetField && this.copiedVar[this.targetCopiedIndex].GetType() != this.targetPropertyInfo.GetValue(target, null).GetType())
                    | (this.isTargetField && this.copiedVar[this.targetCopiedIndex].GetType() != this.targetFieldInfo.GetValue(target).GetType())){
                    SetValueWindowValue(4);
                    return;
                }else{ 

                    if (this.isTargetField)
                        this.targetFieldInfo.SetValue(target, this.copiedVar[this.targetCopiedIndex]);
                    else
                        this.targetPropertyInfo.SetValue(target, this.copiedVar[this.targetCopiedIndex], null);

                    SetValueWindowValue(3);
                }
            }

            if (GUILayout.Button("SetNull")){
                if (this.isTargetField)
                    this.targetFieldInfo.SetValue(target, null);
                else
                    this.targetPropertyInfo.SetValue(target, null, null);

                SetValueWindowValue(3);
            }

            GUILayout.EndHorizontal();

            this.editValueInput[0] = GUILayout.TextField(this.editValueInput[0]);

            if(GUILayout.Button("Try to cast")){
                try{

                    if (!this.isTargetField){
                        object var = TryToCastString(this.editValueInput[0], this.targetPropertyInfo.GetValue(target, null).GetType());
                        if (var == null){
                            SetValueWindowValue(4);
                            return;
                        }else{
                            SetValueWindowValue(3);
                            this.targetPropertyInfo.SetValue(target, var, null);
                        }
                    }else{
                        object var = TryToCastString(this.editValueInput[0], this.targetFieldInfo.GetValue(target).GetType());
                        if (var == null){
                            SetValueWindowValue(4);
                            return;
                        }else{
                            SetValueWindowValue(3);
                            this.targetFieldInfo.SetValue(target, var);
                        }
                    }
                
                }catch (Exception e){
                    SetValueWindowValue(4);
                }
            }

            int window = this.objectComponentValuesWindowEnable;
            if (this.editPhysicsValue)
                window = this.otherPhysicsWindowEnable;

            if (window == 3)
                GUILayout.Label("Last cast was successfuly!");
            else if (window == 4)
                GUILayout.Label("Last cast was not successfuly!");

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void SetValueWindowValue(int value){
            if (this.editPhysicsValue)
                this.otherPhysicsWindowEnable = value;
            else
                this.objectComponentValuesWindowEnable = value;
        }

        private string searchQueryMethods = "";
        private int objectComponentMethodesWindowEnable = 0 /*0 = false, 1 = NormalMethodsWindow, 2 = EditMethodsWindow, Good cast = 3, bad cast = 4*/;
        private void ObjectComponentMethodesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (this.targetComponent == null)
                return;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectComponentMethodesWindowEnable = 0;

            GUILayout.Label("Search:");
            this.searchQueryMethods = GUILayout.TextField(this.searchQueryMethods);

            this.scrollPosition[5] = GUILayout.BeginScrollView(this.scrollPosition[5]);

            foreach(MethodInfo method in this.targetComponent.GetType().GetMethods(
                BindingFlags.Static | BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.ExactBinding | BindingFlags.GetField | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.SetField | BindingFlags.SetProperty)){
                try {
                    if (string.IsNullOrEmpty(this.searchQueryMethods) || method.Name.ToLower().Contains(this.searchQueryMethods.ToLower()))
                        MethodDraw(method);
                }catch(Exception e){
                    if (this.logExceptions)
                        Debug.Log(e.Message + "|" + e.StackTrace);
                    continue;
                }
            }

            GUILayout.Label("Other");

            foreach (MethodInfo method in this.targetComponent.GetType().GetMethods()){
                try{
                    if (string.IsNullOrEmpty(this.searchQueryMethods) || method.Name.ToLower().Contains(this.searchQueryMethods.ToLower()))
                        MethodDraw(method);
                }catch (Exception e){
                    if (this.logExceptions)
                        Debug.Log(e.Message + "|" + e.StackTrace);
                    continue;
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void InvokeMethod(MethodInfo method, object[] paramets){
            if (method.ReturnType == typeof(IEnumerator) || method.ReturnType == typeof(Coroutine))
                StartCoroutine((IEnumerator)method.Invoke(this.targetComponent, paramets));
            else
                method.Invoke(this.targetComponent, paramets);
        }

        private void MethodDraw(MethodInfo method){
            GUILayout.BeginHorizontal();

            string endString = method.Name + "(";
            foreach (ParameterInfo parm in method.GetParameters()){
                string type = parm.GetType().Name;
                endString = endString + " " + parm.Name + ",";
            }
            if (method.GetParameters().Length > 0)
                endString = endString.Remove(endString.Length - 1);
            endString = endString + " )";

            GUILayout.Label(endString);
            if (GUILayout.Button("Invoke")){
                if (method.GetParameters().Length == 0)
                    InvokeMethod(method, null);
                else{
                    this.objectComponentMethodesWindowEnable = 2;
                    this.targetMethod = method;

                    string parms = string.Empty;
                    foreach (ParameterInfo parm in this.targetMethod.GetParameters()){
                        if (parms != string.Empty)
                            parms = parms + "#";

                        parms = parms + parm.ParameterType;
                    }

                    this.editValueInput[1] = parms;
                }
            }

            GUILayout.EndHorizontal();
        }

        private void EditMethodInvokeWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (this.targetComponent == null | this.targetMethod == null)
                return;

            this.scrollPosition[6] = GUILayout.BeginScrollView(this.scrollPosition[6]);

            GUILayout.BeginVertical();

            if (GUILayout.Button("Back"))
                this.objectComponentMethodesWindowEnable = 1;

            GUILayout.Label("Write \"!(CopyIndexNumber)\" for paste the current copied value.\"v\" for view an enum.");

            string parms = string.Empty;
            foreach (ParameterInfo parm in this.targetMethod.GetParameters()){
                if (parms != string.Empty)
                    parms = parms + ",";

                parms = parms + parm.ParameterType + " " + parm.Name;
            }

            GUILayout.Label(this.targetMethod.Name + "(" + parms + ")");

            this.editValueInput[1] = GUILayout.TextField(this.editValueInput[1]);

            if(GUILayout.Button("Try to cast & invoke")){
                try{
                    int i = 0;
                    string[] args = this.editValueInput[1].Split('#');
                    object[] parameters = new object[this.targetMethod.GetParameters().Length];
                    foreach (ParameterInfo p in this.targetMethod.GetParameters()){
                        parameters[i] = TryToCastString(args[i], p.ParameterType);
                        if(parameters[i] == null){
                            this.objectComponentMethodesWindowEnable = 4;
                            return;
                        }
                        i++;
                    }

                    InvokeMethod(this.targetMethod, parameters);
                    this.objectComponentMethodesWindowEnable = 3;
                }
                catch (Exception e){
                    this.objectComponentMethodesWindowEnable = 4;
                }
            }

            if (this.objectComponentMethodesWindowEnable == 3)
                GUILayout.Label("Last cast was successfuly!");
            else if (this.objectComponentMethodesWindowEnable == 4)
                GUILayout.Label("Last cast was not successfuly!");

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private bool targetCameraWindowEnable = false;
        private void TargetCameraWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.targetCameraWindowEnable = false;

            this.scrollPosition[3] = GUILayout.BeginScrollView(this.scrollPosition[3]);

            foreach(Camera cam in Camera.allCameras){
                GUILayout.BeginHorizontal();

                if(cam.tag.Equals("MainCamera"))
                    GUILayout.Label(cam.name + " (main)");
                else
                    GUILayout.Label(cam.name);

                if (GUILayout.Button("Choose")){
                    this.targetCamera = cam;
                    this.targetObject = this.targetCamera.gameObject;
                }

                if(GUILayout.Button("Set Main")){
                    Camera old = Camera.main;
                    old.enabled = false;
                    old.tag = "Untagged";
                    cam.tag = "MainCamera";
                    old.enabled = true;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        public static object TryToCastString(string valStr, Type type){
            object var = null;

            if(!type.Equals(typeof(string)) && valStr.StartsWith("!")){
                var = PDebugReloaded.instance.copiedVar[(int)StringToFloat(valStr.Replace("!", "")) - 1];
                return var;
            }

            if (type.Equals(typeof(UnityEngine.Vector2))
                | type.Equals(typeof(UnityEngine.Vector3))
                    | type.Equals(typeof(UnityEngine.Vector4))){
                valStr = valStr.Replace("(", "").Replace(")", "").Replace(" ", "");
                Debug.Log(valStr);
            }

            if (type.Equals(typeof(int)))
                var = (int)StringToFloat(valStr);
            else if (type.Equals(typeof(float)))
                var = StringToFloat(valStr);
            else if (type.Equals(typeof(UnityEngine.Vector2)))
                var = new Vector2(StringToFloat(valStr.Split(',')[0]), StringToFloat(valStr.Split(',')[1]));
            else if (type.Equals(typeof(UnityEngine.Vector3)))
                var = new Vector3(StringToFloat(valStr.Split(',')[0]), StringToFloat(valStr.Split(',')[1]), StringToFloat(valStr.Split(',')[2]));
            else if (type.Equals(typeof(UnityEngine.Vector4)))
                var = new Vector4(StringToFloat(valStr.Split(',')[0]), StringToFloat(valStr.Split(',')[1]), StringToFloat(valStr.Split(',')[2]), StringToFloat(valStr.Split(',')[3]));
            else if (type.Equals(typeof(bool)) | type.Equals(typeof(Boolean))){
                if (valStr.Equals("false", StringComparison.OrdinalIgnoreCase) | valStr.Equals("f", StringComparison.OrdinalIgnoreCase))
                    var = false;
                else
                    var = true;
            }
            else if (type.Equals(typeof(string)) | type.Equals(typeof(String)))
                var = (string)valStr;
            else if (type.Equals(typeof(Int32)))
            {
                var = valStr;
                Int32 output = 0;
                Int32.TryParse(valStr, out output);
                var = output;
            }
            else if (type.Equals(typeof(Int16)))
            {
                Int16 output = 0;
                Int16.TryParse(valStr, out output);
                var = output;
            }
            else if (type.Equals(typeof(Int64)))
            {
                Int64 output = 0;
                Int64.TryParse(valStr, out output);
                var = output;
            }
            else if (type.Equals(typeof(Color)))
            {
                var = (Color32)PDebugReloaded.instance.GetColorFromHex(valStr);
            }
            else if (type.Equals(typeof(Color32)))
            {
                var = (Color32)PDebugReloaded.instance.GetColorFromHex(valStr);
            }
            else if (type.IsEnum)
            {
                if (valStr.Equals("v"))
                {
                    PDebugReloaded.instance.viewEnumValuesWindowEnable = true;
                    PDebugReloaded.instance.targetEnum = type;
                    return null;
                }

                var = Enum.ToObject(type, (int)StringToFloat(valStr));
            }

            return var;
        }

        private Color GetColorFromHex(string hex){
            Color color = Color.white;
            hex = hex.Replace("#", "");
            ColorUtility.TryParseHtmlString("#" + hex, out color);
            return color;
        }

        public static string ColorToHex(Color color){
            Color32 color32 = color;
            string hex = color32.r.ToString("X2") + color32.g.ToString("X2") + color32.b.ToString("X2");
            return hex;
        }

        private bool viewEnumValuesWindowEnable = false;
        private object targetEnum;
        private void ViewEnumValuesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.viewEnumValuesWindowEnable = false;

            if (this.targetEnum == null)
                return;

            this.scrollPosition[8] = GUILayout.BeginScrollView(this.scrollPosition[8]);

            for (int i = 0; i < Enum.GetValues((Type)this.targetEnum).Length; i++){
                GUILayout.BeginHorizontal();

                GUILayout.Label(Enum.GetName((Type)this.targetEnum, Enum.GetValues((Type)this.targetEnum).GetValue(i)) + " \\ " +
                    (int)Enum.GetValues((Type)this.targetEnum).GetValue(i));

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private bool objectChildWindowEnable = false;
        private void ObjectChildrenWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectChildWindowEnable = false;

            if (this.targetObject == null)
                return;

            this.scrollPosition[2] = GUILayout.BeginScrollView(this.scrollPosition[2]);

            foreach (Transform child in this.targetObject.transform){
                GUILayout.BeginHorizontal();

                GUILayout.Label(child.gameObject.name);
                if (GUILayout.Button("Edit"))
                    this.targetObject = child.gameObject;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private bool objectLayerWindowEnable = false;
        private void ObjectLayerWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectLayerWindowEnable = false;

            if (this.targetObject == null)
                return;

            this.scrollPosition[6] = GUILayout.BeginScrollView(this.scrollPosition[6]);

            GUILayout.Label("Current Layer: " + LayerMask.LayerToName(this.targetObject.layer));

            for (int i = 0; i < 31; i++){
                GUILayout.BeginHorizontal();

                GUILayout.Label(LayerMask.LayerToName(i));
                if (GUILayout.Button("Set"))
                    this.targetObject.layer = i;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        #region Find window
        private bool objectGetObjectByNameWindowEnable = false;
        private List<GameObject> allObjects = new List<GameObject>();
        private Dictionary<GameObject, bool> expandedStates;
        private string searchQuery = "";

        private void ObjectGetObjectByNameWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectGetObjectByNameWindowEnable = false;

            if (GUILayout.Button("Copy GameObject list to clipboard")){
                GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                List<string> list = new List<string>();

                foreach (GameObject obj in allObjects){
                    list.Add(obj.name);
                }

                list.Sort();

                string outputString = string.Empty;

                foreach (string obj in list){
                    outputString = outputString + obj + "\n";
                }

                GUIUtility.systemCopyBuffer = outputString;
            }

            /*if (GUILayout.Button("Get first Directional Light"))
                this.targetObject = Light.GetLights(LightType.Directional, 0)[0].gameObject;
            */

            if (GUILayout.Button("Refresh")){
                this.allObjects = new List<GameObject>(FindObjectsOfType<GameObject>());
                this.expandedStates = new Dictionary<GameObject, bool>();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:");
            this.searchQuery = GUILayout.TextField(this.searchQuery);
            GUILayout.EndHorizontal();

            this.scrollPosition[9] = GUILayout.BeginScrollView(this.scrollPosition[9]);

            foreach (GameObject obj in this.allObjects){
                if (obj == null)
                    continue;

                if (string.IsNullOrEmpty(searchQuery) || obj.name.ToLower().Contains(this.searchQuery.ToLower())){
                    DrawObject(obj);
                }
            }

            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private void DrawObject(GameObject obj){
            if (obj.transform.parent == null){
                if (!this.expandedStates.ContainsKey(obj))
                    this.expandedStates[obj] = false;
                bool isExpanded = expandedStates[obj];
                DrawChildObjects(obj, ref isExpanded);
                this.expandedStates[obj] = isExpanded;
            }
        }

        private void DrawChildObjects(GameObject parent, ref bool isExpanded, string add = ""){
            GUILayout.BeginHorizontal();

            GUILayout.Label(add + parent.name);

            if (parent.transform.childCount > 0 && GUILayout.Button(isExpanded ? "-" : "+")){
                isExpanded = !isExpanded;
            }

            if (GUILayout.Button("Get"))
                this.targetObject = parent;

            GUILayout.EndHorizontal();

            if (isExpanded){
                foreach (Transform child in parent.transform){
                    if (!this.expandedStates.ContainsKey(child.gameObject))
                        this.expandedStates[child.gameObject] = false;
                    bool childIsExpanded = this.expandedStates[child.gameObject];
                    DrawChildObjects(child.gameObject, ref childIsExpanded, "-" + add);
                    this.expandedStates[child.gameObject] = childIsExpanded;
                }
            }
        }
        #endregion
        #endregion

        private string timeInput = "1";
        private void TimeWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(2, 21);

            GUI.Label(new Rect(10f, 30f, 200f, 50f), "Delta Time: " + Time.deltaTime + "\nTime Scale: " + Time.timeScale);

            this.timeInput = GUI.TextField(new Rect(10f, 65f, 70, 18), this.timeInput);
            if (GUI.Button(new Rect(10f, 85f, 70f, 20f), "Change"))
                Time.timeScale = StringToFloat(this.timeInput);
            if (GUI.Button(new Rect(85f, 85f, 70f, 20f), "Pause")){
                if (Time.timeScale == 0)
                    Time.timeScale = 1;
                else
                    Time.timeScale = 0;
            }
            GUI.DragWindow();
        }

        private void SceneWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(3, 22);

            this.scrollPosition[0] = GUILayout.BeginScrollView(this.scrollPosition[0]);

            GUILayout.Label("ActiveScene: " + SceneManager.GetActiveScene().name);

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++){
                GUILayout.BeginHorizontal();

                string sceneName = string.Empty;
                try{//If it gives an error here, simple delete "sceneName = UnityEngine.SceneManagement.SceneManager.GetSceneByBuildIndex(i).name;"
#if UNITY_2018_1_OR_NEWER
                    sceneName = UnityEngine.SceneManagement.SceneManager.GetSceneByBuildIndex(i).name;
#endif
                }catch (Exception e){}

                GUILayout.Label(i.ToString() + " " + sceneName);
                if (GUILayout.Button("Load"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(i);

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void ApplicationWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(4, 23);

            if (GUI.Button(new Rect(10f, 30f, 200f, 15f), "RunInBackground: " + Application.runInBackground))
                Application.runInBackground = !Application.runInBackground;

            GUI.Label(new Rect(10f, 50f, 500f, 500f), "ProductName: "+ Application.productName + "\nCompanyName: " 
                + Application.companyName +"\nUnityVersion: " + Application.unityVersion
                + "\n" + this.shortcutMainKey.ToString().Replace("KeyCode.", "") + " + , C switch cursor visibility\n,M switch menu visibility, P toggle pause, 1-5 switch copyIndex" 
                + "\n" + this.NAME + " by PandaHexCode");

            if (GUI.Button(new Rect(215f, 30f, 100f, 15f), "Is3D: " + this.is3D))
                this.is3D = !this.is3D;
            GUI.DragWindow();
        }

        #region Other Window
        private Light customLight = null;
        private GameObject oldCamera;
        private FreeCamController freeCam;
        private void OtherWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(5, 24);

            float fps = (int)(1f / Time.unscaledDeltaTime);

            GUI.Label(new Rect(10f, 30f, 200f, 50f), "FPS: " + fps);

            if (GUI.Button(new Rect(10f, 70f, 85f, 20f), "Resources"))
                this.otherResourcesWindowEnable = !this.otherResourcesWindowEnable;
            if (GUI.Button(new Rect(10f, 100f, 85f, 20f), "Physics")){
                if (this.otherPhysicsWindowEnable > 0)
                    this.otherPhysicsWindowEnable = 0;
                else
                    this.otherPhysicsWindowEnable = 1;
            }

            if (GUI.Button(new Rect(200f, 100f, 85f, 20f), "Render")){
                if (this.targetCamera == null)
                    this.targetCamera = Camera.main;
                this.otherRenderWindowEnable = !this.otherRenderWindowEnable;
            }

            if (GUI.Button(new Rect(200f, 70f, 85f, 20f), "Custom"))
                this.windowRegionEnabled[6] = !this.windowRegionEnabled[6];
            if (GUI.Button(new Rect(10f, 125f, 85f, 20f), "Assemblies")){
                if (this.assemblyWindowEnable > 0)
                    this.assemblyWindowEnable = 0;
                else
                    this.assemblyWindowEnable = 1;
            }

            if (GUI.Button(new Rect(100f, 125f, 95f, 20f), "Materials")){
                if (this.materialsWindowEnable > 0)
                    this.materialsWindowEnable = 0;
                else
                    this.materialsWindowEnable = 1;
            }

            if (GUI.Button(new Rect(200f, 125f, 85f, 20f), "Experiment"))
                this.experimentWindowEnabled = !this.experimentWindowEnabled;

            if (GUI.Button(new Rect(100f, 100f, 95f, 20f), this.customLight == null ?  "CLight: Off" : "CLight: On")){
                if (this.customLight != null)
                    Destroy(this.customLight.gameObject);
                else{
                    this.customLight = new GameObject("CustomLight").AddComponent<Light>();
                    this.customLight.type = LightType.Directional;
                    this.customLight.transform.localEulerAngles = new Vector3(50, -30, 0);
                }
            }

            if (this.freeCam != null && this.freeCam.transform == null)
                this.freeCam = null;

            if (GUI.Button(new Rect(100f, 70f, 95f, 20f), this.freeCam == null ? "FreeCam: Off" : "FreeCam: On")){
                if (this.freeCam != null){  
                    Destroy(this.freeCam.transform.gameObject);
                    this.freeCam = null;
                    this.oldCamera.gameObject.SetActive(true);
                    this.targetObject = this.oldCamera;
                    if (this.targetCamera == null)
                        this.targetCamera = Camera.main;
                    return;
                }
                if (this.targetCamera == null)
                    this.targetCamera = Camera.main;
                GameObject clon = Instantiate(this.targetCamera.gameObject);
                foreach (var comp in clon.GetComponents<Component>()){
                    if (!(comp is Transform) && !(comp is Camera))
                        Destroy(comp);
                }
                this.oldCamera = this.targetCamera.gameObject;
                clon.transform.position = this.oldCamera.transform.position;
                this.oldCamera.SetActive(false);
                this.freeCam = new FreeCamController();
                this.freeCam.transform = clon.transform;
                this.targetCamera = clon.GetComponent<Camera>();
                this.targetCamera.Render();
                this.targetObject = this.targetCamera.gameObject;
                clon.name = "FreeCam";
                clon.SetActive(true);
            }

            GUI.DragWindow();
        }

        private bool otherResourcesWindowEnable = false;
        private void ResourcesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.otherResourcesWindowEnable = false;

            this.scrollPosition[0] = GUILayout.BeginScrollView(this.scrollPosition[0]);

            List<GameObject> resources = new List<GameObject>();
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll(typeof(UnityEngine.GameObject))){
                if (obj.scene.name == null && obj.transform.parent == null)
                    resources.Add(obj);
            }

            foreach(GameObject gm in resources){
                GUILayout.BeginHorizontal();

                GUILayout.Label(gm.name);

                if (GUILayout.Button("Edit"))
                    this.targetObject = gm;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private int otherPhysicsWindowEnable = 0;
        private void PhysicsWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.otherPhysicsWindowEnable = 0;

            this.scrollPosition[2] = GUILayout.BeginScrollView(this.scrollPosition[2]);

            PropertyInfo[] properties;
            if (this.is3D){
                properties = typeof(Physics).GetProperties();
                GUILayout.Label("Physics3D");
            }else{
                GUILayout.Label("Physics2D");
                properties = typeof(Physics2D).GetProperties();
            }

            foreach(PropertyInfo prop in properties){
                try{

                    var value = prop.GetValue(null, null);

                    GUILayout.BeginHorizontal();

                    GUILayout.Label(prop.Name + " - " + value);

                    if (GUILayout.Button("Edit")){
                        this.targetPropertyInfo = prop;
                        this.editValueInput[0] = value.ToString().Replace("(", "").Replace(")", "");
                        this.otherPhysicsWindowEnable = 2;
                        this.isTargetField = false;
                        this.editPhysicsValue = true;
                    }

                    GUILayout.EndHorizontal();

                }
                catch(Exception e){
                    continue;
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private bool otherRenderWindowEnable = false;
        public bool[] drawCollToggles = new bool[4] { true, true, true, true};
        private void RenderWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.otherRenderWindowEnable = false;

            if (GUI.Button(new Rect(10f, 70f, 85f, 20f), "Wireframe")){
                CheckTargetCameraMod();
                this.targetCamera.GetComponent<RenderCameraMod>().wireframe = !this.targetCamera.GetComponent<RenderCameraMod>().wireframe;
                this.targetCamera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            }

            if (GUI.Button(new Rect(100f, 70f, 85f, 20f), "DrawColl")){
                CheckTargetCameraMod();
                this.targetCamera.GetComponent<RenderCameraMod>().drawCollision = !this.targetCamera.GetComponent<RenderCameraMod>().drawCollision;
            }

            if (this.is3D){
                this.drawCollToggles[0] = GUI.Toggle(new Rect(190, 70, 50, 20), this.drawCollToggles[0], "Mesh");
                this.drawCollToggles[1] = GUI.Toggle(new Rect(240, 70, 55, 20), this.drawCollToggles[1], "Other");
            }
            this.drawCollToggles[2] = GUI.Toggle(new Rect(300, 70, 60, 20), this.drawCollToggles[2], "Trigger");
            this.drawCollToggles[3] = GUI.Toggle(new Rect(360, 70, 60, 20), this.drawCollToggles[3], "!Trigger");

            if (GUI.Button(new Rect(10f, 100f, 105f, 20f), "Flag: " + this.targetCamera.clearFlags)){
                switch (this.targetCamera.GetComponent<Camera>().clearFlags){
                    case CameraClearFlags.Skybox:
                        this.targetCamera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                        break;

                    case CameraClearFlags.SolidColor:
                        this.targetCamera.GetComponent<Camera>().clearFlags = CameraClearFlags.Depth;
                        break;

                    case CameraClearFlags.Depth:
                        this.targetCamera.GetComponent<Camera>().clearFlags = CameraClearFlags.Nothing;
                        break;

                    case CameraClearFlags.Nothing:
                        this.targetCamera.GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
                        break;
                }
            }
            GUI.DragWindow();
        }

        private int materialsWindowEnable = 0;
        private Material targetMaterial;
        private List<Material> materials = null;
        private void MaterialsWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.materialsWindowEnable = 0;

            if (this.materials == null | GUILayout.Button("Reload")){
                this.materials = new List<Material>();

                Renderer[] renderers = FindObjectsOfType<Renderer>();

                /*Try to add every material from every current renderer from the scene*/
                foreach (Renderer renderer in renderers){
                    if (renderer == null)
                        continue;

                    foreach (Material material in renderer.sharedMaterials){
                        try{
                            if (material != null && !this.materials.Contains(material))
                                this.materials.Add(material);
                        }catch (Exception e){
                            continue;
                        }
                    }
                }

                renderers = null;

                this.materials = this.materials.OrderBy(m => m.name).ToList();
            }

            this.scrollPosition[5] = GUILayout.BeginScrollView(this.scrollPosition[5]);

            GUILayout.BeginHorizontal();

            this.editValueInput[6] = GUILayout.TextField(this.editValueInput[6]);

            if (GUILayout.Button("Export")){

                if (!Directory.Exists(this.editValueInput[6]))
                    Directory.CreateDirectory(this.editValueInput[6]);
              
                foreach(Material mat in this.materials){
                    try{
                        string[] properties = GetMaterialTextureProperties(mat);

                        foreach (string propName in properties){
                            Texture tex = mat.GetTexture(propName);

                            if(tex != null)
                                ExportTextureToPNG(tex, this.editValueInput[6] + "\\" + mat.name + ",__" + propName + ".png");

                            tex = null;
                        }

                        properties = null;
                    }catch (Exception e){
                        Debug.LogError("Error by exporting " + mat.name + "!");   
                    }
                }

            }

            if (GUILayout.Button("Load")){

                GC.Collect();

                if (!Directory.Exists(this.editValueInput[6]))
                    return;

                foreach(Material mat in this.materials){
                    try{

                        foreach (string fileName in Directory.GetFiles(this.editValueInput[6])){
                            string file = fileName.Replace(this.editValueInput[6] + "\\", "").Replace(".png", "");

                            string[] args = file.Split(new string[] { ",__" }, StringSplitOptions.None);

                            Texture tex = LoadTexture(fileName);

                            if (args[0].Equals(mat.name))
                                mat.SetTexture(args[1], tex);

                            GC.Collect();
                            args = null;
                            file = null;
                        }

                    }catch(Exception e){
                        Debug.LogError(e.Message + "|" + e.StackTrace);
                        continue;
                    }
                }

                GC.Collect();

            }

            GUILayout.EndHorizontal();

            foreach (Material material in this.materials){
                GUILayout.BeginHorizontal();

                try{
                    GUILayout.Label(material.name);
                    if (GUILayout.Button("Edit")){
                        this.materialsWindowEnable = 2;
                        this.targetMaterial = material;
                    }
                }catch(Exception e){
                    GUILayout.EndHorizontal();
                    continue;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private Texture[] copiedTexture = new Texture[5];
        private void MaterialEditWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 50, 20), "Back") | this.targetMaterial == null)
                this.materialsWindowEnable = 1;

            this.scrollPosition[5] = GUILayout.BeginScrollView(this.scrollPosition[5]);

            GUILayout.Label("CopyTextureFromPath");

            GUILayout.BeginHorizontal();

            this.editValueInput[3] = GUILayout.TextField(this.editValueInput[3]);
            if (GUILayout.Button("Copy")){
                if (this.onlyUseVarCopy)
                    this.copiedVar[this.targetCopiedIndex] = LoadTexture(this.editValueInput[3]);
                else
                    this.copiedTexture[this.targetCopiedIndex] = LoadTexture(this.editValueInput[3]);
            }

            GUILayout.EndHorizontal();

            GUILayout.Label("Shader: " + this.targetMaterial.shader.name);

            string[] properties = GetMaterialTextureProperties(this.targetMaterial);

            foreach (string propName in properties){
                try{

                    Texture tex = this.targetMaterial.GetTexture(propName);
                    if (tex == null){
                        GUILayout.BeginHorizontal();

                        GUILayout.Label(propName + " - No texture");
                        if (this.copiedTexture[this.targetCopiedIndex] != null && GUILayout.Button("Paste"))
                            this.targetMaterial.SetTexture(propName, this.copiedTexture[this.targetCopiedIndex]);

                        GUILayout.EndHorizontal();
                        continue;
                    }

                    GUILayout.Label(propName);

                    GUILayout.Label(tex, GUILayout.Width(120), GUILayout.Height(120));

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("Copy")){
                        if (this.onlyUseVarCopy)
                            this.copiedVar[this.targetCopiedIndex] = tex;
                        else
                            this.copiedTexture[this.targetCopiedIndex] = tex;
                    }

                    if (this.copiedTexture[this.targetCopiedIndex] != null && GUILayout.Button("Paste"))
                        this.targetMaterial.SetTexture(propName, this.copiedTexture[this.targetCopiedIndex]);

                    if (GUILayout.Button("Remove texture"))
                        this.targetMaterial.SetTexture(propName, null);

                    if (GUILayout.Button("Export"))
                        ExportTextureToPNG(tex, this.editValueInput[3]);

                    GUILayout.EndHorizontal();
                }catch(Exception e){
                    continue;
                }
            }

            GUILayout.Label("Colour");

            GUILayout.BeginHorizontal();

            this.editValueInput[4] = GUILayout.TextField(this.editValueInput[4]);

            if (GUILayout.Button("Get"))
                this.editValueInput[4] = ColorToHex(this.targetMaterial.color);

            if (GUILayout.Button("Set"))
                this.targetMaterial.color = GetColorFromHex(this.editValueInput[4]);

            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private string[] GetMaterialTextureProperties(Material mat){
#if UNITY_2018_1_OR_NEWER
            return mat.GetTexturePropertyNames();
#else
            return new string[] { "_MainTex", "_BumpMap", "_DetailNormalMap", "_ParallaxMap", "_OcclusionMap", "_EmissionMap", "_DetailMask", "_DetailAlbedoMap", "_MetallicGlossMap"};
#endif
        }

        public static Texture LoadTexture(string path){
            Texture2D tex = new Texture2D(2, 2);

            byte[] fileData = File.ReadAllBytes(path);
            bool success = tex.LoadImage(fileData);

            if (!success){
                Debug.LogError("Failed to load texture from file: " + path);
                return null;
            }

            return tex;
        }

        public static void ExportTextureToPNG(Texture texture, string filename){
            RenderTexture tmp = RenderTexture.GetTemporary(
            texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear
        );
            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D tex = new Texture2D(texture.width, texture.height);
            tex.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filename, bytes);

            UnityEngine.Object.DestroyImmediate(tex);
        }

        private int assemblyWindowEnable = 0;
        private Assembly targetAssembly = null;
        private void AssemblyWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.assemblyWindowEnable = 0;

            this.scrollPosition[1] = GUILayout.BeginScrollView(this.scrollPosition[1]);

            GUILayout.Label("MainAppDomain");
            DrawAppDomainAssembly(AppDomain.CurrentDomain);

            if (PDebugReloaded.customCodeAppDomain != null){
                GUILayout.Label("CustomCodeAppDomain");
                DrawAppDomainAssembly(PDebugReloaded.customCodeAppDomain);
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void DrawAppDomainAssembly(AppDomain appDomain){
            GUILayout.BeginHorizontal();
            this.editValueInput[5] = GUILayout.TextField(this.editValueInput[5]);
            if (GUILayout.Button("Load")){
                if (appDomain != AppDomain.CurrentDomain)
                    PDebugReloaded.customCodeAppDomain.Load(AssemblyName.GetAssemblyName(this.editValueInput[5]));
                else
                    Assembly.LoadFrom(this.editValueInput[5]);

                this.editValueInput[5] = string.Empty;
            }

            GUILayout.EndHorizontal();

            foreach (Assembly assembly in appDomain.GetAssemblies()){
                GUILayout.BeginHorizontal();

                GUILayout.Label(assembly.GetName().Name);
                if (GUILayout.Button("Types")){
                    this.typeList = null;
                    this.targetAssembly = assembly;
                    this.assemblyWindowEnable = 2;
                }

                GUILayout.EndHorizontal();
            }

            if (appDomain != AppDomain.CurrentDomain && GUILayout.Button("Delete & Add " + appDomain.FriendlyName))
                UnloadAndAddCustomCodeAppDomain();
        }

        private void UnloadAndAddCustomCodeAppDomain(bool tryToCopyTheAssemblies = false, string exceptLocation = ""){
#if UNITY_2018_1_OR_NEWER
            List<string> assemblies = new List<string>();
            if (tryToCopyTheAssemblies){
                foreach (Assembly assembly in PDebugReloaded.customCodeAppDomain.GetAssemblies())
                    assemblies.Add(assembly.Location);
            }

            AppDomain.Unload(PDebugReloaded.customCodeAppDomain);
            PDebugReloaded.customCodeAppDomain = null;
            PDebugReloaded.customCodeAppDomain = AppDomain.CreateDomain("CustomCodeAppDomain");

            if (tryToCopyTheAssemblies){
                foreach (string assembly in assemblies){
                    try{
                        if (!assembly.EndsWith(exceptLocation) && !string.IsNullOrEmpty(assembly))
                            PDebugReloaded.customCodeAppDomain.Load(AssemblyName.GetAssemblyName(assembly));
                    }catch(Exception e){
                        Debug.Log(assembly);
                    }
                }
            }
#endif
        }


        private List<Type> typeList = null;
        private void AssemblyTypesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), "") | this.targetAssembly == null)
                this.assemblyWindowEnable = 0;

            this.scrollPosition[1] = GUILayout.BeginScrollView(this.scrollPosition[1]);

            if (GUILayout.Button("Back"))
                this.assemblyWindowEnable = 1;

            if (this.typeList == null){
                this.typeList = new List<Type>();
                this.typeList.AddRange(this.targetAssembly.GetTypes());
                this.typeList = this.typeList.OrderBy(m => m.Name).ToList();
            }

            int i = 0;
            foreach (Type type in this.typeList){
                GUILayout.BeginHorizontal();

                GUILayout.Label(type.Name);

                if (type.IsSubclassOf(typeof(Component))){
                    GUILayout.Label(" (Mono)");
                    if (this.targetObject != null && GUILayout.Button("Add to GameObject"))
                        this.targetObject.AddComponent(type);

                    if (GUILayout.Button("Instances")){
                        this.targetType = type;
                        this.assemblyWindowEnable = 3;
                    }
                }

                if (GUILayout.Button("CreateInstance")){
                    this.targetAssembly.CreateInstance(type.Name);
                }

                GUILayout.EndHorizontal();
                i++;
            }

            i = 0;

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private Type targetType = null;
        private void AssemblyTypeInstancesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), "") | this.targetType == null)
                this.assemblyWindowEnable = 0;

            this.scrollPosition[1] = GUILayout.BeginScrollView(this.scrollPosition[1]);

            if (GUILayout.Button("Back"))
                this.assemblyWindowEnable = 2;

            foreach (var obj in Resources.FindObjectsOfTypeAll(this.targetType)){
                GUILayout.BeginHorizontal();
                GUILayout.Label(obj.name);
                if (GUILayout.Button("Set TargetObject")){
                    Component comp = (Component)obj;
                    this.targetObject = comp.gameObject;
                }
                GUILayout.EndHorizontal();
            }


            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void OnApplicationQuit(){
            UnloadAndAddCustomCodeAppDomain();  
        }

        private void CustomWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(6, 35);

            this.scrollPosition[0] = GUILayout.BeginScrollView(this.scrollPosition[0]);

            GUILayout.BeginVertical();

            if (GUILayout.Button("GC.Collect()"))
                GC.Collect();
            
            /*This window is for buttons, for specific game functions*/

            GUILayout.EndVertical();

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        #region Experiment
        private bool experimentWindowEnabled = false;
        private List<ObjectSave> objectSaves = new List<ObjectSave>();
        private List<GameObject> changedObjects = new List<GameObject>();

        private class ObjectSave{
            public int instanceId;
            public GameObject gm;
            public Vector3 pos;
            public Vector3 scale;
            public Quaternion rot;

            public ObjectSave(int id, GameObject gm, Vector3 pos, Vector3 scale, Quaternion rot){
                this.instanceId = id;
                this.gm = gm;
                this.pos = pos;
                this.scale = scale;
                this.rot = rot;
            }
        }

        private void ExperimentWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            DrawCloseButton(6, 36);

            if (GUILayout.Button("Save objects data")){
                List<GameObject> gms = new List<GameObject>(FindObjectsOfType<GameObject>());

                this.objectSaves.Clear();

                foreach(GameObject gm in gms)
                    this.objectSaves.Add(new ObjectSave(gm.GetInstanceID(), gm, gm.transform.position, gm.transform.localScale, gm.transform.rotation));
            }

            if(GUILayout.Button("Load objects data")){
                List<GameObject> gms = new List<GameObject>(FindObjectsOfType<GameObject>());

                this.changedObjects.Clear();

                foreach (ObjectSave save in this.objectSaves){
                    if (save.gm == null)
                        continue;

                    foreach(GameObject gm in gms){
                        if(gm.GetInstanceID() == save.instanceId){
                            if (gm.transform.position != save.pos || gm.transform.localScale != save.scale || gm.transform.rotation != save.rot)
                                this.changedObjects.Add(gm);

                            gm.transform.position = save.pos;
                            gm.transform.localScale = save.scale;
                            gm.transform.rotation = save.rot;
                            break;
                        }
                    }
                }
            }

            if(this.changedObjects.Count > 0){
                GUILayout.Label("Changed objects:");

                this.scrollPosition[0] = GUILayout.BeginScrollView(this.scrollPosition[0]);

                foreach(GameObject gm in this.changedObjects){
                    if(gm == null)
                        continue;
                    GUILayout.BeginHorizontal();

                    GUILayout.Label(gm.name);
                    if (GUILayout.Button("Get"))
                        this.targetObject = gm;

                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
            }

            GUI.DragWindow();
        }

        #endregion

        private void CheckTargetCameraMod(){
            if (this.targetCamera.gameObject.GetComponent<RenderCameraMod>() == null)
                this.targetCamera.gameObject.AddComponent<RenderCameraMod>();
        }
        #endregion

        private void HandleLog(string logString, string stackTrace, UnityEngine.LogType type){
            this.logs.Add(new LogEntry(logString, stackTrace, type));
        }

        public static float StringToFloat(string text){
            float output = 0;
            float.TryParse(text, out output);

            return output;
        }

        private struct LogEntry{
            public string logString;
            public string stackTrace;
            public UnityEngine.LogType type;

            public LogEntry(string logString, string stackTrace, UnityEngine.LogType type){
                this.logString = logString;
                this.stackTrace = stackTrace;
                this.type = type;
            }
        }

    }

    public class FreeCamController{

        public static bool useUnscaledDeltaTime = true;

        public Transform transform;

        public float moveSpeed = 8;
        public float fastMoveSpeed = 25;
        public float sensitivity = 3;

        private bool canMove = true;

        public void Update(){
            float deltaTime = Time.deltaTime;
            if (FreeCamController.useUnscaledDeltaTime)
                deltaTime = Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.E))
                this.canMove = !this.canMove;

            if (Input.GetKey(KeyCode.U))
                this.transform.Rotate(Vector3.forward * 20 * deltaTime);
            else if (Input.GetKey(KeyCode.J))
                this.transform.Rotate(Vector3.back * 20 * deltaTime);
            if (Input.GetKey(KeyCode.K))
                this.transform.Rotate(Vector3.right * 20 * deltaTime);
            else if (Input.GetKey(KeyCode.H))
                this.transform.Rotate(Vector3.left * 20 * deltaTime);

            if (!this.canMove)
                return;

            float speed = this.moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed = this.fastMoveSpeed;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                this.transform.position = this.transform.position + (-this.transform.right * speed * deltaTime);

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                this.transform.position = this.transform.position + (this.transform.right * speed * deltaTime);

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                this.transform.position = this.transform.position + (this.transform.forward * speed * deltaTime);

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                this.transform.position = this.transform.position + (-this.transform.forward * speed * deltaTime);

            float newRotationX = this.transform.localEulerAngles.y + Input.GetAxisRaw("Mouse X") * sensitivity;
            float newRotationY = this.transform.localEulerAngles.x - Input.GetAxisRaw("Mouse Y") * sensitivity;
            this.transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
        }

    }

    public class RenderCameraMod : MonoBehaviour{

        public bool wireframe = false;
        public bool drawCollision = false;

        private Material material;
        private LineRenderer lineRenderer;

        private void Awake(){
            Shader standardShader = Shader.Find("Standard");
            this.material = new Material(standardShader);
            this.material.SetColor("Albedo", Color.red);
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        private void OnPreRender(){
            GL.wireframe = this.wireframe;
        }

        private void OnPostRender(){
            if (!this.drawCollision)
                return;

            if (PDebugReloaded.instance.is3D){
                //Made with AI, because i'm bad at math
                GL.PushMatrix();
                material.SetPass(0);
                GL.LoadProjectionMatrix(Camera.current.projectionMatrix);
                GL.modelview = Camera.current.worldToCameraMatrix;

                GL.Begin(GL.LINES);
                GL.Color(Color.red);

                bool extraCheck = !PDebugReloaded.instance.drawCollToggles[0] | !PDebugReloaded.instance.drawCollToggles[1] | !PDebugReloaded.instance.drawCollToggles[2] | !PDebugReloaded.instance.drawCollToggles[3];

                foreach (Collider collider in FindObjectsOfType<Collider>())
                {
                    if (!collider.enabled)
                        continue;
                    if (extraCheck)
                    {
                        if (!PDebugReloaded.instance.drawCollToggles[0] && collider is MeshCollider)
                            continue;
                        else if (!PDebugReloaded.instance.drawCollToggles[1] &&
                            (!(collider is BoxCollider) && !(collider is SphereCollider) && !(collider is MeshCollider) && !(collider is CapsuleCollider)))
                            continue;
                        else if (!PDebugReloaded.instance.drawCollToggles[2] && collider.isTrigger)
                            continue;
                        else if (!PDebugReloaded.instance.drawCollToggles[3] && !collider.isTrigger)
                            continue;
                    }

                    Bounds bounds = collider.bounds;
                    Vector3 min = bounds.min;
                    Vector3 max = bounds.max;

                    // Bottom lines
                    GL.Vertex(new Vector3(min.x, min.y, min.z));
                    GL.Vertex(new Vector3(max.x, min.y, min.z));

                    GL.Vertex(new Vector3(max.x, min.y, min.z));
                    GL.Vertex(new Vector3(max.x, min.y, max.z));

                    GL.Vertex(new Vector3(max.x, min.y, max.z));
                    GL.Vertex(new Vector3(min.x, min.y, max.z));

                    GL.Vertex(new Vector3(min.x, min.y, max.z));
                    GL.Vertex(new Vector3(min.x, min.y, min.z));

                    // Top lines
                    GL.Vertex(new Vector3(min.x, max.y, min.z));
                    GL.Vertex(new Vector3(max.x, max.y, min.z));

                    GL.Vertex(new Vector3(max.x, max.y, min.z));
                    GL.Vertex(new Vector3(max.x, max.y, max.z));

                    GL.Vertex(new Vector3(max.x, max.y, max.z));
                    GL.Vertex(new Vector3(min.x, max.y, max.z));

                    GL.Vertex(new Vector3(min.x, max.y, max.z));
                    GL.Vertex(new Vector3(min.x, max.y, min.z));

                    // Side lines
                    GL.Vertex(new Vector3(min.x, min.y, min.z));
                    GL.Vertex(new Vector3(min.x, max.y, min.z));

                    GL.Vertex(new Vector3(max.x, min.y, min.z));
                    GL.Vertex(new Vector3(max.x, max.y, min.z));

                    GL.Vertex(new Vector3(max.x, min.y, max.z));
                    GL.Vertex(new Vector3(max.x, max.y, max.z));

                    GL.Vertex(new Vector3(min.x, min.y, max.z));
                    GL.Vertex(new Vector3(min.x, max.y, max.z));
                }
                GL.End();
                GL.PopMatrix();
            }else{/*Also AI Code*/
                GL.PushMatrix();
                material.SetPass(0);
                GL.LoadOrtho();

                GL.Begin(GL.LINES);

                bool extraCheck = !PDebugReloaded.instance.drawCollToggles[2] | !PDebugReloaded.instance.drawCollToggles[3];
                var colliders = FindObjectsOfType<Collider2D>();
                foreach (var collider in colliders)
                {
                    if (!collider.enabled)
                        continue;

                    if(collider.isTrigger)
                        GL.Color(Color.green);
                    else
                        GL.Color(Color.red);
                  
                    if (extraCheck){
                        if (!PDebugReloaded.instance.drawCollToggles[2] && collider.isTrigger)
                            continue;
                        else if (!PDebugReloaded.instance.drawCollToggles[3] && !collider.isTrigger)
                            continue;
                    }
                    if (collider is BoxCollider2D boxCollider)
                    {
                        DrawBoxCollider2D(boxCollider);
                    }
                    else if (collider is CircleCollider2D sphereCollider)
                    {
                        DrawSphereCollider2D(sphereCollider);
                    }
                    else if (collider is CapsuleCollider2D capsuleCollider)
                    {
                        DrawCapsuleCollider2D(capsuleCollider);
                    }
                    else if (collider is TilemapCollider2D tilemapCollider)
                    {
                        DrawTilemapCollider2D(tilemapCollider);
                    }
                    else if (collider is EdgeCollider2D edgeCollider)
                    {
                        DrawEdgeCollider2D(edgeCollider);
                    }
                }

                GL.End();
                GL.PopMatrix();
            }
        }

        void DrawBoxCollider2D(BoxCollider2D collider)
        {
            var center = collider.bounds.center;
            var size = collider.size;
            var halfSize = size * 0.5f;
            var min = center - (Vector3)halfSize;
            var max = center + (Vector3)halfSize;

            // Zeichne die Box Collider-Kanten
            DrawRect(min, max);
        }

        void DrawSphereCollider2D(CircleCollider2D collider)
        {
            var center = collider.bounds.center;
            var radius = collider.radius;

            // Zeichne die Kanten des Sphere Colliders
            DrawCircle(center, radius);
        }

        void DrawCapsuleCollider2D(CapsuleCollider2D collider)
        {
            var center = collider.bounds.center;
            var size = collider.size;
            var radius = collider.size.x * 0.5f;
            var height = collider.size.y - collider.size.x;

            // Zeichne die Kanten des Capsule Colliders
            DrawCapsule(center, radius, height);
        }

        void DrawTilemapCollider2D(TilemapCollider2D collider)
        {
            var bounds = collider.bounds;

            // Zeichne die Kanten des Tilemap Colliders
            DrawRect(bounds.min, bounds.max);
        }

        void DrawEdgeCollider2D(EdgeCollider2D collider)
        {
            var points = collider.points;
            var startPoint = collider.bounds.min;

            // Zeichne die Kanten des Edge Colliders
            for (int i = 0; i < points.Length - 1; i++)
            {
                var point1 = startPoint + (Vector3)points[i];
                var point2 = startPoint + (Vector3)points[i + 1];
                GL.Vertex3(point1.x, point1.y, 0);
                GL.Vertex3(point2.x, point2.y, 0);
            }
        }

        // Zeichne ein Rechteck (Box) mit den angegebenen Eckpunkten
        void DrawRect(Vector2 min, Vector2 max)
        {
            GL.Vertex3(min.x, min.y, 0);
            GL.Vertex3(max.x, min.y, 0);

            GL.Vertex3(max.x, min.y, 0);
            GL.Vertex3(max.x, max.y, 0);

            GL.Vertex3(max.x, max.y, 0);
            GL.Vertex3(min.x, max.y, 0);

            GL.Vertex3(min.x, max.y, 0);
            GL.Vertex3(min.x, min.y, 0);
        }

        // Zeichne einen Kreis (Sphere) mit dem angegebenen Mittelpunkt und Radius
        void DrawCircle(Vector2 center, float radius)
        {
            const int segments = 32;
            float anglePerSegment = (Mathf.PI * 2f) / segments;

            for (int i = 0; i < segments; i++)
            {
                var angle = i * anglePerSegment;
                var startPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var endPoint = center + new Vector2(Mathf.Cos(angle + anglePerSegment), Mathf.Sin(angle + anglePerSegment)) * radius;

                GL.Vertex3(startPoint.x, startPoint.y, 0);
                GL.Vertex3(endPoint.x, endPoint.y, 0);
            }
        }

        // Zeichne eine Kapsel (Capsule) mit dem angegebenen Mittelpunkt, Radius und Höhe
        void DrawCapsule(Vector2 center, float radius, float height)
        {
            var halfHeight = height * 0.5f;
            var top = center + Vector2.up * halfHeight;
            var bottom = center + Vector2.down * halfHeight;

            // Zeichne die Kanten der Kapsel
            DrawCircle(top, radius);
            DrawCircle(bottom, radius);
            DrawRect(top - new Vector2(radius, 0), bottom + new Vector2(radius, 0));
        }

    }

}
