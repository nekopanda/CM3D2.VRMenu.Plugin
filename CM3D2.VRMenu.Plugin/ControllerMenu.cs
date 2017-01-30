using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CM3D2.VRMenu.Plugin
{
    // メニュー表示実体の抽象クラス
    public class VRMenuButtons : MonoBehaviour
    {
        public VRMenuPage MenuData;
    }

    public abstract class ControllerMenu
    {
        private VRMenuController controller;
        private VRMenuButtons currentSystemMenu;
        private List<VRMenuButtons> activeMenu = new List<VRMenuButtons>();
        private Dictionary<VRMenuPage, VRMenuButtons> menuCache = new Dictionary<VRMenuPage, VRMenuButtons>();

        // システムメニューは非表示にできない

        private bool userMenuVisible_ = false;
        public bool UserMenuVisible
        {
            get { return userMenuVisible_; }
            set
            {
                if (userMenuVisible_ != value)
                {
                    userMenuVisible_ = value;

                    // システムメニューがなくて、ユーザメニューがあったら
                    if (systemMenuActive_ == false && activeMenu.Count > 0)
                    {
                        activeMenu.Last().gameObject.SetActive(userMenuVisible_);
                    }
                }
            }
        }

        private bool systemMenuActive_ = false;
        public bool SystemMenuActive
        {
            get
            {
                return systemMenuActive_;
            }
            set
            {
                if (systemMenuActive_ != value)
                {
                    systemMenuActive_ = value;

                    if(currentSystemMenu == null)
                    {
                        currentSystemMenu = getButtons(controller.Mode.SystemMenuTop, true);
                    }
                    if (currentSystemMenu != null)
                    {
                        currentSystemMenu.gameObject.SetActive(systemMenuActive_);
                    }
                    if (userMenuVisible_ && activeMenu.Count > 0)
                    {
                        activeMenu.Last().gameObject.SetActive(!systemMenuActive_);
                    }
                }
            }
        }

        private bool MenuVisible {
            get {
                return activeMenu.Count > 0 || (systemMenuActive_ && currentSystemMenu != null);
            }
        }

        public VRMenuButtons CurrentMenu {
            get {
                if (systemMenuActive_)
                {
                    return currentSystemMenu;
                }
                else if (activeMenu.Count > 0)
                {
                    return activeMenu.Last();
                }
                return null;
            }
        }

        protected abstract VRMenuButtons CreateMenuButtons(VRMenuPage menu);

        private float timeMenuDown = 0;
        private float timePrevMenuClick = 0;
        private bool isLongPressProcessed;

        public ControllerMenu(VRMenuController controller)
        {
            this.controller = controller;
        }

        public void updateMenu()
        {
            // クリックフラグ更新
            bool isDoubleClicked = false;
            bool isClicked = false;
            bool isLongPressed = false;

            if (controller.Device.GetPressDown(Button.Menu))
            {
                timeMenuDown = Time.realtimeSinceStartup;
                isLongPressProcessed = false;
            }
            else if (controller.Device.GetPress(Button.Menu))
            {
                if (isLongPressProcessed == false &&
                    Time.realtimeSinceStartup - timeMenuDown > 1.5f)
                {
                    isLongPressed = true;
                    isLongPressProcessed = true;
                }
            }
            else if (controller.Device.GetPressUp(Button.Menu))
            {
                if (isLongPressProcessed == false)
                {
                    float now = Time.realtimeSinceStartup;
                    if (now - timePrevMenuClick < 0.6f)
                    {
                        isDoubleClicked = true;
                        isClicked = true;
                        timePrevMenuClick = 0; // 連続ダブルクリック誤認しないように
                    }
                    else
                    {
                        isClicked = true;
                        timePrevMenuClick = now;
                    }
                }
            }

            if(isDoubleClicked)
            {
                controller.Mode.SwitchToPreviousMode();
            }
            if(isClicked)
            {
                if (SystemMenuActive)
                {
                    // 表示しているので閉じる
                    SystemMenuActive = false;
                    controller.Hacker.DisableInput = false;
                }
                else
                {
                    // システムメニューを表示する
                    if (currentSystemMenu != null)
                    {
                        SystemMenuActive = true;
                        controller.Hacker.DisableInput = true;
                    }
                }
            }
            if(isLongPressed)
            {
                controller.ResetGUIPosition();
            }

            if (MenuVisible)
            {
                if (controller.Device.GetPressDown(Button.Trigger))
                {
                    // トリガーが押されていたら戻る
                    var current = CurrentMenu;
                    if(current != null)
                    {
                        var menu = current.MenuData as VRMenu;
                        if (menu != null)
                        {
                            menu.BackMenu(controller);
                        }
                    }
                }
            }
        }

        private VRMenuButtons getButtons(VRMenuPage menu, bool allowCreation)
        {
            if(menu == null)
            {
                return null;
            }
            VRMenuButtons buttons;
            if (!menuCache.TryGetValue(menu, out buttons))
            {
                if (allowCreation == false)
                {
                    return null;
                }
                buttons = CreateMenuButtons(menu);
                buttons.gameObject.SetActive(false);
                menuCache.Add(menu, buttons);
            }
            return buttons;
        }

        /// <summary>
        /// システムメニューの表示・非表示を変更せずに、システムメニューをセット
        /// </summary>
        public void SetSystemMenu(VRMenu menu)
        {
            var last = currentSystemMenu;
            VRMenuButtons current = getButtons(menu, true);
            currentSystemMenu = current;

            if (SystemMenuActive)
            {
                if(last != null)
                {
                    last.gameObject.SetActive(false);
                }
                current.gameObject.SetActive(true);
            }
        }

        /*
        /// <summary>
        /// システムメニューの表示・非表示を変更せずに、システムメニューをpop
        /// システムメニューは最低1枚は必要なので最後の1枚はpopできない
        /// </summary>
        public VRMenuPage PopSystemMenu()
        {
            VRMenuPage menu = null;
            if (systemMenuStack.Count >= 2)
            {
                var buttons = systemMenuStack.Last();
                systemMenuStack.RemoveAt(systemMenuStack.Count - 1);
                menu = buttons.MenuData;
                if (SystemMenuActive)
                {
                    buttons.gameObject.SetActive(false);
                    systemMenuStack.Last().gameObject.SetActive(true);
                }
            }
            return menu;
        }
        */

        /// <summary>
        /// システムメニューをセットして表示
        /// </summary>
        /// <param name="menu"></param>
        public void ShowSystemMenu(VRMenu menu)
        {
            if (menu == null)
            {
                var e = new ArgumentNullException("メニューをnullにすることはできません");
                Log.Debug(e);
                throw e;
            }

            // まずユーザメニューを非表示にする
            if (activeMenu.Count > 0)
            {
                activeMenu.Last().gameObject.SetActive(false);
            }

            // こっそりactiveフラグを書き換える
            systemMenuActive_ = true;

            // pushして表示
            SetSystemMenu(menu);
        }

        /// <summary>
        /// ユーザメニューの表示
        /// </summary>
        public void SetUserMenuVisiblity(VRMenuPage menu, bool visiblity)
        {
            VRMenuButtons buttons = getButtons(menu, visiblity);
            if (buttons == null)
            {
                return;
            }

            activeMenu.Remove(buttons);

            if (visiblity)
            {
                activeMenu.Add(buttons);

                if (SystemMenuActive == false)
                {
                    buttons.gameObject.SetActive(true);
                }
            }
            else
            {
                buttons.gameObject.SetActive(false);

                if (SystemMenuActive == false && activeMenu.Count > 0)
                {
                    activeMenu.Last().gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// メニューを解放
        /// </summary>
        public void ReleaseMenu(VRMenuPage menu)
        {
            VRMenuButtons buttons;
            if (menuCache.TryGetValue(menu, out buttons))
            {
                activeMenu.Remove(buttons);
                if (menu == currentSystemMenu)
                {
                    currentSystemMenu = null;
                }
                menuCache.Remove(menu);

                // オブジェクトが破棄されていなかったら
                if (buttons != null)
                {
                    GameObject.Destroy(buttons.gameObject);
                }
            }
        }

    }

    public class ViveControllerMenu : ControllerMenu
    {
        private ViveVRMenuController viveController;

        public ViveControllerMenu(ViveVRMenuController controller)
            : base(controller)
        {
            viveController = controller;
        }

        protected override VRMenuButtons CreateMenuButtons(VRMenuPage menu)
        {
            GameObject attachPoint = viveController.TrackedObject.gameObject;

            GameObject menuRoot = new GameObject("VRMenu");
            menuRoot.transform.SetParent(attachPoint.transform, false);

            // タッチパッドからの相対位置を調整
            menuRoot.transform.localPosition = new Vector3(0, 0.002f, -0.049f) +
                Quaternion.Euler(-96.55f, 0, 0) * new Vector3(0, 0, 0.0031f);
            menuRoot.transform.localRotation = Quaternion.Euler(-96.55f + 180, 0, 0);
            menuRoot.transform.localScale = Vector3.one;

            ViveMenuButtons buttons = menuRoot.AddComponent<ViveMenuButtons>();

            buttons.TrackedObject = viveController.TrackedObject;
            buttons.ControllerId = viveController.ControllerId;
            buttons.MenuData = menu;

            // パラメータを設定
            buttons.TouchPadRadius = 0.021f;
            buttons.TextDepth = 0.01f;
            buttons.TextCircleRadius = 0.03f;
            buttons.LineWidth = 0.002f;
            buttons.BoldLineWidth = 0.003f;

            return buttons;
        }
    }
}
