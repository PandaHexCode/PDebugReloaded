using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;
using System.Text;
using System.IO;


namespace PandaHexCode.PDebug{

    public class Injector{
        public static void Inject(){
            new Thread(() =>{
                Thread.Sleep(5000);

                GameObject someObject = new GameObject();
                someObject.AddComponent<PDebugReloaded>();
                UnityEngine.Object.DontDestroyOnLoad(someObject);
            }).Start();
        }
    }

    public class PDebugReloaded : MonoBehaviour{

        public static PDebugReloaded instance = null;

        private string NAME = "PDebugReloaded";
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);

        private enum State { Console = 0, Objects = 1, Time = 2, Scene = 3, Application = 4, Other = 5, Custom = 6};
        private State currentState = State.Console;

        private GameObject targetObject = null;/*Current GameObject in Objects Tab*/
        private Camera targetCamera = null;/*Camera that calculating the Ray*/
        private UnityEngine.Component targetComponent = null;
        private MethodInfo targetMethod;
        private PropertyInfo targetPropertyInfo;
        private FieldInfo targetFieldInfo;
        private bool isTargetField = false;

        private List<LogEntry> logs = new List<LogEntry>();/*For the console window*/

        private bool showMenu = true;
        private bool is3D = true;/*This Script uses a other Ray for 3D Games!*/
        private bool logExceptions = false;/*True needs lot more Performance!*/
        private bool viewCursor = true;

        private Vector2[] scrollPosition = new Vector2[8];/*For the OnGUI windows*/

        private void Awake(){
            this.targetCamera = Camera.main;

            /*Check if an instance allready exits and if exits destroy this, else create a new gameobject with the instance*/
            if (PDebugReloaded.instance == null){
                if (this.gameObject.name != this.NAME + "-INSTANCE"){
                    GameObject gm = new GameObject(this.NAME + "-INSTANCE");
                    PDebugReloaded.instance = gm.AddComponent<PDebugReloaded>();
                    PDebugReloaded.instance.logExceptions = this.logExceptions;
                    PDebugReloaded.instance.backgroundColor = this.backgroundColor;
                    DontDestroyOnLoad(instance);
                    Destroy(this);
                }
            }else if (PDebugReloaded.instance != this)
                Destroy(this);

            Application.logMessageReceived += HandleLog;/*Register Console Log*/
        }

