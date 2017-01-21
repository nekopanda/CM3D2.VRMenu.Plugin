using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PluginExt;
using UnityInjector;
using UnityInjector.Attributes;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace CM3D2.VRMenuPlugin
{
    public enum Controller
    {
        Left = 0,
        Right,
        Max
    }

    [PluginName("VRMenuPlugin"), PluginVersion("0.0.0.1")]
    public class VRMenuPlugin : ExPluginBase
    {
        private static VRMenuPlugin instance_;
        public static VRMenuPlugin Instance { get { return instance_; } }

        private static bool initialized_;
        public static bool Initialized { get { return initialized_; } }

        public class ControllerConfig
        {
            public bool EnableGripGUI = true;
            public bool EnableGripObject = true;
            public bool EnableGripWorld = true;
        }

        public class PluginConfig
        {
            public float GUISize = 0.3f;
            public float PointerSize = 0.02f;
            public float PointerDistance = 0.1f;
            public bool PointerAlwaysVisible = false;
        }

        private PluginConfig config_;
        public PluginConfig Config {
            get {
                if(config_ == null)
                {
                    config_ = ReadConfig<PluginConfig>();
                }
                return config_;
            }
        }
        public ControllerConfig configLeft_;
        public ControllerConfig ConfigLeft {
            get {
                if (configLeft_ == null)
                {
                    configLeft_ = ReadConfig<ControllerConfig>("Left");
                }
                return configLeft_;
            }
        }
        public ControllerConfig configRight_;
        public ControllerConfig ConfigRight {
            get {
                if (configRight_ == null)
                {
                    configRight_ = ReadConfig<ControllerConfig>("Right");
                }
                return configRight_;
            }
        }

        public bool IsNeedWriteConfig;

        private VRMenuController[] controllers_ = new VRMenuController[(int)Controller.Max];
        public VRMenuController[] Controllers { get { return controllers_; } }

        private GameObject cameraOffset;
        private GameObject cameraRig;
        private GameObject ovrScreen;

        public void ShowMenu(VRMenuPage menu, Controller controller)
        {
            SetMenuVisiblity(menu, controller, true);
        }

        public void HideMenu(VRMenuPage menu, Controller controller)
        {
            SetMenuVisiblity(menu, controller, false);
        }

        public void SetMenuVisiblity(VRMenuPage menu, Controller controller, bool visiblity)
        {
            VRMenuController menuctrl = controllers_[(int)controller];
            if (menuctrl != null)
            {
                menuctrl.Menu.SetUserMenuVisiblity(menu, visiblity);
            }
        }

        // 自分の頭
        public Transform Head {
            get {
                return GameMain.Instance.OvrMgr.EyeAnchor;
            }
        }

        // プレイルームを動かすためのハンドル
        public GameObject PlayRoomOffset {
            get {
                return cameraOffset;
            }
        }

        // プレイルームの原点（床）
        public GameObject PlayRoom {
            get {
                return cameraRig;
            }
        }

        //
        public GameObject OVRScreen {
            get {
                return ovrScreen;
            }
        }

        // Use this for initialization
        void Start()
        {
            try
            {
                Log.Out("Start");
                Log.Out(this.GetType().AssemblyQualifiedName);
                DontDestroyOnLoad(gameObject);

                // 例外の詳細情報を吐かせる
                Util.RegisterLogCallback();

                instance_ = this;

                // 設定メニューに追加
                createSettingMenu();

                initialized_ = true;

                StartCoroutine(findOvrScreen());
                StartCoroutine(writeConfigCo());

                Log.Out("Coroutine intalled ...");
            }
            catch(Exception e)
            {
                Log.Out(e);
            }
        }

        // 誰よりも早くovr_screenを見つけて非アクティブ化
        private IEnumerator findOvrScreen()
        {
            while(true)
            {
                ovrScreen = GameObject.Find("ovr_screen");
                if(ovrScreen != null)
                {
                    ovrScreen.SetActive(false);
                    StartCoroutine(tryInstallCo());
                    break;
                }
                yield return null;
            }
            yield break;
        }

        private IEnumerator debug()
        {
            while (true)
            {
                yield return null;
                var ovr_screen = GameObject.Find("ovr_screen");
                if (ovr_screen == null)
                {
                    Log.Out("ovr_screen == NULL ?????");
                    continue;
                }
                var meshre = ovr_screen.GetComponentInChildren<MeshRenderer>();
                if(meshre == null)
                {
                    Log.Out("MeshRenderer == NULL");
                    continue;
                }
                if(meshre.material == null)
                {
                    Log.Out("meshre.material == NULL");
                    continue;
                }
                if(meshre.material.mainTexture == null)
                {
                    Log.Out("meshre.material.mainTexture == NULL");
                    continue;
                }
                var rt = meshre.material.mainTexture as RenderTexture;
                if (rt == null)
                {
                    Log.Out("rtOVRUI == NULL [" + meshre.material.mainTexture.ToString() + "]");
                    continue;
                }
                Log.Out("FOUND !!!");
            }
        }

        private void createSettingMenu()
        {
            var menuGUISize = Helper.InstantiateMenu(gameObject,
                new {
                    Control = "RepeatButtonsMenu",
                    Text = "GUIサイズ",
                    Caption = "現在のサイズ: " + Config.GUISize.ToString("F2"),
                    AngleOffset = 90,
                    TickMode = "Tick",
                    OnTick = (Action<object, int, int>)((m, _, i) =>
                    {
                        float current = Config.GUISize;
                        float factor = (i == 0) ? 1.0f / 1.05f : 1.05f;
                        float next = Math.Max(Math.Min(current * factor, 6f), 0.01f);
                        Config.GUISize = next;
                        IsNeedWriteConfig = true;
                        if (GUIQuad.Instance != null)
                        {
                            GUIQuad.Instance.UpdateGuiQuadScale();
                        }
                          ((RepeatButtonsMenu)m).Caption = "現在のサイズ: " + Config.GUISize.ToString("F2");
                    }),
                    Items = new object[] {
                        "小さくする",
                        "大きくする"
                    }
                });

            var menuPointerSize = Helper.InstantiateMenu(gameObject,
                new {
                    Control = "RepeatButtonsMenu",
                    Text = "ポインタサイズ",
                    Caption = "現在のサイズ: " + (Config.PointerSize * 100).ToString("F2"),
                    AngleOffset = 90,
                    TickMode = "Tick",
                    OnTick = (Action<object, int, int>)((m, _, i) => {
                        float current = Config.PointerSize;
                        float factor = (i == 0) ? 1.0f / 1.05f : 1.05f;
                        float next = Math.Max(Math.Min(current * factor, 0.2f), 0.001f);
                        Config.PointerSize = next;
                        IsNeedWriteConfig = true;

                        foreach (var c in Controllers)
                        {
                            if (c != null)
                            {
                                c.UpdatePointerSize();
                            }
                        }
                          ((RepeatButtonsMenu)m).Caption = "現在のサイズ: " + (Config.PointerSize * 100).ToString("F2");
                    }),
                    Items = new object[] {
                        "小さくする",
                        "大きくする"
                    }
                });

            var menuPointerDistance = Helper.InstantiateMenu(gameObject,
                new {
                    Control = "RepeatButtonsMenu",
                    Text = "ポインタ距離",
                    Caption = "現在の距離: " + (Config.PointerDistance * 100).ToString("F1"),
                    AngleOffset = 90,
                    TickMode = "Tick",
                    OnTick = (Action<object, int, int>)((m, _, i) => {
                        float current = Config.PointerDistance;
                        float diff = Math.Max(Math.Abs(current) * 0.05f, 0.001f) * ((i == 0) ? -1 : 1);
                        float next = Math.Max(Math.Min(current + diff, 5), -5);
                        Config.PointerDistance = next;
                        IsNeedWriteConfig = true;

                        foreach (var c in Controllers)
                        {
                            if (c != null)
                            {
                                c.UpdatePointerDistance();
                            }
                        }
                          ((RepeatButtonsMenu)m).Caption = "現在の距離: " + (Config.PointerDistance * 100).ToString("F1");
                    }),
                    Items = new object[] {
                        "小さくする",
                        "大きくする"
                    }
                });

            var menuPointerVisible =
                new {
                    Control = "ToggleButton",
                    TextOn = "ポインタを常に表示",
                    TextOff = "ポインタを普通に表示",
                    Getter = (Func<object, bool>)(_ => Config.PointerAlwaysVisible),
                    Setter = (Action<object, bool>)((_, v) => {
                        Config.PointerAlwaysVisible = v;
                        foreach (var c in Controllers)
                        {
                            if (c != null)
                            {
                                c.UpdatePointerAlwaysVisible();
                            }
                        }
                    })
                };

            var menuGripLeft = Helper.InstantiateMenu(gameObject, createGripMenu(ConfigLeft));
            var menuGripRight = Helper.InstantiateMenu(gameObject, createGripMenu(ConfigRight));

            var menuLeft = Helper.InstantiateMenu(gameObject,
                createSettingMenu(new object[] {
                    menuGUISize, menuPointerSize, menuPointerDistance, menuPointerVisible, menuGripLeft }));
            var menuRight = Helper.InstantiateMenu(gameObject,
                createSettingMenu(new object[] {
                    menuGUISize, menuPointerSize, menuPointerDistance, menuPointerVisible, menuGripRight }));

            Helper.InstallMenuButton(this, menuLeft, menuRight);
        }

        private object createSettingMenu(object[] items)
        {
            return new {
                Name = "VRMenu設定",
                Control = "VRMenu",
                Items = items
            };
        }

        private object createGripMenu(ControllerConfig config)
        {
            return new {
                Name = "掴み設定",
                Control = "VRMenu",
                Items = new object[] {
                    new {
                        Control = "ToggleButton",
                        TextOn = "GUI-有効",
                        TextOff = "GUI-無効",
                        Getter = (Func<object, bool>)(_ => config.EnableGripGUI),
                        Setter = (Action<object, bool>)((_, v) => config.EnableGripGUI = v)
                    },
                    new {
                        Control = "ToggleButton",
                        TextOn = "オブジェクト-有効",
                        TextOff = "オブジェクト-無効",
                        Getter = (Func<object, bool>)(_ => config.EnableGripObject),
                        Setter = (Action<object, bool>)((_, v) => config.EnableGripObject = v)
                    },
                    new {
                        Control = "ToggleButton",
                        TextOn = "ワールド-有効",
                        TextOff = "ワールド-無効",
                        Getter = (Func<object, bool>)(_ => config.EnableGripWorld),
                        Setter = (Action<object, bool>)((_, v) => config.EnableGripWorld = v)
                    }
                }
            };
        }

        private void createSettingMenuOld()
        {
            RepeatButtonsMenu menuGUISize;
            RepeatButtonsMenu menuPointerSize;
            RepeatButtonsMenu menuPointerDistance;
            VRMenu menuGrip;

            VRMenu settingMenu = gameObject.AddComponent<VRMenu>();

            {
                menuGUISize = settingMenu.AddSubMenu<RepeatButtonsMenu>("GUIサイズ");
                menuGUISize.AngleOffset = 90;
                menuGUISize.TickMode = RepeatButtonTickMode.Tick;
                Action updateGUISizeCaption = () => {
                    menuGUISize.Caption = "現在のサイズ: " + Config.GUISize.ToString("F2");
                };
                menuGUISize.OnTick += (m, _, i) => {
                    float current = Config.GUISize;
                    float factor = (i == 0) ? 1.0f / 1.05f : 1.05f;
                    float next = Math.Max(Math.Min(current * factor, 6f), 0.01f);
                    Config.GUISize = next;
                    IsNeedWriteConfig = true;
                    if (GUIQuad.Instance != null)
                    {
                        GUIQuad.Instance.UpdateGuiQuadScale();
                    }
                    updateGUISizeCaption();
                };
                menuGUISize.AddButton<SimpleButton>("小さくする");
                menuGUISize.AddButton<SimpleButton>("大きくする");
                updateGUISizeCaption();
            }

            {
                menuPointerSize = settingMenu.AddSubMenu<RepeatButtonsMenu>("ポインタサイズ");
                menuPointerSize.AngleOffset = 90;
                menuPointerSize.TickMode = RepeatButtonTickMode.Tick;
                Action updatePointerSizeCaption = () => {
                    float sizeInCm = Config.PointerSize * 100;
                    menuPointerSize.Caption = "現在のサイズ: " + sizeInCm.ToString("F2");
                };
                menuPointerSize.OnTick += (m, _, i) => {
                    float current = Config.PointerSize;
                    float factor = (i == 0) ? 1.0f / 1.05f : 1.05f;
                    float next = Math.Max(Math.Min(current * factor, 0.2f), 0.001f);
                    Config.PointerSize = next;
                    IsNeedWriteConfig = true;

                    foreach (var c in Controllers)
                    {
                        if (c != null)
                        {
                            c.UpdatePointerSize();
                        }
                    }
                    updatePointerSizeCaption();
                };
                menuPointerSize.AddButton<SimpleButton>("小さくする");
                menuPointerSize.AddButton<SimpleButton>("大きくする");
                updatePointerSizeCaption();
            }

            {
                menuPointerDistance = settingMenu.AddSubMenu<RepeatButtonsMenu>("ポインタ距離");
                menuPointerDistance.AngleOffset = 90;
                menuPointerDistance.TickMode = RepeatButtonTickMode.Tick;
                Action updatePointerDistanceCaption = () => {
                    float sizeInCm = Config.PointerDistance * 100;
                    menuPointerDistance.Caption = "現在の距離: " + sizeInCm.ToString("F1");
                };
                menuPointerDistance.OnTick += (m, _, i) => {
                    float current = Config.PointerDistance;
                    float diff = Math.Max(Math.Abs(current) * 0.05f, 0.001f) * ((i == 0) ? -1 : 1);
                    float next = Math.Max(Math.Min(current + diff, 5), -5);
                    Config.PointerDistance = next;
                    IsNeedWriteConfig = true;

                    foreach (var c in Controllers)
                    {
                        if (c != null)
                        {
                            c.UpdatePointerDistance();
                        }
                    }
                    updatePointerDistanceCaption();
                };
                menuPointerDistance.AddButton<SimpleButton>("小さくする");
                menuPointerDistance.AddButton<SimpleButton>("大きくする");
                updatePointerDistanceCaption();
            }

            {
                menuGrip = settingMenu.AddSubMenu<VRMenu>("掴み設定");
                menuGrip.Caption = "掴み設定";
                menuGrip.Add(new ToggleButton() {
                    TextOn = "GUI-有効",
                    TextOff = "GUI-無効",
                    Getter = _ => ConfigLeft.EnableGripGUI,
                    Setter = (_, v) => ConfigLeft.EnableGripGUI = v
                });
                menuGrip.Add(new ToggleButton() {
                    TextOn = "オブジェクト-有効",
                    TextOff = "オブジェクト-無効",
                    Getter = _ => ConfigLeft.EnableGripObject,
                    Setter = (_, v) => ConfigLeft.EnableGripObject = v
                });
                menuGrip.Add(new ToggleButton() {
                    TextOn = "ワールド-有効",
                    TextOff = "ワールド-無効",
                    Getter = _ => ConfigLeft.EnableGripWorld,
                    Setter = (_, v) => ConfigLeft.EnableGripWorld = v
                });
            }
        }

        private IEnumerator writeConfigCo()
        {
            while(true)
            {
                if(IsNeedWriteConfig)
                {
                    SaveConfig(Config);
                    SaveConfig(ConfigLeft, "Left");
                    SaveConfig(ConfigRight, "Right");
                    IsNeedWriteConfig = false;
                }
                yield return new WaitForSeconds(2);
            }
        }

        private IEnumerator tryInstallCo()
        {
            cameraOffset = null;

            while (true)
            {
                if (cameraOffset == null)
                {
                    tryGetCamOffset();
                }
                if(cameraOffset != null)
                {
                    if (controllers_.Any(s => s == null))
                    {
                        tryInstallController();
                    }
                    if(!controllers_.Any(s => s == null))
                    {
                        break;
                    }
                    Log.Out("not completed ...");
                }
                yield return new WaitForSeconds(0.5f);
            }

            yield break;
        }

        private void tryGetCamOffset()
        {
            Transform transform = Util.SearchInChildren(GameMain.Instance.transform, "ViveCamOffset", 0, 1);
            if(transform != null)
            {
                cameraOffset = transform.gameObject;
                Log.Out("ViveCamOffset found");
            }
        }

        private void installControllerVIVE(GameObject go, Controller controller)
        {
            ViveVRMenuController menuctrl = go.GetComponent<ViveVRMenuController>();
            if (menuctrl == null)
            {
                menuctrl = go.AddComponent<ViveVRMenuController>();
                menuctrl.ControllerId = controller;
                if(cameraRig == null)
                {
                    cameraRig = go.transform.parent.gameObject;
                }
            }
            controllers_[(int)controller] = menuctrl;
        }

        private void tryInstallController()
        {
            var ovr_obj = GameMain.Instance.OvrMgr.ovr_obj;
            if (ovr_obj != null)
            {
                if (controllers_[(int)Controller.Left] == null && ovr_obj.left_controller.track_object != null)
                {
                    GameObject left = ovr_obj.left_controller.track_object.gameObject;
                    if (left != null)
                    {
                        installControllerVIVE(left, Controller.Left);
                    }
                }
                if (controllers_[(int)Controller.Right] == null && ovr_obj.right_controller.track_object != null)
                {
                    GameObject right = ovr_obj.right_controller.track_object.gameObject;
                    if (right != null)
                    {
                        installControllerVIVE(right, Controller.Right);
                    }
                }
            }
        }
    }
}
