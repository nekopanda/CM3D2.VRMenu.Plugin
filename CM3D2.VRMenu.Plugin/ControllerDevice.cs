using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CM3D2.VRMenu.Plugin
{
    // コントローラデバイス
    // ここでは細かなアクセスは定義しないで、意味づけされた操作だけ定義

    public enum Button
    {
        Trigger, Grip, Menu, Left, Right
    }

    public abstract  class ControllerDevice
    {
        public abstract object RawDevice { get; }

        // Left,RightはViveだと１つのタッチパッド上での仮想的なものなので
        // Down->Press->Upが対応しないことがあることに注意

        public abstract bool GetPressDown(Button button);
        public abstract bool GetPress(Button button);
        public abstract bool GetPressUp(Button button);
    }

    public class ViveControllerDevice : ControllerDevice
    {
        public SteamVR_TrackedObject TrackedObject { get; private set; }

        public bool IsValid {
            get {
                return TrackedObject.isValid;
            }
        }

        public SteamVR_Controller.Device Device
        {
            get
            {
                return SteamVR_Controller.Input((int)TrackedObject.index);
            }
        }

        public override object RawDevice { get { return TrackedObject; } }

        public ViveControllerDevice(SteamVR_TrackedObject trackedObject)
        {
            TrackedObject = trackedObject;
        }

        private static readonly ulong[] buttonMask = new ulong[] {
            SteamVR_Controller.ButtonMask.Trigger,
            SteamVR_Controller.ButtonMask.Grip,
            SteamVR_Controller.ButtonMask.ApplicationMenu,
            0,
            0
        };

        public override bool GetPressDown(Button button)
        {
            if(IsValid == false)
            {
                return false;
            }
            var device = Device;
            if (button == Button.Trigger)
            {
                return device.GetTouchDown(SteamVR_Controller.ButtonMask.Trigger);
            }
            ulong mask = buttonMask[(int)button];
            if (mask > 0)
            {
                return device.GetPressDown(mask);
            }
            if (device.GetPressDown(SteamVR_Controller.ButtonMask.Touchpad))
            {
                return (device.GetAxis().x < 0.5) == (button == Button.Left);
            }
            return false;
        }

        public override bool GetPress(Button button)
        {
            if (IsValid == false)
            {
                return false;
            }
            var device = Device;
            if (button == Button.Trigger)
            {
                return device.GetTouch(SteamVR_Controller.ButtonMask.Trigger);
            }
            ulong mask = buttonMask[(int)button];
            if (mask > 0)
            {
                return device.GetPress(mask);
            }
            if (device.GetPress(SteamVR_Controller.ButtonMask.Touchpad))
            {
                return (device.GetAxis().x < 0.5) == (button == Button.Left);
            }
            return false;
        }

        public override bool GetPressUp(Button button)
        {
            if (IsValid == false)
            {
                return false;
            }
            var device = Device;
            if (button == Button.Trigger)
            {
                return device.GetTouchUp(SteamVR_Controller.ButtonMask.Trigger);
            }
            ulong mask = buttonMask[(int)button];
            if (mask > 0)
            {
                return device.GetPressUp(mask);
            }
            if (device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
            {
                return (device.GetAxis().x < 0.5) == (button == Button.Left);
            }
            return false;
        }
    }
}
