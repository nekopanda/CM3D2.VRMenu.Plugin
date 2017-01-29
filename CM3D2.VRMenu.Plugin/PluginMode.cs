using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace CM3D2.VRMenu.Plugin
{
    class BuiltinModeBase : IVRControllerMode
    {
        public bool IsDiableGripByTrigger { get { return false; } }
        public bool IsDiableGripByGrip { get { return false; } }
        public bool IsEnabled { get { return true; } }
        public virtual string ModeName { get { return "デフォルト"; } }

        public ContollerHacker GetHacker(Controller controller)
        {
            return VRMenuPlugin.Instance.Controllers[(int)controller].Hacker;
        }
        public VRMenuController GetController(Controller controller)
        {
            return VRMenuPlugin.Instance.Controllers[(int)controller];
        }

        public virtual void OnActivated(Controller controller)
        {
            Log.Debug("Mode Activated");
        }

        public virtual void OnDeactivated(Controller controller)
        {
            Log.Debug("Mode Deactivated");
        }

        public virtual void OnTouchPadState(Controller controller, bool enable)
        {
            //Log.Debug("Mode Deactivated");
        }
    }
    /*
    class DefaultMode : BuiltinModeBase
    {
        private ContollerHacker.Mode mode;
        public DefaultMode(ContollerHacker.Mode mode)
        {
            this.mode = mode;
        }
        public override string ModeName { get { return mode.ToString(); } }
        public override void OnActivated(Controller controller)
        {
            GetHacker(controller).ChangeMode(mode, mode.ToString());
        }
        public override void OnDeactivated(Controller controller)
        {
            var hacker = GetHacker(controller);
            hacker.ChangeMode(ContollerHacker.Mode.None, "");
        }
    }
    */
    class PointerMode : BuiltinModeBase
    {
        public override string ModeName { get { return "POINTER"; } }
        public override void OnActivated(Controller controller)
        {
            var cont = GetController(controller);
            //cont.Hacker.ChangeMode(ContollerHacker.Mode.None, ModeName.ToString());
            cont.AlwaysClickableOnGUI = true;
        }
        public override void OnDeactivated(Controller controller)
        {
            var cont = GetController(controller);
            cont.AlwaysClickableOnGUI = false;
        }
        public override void OnTouchPadState(Controller controller, bool enable)
        {
            var cont = GetController(controller);
            if (enable)
            {
                cont.AlwaysClickableOnGUI = true;
            }
            else
            {
                cont.AlwaysClickableOnGUI = false;
            }
        }
    }

    class ItemMode : BuiltinModeBase, IControllerModel
    {
        public static readonly string[] ItemName = new string[]
        {
            "コントローラに戻す", "ペンライト", "おもちゃ１", "おもちゃ２"
        };
        private ContollerHacker.Item item;
        public ItemMode(ContollerHacker.Item item)
        {
            this.item = item;
        }
        public override string ModeName { get { return ItemName[(int)item]; } }

        public override void OnActivated(Controller controller)
        {
            var cont = GetController(controller);
            cont.Model.Push(this);
        }
        public override void OnDeactivated(Controller controller)
        {
            var cont = GetController(controller);
            cont.Model.Remove(this);
        }
        // ITEMはOnTouchPadStateが呼ばれない
        public override void OnTouchPadState(Controller controller, bool enable) { }

        public void Show(VRMenuController controller)
        {
            controller.Hacker.ChangeItem(item);
            if (item != ContollerHacker.Item.Controller)
            {
                controller.SuppressPointer(true);
            }
        }
        public void Hide(VRMenuController controller)
        {
            controller.Hacker.ChangeItem(ContollerHacker.Item.Controller);
            if (item != ContollerHacker.Item.Controller)
            {
                controller.SuppressPointer(false);
            }
        }
    }

    class CameraMode : IVRControllerMode, IControllerModel
    {
        public bool IsDiableGripByTrigger { get { return true; } }
        public bool IsDiableGripByGrip { get { return false; } }
        public bool IsEnabled { get { return true; } }
        public string ModeName { get { return "CAMERA"; } }

        public VRMenuController GetController(Controller controller)
        {
            return VRMenuPlugin.Instance.Controllers[(int)controller];
        }
        public void OnActivated(Controller controller) { }
        public void OnDeactivated(Controller controller) { }
        public void OnTouchPadState(Controller controller, bool enable)
        {
            var cont = GetController(controller);
            if (enable)
            {
                cont.Model.Push(this);
            }
            else
            {
                cont.Model.Remove(this);
            }
        }

        public void Show(VRMenuController controller)
        {
            controller.Hacker.ChangeMode(ContollerHacker.Mode.Camera);
            controller.Hacker.Text = ModeName;
            controller.SuppressPointer(true);
        }
        public void Hide(VRMenuController controller)
        {
            controller.Hacker.ChangeMode(ContollerHacker.Mode.None);
            controller.Hacker.Text = ModeName;
            controller.SuppressPointer(false);
        }
    }

    class YotogiCommandTool : VRMenuPage, IVRControllerMode
    {
        private static string[] EmptyString = new string[0];

        public bool IsDiableGripByTrigger { get { return false; } }
        public bool IsDiableGripByGrip { get { return false; } }
        
        public bool IsEnabled {
            get {
                return (GameMain.Instance.GetNowSceneName() == "SceneYotogi");
            }
        }

        public string ModeName { get { return "YOTOGI"; } }

        // 左右のコントローラがあるので有効化されてるコントローラの数を数える
        private int activatedCount;
        
        private GameObject commandUnit;
        private List<GameObject> buttonList = new List<GameObject>();

        public YotogiCommandTool()
        {
            Texts = EmptyString;
        }

        public override void OnClicked(Controller controller, int index)
        {
            if (index < buttonList.Count)
            {
                var go = buttonList[index];
                if(go.activeInHierarchy)
                {
                    go.SendMessage("OnClick");
                }
            }
        }

        public void OnActivated(Controller controller)
        {
            Log.Debug("Yotogi Command Tool Activated");
            ++activatedCount;
            VRMenuPlugin.Instance.ShowMenu(this, controller);
        }

        public void OnDeactivated(Controller controller)
        {
            --activatedCount;
            VRMenuPlugin.Instance.HideMenu(this, controller);
        }

        public void OnTouchPadState(Controller controller, bool enable) { }

        private void OnLevelWasLoaded(int index)
        {
            reset();
        }

        private void reset()
        {
            Texts = EmptyString;
            buttonList.Clear();
            prevList = null;
        }

        // 前と変わったか
        private GameObject[] prevList;
        private bool isMatchPrev()
        {
            if(commandUnit.activeInHierarchy == false)
            {
                return prevList == null;
            }

            var tr = commandUnit.transform;
            if (prevList == null || prevList.Length != tr.childCount)
            {
                return false;
            }
            for (int i = 0; i < prevList.Length; ++i)
            {
                if(prevList[i] != tr.GetChild(i).gameObject)
                {
                    return false;
                }
            }
            return true;
        }

        private void createLabels()
        {
            List<string> labelList = new List<string>();
            buttonList.Clear();

            if(commandUnit.activeInHierarchy)
            {
                List<GameObject> cmds = new List<GameObject>();
                foreach (Transform tr in commandUnit.transform)
                {
                    cmds.Add(tr.gameObject);
                    if (tr.name == null || tr.name.StartsWith("cm:") == false || (tr.gameObject == null))
                    {
                        continue;
                    }
                    GameObject go = tr.gameObject;
                    labelList.Add(go.GetComponentInChildren<UILabel>().text);
                    buttonList.Add(go);
                }
                prevList = cmds.ToArray();
            }
            else
            {
                prevList = null;
            }

            Texts = labelList.ToArray();
        }

        private void Update()
        {
            if (IsEnabled && activatedCount > 0)
            {
                if (commandUnit == null)
                {
                    commandUnit = GameObject.Find("/UI Root/YotogiPlayPanel/CommandViewer/SkillViewer/MaskGroup/SkillGroup/CommandParent/CommandUnit");
                }
                if (commandUnit != null)
                {
                    if(isMatchPrev() == false)
                    {
                        createLabels();
                    }
                }
            }
        }
    }

    class MoveMode : IVRControllerMode
    {
        public bool IsDiableGripByTrigger { get { return false; } }
        public bool IsDiableGripByGrip { get { return false; } }
        public bool IsEnabled { get { return true; } }
        public string ModeName { get { return "MOVE"; } }

        private class State : MonoBehaviour
        {
            private bool isActive;
            private ControllerDevice device;
            private GameObject point;
            private bool isReal;

            private bool notReached;
            private Vector3 currentPos;

            public void SetInputActive(bool active)
            {
                if (isActive != active)
                {
                    isActive = active;
                    //Log.Out("SetInputActive: " + isActive);
                }
                if (isActive && point == null)
                {
                    device = gameObject.GetComponent<VRMenuController>().Device;

                    point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    point.name = "VRMenu Move Point";
                    point.GetComponent<Collider>().enabled = false;
                    point.transform.parent = VRMenuPlugin.Instance.PlayRoom.transform;
                    point.transform.localScale = Vector3.one * 0.2f;
                    point.GetComponent<MeshRenderer>().material = new Material(Asset.Instance.PointerShader);
                    point.SetActive(false);
                }
            }

            private static readonly float G = 1;

            private Vector3 calcPoint()
            {
                var s = transform.position;
                var fwd = transform.forward * 4;

                float roomY = VRMenuPlugin.Instance.PlayRoom.transform.position.y;
                float y = s.y - (isReal ? roomY : 0);
                float fwdy = fwd.y;

                // yがマイナスだったらy方向を逆にする
                if (y < 0)
                {
                    y = -y;
                    fwdy = -fwdy;
                }

                // 落ちるまでの時間
                float t = (float)(fwdy + Math.Sqrt(fwdy * fwdy + 2 * G * y)) / G;

                // 落ちたポイント
                Vector3 pos = s + t * fwd;
                pos.y = isReal ? roomY : 0;

                return pos;
            }

            private void Update()
            {
                if (isActive == false)
                {
                    if (point != null)
                    {
                        point.SetActive(false);
                    }
                    return;
                }

                bool isPressDownLeft = device.GetPressDown(Button.Left);
                bool isPressDownRight = device.GetPressDown(Button.Right);
                bool isPressLeft = device.GetPress(Button.Left);
                bool isPressRight = device.GetPress(Button.Right);
                bool isPressUpLeft = device.GetPressUp(Button.Left);
                bool isPressUpRight = device.GetPressUp(Button.Right);

                if (isPressDownLeft || isPressDownRight)
                {
                    notReached = true;
                    currentPos = VRMenuPlugin.Instance.Head.transform.position;
                    currentPos.y = isReal ? VRMenuPlugin.Instance.PlayRoom.transform.position.y : 0;
                    point.transform.position = currentPos;
                    point.SetActive(true);
                }
                else if (point.activeSelf && (isPressLeft || isPressRight))
                {
                    const float speed = 0.3f;
                    isReal = isPressLeft;
                    var target = calcPoint();
                    if(notReached)
                    {
                        var diff = target - point.transform.position;
                        if (diff.magnitude < speed)
                        {
                            notReached = false;
                            currentPos = target;
                        }
                        else
                        {
                            currentPos += diff.normalized * speed;
                        }
                        point.transform.position = currentPos;
                    }
                    else
                    {
                        point.transform.position = target;
                    }
                }
                else if (point.activeSelf && (isPressUpLeft || isPressUpRight))
                {
                    if(notReached == false)
                    {
                        var head = VRMenuPlugin.Instance.Head;
                        // 移動先
                        var target = point.transform.position;
                        // 高さは実際の高さに合わせる
                        float roomY = VRMenuPlugin.Instance.PlayRoom.transform.position.y;
                        target.y = head.transform.position.y - (isReal ? 0 : roomY);
                        // 移動量
                        var diff = target - head.position;
                        // 移動を反映させる
                        VRMenuPlugin.Instance.PlayRoomOffset.transform.position += diff;
                    }

                    point.SetActive(false);
                }

            }
        }

        private State[] stateList = new State[(int)Controller.Max];

        public void OnActivated(Controller controller)
        {
            State state = stateList[(int)controller];
            if (state == null)
            {
                state = VRMenuPlugin.Instance.Controllers[(int)controller].gameObject.AddComponent<State>();
                stateList[(int)controller] = state;
            }
            state.SetInputActive(true);
        }
        public void OnDeactivated(Controller controller)
        {
            State state = stateList[(int)controller];
            if (state != null)
            {
                state.SetInputActive(false);
            }
        }
        public void OnTouchPadState(Controller controller, bool enable)
        {
            State state = stateList[(int)controller];
            if (state != null)
            {
                state.SetInputActive(enable);
            }
        }
    }

    class ItemPickMode : MonoBehaviour, IVRControllerMode
    {
        public bool IsDiableGripByTrigger { get { return false; } }
        public bool IsDiableGripByGrip { get { return false; } }
        public bool IsEnabled { get { return VRMenuPlugin.Instance.SpawnItemUI.IsEnabledScene; } }
        public string ModeName { get { return "ITEM-PICK"; } }

        private bool[] controllerActive = new bool[(int)Controller.Max];

        private void Update()
        {
            bool isPressUp = false;
            for(int i = 0; i < controllerActive.Length; ++i)
            {
                if(controllerActive[i] == false)
                {
                    continue;
                }
                var dev = VRMenuPlugin.Instance.Controllers[i].Device;
                isPressUp |= (dev.GetPressUp(Button.Left) || dev.GetPressUp(Button.Right));
            }
            if(isPressUp)
            {
                VRMenuPlugin.Instance.SpawnItemUI.ToggleShowHandle();
            }
        }

        public void OnActivated(Controller controller)
        {
            VRMenuPlugin.Instance.SpawnItemUI.ShowGUI(true);
        }
        public void OnDeactivated(Controller controller)
        {
            VRMenuPlugin.Instance.SpawnItemUI.ShowGUI(false);
        }
        public void OnTouchPadState(Controller controller, bool enable)
        {
            controllerActive[(int)controller] = enable;
        }
    }

    class SampleMode : VRMenu, IVRControllerMode
    {
        public bool IsDiableGripByTrigger { get { return false; } }
        public bool IsDiableGripByGrip { get { return false; } }
        public bool IsEnabled { get { return true; } }
        public string ModeName { get { return "サンプルモード"; } }

        public SampleMode()
        {
            string[] text = new string[] { "ロリ", "妹", "お姉ちゃん", "人妻" };
            for (int i = 0; i < text.Length; ++i)
            {
                AddButton<SimpleButton>(text[i], i);
            }
        }

        public override void OnClicked(Controller controller, int buttonIndex)
        {
            Console.WriteLine("「" + Texts[buttonIndex] + "」が選択されました");
        }

        public override void OnSelectionChanged(Controller controller, int buttonIndex)
        {
            if (buttonIndex >= 0) Console.WriteLine(Texts[buttonIndex] + "？");
        }

        public void OnActivated(Controller controller)
        {
            VRMenuPlugin.Instance.ShowMenu(this, controller);
        }

        public void OnDeactivated(Controller controller)
        {
            VRMenuPlugin.Instance.HideMenu(this, controller);
        }

        public void OnTouchPadState(Controller controller, bool enable) { }
    }
    /*
    class LegacyGrabMode : IVRControllerMode
    {
        public bool IsDiableGripByTrigger { get { return true; } }
        public bool IsDiableGripByGrip { get { return false; } }
        public bool IsEnabled { get { return true; } }
        public virtual string ModeName { get { return "LEGACY GRAB"; } }

        public ContollerHacker GetHacker(Controller controller)
        {
            ViveVRMenuController viveCtrl =
                (ViveVRMenuController)VRMenuPlugin.Instance.Controllers[(int)controller];
            return viveCtrl.Hacker;
        }
        public VRMenuController GetController(Controller controller)
        {
            return VRMenuPlugin.Instance.Controllers[(int)controller];
        }
        public void OnActivated(Controller controller)
        {
            Console.WriteLine(ModeName + " Mode Activated");
            //GetController(controller).PointerEnabled = false;
            GetHacker(controller).ChangeMode(ContollerHacker.Mode.Grab, ModeName);
        }
        public void OnDeactivated(Controller controller)
        {
            var hacker = GetHacker(controller);
            //GetController(controller).PointerEnabled = true;
            hacker.ChangeMode(ContollerHacker.Mode.None, "");
        }
        public virtual void OnTouchPadState(Controller controller, bool enable)
        {
            //Console.WriteLine("Mode Deactivated");
        }
    }
    */
    public static class ModeImpl
    {
        public static void InitBuiltinMode(ControllerMode modeSystem, GameObject parent)
        {
            var grabMode = new PointerMode();
            modeSystem.AddMode(grabMode, SystemMenuCategory.MODE, 0);

            modeSystem.SetDefaultMode(grabMode);

            modeSystem.AddMode(new MoveMode(), SystemMenuCategory.MODE, 10);
            modeSystem.AddMode(new CameraMode(), SystemMenuCategory.MODE, 10);

            //modeSystem.AddMode(new LegacyGrabMode(), SystemMenuCategory.MODE, 5);

            int itemMetric = 10;
            foreach (ContollerHacker.Item item in Enum.GetValues(typeof(ContollerHacker.Item)))
            {
                modeSystem.AddMode(new ItemMode(item), SystemMenuCategory.ITEM, itemMetric);
                itemMetric += 10;
            }

            modeSystem.AddMode(parent.AddComponent<YotogiCommandTool>(), SystemMenuCategory.MODE, 20);
            modeSystem.AddMode(parent.AddComponent<ItemPickMode>(), SystemMenuCategory.MODE, 25);

            //modeSystem.AddMode(parent.AddComponent<SampleMode>(), SystemMenuCategory.TOOL, 100);
        }
    }
}
