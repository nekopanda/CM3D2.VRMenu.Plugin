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
using System.Reflection;

namespace CM3D2.VRMenu.Plugin
{
    public enum Controller
    {
        Left = 0,
        Right,
        Max
    }

    [
        PluginFilter("CM3D2VRx64"),
        PluginFilter("CM3D2OHVRx64"),
        PluginName("VRMenuPlugin"),
        PluginVersion("0.0.1.0")
    ]
    public class VRMenuPlugin : ExPluginBase
    {
        private static VRMenuPlugin instance_;
        public static VRMenuPlugin Instance { get { return instance_; } }

        private static bool initialized_;
        public static bool Initialized { get { return initialized_; } }

        public class ControllerConfig
        {
            public bool EnableGripGUI = true;
            public bool EnableGripMaid = true;
            public bool EnableGripObject = true;
            public bool EnableGripWorld = true;
        }

        public class PluginConfig
        {
            public float GUISize = 0.3f;
            public float PointerSize = 0.02f;
            public float PointerDistance = 0.1f;
            public bool PointerAlwaysVisible = false;
            // シーン開始時の頭の高さオフセット
            public float HeadOffset = 0;
            public bool EnableLightPhysics = true;
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

        public SpawnItemUI SpawnItemUI { get; private set; }

        public ItemManager SpawnItemManager {
            get {
                if(SpawnItemUI == null)
                {
                    return null;
                }
                return SpawnItemUI.ItemManager;
            }
        }

        // UnityInjectorにAddComponentしていくのはお行儀が悪いので
        // プラグイン専用のゲームオブジェクトを作る
        private GameObject pluginRootObj;

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

        //
        // Vive VRカメラと位置について
        // 
        // CameraRigはVive VRカメラのこと
        // これはユーザのプレイルームの原点（真ん中の床）を表している
        // CM3D2ではViveCamOffsetというオブジェクトをゲームメインにぶら下げて
        // 空間の移動（＝CameraRigの移動）は、まずViveCamOffsetを目的の位置に動かして
        // ViveCamera.Update()でCameraRigの位置をViveCamOffsetの位置に合わせている
        // このとき実際にはViveCamOffsetにぶら下げたBaseオブジェクトのTransformをCameraRigにコピーしてる
        // CameraRigの位置は実際には床の位置なので、ユーザの頭はそれよりも高いところにある
        // BaseオブジェクトはCameraRigの位置とユーザの頭の位置(EyeAnchor)の違いを
        // 吸収するためにあると思われるが、VR ver1.29現在、
        // 実装が未完成で有効に使われていない状態となっている
        //
        // 本プラグインではシーン開始時にBaseオブジェクトを
        // 現在の頭の位置から逆算した位置に合わせるようにした
        // 

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

        private Type lightPhysics;

        // Use this for initialization
        void Start()
        {
            DontDestroyOnLoad(gameObject);

            pluginRootObj = new GameObject("VRMenuPlugin");
            pluginRootObj.transform.parent = transform;

            instance_ = this;

            // SpawnItemUI初期化
            var itemUI = pluginRootObj.AddComponent<SpawnItemUI>();
            itemUI.Initialize();
            SpawnItemUI = itemUI;
            
            // LightPhysics
            lightPhysics = Type.GetType("CM3D2.LightPhysics.Managed.LightPhysics, CM3D2.LightPhysics.Managed");

            // 設定メニューに追加
            createSettingMenu();

            // 設定を反映
            setLightyPhysics();

            initialized_ = true;

            StartCoroutine(findOvrScreenCo());
            StartCoroutine(writeConfigCo());

            Log.Debug("Coroutine intalled ...");
        }

        private void setLightyPhysics()
        {
            if(lightPhysics != null)
            {
                lightPhysics.GetProperty("Enable").SetValue(null, Config.EnableLightPhysics, null);
            }
        }

        private Transform m_trOffseBase;
        private void OnLevelWasLoaded(int level)
        {
            // シーン開始時にオフセットベースを頭の位置に合わせて調整
            if(PlayRoom != null)
            {
                if(m_trOffseBase == null)
                {
                    var camera = GameMain.Instance.OvrMgr.OvrCamera;
                    var field = camera.GetType().GetField("m_trOffseBase", BindingFlags.Instance | BindingFlags.NonPublic);
                    m_trOffseBase = (Transform)field.GetValue(camera);
                }

                if(m_trOffseBase != null)
                {
                    var headToPlayRoom = PlayRoom.transform.position - Head.position;
                    headToPlayRoom.y += Config.HeadOffset;
                    var pos = m_trOffseBase.localPosition;
                    m_trOffseBase.localPosition = new Vector3(0, headToPlayRoom.y, 0);
                }
            }
        }

