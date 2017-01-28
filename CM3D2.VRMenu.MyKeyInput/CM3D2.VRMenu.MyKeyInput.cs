using PluginExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityInjector.Attributes;

namespace CM3D2.VRMenu.MyKeyInput
{
    [
        PluginFilter("CM3D2VRx64"),
        PluginFilter("CM3D2OHVRx64"), 
        PluginName("VRMenuMyKeyInput"),
        PluginVersion("0.0.0.1")
    ]
    public class VRMenuMyKeyInput : ExPluginBase
    {
        private void Start()
        {
            var type = Type.GetType("CM3D2.VRMenuPlugin.Helper, CM3D2.VRMenu.Plugin");
            if(type == null)
            {
                Console.WriteLine("[VRMenuMyKeyInput] VRMenuプラグインが読み込めないため無効化されました");
                return;
            }
            var types = new Type[] { typeof(MonoBehaviour), typeof(object) };
            var installToolButton = type.GetMethod("InstallToolButton", types);
            var installMenuButton = type.GetMethod("InstallMenuButton", types);
            if (installToolButton == null || installMenuButton == null)
            {
                Console.WriteLine("[VRMenuMyKeyInput] メソッドの取得に失敗しました");
                return;
            }
            installToolButton.Invoke(null, new object[] { this, createVibeYourMaidController() });
            installMenuButton.Invoke(null, new object[] { this, createEtcController() });
            installMenuButton.Invoke(null, new object[] { this, createDesktopScreenMenu() });
            Console.WriteLine("[VRMenuMyKeyInput] メニューをインストールしました");
        }

        private int GetFKey(int num)
        {
            return 0x70 + (num - 1);
        }

        [DllImport("user32.dll")]
        public static extern uint keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private void KeyInput(int keycode)
        {
            uint KEYEVENTF_KEYUP = 2;
            keybd_event((byte)keycode, 0, 0, (UIntPtr)0);
            keybd_event((byte)keycode, 0, KEYEVENTF_KEYUP, (UIntPtr)0);
        }

        private object createVibeYourMaidController()
        {
            return new {
                Name = "VibeYourMaid",
                Control = "VRMenu",
                Metric = 20,
                Items = new object[] {
                    new {
                        Control = "VRMenu",
                        Text = "リモコン",
                        Caption = "リモコンスイッチ",
                        Items = new object[] {
                            new {
                                Control = "SimpleButton",
                                Text = "停止",
                                Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput('J'))
                            },
                            new {
                                Control = "SimpleButton",
                                Text = "弱",
                                Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput('K'))
                            },
                            new {
                                Control = "SimpleButton",
                                Text = "強",
                                Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput('L'))
                            }
                        }
                    },
                    new {
                        Control = "SimpleButton",
                        Text = "GUI表示切り替え(O)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput('O'))
                    },
                    new {
                        Control = "SimpleButton",
                        Text = "プラグインON/OFF(I)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput('I'))
                    }
                }
            };
        }

        private object createEtcController()
        {
            return new {
                Name = "マイメニュー",
                Control = "VRMenu",
                Metric = 20,
                Items = new object[] {
                    new {
                        Control = "SimpleButton",
                        Text = "AddMods/YotogiSlider(F5)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput(GetFKey(5)))
                    },
                    new {
                        Control = "SimpleButton",
                        Text = "ShapeAnimator(F4)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput(GetFKey(4)))
                    },
                    new {
                        Control = "SimpleButton",
                        Text = "KissYourMaid(H)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput('H'))
                    },
                    new {
                        Control = "SimpleButton",
                        Text = "YotogiUtil(F3)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput(GetFKey(3)))
                    },
                    new {
                        Control = "SimpleButton",
                        Text = "??????(F14)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput(GetFKey(14)))
                    }
                }
            };
        }

        private object createDesktopScreenMenu()
        {
            return new {
                Name = "デスクトップスクリーン",
                Control = "RepeatButtonsMenu",
                Metric = 20,
                TickMode = "Smooth",
                OnTick = (Action<object, int, int>)((m, _, i) =>
                {
                    if(i == 1)
                    { // 拡大
                        KeyInput(GetFKey(11));
                    }
                    else if(i == 2)
                    { // 縮小
                        KeyInput(GetFKey(12));
                    }
                }),
                Items = new object[] {
                    new {
                        Control = "SimpleButton",
                        Text = "表示切り替え",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput(GetFKey(10)))
                    },
                    "拡大",
                    "縮小"
                }
            };
        }
    }
}