        private void Update(){/*For TargetGameObject find*/
            if (this.viewCursor){
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }else{
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            if (Input.GetKeyDown(KeyCode.C) && Input.GetKey(KeyCode.Tab))
                this.viewCursor = !this.viewCursor;

            if (Input.GetKeyDown(KeyCode.M) && Input.GetKey(KeyCode.Tab))
                this.showMenu = !this.showMenu;

            if (Input.GetKeyDown(KeyCode.P) && Input.GetKey(KeyCode.Tab)){
                if (Time.timeScale == 0)
                    Time.timeScale = 1;
                else
                    Time.timeScale = 0;
            }

            if (this.currentState == State.Objects) {
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

        private void OnGUI(){/*Draw the current window*/
            GUI.backgroundColor = this.backgroundColor;

            GUI.Label(new Rect(Screen.width - 218, 0, 500, 50), "PDebugReloaded by PandaHexCode");

            if (!this.showMenu)
                return;

            DrawMainButtons();

            switch (this.currentState){

                case State.Console:
                    GUI.Window(0, new Rect(10, 40, 300, 200), ConsoleWindow, "Console");
                    break;

                case State.Objects:
                    GUI.Window(0, new Rect(10, 40, 380, 160), ObjectsWindow, "GameObjects");
                    
                    if (this.objectPosRotScaEditWindowEnable)
                        GUI.Window(1, new Rect(395, 40, 245, 180), ObjectPosRotScaEditWindow, "Edit");
                    
                    if(this.objectComponentsWindowEnable)
                        GUI.Window(2, new Rect(10, 205, 380, 140), ObjectComponentsWindow, "Components");

                    if (this.objectComponentValuesWindowEnable != 0){
                        int xPos = 10;
                        if (this.objectComponentsWindowEnable)
                            xPos = 395;
                      
                        if(this.objectComponentValuesWindowEnable == 1)
                            GUI.Window(5, new Rect(xPos, 205, 380, 140), ObjectComponentValuesWindow, "Values");
                        else
                            GUI.Window(5, new Rect(xPos, 205, 380, 140), EditValueWindow, "EditValue - " + this.targetPropertyInfo.Name);
                    }else if(this.objectComponentMethodesWindowEnable != 0){
                        int xPos = 10;
                        if (this.objectComponentsWindowEnable)
                            xPos = 395;

                        if (this.objectComponentMethodesWindowEnable == 1)
                            GUI.Window(5, new Rect(xPos, 205, 380, 140), ObjectComponentMethodesWindow, "Methodes");
                        else
                            GUI.Window(5, new Rect(xPos, 205, 380, 140), EditMethodInvokeWindow, "EditInvoke - " + this.targetMethod.Name);
                    }

                    if (this.objectChildWindowEnable){
                        if(!this.objectPosRotScaEditWindowEnable)
                            GUI.Window(1, new Rect(395, 40, 245, 180), ObjectChildrenWindow, "Children");
                        else
                            GUI.Window(3, new Rect(645, 40, 245, 180), ObjectChildrenWindow, "Children");
                    }

                    if (this.targetCameraWindowEnable){
                        if (!this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable)
                            GUI.Window(1, new Rect(395, 40, 245, 180), TargetCameraWindow, "TargetCamera");
                        else if ((this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable) | (this.objectChildWindowEnable && !this.objectPosRotScaEditWindowEnable))
                            GUI.Window(3, new Rect(645, 40, 245, 180), TargetCameraWindow, "TargetCamera");
                        else
                            GUI.Window(4, new Rect(895, 40, 245, 180), TargetCameraWindow, "TargetCamera");
                    }else if (this.objectLayerWindowEnable){
                        if (!this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable)
                            GUI.Window(1, new Rect(395, 40, 245, 180), ObjectLayerWindow, "Layer");
                        else if ((this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable) | (this.objectChildWindowEnable && !this.objectPosRotScaEditWindowEnable))
                            GUI.Window(3, new Rect(645, 40, 245, 180), ObjectLayerWindow, "Layer");
                        else
                            GUI.Window(4, new Rect(895, 40, 245, 180), ObjectLayerWindow, "Layer");
                    }else if (this.objectGetObjectByNameWindowEnable){
                        if (!this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable)
                            GUI.Window(1, new Rect(395, 40, 245, 180), ObjectGetObjectByNameWindow, "FindObject");
                        else if ((this.objectPosRotScaEditWindowEnable && !this.objectChildWindowEnable) | (this.objectChildWindowEnable && !this.objectPosRotScaEditWindowEnable))
                            GUI.Window(3, new Rect(645, 40, 245, 180), ObjectGetObjectByNameWindow, "FindObject");
                        else
                            GUI.Window(4, new Rect(895, 40, 245, 180), ObjectGetObjectByNameWindow, "FindObject");
                    }
                    break;

                case State.Time:
                    GUI.Window(0, new Rect(10, 40, 380, 150), TimeWindow, "Time");
                    break;

                case State.Scene:
                    GUI.Window(0, new Rect(10, 40, 380, 150), SceneWindow, "Scene");
                    break;

                case State.Application:
                    GUI.Window(0, new Rect(10, 40, 380, 160), ApplicationWindow, "Application");
                    break;

                case State.Other:
                    GUI.Window(0, new Rect(10, 40, 380, 150), OtherWindow, "Other");

                    if(this.otherResourcesWindowEnable)
                        GUI.Window(1, new Rect(395, 40, 245, 180), ResourcesWindow, "Resources");
                    
                    if (this.otherPhysicsWindowEnable)
                        GUI.Window(2, new Rect(10, 205, 500, 140), PhysicsWindow, "Physics");

                    if (this.otherRenderWindowEnable){
                        if (this.otherPhysicsWindowEnable)
                            GUI.Window(5, new Rect(515, 205, 380, 140), RenderWindow, "Render\nTargetCamera: " + this.targetCamera.name);
                        else
                            GUI.Window(5, new Rect(10, 205, 380, 140), RenderWindow, "Render\nTargetCamera: " + this.targetCamera.name);
                    }
                    break;

                case State.Custom:
                    GUI.Window(0, new Rect(10, 40, 380, 150), CustomWindow, "Custom");
                    break;

            }
        }

        private void DrawMainButtons(){
            if (GUI.Button(new Rect(10f, 10f, 70f, 20f), "Console"))
                this.currentState = State.Console;
            if (GUI.Button(new Rect(85f, 10f, 70f, 20f), "Objects"))
                this.currentState = State.Objects;
            if (GUI.Button(new Rect(160f, 10f, 70f, 20f), "Time"))
                this.currentState = State.Time;
            if (GUI.Button(new Rect(235f, 10f, 70f, 20f), "Scene"))
                this.currentState = State.Scene;
            if (GUI.Button(new Rect(310f, 10f, 80f, 20f), "Application"))
                this.currentState = State.Application;
            if (GUI.Button(new Rect(395f, 10f, 70f, 20f), "Other"))
                this.currentState = State.Other;
        }

        private void ConsoleWindow(int windowID) {
            GUI.backgroundColor = this.backgroundColor;

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
        }

        private GameObject copiedGameObject = null;
        private void ObjectsWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0f, 0f, 100f, 20f), "Target Camera")){
                this.targetCameraWindowEnable = !this.targetCameraWindowEnable;
                this.objectLayerWindowEnable = false;
                this.objectGetObjectByNameWindowEnable = false;
            }

