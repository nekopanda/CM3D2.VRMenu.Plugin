using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CM3D2.VRMenuPlugin
{
    public enum SystemMenuCategory
    {
        MODE = 0,
        TOOL,
        ITEM,
        MENU,
        MAX
    }

    public interface IVRControllerMode
    {
        // 以下のプロパティは登録時とシーン開始時に読む //

        // モードボタンのラベルや選択中に表示するモード名
        string ModeName { get; }

        // 無効にするとなかったコトにされる
        bool IsEnabled { get; }

        // このモードが選択中はトリガーによる掴む操作を無効化するか
        bool IsDiableGripByTrigger { get; }

        // このモードが選択中はグリップによる掴む操作を無効化するか
        bool IsDiableGripByGrip { get; }

        // モード開始
        void OnActivated(Controller controller);

        // モード終了
        void OnDeactivated(Controller controller);

        // タッチパッドの使用可能状態が変わった
        // GUIスクリーンにコントローラを近づけた、システムメニューが表示された、など
        // VRMenuはコントローラ側で表示・非表示を制御するので問題ないが
        // モード実装側でタッチパッドの入力を直接取っている場合は対応が必要
        void OnTouchPadState(Controller controller, bool enable);
    }

    // 抽象化されたメニューオブジェクト
    // メニューのテキストや設定、クリックされたときのハンドラなどの情報を保持する
    public class VRMenuPage : MonoBehaviour
    {
        // labels: 配列の長さがそのままボタンの数になる
        //         nullが入っている要素はラベルが表示されず選択もできない
        public string[] Texts;
        public bool[] ToggleState;
        public string Caption;
        public bool ShowLabel = true;
        public float AngleOffset;

        public virtual void OnPress(Controller controller, int buttonIndex) { }
        public virtual void OnClicked(Controller controller, int buttonIndex) { }
        public virtual void OnSelectionChanged(Controller controller, int buttonIndex) { }

        protected virtual void OnDestroy()
        {
            foreach (VRMenuController menuctrl in VRMenuPlugin.Instance.Controllers)
            {
                if (menuctrl != null)
                {
                    menuctrl.Menu.ReleaseMenu(this);
                }
            }
        }
    }

    // TODO: ボリュームボタンを実装する

    /// <summary>
    /// 階層メニューのすべてのボタンが実装するインターフェース
    /// </summary>
    public interface IMenuButton
    {
        bool Enabled { get; }
        int Metric { get; }

        /// <summary>
        /// ボタンが配置されたメニューから呼ばれる。通常、この中でメニューのラベルを更新する。
        /// </summary>
        /// <param name="parentMenu">ボタンが配置されたメニュー</param>
        /// <param name="buttonIndex">メニュー上でのボタンのインデックス。無効化されたボタンは-1が入る</param>
        void SetMenu(VRMenu parentMenu, int buttonIndex);

        /// <summary>
        /// ボタンがクリックされたときに呼ばれる
        /// </summary>
        /// <param name="menu">ボタンが配置されたメニュー</param>
        /// <param name="controller">イベント送信元コントローラ識別</param>
        void OnButtonClicked(VRMenu menu, Controller controller);
    }

    /// <summary>
    /// ラベルの同期だけを実装した設定メニューボタンの基本クラス
    /// </summary>
    public abstract class AbstractMenuButton : IMenuButton
    {
        private string text_;
        public string Text
        {
            get { return text_; }
            set
            {
                if (text_ != value)
                {
                    text_ = value;
                    setMenuText();
                }
            }
        }

        private bool toggle_;
        public bool ToggleState {
            get { return toggle_; }
            set {
                if (toggle_ != value)
                {
                    toggle_ = value;
                    setToggleState();
                }
            }
        }

        private bool enabled_ = true;
        public bool Enabled {
            get { return enabled_; }
            set {
                if (enabled_ != value)
                {
                    enabled_ = value;
                    setEnableState();
                }
            }
        }

        public int Metric { get; set; }

        protected int index;
        protected VRMenu menu;

        public abstract void OnButtonClicked(VRMenu menu, Controller controller);

        private void setMenuText()
        {
            if (menu != null && index >= 0 && index < menu.Texts.Length)
            {
                menu.Texts[index] = text_;
            }
        }

        private void setToggleState()
        {
            if (menu != null && index >= 0 && index < menu.ToggleState.Length)
            {
                menu.ToggleState[index] = toggle_;
            }
        }

        private void setEnableState()
        {
            if (menu != null)
            {
                menu.Reset();
            }
        }

        public virtual void SetMenu(VRMenu parentMenu, int buttonIndex)
        {
            index = buttonIndex;
            menu = parentMenu;
            setMenuText();
            setToggleState();
        }
    }

    /// <summary>
    /// モード選択用ボタンに必要な機能だけを実装したクラス
    /// </summary>
    class ModeButton : IMenuButton
    {
        private Action<ModeButton> clicked;
        public IVRControllerMode Mode;
        public SystemMenuCategory Category;
        public int Metric { get; set; }
        public bool Enabled { get { return Mode.IsEnabled; } }

        public void OnButtonClicked(VRMenu menu, Controller controller)
        {
            clicked(this);
        }

        public void SetMenu(VRMenu parentMenu, int buttonIndex)
        {
            if(buttonIndex >= 0)
            {
                parentMenu.Texts[buttonIndex] = Mode.ModeName;
            }
        }

        public ModeButton(Action<ModeButton> clicked)
        {
            this.clicked = clicked;
        }
    }

    /// <summary>
    /// クリックイベント発行を実装した、すぐに使えるシンプルなメニューボタン
    /// </summary>
    public class SimpleButton : AbstractMenuButton
    {
        //public event Action<SimpleButton, VRMenuPage, ControllerId> Clicked;
        public event Action<object, object, int> Clicked;

        public override void OnButtonClicked(VRMenu menu, Controller controller)
        {
            if (Clicked != null)
            {
                Clicked(this, menu, (int)controller);
            }
        }
    }

    /// <summary>
    /// ON/OFF切り替え用のメニューボタン
    /// </summary>
    public class ToggleButton : AbstractMenuButton
    {
        //public Func<VRMenuPage, bool> Getter;
        //public Action<VRMenuPage, bool> Setter;
        public Func<object, bool> Getter;
        public Action<object, bool> Setter;
        public string TextOn;
        public string TextOff;

        public override void OnButtonClicked(VRMenu menu, Controller controller)
        {
            bool next = !ToggleState;
            ToggleState = next;
            Text = ToggleState ? TextOn : TextOff;
            Setter(menu, next);
        }

        public override void SetMenu(VRMenu parentMenu, int buttonIndex)
        {
            ToggleState = Getter(parentMenu);
            Text = ToggleState ? TextOn : TextOff;
            base.SetMenu(parentMenu, buttonIndex);
        }
    }

    /// <summary>
    /// 階層メニューの基本機能を実装したクラス
    /// </summary>
    public class VRMenu : VRMenuPage, IMenuButton
    {
        private class ButtonHelper : AbstractMenuButton
        {
            public override void OnButtonClicked(VRMenu menu, Controller controller) { }
        }

        protected List<IMenuButton> buttons = new List<IMenuButton>();
        protected IMenuButton[] enabledButtons = null;
        private ButtonHelper buttonHelper = new ButtonHelper();

        public string Name { get; set; }

        public string Text
        {
            get { return buttonHelper.Text; }
            set { buttonHelper.Text = value; }
        }

        public bool Enabled
        {
            get { return buttonHelper.Enabled; }
            set { buttonHelper.Enabled = value; }
        }

        public int Metric { get; set; }

        public VRMenu ParentMenu { get; protected set; }

        protected bool IsNeedUpdate;

        public VRMenu()
        {
            Texts = new string[0];
            ToggleState = new bool[0];
        }

        public void Reset()
        {
            IsNeedUpdate = true;
        }

        private void OnLevelWasLoaded(int index)
        {
            IsNeedUpdate = true;
        }

        public override void OnClicked(Controller controller, int index)
        {
            if (enabledButtons != null && index < enabledButtons.Length)
            {
                enabledButtons[index].OnButtonClicked(this, controller);
            }
        }

        private void Update()
        {
            if (IsNeedUpdate)
            {
                var flags = buttons.Select(d => d.Enabled).ToArray();
                var enabledCount = flags.Count(f => f);
                Texts = new string[enabledCount];
                ToggleState = new bool[enabledCount];
                enabledButtons = new IMenuButton[enabledCount];

                int index = 0;
                for (int i = 0; i < flags.Length; ++i)
                {
                    var button = buttons[i];
                    if (flags[i])
                    {
                        enabledButtons[index] = button;
                        button.SetMenu(this, index++);
                    }
                    else
                    {
                        button.SetMenu(this, -1);
                    }
                }

                IsNeedUpdate = false;
            }
        }

        /// <summary>
        /// ボタンを追加
        /// </summary>
        public void Add(IMenuButton button)
        {
            int index = buttons.FindIndex(d => d.Metric > button.Metric);
            if(index == -1) {
                buttons.Add(button);
            }
            else {
                buttons.Insert(index, button);
            }
            IsNeedUpdate = true;
        }

        /// <summary>
        /// ボタンを削除
        /// </summary>
        public bool Remove(IMenuButton button)
        {
            int index = buttons.IndexOf(button);
            if (index != -1)
            {
                buttons.RemoveAt(index);
                IsNeedUpdate = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// ボタンTypeのインスタンスを作って追加
        /// </summary>
        public Type AddButton<Type>(string label, int metric = 0)
            where Type : AbstractMenuButton, new()
        {
            var button = new Type();
            button.Text = label;
            button.Metric = metric;
            Add(button);
            return button;
        }

        /// <summary>
        /// サブメニューTypeのインスタンスを作って追加
        /// </summary>
        public Type AddSubMenu<Type>(string label, int metric = 0)
            where Type : VRMenu
        {
            var subMenu = gameObject.AddComponent<Type>();
            subMenu.Text = label;
            subMenu.Metric = metric;
            Add(subMenu);
            return subMenu;
        }

        // ISettingMenuButton //

        public void SetMenu(VRMenu parentMenu, int buttonIndex)
        {
            buttonHelper.SetMenu(parentMenu, buttonIndex);
        }

        // 親メニューから選択されたとき
        public void OnButtonClicked(VRMenu menu, Controller controllerId)
        {
            ParentMenu = menu;

            var controller = VRMenuPlugin.Instance.Controllers[(int)controllerId];
            if (IsSystemMenu(controller))
            {
                controller.Menu.ShowSystemMenu(this);
            }
            else
            {
                controller.Menu.SetUserMenuVisiblity(menu, false);
                controller.Menu.SetUserMenuVisiblity(this, true);
            }
        }

        private bool IsSystemMenu(VRMenuController controller)
        {
            VRMenu menu = this;
            while(menu.ParentMenu != null)
            {
                menu = menu.ParentMenu;
            }
            return menu == controller.Mode.SystemMenuTop;
        }

        public void BackMenu(VRMenuController controller)
        {
            if(ParentMenu != null)
            {
                if (IsSystemMenu(controller))
                {
                    controller.Menu.SetSystemMenu(ParentMenu);
                }
                else
                {
                    controller.Menu.SetUserMenuVisiblity(this, false);
                    controller.Menu.SetUserMenuVisiblity(ParentMenu, true);
                }
            }
        }
    }

    class ModeMenu : VRMenu
    {
        public SystemMenuCategory Category;

        public void AddMode(Action<ModeButton> clicked,
            IVRControllerMode mode, SystemMenuCategory category, int metric)
        {
            Add(new ModeButton(clicked) { Mode = mode, Category = category, Metric = metric });
        }

        public bool RemoveMode(IVRControllerMode mode)
        {
            for(int i = 0; i < buttons.Count; ++i)
            {
                if(((ModeButton)buttons[i]).Mode == mode)
                {
                    buttons.RemoveAt(i);
                    IsNeedUpdate = true;
                    return true;
                }
            }
            return false;
        }
    }

    // 
    public enum RepeatButtonTickMode
    {
        Smooth, // 押している間Updateごと
        Tick,   // 押している間0.1秒ごと
        Click   // 1回の押しで1回
    }

    public class RepeatButtonsMenu : VRMenu
    {
        public RepeatButtonTickMode TickMode;
        //public event Action<VRMenuPage, ControllerId, int> OnTick;
        public event Action<object, int, int> OnTick;

        private float prevClickTime = 0;
        private void InvokeTick(Controller controllerId, int buttonIndex)
        {
            if (OnTick != null)
            {
                OnTick(this, (int)controllerId, buttonIndex);
            }
        }
        public override void OnPress(Controller controllerId, int buttonIndex)
        {
            if (TickMode == RepeatButtonTickMode.Smooth)
            {
                InvokeTick(controllerId, buttonIndex);
            }
            else if (TickMode == RepeatButtonTickMode.Tick)
            {
                if (Time.realtimeSinceStartup - prevClickTime > 0.1f)
                {
                    InvokeTick(controllerId, buttonIndex);
                    prevClickTime = Time.realtimeSinceStartup;
                }
            }
            base.OnPress(controllerId, buttonIndex);
        }
        public override void OnClicked(Controller controllerId, int buttonIndex)
        {
            if (TickMode == RepeatButtonTickMode.Click)
            {
                InvokeTick(controllerId, buttonIndex);
            }
            base.OnClicked(controllerId, buttonIndex);
        }
    }

    public class ControllerMode
    {
        private class ModeCategoryState
        {
            public IVRControllerMode current;
            public IVRControllerMode previous;
            // 無効になったので自動的に切り替えられてしまった直前のモード
            public IVRControllerMode prevEnabled;
            public bool touchPadState;
        }

        private enum ModeCategory
        {
            Mode, Item
        }

        public VRMenu SystemMenuTop { get; private set; }
        
        private VRMenuController controller;
        private VRMenu[] categories = new VRMenu[(int)SystemMenuCategory.MAX];
        private IVRControllerMode defaultMode;

        private Dictionary<IVRControllerMode, SystemMenuCategory> modeDict = new Dictionary<IVRControllerMode, SystemMenuCategory>();

        // Mode, Item
        private ModeCategoryState modeState = new ModeCategoryState();
        private ModeCategoryState itemModeState = new ModeCategoryState();
        private ModeCategoryState getModeState(SystemMenuCategory category)
        {
            return (category == SystemMenuCategory.ITEM) ? itemModeState : modeState;
        }
        private ModeCategoryState getModeState(ModeCategory modeCategory)
        {
            return (modeCategory == ModeCategory.Item) ? itemModeState : modeState;
        }

        public ControllerMode(VRMenuController controller)
        {
            this.controller = controller;

            SystemMenuTop = controller.gameObject.AddComponent<VRMenu>();
            for (int i = 0; i < (int)SystemMenuCategory.MAX; ++i)
            {
                if(i == (int)SystemMenuCategory.MENU || i == (int)SystemMenuCategory.TOOL)
                {
                    categories[i] = controller.gameObject.AddComponent<VRMenu>();
                }
                else
                {
                    var menu = controller.gameObject.AddComponent<ModeMenu>();
                    menu.Category = (SystemMenuCategory)i;
                    categories[i] = menu;
                }
                categories[i].Text = ((SystemMenuCategory)i).ToString();
                SystemMenuTop.Add(categories[i]);
            }
        }

        public void UpdateForNewScene()
        {
            var mode = getModeState(ModeCategory.Mode);
            if(mode.prevEnabled != null)
            {
                // 無効になったモードがあるなら戻す
                if(mode.prevEnabled.IsEnabled)
                {
                    SwitchMode(mode.prevEnabled, ModeCategory.Mode, true);
                    mode.prevEnabled = null;
                    return;
                }
            }
            if(mode.current != null)
            {
                // 新しいシーンで現在のモードが無効になったらデフォルトモードにする
                if(mode.current.IsEnabled == false)
                {
                    mode.prevEnabled = mode.current;
                    SwitchMode(defaultMode, ModeCategory.Mode, true);
                    return;
                }
            }
        }

        public void ChangeTouchPadState(bool enable)
        {
            var catState = getModeState(ModeCategory.Mode);
            if(catState.touchPadState != enable)
            {
                catState.touchPadState = enable;
                //Log.Out("TouchPatState changed: " + enable);
                if (catState.current == null)
                {
                    return;
                }
                var modeState = modeDict[catState.current];
                catState.current.OnTouchPadState(controller.ControllerId, enable);
            }
        }

        public IVRControllerMode GetCurrentMode()
        {
            return getModeState(ModeCategory.Mode).current;
        }

        internal void SetDefaultMode(IVRControllerMode mode)
        {
            defaultMode = mode;
        }

        public void SwitchToDefaultMode()
        {
            SwitchMode(defaultMode, ModeCategory.Mode, false);
        }

        public void SwitchToPreviousMode()
        {
            var modeState = getModeState(ModeCategory.Mode);
            if(modeState.previous != null)
            {
                SwitchMode(modeState.previous, ModeCategory.Mode, false);
            }
        }

        private void SwitchMode(IVRControllerMode newMode, ModeCategory category, bool dontTouchPrevious)
        {
            var catState = getModeState(category);
            if (catState.current != null)
            {
                if (catState.touchPadState)
                {
                    //Log.Out("変更前モードのタッチパッドを無効化します");
                    // タッチパッド状態が有効になっていたら無効にする
                    catState.touchPadState = false;
                    catState.current.OnTouchPadState(controller.ControllerId, false);
                }
                //Log.Out("変更前モードを非アクティブ化");
                catState.current.OnDeactivated(controller.ControllerId);
            }
            if(dontTouchPrevious == false)
            {
                catState.previous = catState.current;
                catState.prevEnabled = null;
            }
            catState.current = newMode;
            if (newMode != null)
            {
                if (category == ModeCategory.Mode)
                {
                    controller.Hacker.Text = newMode.ModeName;
                }
                newMode.OnActivated(controller.ControllerId);
                Log.Debug("モード変更: " + newMode.ModeName);
            }
            else
            {
                controller.Hacker.Text = "??????";
                Log.Debug("モード変更: ??????");
            }
        }

        private void ModeSelected(ModeButton button)
        {
            IVRControllerMode newMode = button.Mode;
            SystemMenuCategory category = button.Category;

            var menu = controller.Menu;
            menu.SystemMenuActive = false;
            categories[(int)category].BackMenu(controller);

            ModeCategory modeCat =
                (category == SystemMenuCategory.ITEM)
                ? ModeCategory.Item
                : ModeCategory.Mode;

            SwitchMode(newMode, modeCat, false);
        }

        public void AddMode(IVRControllerMode mode, SystemMenuCategory category, int metric)
        {
            if (modeDict.ContainsKey(mode))
            {
                return;
            }
            if (category != SystemMenuCategory.MODE && category != SystemMenuCategory.ITEM)
            {
                throw new Exception("モードを追加できるカテゴリはMODEとITEMだけです(" + category + ")");
            }

            ((ModeMenu)categories[(int)category]).AddMode(ModeSelected, mode, category, metric);
            modeDict.Add(mode, category);
        }

        public void RemoveMode(IVRControllerMode mode)
        {
            if (modeDict.ContainsKey(mode))
            {
                ((ModeMenu)categories[(int)modeDict[mode]]).RemoveMode(mode);
                modeDict.Remove(mode);
            }
        }

        public void AddButton(VRMenu menupage, SystemMenuCategory category)
        {
            if(category != SystemMenuCategory.TOOL && category != SystemMenuCategory.MENU)
            {
                throw new Exception("ボタンを追加できるカテゴリはTOOLとMENUだけです(" + category + ")");
            }
            if(String.IsNullOrEmpty(menupage.Name))
            {
                throw new Exception("VRMenu.Nameプロパティは必須項目です");
            }
            var menu = categories[(int)category] as VRMenu;
            menu.Add(menupage);
        }

        public bool RemoveButton(VRMenu menupage, SystemMenuCategory category)
        {
            if (category != SystemMenuCategory.TOOL && category != SystemMenuCategory.MENU)
            {
                throw new Exception("ボタンを追加できるカテゴリはTOOLとMENUだけです(" + category + ")");
            }
            var menu = categories[(int)category] as VRMenu;
            return menu.Remove(menupage);
        }
    }
}
