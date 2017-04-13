using CM3D2.GripMovePlugin.Plugin;
using PluginExt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityInjector.Attributes;

namespace CM3D2.VRMenu.Plugin
{
    [
        PluginFilter("CM3D2VRx64"),
        PluginFilter("CM3D2OHVRx64"),
        PluginName("VRMenuWithGripMovePlugin"),
        PluginVersion("0.0.3.1")
    ]
    public class VRMenuWithGripMovePlugin : ExPluginBase
    {
        private void Start()
        {
            DontDestroyOnLoad(this);
            StartCoroutine(disableGripMovePluginCo());
            Helper.InstallMode(this, new VRMenuIKTool(gameObject), SystemMenuCategory.MODE, 15);
        }

        private IEnumerator disableGripMovePluginCo()
        {
            var assembly = typeof(DirectTouchTool).Assembly;
            var GripMoveControllerBase = assembly.GetType("CM3D2.GripMovePlugin.Plugin.GripMoveControllerBase");
            if (GripMoveControllerBase == null)
            {
                Log.Debug("GripMoveControllerBase == null");
                yield break;
            }
            var MenuToolBase = assembly.GetType("CM3D2.GripMovePlugin.Plugin.MenuToolBase");
            if (MenuToolBase == null)
            {
                Log.Debug("MenuToolBase == null");
                yield break;
            }
            while (true)
            {
                foreach(var menuctrl in VRMenuPlugin.Instance.Controllers)
                {
                    if(menuctrl != null)
                    {
                        var controller = menuctrl.gameObject.GetComponent(GripMoveControllerBase) as MonoBehaviour;
                        if (controller != null && controller.enabled)
                        {
                            controller.enabled = false;
                        }
                        var menutool = menuctrl.gameObject.GetComponent(MenuToolBase) as MonoBehaviour;
                        if (menutool != null && menutool.enabled)
                        {
                            menutool.enabled = false;
                        }
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    public class VRMenuIKTool : IVRControllerMode
    {
        private class GripTriggerHandler : MonoBehaviour, IGripTriggerHandler
        {
            public VRMenuIKTool Tool;
            public Controller ControllerId;

            private bool isActive;
            private VRMenuController controller;
            private ReferenceCountSet<GameObject> touchingObjects = new ReferenceCountSet<GameObject>();
            private GameObject grippingObject;

            private static List<GameObject> grippingObjects = new List<GameObject>();

            public void SetAvtive(bool active)
            {
                if (isActive != active)
                {
                    isActive = active;

                    if (isActive && controller == null)
                    {
                        // VRMenuControllerが作られるのが遅いのでここで初期化
                        controller = VRMenuPlugin.Instance.Controllers[(int)ControllerId];
                        controller.AddGripHandler(this);
                        transform.parent = controller.transform;
                    }
                }
            }

            private void OnLevelWasLoaded(int index)
            {
                grippingObjects.Clear();
                touchingObjects.Clear();
                grippingObject = null;
            }

            public void OnTriggerEnter(UnityEngine.Collider other)
            {
                if (isActive)
                {
                    if (other.gameObject.GetComponent<MoveableGUIObject>() != null)
                    {
                        Log.Debug("Enter " + other.gameObject.name);
                        touchingObjects.Add(other.gameObject);
                    }
                }
            }

            public void OnTriggerStay(UnityEngine.Collider other) { }

            public void OnTriggerExit(UnityEngine.Collider other)
            {
                touchingObjects.Remove(other.gameObject);
            }

            public bool OnGripTest()
            {
                return isActive && (touchingObjects.Count > 0);
            }

            public void OnGripStart()
            {
                if (isActive)
                {
                    // 最もポインタに近いハンドルを掴む
                    float mindist = float.PositiveInfinity;
                    foreach(var obj in touchingObjects.Items)
                    {
                        if (grippingObjects.Contains(obj))
                        {
                            continue;
                        }
                        float dist = Vector3.Distance(obj.transform.position, controller.Pointer.transform.position);
                        if (dist < mindist)
                        {
                            grippingObject = obj;
                            mindist = dist;
                        }
                    }
                    if (grippingObject != null)
                    {
                        transform.position = grippingObject.transform.position;
                        transform.rotation = grippingObject.transform.rotation;
                        grippingObjects.Add(grippingObject);
                    }
                }
            }

            public void OnGripReleased()
            {
                grippingObjects.Remove(grippingObject);
                grippingObject = null;
            }

            private void Update()
            {
                if (grippingObject != null)
                {
                    grippingObject.transform.position = transform.position;
                    grippingObject.transform.rotation = transform.rotation;
                    grippingObject.GetComponent<MoveableGUIObject>().OnMoved();
                }
            }
        }

        public bool IsDiableGripByTrigger { get { return false; } }
        public bool IsDiableGripByGrip { get { return false; } }

        private bool IsLevelForIKMode(int level)
        {
            return (GameMain.Instance.GetNowSceneName() == "ScenePhotoMode");
        }

        public bool IsEnabled {
            get {
                return IsLevelForIKMode(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public string ModeName { get { return "IK"; } }

        private GripTriggerHandler[] triggerHandlers = new GripTriggerHandler[(int)Controller.Max];
        private IKTool ikTool;
        private int activatedCount;

        public VRMenuIKTool(GameObject parent)
        {
            for (int i = 0; i < triggerHandlers.Length; ++i)
            {
                GameObject go = new GameObject("VRMenuIKTool Grab Handle");
                var handler = go.AddComponent<GripTriggerHandler>();

                // 破棄されないようにとりあえず親を設定
                go.transform.parent = parent.transform;

                handler.Tool = this;
                handler.ControllerId = (Controller)i;
                triggerHandlers[i] = handler;
            }
        }

        public void OnActivated(Controller controller)
        {
            if (activatedCount++ == 0)
            {
                if (ikTool == null)
                {
                    ikTool = IKTool.Create();
                }
                ikTool.EnableIKMode(true);
            }
            triggerHandlers[(int)controller].SetAvtive(true);
        }

        public void OnDeactivated(Controller controller)
        {
            triggerHandlers[(int)controller].SetAvtive(false);
            if (--activatedCount == 0)
            {
                ikTool.EnableIKMode(false);
            }
        }

        public void OnTouchPadState(Controller controller, bool enable)
        {
        }
    }
}