            if(this.targetObject == null){
                GUI.Label(new Rect(10f, 30f, 500f, 100f), "Please click on an GameObject and press \"i\"!");
                return;
            }

            GUI.Label(new Rect(10f, 30f, 200f, 50f), "Target GameObject: " + this.targetObject.name + "\nTransform instance id: " + this.targetObject.transform.GetInstanceID());

            if (GUI.Button(new Rect(10f, 100f, 70f, 20f), "Destroy"))
                Destroy(this.targetObject);

            if (GUI.Button(new Rect(10f, 75f, 40f, 20f), "Copy"))
                this.copiedGameObject = this.targetObject;

            if (this.copiedGameObject != null && GUI.Button(new Rect(55f, 75f, 25f, 20f), "P")){
                GameObject obj = Instantiate(this.copiedGameObject);
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
        }

        private bool objectComponentsWindowEnable = false;
        private Component copiedComponent = null;
        private void ObjectComponentsWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectComponentsWindowEnable = false;

            if (this.targetObject == null)
                return;

            this.scrollPosition[1] = GUILayout.BeginScrollView(this.scrollPosition[1]);

            if (this.copiedComponent != null && GUILayout.Button("Paste"))
                this.targetObject.AddComponent(this.copiedComponent.GetType());

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

                if (GUILayout.Button("Copy"))
                    this.copiedComponent = comp;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private int objectComponentValuesWindowEnable = 0 /*0 = false, 1 = NormalValuesWindow, 2 = EditValuesWindow, Good cast = 3, bad cast = 4*/;
        private void ObjectComponentValuesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectComponentValuesWindowEnable = 0;

            if (this.targetComponent == null)
                return;

            this.scrollPosition[4] = GUILayout.BeginScrollView(this.scrollPosition[4]);


            foreach(FieldInfo field in this.targetComponent.GetType().GetFields(BindingFlags.Static | BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.ExactBinding | BindingFlags.GetField | BindingFlags.SetProperty | BindingFlags.GetProperty | BindingFlags.SetField | BindingFlags.SetProperty)){
                try{
                    if (field.Name.Equals("rigidbody") | field.Name.Equals("rigidbody2D") | field.Name.Equals("camera"))
                        continue;

                    var value = field.GetValue(this.targetComponent);

                    GUILayout.BeginHorizontal();

                    GUILayout.Label(field.Name + " - " + value);

                    if (GUILayout.Button("Edit")){
                        this.targetFieldInfo = field;
                        this.editValueInput = value.ToString().Replace("(", "").Replace(")", "");
                        this.objectComponentValuesWindowEnable = 2;
                        this.isTargetField = true;
                    }

                    GUILayout.EndHorizontal();
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

                    GUILayout.BeginHorizontal();

                    GUILayout.Label(prop.Name + " - " + value);

                    if (GUILayout.Button("Edit")){
                        this.targetPropertyInfo = prop;
                        this.editValueInput = value.ToString().Replace("(", "").Replace(")", "");
                        this.objectComponentValuesWindowEnable = 2;
                        this.isTargetField = false;
                    }

                    GUILayout.EndHorizontal();
                }catch (Exception e){
                    if (this.logExceptions)
                        Debug.Log(e.Message + "|" + e.StackTrace);
                    continue;
                } 
            }

