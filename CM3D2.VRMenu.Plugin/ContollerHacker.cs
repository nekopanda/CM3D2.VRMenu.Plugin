using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CM3D2.VRMenu.Plugin
{
    public class ContollerHacker
    {
        public enum Item
        {
            Controller, Sticklight, VibePink, AnalVibe
        }

        public enum Mode
        {
            Grab, Camera, Pointer, MoveDir, MoveDraw, None
        }

        private SteamVR_TrackedObject trackedObject;
        private ViveController viveController;

        private SteamVR_Controller.Device Device {
            get {
                return SteamVR_Controller.Input((int)trackedObject.index);
            }
        }

        private MethodInfo changeModeMethod;

        private FieldInfo pressMenuBtnLongField;
        private FieldInfo modeField;
        private FieldInfo textModeField;

        private ViveController.OvrControllerMode ModeField {
            get { return (ViveController.OvrControllerMode)modeField.GetValue(viveController); }
            set { modeField.SetValue(viveController, value); }
        }

        private string TextMode
        {
            get { return (string)((Text)textModeField.GetValue(viveController)).text; }
            set { ((Text)textModeField.GetValue(viveController)).text = value; }
        }

        private string text_ = "";
        public string Text {
            get { return text_; }
            set {
                if(text_ != value)
                {
                    text_ = value;
                    TextMode = value;
                }
            }
        }

        private OvrHandItemMgr handItem;
        private ViveController.OvrControllerMode currentTargetMode = ViveController.OvrControllerMode.MOUSE_POINTER;

        private bool disableInput_ = false;
        public bool DisableInput {
            get { return disableInput_; }
            set {
                if(disableInput_ != value)
                {
                    disableInput_ = value;

                    if (currentTargetMode != ViveController.OvrControllerMode.MAX)
                    {
                        if (disableInput_)
                        {
                            // MAXにして反応しなくする
                            ModeField = ViveController.OvrControllerMode.MAX;
                        }
                        else
                        {
                            // 戻す
                            ModeField = currentTargetMode;
                        }
                    }
                }
            }
        }

        private bool firstChangeItem = true;

        public ContollerHacker(GameObject controllerObject)
        {
            trackedObject = controllerObject.GetComponent<SteamVR_TrackedObject>();
            viveController = controllerObject.GetComponent<ViveController>();

            AddMaxModeText();

            changeModeMethod = viveController.GetType().GetMethod("ChangeMode",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var handItemField = viveController.GetType().GetField("m_HandItem",
                BindingFlags.NonPublic | BindingFlags.Instance);
            handItem = (OvrHandItemMgr)handItemField.GetValue(viveController);

            pressMenuBtnLongField = viveController.GetType().GetField("m_bPressMenuBtnLong",
                BindingFlags.NonPublic | BindingFlags.Instance);

            modeField = viveController.GetType().GetField("m_eMode",
                BindingFlags.NonPublic | BindingFlags.Instance);

            textModeField = viveController.GetType().GetField("m_txMode",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private void AddMaxModeText()
        {
            // OvrControllerMode.MAX のモードテキストを追加
            var strModeField = viveController.GetType().GetField("m_strMode",
                BindingFlags.NonPublic | BindingFlags.Instance);
            string[] strMode = (string[])strModeField.GetValue(viveController);
            string[] newMode = new string[strMode.Length + 1];
            Array.Copy(strMode, newMode, strMode.Length);
            newMode[strMode.Length] = "";
            strModeField.SetValue(viveController, newMode);
        }

        public void Update()
        {
            // メニューボタンクリックを無効化
            var device = this.Device;
            if (device.GetPressDown(SteamVR_Controller.ButtonMask.ApplicationMenu) ||
                device.GetPress(SteamVR_Controller.ButtonMask.ApplicationMenu) ||
                device.GetPressUp(SteamVR_Controller.ButtonMask.ApplicationMenu))
            {
                pressMenuBtnLongField.SetValue(viveController, true);
                viveController.m_fMenuPressBeforeTime = 0.0f; // なぜかこれだけpublicになってる・・・
            }

            if (ModeField != currentTargetMode)
            {
                // 他のモードにはさせない！！強制的に戻す
                ModeField = currentTargetMode;
            }
        }

        public void ChangeItem(Item item)
        {
            DisableInput = false;
            if(item == Item.Controller)
            {
                changeModeMethod.Invoke(viveController, new object[] { ViveController.OvrControllerMode.MAX });
                TextMode = text_;
            }
            else
            {
                int modelIndex = (int)item - 1;
                if(modelIndex < handItem.ModelNum)
                {
                    handItem.ChangeModel(modelIndex);
                    currentTargetMode = ViveController.OvrControllerMode.ITEM;
                    changeModeMethod.Invoke(viveController, new object[] { ViveController.OvrControllerMode.ITEM });

                    if(firstChangeItem)
                    {
                        // 初回はOvrHandItemMgrのStart()でペンライトに戻されてしまうので、
                        // １フレーム後に再度ChangeItemを呼び出す
                        viveController.StartCoroutine(delayedChangeItem(modelIndex));
                        firstChangeItem = false;
                    }
                }
            }
        }

        private IEnumerator delayedChangeItem(int modelIndex)
        {
            yield return null;
            handItem.ChangeModel(modelIndex);
            yield break;
        }

        // モードを変えるとテキストが変更される可能性がある
        public void ChangeMode(Mode mode)
        {
            if(mode != Mode.None)
            {
                //Log.Out("None以外へのモード変更は予期しない動作を引き起こします！！");
            }
            DisableInput = false;
            ViveController.OvrControllerMode newMode;
            if (mode == Mode.Grab)
            {
                newMode = ViveController.OvrControllerMode.GRAB;
            }
            else if (mode == Mode.Pointer)
            {
                newMode = ViveController.OvrControllerMode.MOUSE_POINTER;
            }
            else if (mode == Mode.Camera)
            {
                newMode = ViveController.OvrControllerMode.CAMERA;
            }
            else if(mode == Mode.None)
            {
                newMode = ViveController.OvrControllerMode.MAX;
            }
            else {
                newMode = ViveController.OvrControllerMode.MOVE;
                if (mode == Mode.MoveDir)
                {
                    GameMain.Instance.CMSystem.OvrMoveMode = 0;
                }
                else
                {
                    GameMain.Instance.CMSystem.OvrMoveMode = 1;
                }
            }
            if(newMode != currentTargetMode)
            {
                currentTargetMode = newMode;
                changeModeMethod.Invoke(viveController, new object[] { currentTargetMode });

                if(viveController.enabled == false)
                {
                    // なぜかdisableになることがある？？
                    Log.Debug("ViveController disabled !!!!! Force enable it!");
                    viveController.enabled = true;
                }
            }
            text_ = TextMode;
        }
    }

    public interface IControllerModel
    {
        void Show(VRMenuController controller);
        void Hide(VRMenuController controller);
    }

    public class ControllerModel
    {
        private VRMenuController controller;
        private List<IControllerModel> modelStack = new List<IControllerModel>();

        public ControllerModel(VRMenuController controller)
        {
            this.controller = controller;
        }

        public void Push(IControllerModel model)
        {
            //Log.Out("Push Model: " + model.ToString());
            if(modelStack.Count > 0)
            {
                modelStack.Last().Hide(controller);
            }
            modelStack.Add(model);
            model.Show(controller);
        }

        public bool Remove(IControllerModel model)
        {
            //Log.Out("Remove Model: " + model.ToString());
            int index = modelStack.IndexOf(model);
            if (index != -1)
            {
                modelStack.RemoveAt(index);
                if (modelStack.Count > 0)
                {
                    if (modelStack.Last() == model)
                    {
                        // 何もしない
                    }
                    else
                    {
                        model.Hide(controller);
                        modelStack.Last().Show(controller);
                    }
                }
                else
                {
                    model.Hide(controller);
                }
                //Log.Out("Removed");
                return true;
            }
            return false;
        }
    }
}