        // 誰よりも早くovr_screenを見つけて非アクティブ化
        private IEnumerator findOvrScreenCo()
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
                    Log.Debug("ovr_screen == NULL ?????");
                    continue;
                }
                var meshre = ovr_screen.GetComponentInChildren<MeshRenderer>();
                if(meshre == null)
                {
                    Log.Debug("MeshRenderer == NULL");
                    continue;
                }
                if(meshre.material == null)
                {
                    Log.Debug("meshre.material == NULL");
                    continue;
                }
                if(meshre.material.mainTexture == null)
                {
                    Log.Debug("meshre.material.mainTexture == NULL");
                    continue;
                }
                var rt = meshre.material.mainTexture as RenderTexture;
                if (rt == null)
                {
                    Log.Debug("rtOVRUI == NULL [" + meshre.material.mainTexture.ToString() + "]");
                    continue;
                }
                Log.Debug("FOUND !!!");
            }
        }

        private void createSettingMenu()
        {
            // 設定
            var menuGUISize = Helper.InstantiateMenu(pluginRootObj,
                new {
                    Control = "RepeatButtonsMenu",
                    Text = "GUIサイズ",
                    Caption = "現在のサイズ: " + Config.GUISize.ToString("F2"),
                    AngleOffset = 90,
                    TickMode = "Tick",
                    OnTick = (Action<object, int, int>)((m, _, i) => {
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

            var headOffsetDistance = Helper.InstantiateMenu(pluginRootObj,
                new {
                    Control = "RepeatButtonsMenu",
                    Text = "頭の高さ調整",
                    Caption = "現在のオフセット: " + (int)Math.Round(Config.HeadOffset * 100) + "cm",
                    AngleOffset = 90,
                    TickMode = "Tick",
                    OnTick = (Action<object, int, int>)((m, _, i) => {
                        float current = Config.HeadOffset;
                        float diff = 0.02f * ((i == 0) ? -1 : 1);
                        float next = Math.Max(Math.Min(current + diff, 2.0f), -2.0f);
                        Config.HeadOffset = next;
                        IsNeedWriteConfig = true;
                        ((RepeatButtonsMenu)m).Caption = "現在のオフセット: " + (int)Math.Round(Config.HeadOffset * 100) + "cm";
                    }),
                    Items = new object[] {
                        "低くする",
                        "高くする"
                    }
                });

            var settingList = new List<object>();

            settingList.Add(menuGUISize);
            settingList.Add(headOffsetDistance);

            if (lightPhysics != null)
            {
                var lightPhysicsEnable =
                    new {
                        Control = "ToggleButton",
                        TextOn = "LightPhysics:有効",
                        TextOff = "LightPhysics:無効",
                        Getter = (Func<object, bool>)(_ => Config.PointerAlwaysVisible),
                        Setter = (Action<object, bool>)((_, v) => {
                            Config.EnableLightPhysics = v;
                            IsNeedWriteConfig = true;
                            setLightyPhysics();
                        })
                    };

                settingList.Add(lightPhysicsEnable);
            }

            var settingMenu = Helper.InstantiateMenu(pluginRootObj, new {
                Name = "VRMenu設定",
                Control = "VRMenu",
                Items = settingList.ToArray()
            });

            Helper.InstallMenuButton(this, settingMenu);

            // ポインタ設定
            var menuPointerSize = Helper.InstantiateMenu(pluginRootObj,
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

            var menuPointerDistance = Helper.InstantiateMenu(pluginRootObj,
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
                    TextOn = "ポインタを常に表示:ON",
                    TextOff = "ポインタを常に表示:OFF",
                    Getter = (Func<object, bool>)(_ => Config.PointerAlwaysVisible),
                    Setter = (Action<object, bool>)((_, v) => {
                        Config.PointerAlwaysVisible = v;
                        IsNeedWriteConfig = true;
                        foreach (var c in Controllers)
                        {
                            if (c != null)
                            {
                                c.UpdatePointerAlwaysVisible();
                            }
                        }
                    })
                };

            var menuGripLeft = Helper.InstantiateMenu(pluginRootObj, createGripMenu(ConfigLeft));
            var menuGripRight = Helper.InstantiateMenu(pluginRootObj, createGripMenu(ConfigRight));

            var menuLeft = Helper.InstantiateMenu(pluginRootObj,
                createPointerMenu(new object[] {
                    menuPointerVisible, menuPointerSize, menuPointerDistance, menuGripLeft }));
            var menuRight = Helper.InstantiateMenu(pluginRootObj,
                createPointerMenu(new object[] {
                    menuPointerVisible, menuPointerSize, menuPointerDistance, menuGripRight }));

            Helper.InstallMenuButton(this, menuLeft, menuRight);
        }

        private object createPointerMenu(object[] items)
        {
            return new {
                Name = "ポインタ設定",
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
                        TextOn = "メイド-有効",
                        TextOff = "メイド-無効",
                        Getter = (Func<object, bool>)(_ => config.EnableGripMaid),
                        Setter = (Action<object, bool>)((_, v) => config.EnableGripMaid = v)
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

        // 設定が変更されていたらファイルに書き込み
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

        // コントローラをインストール
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
                    Log.Debug("not completed ...");
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
                Log.Debug("ViveCamOffset found");
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