            GUILayout.EndScrollView();
        }

        private string editValueInput = string.Empty;
        private object copiedVar = null;
        private void EditValueWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (this.targetComponent == null | (this.targetPropertyInfo == null && this.targetFieldInfo == null))
                return;

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Back"))
                this.objectComponentValuesWindowEnable = 1;

            if (GUILayout.Button("CopyVar")){
                if(this.isTargetField)
                    this.copiedVar = this.targetFieldInfo.GetValue(this.targetComponent);
                else
                    this.copiedVar = this.targetPropertyInfo.GetValue(this.targetComponent, null);
            }

            if (this.copiedVar != null && GUILayout.Button("PasteVar")){
                if((!this.isTargetField && this.copiedVar.GetType() != this.targetPropertyInfo.GetValue(this.targetComponent, null).GetType())
                    | (this.isTargetField && this.copiedVar.GetType() != this.targetFieldInfo.GetValue(this.targetComponent).GetType())){
                    this.objectComponentValuesWindowEnable = 4;
                    return;
                }

                if (this.isTargetField)
                    this.targetFieldInfo.SetValue(this.targetComponent, this.copiedVar);
                else
                    this.targetPropertyInfo.SetValue(this.targetComponent, this.copiedVar, null);

                this.objectComponentValuesWindowEnable = 3;
            }

            GUILayout.EndHorizontal();

            this.editValueInput = GUILayout.TextField(this.editValueInput);

            if(GUILayout.Button("Try to cast")){
                try{

                    if (!this.isTargetField){
                        object var = TryToCastString(this.editValueInput, this.targetPropertyInfo.GetValue(this.targetComponent, null).GetType());
                        if (var == null){
                            this.objectComponentValuesWindowEnable = 4;
                            return;
                        }else
                            this.objectComponentValuesWindowEnable = 3;
                        this.targetPropertyInfo.SetValue(this.targetComponent, var, null);
                    }else{
                        object var = TryToCastString(this.editValueInput, this.targetFieldInfo.GetValue(this.targetComponent).GetType());
                        if (var == null){
                            this.objectComponentValuesWindowEnable = 4;
                            return;
                        }else
                            this.objectComponentValuesWindowEnable = 3;
                        this.targetFieldInfo.SetValue(this.targetComponent, var);
                    }
                
                }catch (Exception e){
                    this.objectComponentValuesWindowEnable = 4;
                }
            }

            if (this.objectComponentValuesWindowEnable == 3)
                GUILayout.Label("Last cast was successfuly!");
            else if (this.objectComponentValuesWindowEnable == 4)
                GUILayout.Label("Last cast was not successfuly!");

            GUILayout.EndVertical();
        }

        private int objectComponentMethodesWindowEnable = 0 /*0 = false, 1 = NormalMethodsWindow, 2 = EditMethodsWindow, Good cast = 3, bad cast = 4*/;
        private void ObjectComponentMethodesWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (this.targetComponent == null)
                return;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectComponentMethodesWindowEnable = 0;

            this.scrollPosition[5] = GUILayout.BeginScrollView(this.scrollPosition[5]);

            foreach(MethodInfo method in this.targetComponent.GetType().GetMethods()){
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
                        method.Invoke(this.targetComponent, null);
                    else{
                        this.objectComponentMethodesWindowEnable = 2;
                        this.targetMethod = method;

                        string parms = string.Empty;
                        foreach (ParameterInfo parm in this.targetMethod.GetParameters()){
                            if (parms != string.Empty)
                                parms = parms + "#";

                            parms = parms + parm.ParameterType;
                        }

                        this.editValueInput = parms;
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void EditMethodInvokeWindow(int windowID){
             GUI.backgroundColor = this.backgroundColor;

            if (this.targetComponent == null | this.targetMethod == null)
                return;

            GUILayout.BeginVertical();

            if (GUILayout.Button("Back"))
                this.objectComponentMethodesWindowEnable = 1;

            string parms = string.Empty;
            foreach (ParameterInfo parm in this.targetMethod.GetParameters()){
                if (parms != string.Empty)
                    parms = parms + ",";

                parms = parms + parm.ParameterType + " " + parm.Name;
            }

            GUILayout.Label(this.targetMethod.Name + "(" + parms + ")");

            this.editValueInput = GUILayout.TextField(this.editValueInput);

            if(GUILayout.Button("Try to cast & invoke")){
                try{
                    int i = 0;
                    string[] args = this.editValueInput.Split('#');
                    object[] parameters = new object[this.targetMethod.GetParameters().Length];
                    foreach (ParameterInfo p in this.targetMethod.GetParameters()){
                        parameters[i] = TryToCastString(args[i], p.ParameterType);
                        if(parameters[i] == null){
                            this.objectComponentMethodesWindowEnable = 4;
                            return;
                        }
                        i++;
                    }
                    
                    this.targetMethod.Invoke(this.targetComponent, parameters);
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
        }

        public object TryToCastString(string valStr, Type type){
            object var = null;

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
                if (valStr.Equals("false", StringComparison.OrdinalIgnoreCase))
                    var = false;
                else
                    var = true;
            }else if (type.Equals(typeof(string)) | type.Equals(typeof(String)))
                var = (string) valStr;
             else if (type.Equals(typeof(Int32))){
                   var = valStr;
            Int32 output = 0;
                Int32.TryParse(valStr, out output);
                var = output;
            }else if (type.Equals(typeof(Int16))){
                Int16 output = 0;
                Int16.TryParse(valStr, out output);
                var = output;
            }else if (type.Equals(typeof(Int64))){
                Int64 output = 0;
                Int64.TryParse(valStr, out output);
                var = output;
            }else if (type.Equals(typeof(Color))){
                Color color = Color.white;
                valStr = valStr.Replace("#", "");
                ColorUtility.TryParseHtmlString("#" + valStr, out color);
                var = color;
            }else if (type.Equals(typeof(Color32))){
                Color color = Color.white;
                valStr = valStr.Replace("#", "");
                ColorUtility.TryParseHtmlString("#" + valStr, out color);
                var = (Color32)color;
            }

            return var;
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
        }

        private bool objectGetObjectByNameWindowEnable = false;
        private void ObjectGetObjectByNameWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.objectGetObjectByNameWindowEnable = false;

            GUILayout.BeginVertical();

            this.editValueInput = GUILayout.TextField(this.editValueInput);

            if (GUILayout.Button("Try to get"))
                this.targetObject = GameObject.Find(this.editValueInput);

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

            if (GUILayout.Button("Get first Directional Light"))
                this.targetObject = Light.GetLights(LightType.Directional, 0)[0].gameObject;

            GUILayout.EndVertical();
        }

        private string timeInput = "1";
        private void TimeWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            GUI.Label(new Rect(10f, 30f, 200f, 50f), "Delta Time: " + Time.deltaTime + "\nTime Scale: " + Time.timeScale);

            this.timeInput = GUI.TextField(new Rect(10f, 65f, 70, 18), this.timeInput);
            if (GUI.Button(new Rect(10f, 85f, 70f, 20f), "Change"))
                Time.timeScale = StringToFloat(this.timeInput);
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
        }

        private void SceneWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            this.scrollPosition[0] = GUILayout.BeginScrollView(this.scrollPosition[0]);

            GUILayout.Label("ActiveScene: " + SceneManager.GetActiveScene().name);

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++){
                GUILayout.BeginHorizontal();

                GUILayout.Label(i.ToString());
                if (GUILayout.Button("Load"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(i);

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void ApplicationWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if(GUI.Button(new Rect(10f, 30f, 200f, 15f), "RunInBackground: " + Application.runInBackground))
                Application.runInBackground = !Application.runInBackground;

            GUI.Label(new Rect(10f, 50f, 500f, 500f), "ProductName: "+ Application.productName + "\nCompanyName: " 
                + Application.companyName +"\nUnityVersion: " + Application.unityVersion
                + "\nTAB + , C switch cursor visibility\n,M switch menu visibility, P toggle pause" 
                + "\n" + this.NAME + " by PandaHexCode");

            if (GUI.Button(new Rect(215f, 30f, 100f, 15f), "Is3D: " + this.is3D))
                this.is3D = !this.is3D;
        }

        private Light customLight = null;
        private GameObject oldCamera;
        private FreeCamController freeCam;
        private void OtherWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            float fps = (int)(1f / Time.unscaledDeltaTime);

            GUI.Label(new Rect(10f, 30f, 200f, 50f), "FPS: " + fps);

            if (GUI.Button(new Rect(10f, 70f, 85f, 20f), "Resources"))
                this.otherResourcesWindowEnable = !this.otherResourcesWindowEnable;
            if (GUI.Button(new Rect(10f, 100f, 85f, 20f), "Physics"))
                this.otherPhysicsWindowEnable = !this.otherPhysicsWindowEnable;
            if (GUI.Button(new Rect(190f, 100f, 85f, 20f), "Render"))
                this.otherRenderWindowEnable = !this.otherRenderWindowEnable;
            if (GUI.Button(new Rect(200f, 70f, 85f, 20f), "Custom"))
                this.currentState = State.Custom;

            if (GUI.Button(new Rect(100f, 100f, 85f, 20f), this.customLight == null ?  "CLight: Off" : "CLight: On")){
                if (this.customLight != null)
                    Destroy(this.customLight.gameObject);
                else{
                    this.customLight = new GameObject("CustomLight").AddComponent<Light>();
                    this.customLight.type = LightType.Directional;
                    this.customLight.transform.localEulerAngles = new Vector3(50, -30, 0);
                }
            }

            if (GUI.Button(new Rect(100f, 70f, 95f, 20f), this.freeCam == null ? "FreeCam: Off" : "FreeCam: On")){
                if (this.freeCam != null){
                    Destroy(this.freeCam.gameObject);
                    this.oldCamera.gameObject.SetActive(true);
                    this.targetObject = this.oldCamera;
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
                this.freeCam = clon.AddComponent<FreeCamController>();
                this.targetCamera = clon.GetComponent<Camera>();
                this.targetCamera.Render();
                this.targetObject = this.targetCamera.gameObject;
                clon.name = "FreeCam";
                this.freeCam.gameObject.SetActive(true);
            }

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
        }

        private bool otherPhysicsWindowEnable = false;
        private string[] physicsInputs = new string[15];
        private void PhysicsWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            if (GUI.Button(new Rect(0, 0, 10, 10), ""))
                this.otherPhysicsWindowEnable = false;

             if (GUI.Button(new Rect(10f, 90f, 100f, 20f), "Reload")) {
                if (this.is3D){
                    this.physicsInputs[0] = Physics.gravity.x.ToString();
                    this.physicsInputs[1] = Physics.gravity.y.ToString();
                    this.physicsInputs[2] = Physics.gravity.z.ToString();
                }else{
                    this.physicsInputs[0] = Physics2D.gravity.x.ToString();
                    this.physicsInputs[1] = Physics2D.gravity.y.ToString();
                    this.physicsInputs[2] = "3D";
                }
            }
            if (GUI.Button(new Rect(112f, 90f, 80f, 20f), "Apply")){
                if (this.is3D){
                    Physics.gravity = new Vector3(StringToFloat(this.physicsInputs[0]), Physics.gravity.y, Physics.gravity.z);
                    Physics.gravity = new Vector3(Physics.gravity.x, StringToFloat(this.physicsInputs[1]), Physics.gravity.z);
                    Physics.gravity = new Vector3(Physics.gravity.x, Physics.gravity.y,StringToFloat(this.physicsInputs[2]));
                }else{
                    Physics2D.gravity = new Vector3(StringToFloat(this.physicsInputs[0]), Physics2D.gravity.y);
                    Physics2D.gravity = new Vector3(Physics2D.gravity.x, StringToFloat(this.physicsInputs[1]));
                }
            }

            GUI.Label(new Rect(10f, 30f, 400f, 50f), "Change Physics settings of ProjectSettings (Is3D: " + this.is3D + ")");
            this.physicsInputs[0] = GUI.TextField(new Rect(10f, 60f, 85f, 20f), "GravityX " + this.physicsInputs[0]).Split(' ')[1];
            this.physicsInputs[1] = GUI.TextField(new Rect(100f, 60f, 85f, 20f), "GravityY " + this.physicsInputs[1]).Split(' ')[1];
            this.physicsInputs[2] = GUI.TextField(new Rect(190f, 60f, 85f, 20f), "GravityZ " + this.physicsInputs[2]).Split(' ')[1];

            if (this.is3D){
                if (GUI.Button(new Rect(280f, 60f, 195f, 20f), "Queries Hit Triggers: " + Physics.queriesHitTriggers))
                    Physics.queriesHitTriggers = !Physics.queriesHitTriggers;
            }else{
                if (GUI.Button(new Rect(290f, 60f, 195f, 20f), "Queries Hit Triggers: " + Physics2D.queriesHitTriggers))
                    Physics2D.queriesHitTriggers = !Physics2D.queriesHitTriggers;
            }
        }

        private bool otherRenderWindowEnable = false;
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
        }

        private void CustomWindow(int windowID){
            GUI.backgroundColor = this.backgroundColor;

            this.scrollPosition[0] = GUILayout.BeginScrollView(this.scrollPosition[0]);

            GUILayout.BeginVertical();

            if (GUILayout.Button("Back"))
                this.currentState = State.Other;

            /*This window is for buttons, for specific game functions*/

            GUILayout.EndVertical();

            GUILayout.EndScrollView();
        }

        private void CheckTargetCameraMod(){
            if (this.targetCamera.gameObject.GetComponent<RenderCameraMod>() == null)
                this.targetCamera.gameObject.AddComponent<RenderCameraMod>();
        }

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

    public class FreeCamController : MonoBehaviour{

        public float moveSpeed = 8;
        public float fastMoveSpeed = 25;
        public float sensitivity = 3;

        private bool canMove = true;

        private void Update(){
            if (Input.GetKeyDown(KeyCode.E))
                this.canMove = !this.canMove;

            if (Input.GetKey(KeyCode.U))
                transform.Rotate(Vector3.forward * 20 * Time.deltaTime);
            else if (Input.GetKey(KeyCode.J))
                transform.Rotate(Vector3.back * 20 * Time.deltaTime);
            if (Input.GetKey(KeyCode.K))
                transform.Rotate(Vector3.right * 20 * Time.deltaTime);
            else if (Input.GetKey(KeyCode.J))
                transform.Rotate(Vector3.left * 20 * Time.deltaTime);

            if (!this.canMove)
                return;

            float speed = this.moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
                speed = this.fastMoveSpeed;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                transform.position = transform.position + (-transform.right * speed * Time.unscaledDeltaTime);

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                transform.position = transform.position + (transform.right * speed * Time.unscaledDeltaTime);

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                transform.position = transform.position + (transform.forward * speed * Time.unscaledDeltaTime);

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                transform.position = transform.position + (-transform.forward * speed * Time.unscaledDeltaTime);

            float newRotationX = transform.localEulerAngles.y + Input.GetAxisRaw("Mouse X") * sensitivity;
            float newRotationY = transform.localEulerAngles.x - Input.GetAxisRaw("Mouse Y") * sensitivity;
            transform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
        }

    }

    public class RenderCameraMod : MonoBehaviour{

        public bool wireframe = false;
        public bool drawCollision = false;

        private Material material;

        private void Awake(){
            Shader standardShader = Shader.Find("Standard");

            this.material = new Material(standardShader);
            this.material.SetColor("Albedo", Color.red);
        }

        private void OnPreRender(){
            GL.wireframe = this.wireframe;
        }

        private void OnPostRender(){
            if (!this.drawCollision)
                return;

            //Made with AI, because i'm bad at math
            GL.PushMatrix();
            material.SetPass(0);
            GL.LoadProjectionMatrix(Camera.current.projectionMatrix);
            GL.modelview = Camera.current.worldToCameraMatrix;

            GL.Begin(GL.LINES);
            GL.Color(Color.red);
            foreach (Collider collider in FindObjectsOfType<Collider>()){
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
        }


    }


}
