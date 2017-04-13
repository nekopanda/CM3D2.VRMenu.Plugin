using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityInjector;
using UnityInjector.Attributes;

namespace CM3D2.VRMenu.MyKeyInput
{
    [
        PluginFilter("CM3D2VRx64"),
        PluginFilter("CM3D2OHVRx64"),
        PluginName("VRMenuMyKeyInput"),
        PluginVersion("0.0.3.1")
    ]
    public class VRMenuMyKeyInput : PluginBase
    {
        private void Start()
        {
            var type = Type.GetType("CM3D2.VRMenu.Plugin.Helper, CM3D2.VRMenu.Plugin");
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
            installToolButton.Invoke(null, new object[] { this, createDesktopScreenMenu() });
            // Console.WriteLine("[VRMenuMyKeyInput] メニューをインストールしました");
        }

        // keycodeについて
        // アルファベットは大文字で指定
        // 数字はそのまま
        // ファンクションキーはGetFKeyで変換（1～24まで）
        // その他のキーはKEYに定義されてる

        private int GetFKey(int num)
        {
            return 0x70 + (num - 1);
        }

        private enum KEY
        {
            BACKSPACE = 0x08,
            TAB = 0x09,
            ENTER = 0x0D,
            SHIFT = 0x10,
            CTRL = 0x11,
            ALT = 0x12,
            PAUSE = 0x13,
            CAPSLOCK = 0x14,
            ESC = 0x1B,
            SPACE = 0x20,
            PAGEUP = 0x21,
            PAGEDOWN = 0x22,
            END = 0x23,
            HOME = 0x24,
            LEFT = 0x25,   // ←
            UP = 0x26,     // ↑
            RIGHT = 0x27,  // →
            DOWN = 0x28,   // ↓
            PRINTSCREEN = 0x2C,
            INSERT = 0x2D,
            DELETE = 0x2E,

            // 以下日本語キーボード用
            COLON = 0xBA,      // :
            SEMICOLON = 0xBB,  // ;
            COMMA = 0xBC,      // ,
            MINUS = 0xBD,      // -
            PERIOD = 0xBE,     // .
            SLASH = 0xBF,      // /
            AT = 0xC0,         // @

            LEFT_KAKKO = 0xDB, // [
            YEN = 0xDC,        // \
            RIGHT_KAKKO = 0xDD,// [
            HAT = 0xDE,        // ^
            BACK_SLASH = 0xE2, // ＼
        }

        [DllImport("user32.dll")]
        public static extern uint keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private void KeyInput(int keycode)
        {
            StartCoroutine(KeyInputCo(keycode, 0, 0));
        }

        // 2つ同時押し
        private void KeyInput(int keycode1, int keycode2)
        {
            StartCoroutine(KeyInputCo(keycode1, keycode2, 0));
        }

        // 3つ同時押し
        private void KeyInput(int keycode1, int keycode2, int keycode3)
        {
            StartCoroutine(KeyInputCo(keycode1, keycode2, keycode3));
        }

        private IEnumerator KeyInputCo(int keycode1, int keycode2, int keycode3)
        {
            uint KEYEVENTF_KEYUP = 2;
            keybd_event((byte)keycode1, 0, 0, (UIntPtr)0);
            yield return null;
            if(keycode2 != 0)
            {
                keybd_event((byte)keycode2, 0, 0, (UIntPtr)0);
                yield return null;
                if (keycode3 != 0)
                {
                    keybd_event((byte)keycode3, 0, 0, (UIntPtr)0);
                    yield return null;
                    keybd_event((byte)keycode3, 0, KEYEVENTF_KEYUP, (UIntPtr)0);
                    yield return null;
                }
                keybd_event((byte)keycode2, 0, KEYEVENTF_KEYUP, (UIntPtr)0);
                yield return null;
            }
            keybd_event((byte)keycode1, 0, KEYEVENTF_KEYUP, (UIntPtr)0);
            yield break;
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
                        Text = "HalfDressing(F2)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput(GetFKey(2)))
                    },
                    new {
                        Control = "SimpleButton",
                        Text = "複数メイド撮影(F7)",
                        Clicked = (Action<object, object, int>)((_,__,___)=>KeyInput(GetFKey(7)))
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
