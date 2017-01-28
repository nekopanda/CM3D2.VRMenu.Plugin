using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CM3D2.VRMenuPlugin
{
    public static class Log
    {
        public static void Out(string str)
        {
            Console.WriteLine("[VRM] " + str);
        }

        public static void Out(Exception e)
        {
            Out(e.Message);
            Out(e.StackTrace);
            if (e.InnerException != null)
            {
                Out("Inner exception:");
                Out(e.InnerException);
            }
        }
    }

    // カウンタ付きセット
    // 1つのアイテムを複数回入れたいときに使う
    // バックエンドはリストなので、要素数が小さいとき用
    public class ReferenceCountSet<ITEM>
    {
        private List<ITEM> items = new List<ITEM>();
        private List<int> counts = new List<int>();

        public List<ITEM> Items { get { return items; } }
        public List<int> ReferenceCounts { get { return counts; } }
        public int Count { get { return items.Count; } }

        public void Add(ITEM item)
        {
            int index = items.IndexOf(item);
            if (index == -1)
            {
                items.Add(item);
                counts.Add(1);
            }
            else
            {
                counts[index]++;
            }
        }

        public bool Remove(ITEM item)
        {
            int index = items.IndexOf(item);
            if (index != -1)
            {
                if (--counts[index] == 0)
                {
                    items.RemoveAt(index);
                    counts.RemoveAt(index);
                }
                return true;
            }
            return false;
        }

        public bool ForceRemove(ITEM item)
        {
            int index = items.IndexOf(item);
            if (index != -1)
            {
                items.RemoveAt(index);
                counts.RemoveAt(index);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            items.Clear();
            counts.Clear();
        }
    }

    public static class Util
    {

        public static Transform SearchInChildren(Transform parent, string name, int depth, int maxdepth)
        {
            foreach (Transform t in parent)
            {
                if (t.name == name)
                {
                    return t;
                }
                if (depth < maxdepth)
                {
                    Transform result = SearchInChildren(t, name, depth + 1, maxdepth);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        public static bool LinePlaneIntersect(Vector3 planeNormal, Vector3 planePos, Vector3 line, Vector3 linePos, out Vector3 point)
        {
            var nline = Vector3.Dot(planeNormal, line);
            if (nline == 0)
            {
                point = Vector3.zero;
                return false;
            }
            point = linePos + ((Vector3.Dot(planeNormal, planePos) - Vector3.Dot(planeNormal, linePos)) / nline) * line;
            return true;
        }

        // ゲーム内で掴める対象
        public static Transform GetGripTarget(GameObject obj)
        {
            if (obj == null || obj.transform == null)
            {
                return null;
            }
            Transform transform = GetTargetTransform(obj);
            if (transform == null)
            {
                if (obj.layer == 17)
                {
                    //Log.Out(obj.layer.ToString());
                    transform = obj.transform;
                }
            }
            return transform;
        }

        private static Transform GetTargetTransform(GameObject obj)
        {
            Transform transform = obj.transform;
            while (transform != null && transform.parent != null)
            {
                if (transform.parent.gameObject.name == "PhotoPrefab" ||
                    transform.parent.gameObject.name == "AllOffset")
                {
                    //Log.Out(transform.parent.gameObject.name);
                    return transform;
                }
                transform = transform.parent;
            }
            return null;
        }

        private static bool? isChubLip_ = null;
        public static bool IsChubLip()
        {
            if (isChubLip_ == null)
            {
                isChubLip_ = Application.dataPath.Contains("CM3D2OH");
            }
            return (bool)isChubLip_;
        }
    }

    public static class WinAPI
    {
        private static IntPtr windowHandle_;
        public static IntPtr WindowHandle
        {
            get
            {
                if (windowHandle_ == IntPtr.Zero)
                {
                    StringBuilder tsb = new StringBuilder(100);
                    foreach (IntPtr hwnd in EnumerateProcessWindowHandles())
                    {
                        tsb.Length = 0;
                        GetWindowText(hwnd, tsb, tsb.Capacity);
                        if (tsb.ToString() == "CUSTOM MAID 3D 2")
                        {
                            windowHandle_ = hwnd;
                            break;
                        }
                    }
                    if (windowHandle_ == IntPtr.Zero)
                    {
                        Log.Out("メインウィンドウが見つかりませんでした！！");
                    }
                }
                return windowHandle_;
            }
        }

        public const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const int MOUSEEVENTF_LEFTUP = 0x0004;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const int MOUSEEVENTF_RIGHTUP = 0x0010;

        [DllImport("USER32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, ref uint lpdwProcessId);

        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumThreadDelegate lpfn, IntPtr lParam);

        public static IEnumerable<IntPtr> EnumerateProcessWindowHandles()
        {
            var handles = new List<IntPtr>();
            int id = Process.GetCurrentProcess().Id;

            var ret = EnumWindows((hWnd, lParam) =>
            {
                uint pid = 0;
                GetWindowThreadProcessId(hWnd, ref pid);
                if (id == (int)pid)
                {
                    handles.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);

            return handles;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr WindowFromPoint(POINT lpPoint);

    }

    public class Asset
    {
        private static Asset instance_;
        public static Asset Instance
        {
            get
            {
                if (instance_ == null)
                {
                    instance_ = new Asset();
                }
                return instance_;
            }
        }

        // IMGUIとOVRUIをブレンドするシェーダ
        public Shader UIBlendShader;

        // ポインタに使うシェーダ (Overlay + 500)
        public Shader PointerShader;
        public Shader ColorShader;

        // ボタンラベルに使うシェーダ
        public Shader LabelShader;
        public Shader UIDefaultShader;
        public Shader UIFontShader;

        public Asset()
        {
            string assetpath = Path.GetDirectoryName(typeof(GUIQuad).Assembly.Location) + "\\cm3d2.vrmenu.plugin";
            Log.Out("loading asset bundle ...");
            var assetBundle = AssetBundle.LoadFromFile(assetpath);
            if (assetBundle == null)
            {
                Log.Out("シェーダのロードに失敗しました。cm3d2.vrmneu.pluginおよびそのmanifestファイルをプラグインDLLと同じフォルダにおいていることを確認してください。または、ゲーム本体のアップデートが必要かもしれません");
            }
            else
            {
                Log.Out("loading shader ...");
                UIBlendShader = (Shader)assetBundle.LoadAsset("Assets/UIBlend.shader", typeof(Shader));
                PointerShader = (Shader)assetBundle.LoadAsset("Assets/Pointer.shader", typeof(Shader));
                ColorShader = (Shader)assetBundle.LoadAsset("Assets/Color.shader", typeof(Shader));
                LabelShader = (Shader)assetBundle.LoadAsset("Assets/Label.shader", typeof(Shader));
                UIDefaultShader = (Shader)assetBundle.LoadAsset("Assets/UI-Default.shader", typeof(Shader));
                UIFontShader = (Shader)assetBundle.LoadAsset("Assets/UI-DefaultFont.shader", typeof(Shader));
                // もう必要ないのでアンロードしておく
                assetBundle.Unload(false);
            }
        }
    }

    // Menu, Mode インストールヘルパー
    public static class Helper
    {
        public static void InstallMode(MonoBehaviour component, IVRControllerMode mode, SystemMenuCategory category, int metric)
        {
            for(int i = 0; i < (int)Controller.Max; ++i)
            {
                component.StartCoroutine(installModeCo(i, mode, category, metric));
            }
        }

        private static IEnumerator installModeCo(int controllerIndex, IVRControllerMode mode, SystemMenuCategory category, int metric)
        {
            while (VRMenuPlugin.Instance == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
            while (VRMenuPlugin.Instance.Controllers[controllerIndex] == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
            VRMenuPlugin.Instance.Controllers[controllerIndex].Mode.AddMode(mode, category, metric);
            yield break;
        }

        public static void InstallToolButton(MonoBehaviour component, object source)
        {
            InstallButton(component, source, SystemMenuCategory.TOOL);
        }

        public static void InstallToolButton(MonoBehaviour component, object leftSource, object rightSource)
        {
            InstallButton(component, leftSource, rightSource, SystemMenuCategory.TOOL);
        }

        public static void InstallMenuButton(MonoBehaviour component, object source)
        {
            InstallButton(component, source, SystemMenuCategory.MENU);
        }

        public static void InstallMenuButton(MonoBehaviour component, object leftSource, object rightSource)
        {
            InstallButton(component, leftSource, rightSource, SystemMenuCategory.MENU);
        }

        public static void InstallButton(MonoBehaviour component, object source, SystemMenuCategory category)
        {
            VRMenu menu = MenuFromSource(component, source);
            for (int i = 0; i < (int)Controller.Max; ++i)
            {
                component.StartCoroutine(installMenuCo(i, menu, category));
            }
        }

        public static void InstallButton(MonoBehaviour component, object leftSource, object rightSource, SystemMenuCategory category)
        {
            VRMenu leftMenu = MenuFromSource(component, leftSource);
            VRMenu rightMenu = MenuFromSource(component, rightSource);
            component.StartCoroutine(installMenuCo((int)Controller.Left, leftMenu, category));
            component.StartCoroutine(installMenuCo((int)Controller.Right, rightMenu, category));
        }

        private static VRMenu MenuFromSource(MonoBehaviour component, object source)
        {
            VRMenu menu;
            if (source is VRMenu)
            {
                menu = (VRMenu)source;
            }
            else
            {
                var menuCreated = CreateMenuInternal(component.gameObject, source);
                if (!(menuCreated is VRMenu))
                {
                    throw new Exception("メニューのトップはメニューでなければなりません");
                }
                menu = (VRMenu)menuCreated;
            }
            return menu;
        }

        private static IEnumerator installMenuCo(int controllerIndex, VRMenu menu, SystemMenuCategory category)
        {
            Log.Out("Start to install menu " + controllerIndex);
            while (VRMenuPlugin.Instance == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
            while (VRMenuPlugin.Instance.Controllers[controllerIndex] == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
            VRMenuPlugin.Instance.Controllers[controllerIndex].Mode.AddButton(menu, category);
            Log.Out("Menu install finished " + controllerIndex);
            yield break;
        }

        private static T GetValue<T>(Type type, string fieldName, object source, bool isOptional)
        {
            // フィールドがあるか見る
            var field = type.GetField(fieldName);
            if (field != null)
            {
                var value = field.GetValue(source);
                if (value != null && !(value is T))
                {
                    throw new Exception("メニューソース " + fieldName +
                        "フィールド値が" + typeof(T).Name + "型ではありません: " + source.ToString());
                }
                return (T)value;
            }
            // プロパティがあるか見る
            var prop = type.GetProperty(fieldName);
            if (prop != null)
            {
                var value = prop.GetValue(source, null);
                if (value != null && !(value is T))
                {
                    throw new Exception("メニューソース " + fieldName +
                        "プロパティ値が" + typeof(T).Name + "型ではありません: " + source.ToString());
                }
                return (T)value;
            }
            // どっちも定義されていない
            if (isOptional)
            {
                return default(T);
            }
            throw new Exception("メニューソースに" + fieldName +
                "フィールドがありません: " + source.ToString());
        }

        private static string OptionalString(string text, string fallback)
        {
            if(text == null)
            {
                // 値が指定されていなかったらfallback
                return fallback;
            }
            if(text == "")
            {
                // 空文字が明示的に指定されていたらnull
                return null;
            }
            // 上記以外の場合はそのまま
            return text;
        }

        private static VRMenu initVRMenu(VRMenu menu, GameObject parent, object source)
        {
            var type = source.GetType();
            menu.Metric = GetValue<int>(type, "Metric", source, true);
            menu.AngleOffset = GetValue<int>(type, "AngleOffset", source, true);
            string name = GetValue<string>(type, "Name", source, true);
            string text = GetValue<string>(type, "Text", source, true);
            string caption = GetValue<string>(type, "Caption", source, true);
            menu.Name = name;
            menu.Text = OptionalString(text, name);
            menu.Caption = OptionalString(caption, menu.Text);
            object[] items = GetValue<object[]>(type, "Items", source, true);
            if (items != null)
            {
                foreach (var item in items)
                {
                    menu.Add(CreateMenuInternal(parent, item));
                }
            }
            return menu;
        }

        private static IMenuButton CreateMenuInternal(GameObject parent, object source)
        {
            if(source is string)
            {
                SimpleButton button = new SimpleButton();
                button.Text = (string)source;
                return button;
            }
            else if(source is IMenuButton)
            {
                return (IMenuButton)source;
            }

            var type = source.GetType();
            var controlName = GetValue<string>(type, "Control", source, false);
            if(controlName == "SimpleButton")
            {
                SimpleButton button = new SimpleButton();
                button.Metric = GetValue<int>(type, "Metric", source, true);
                button.Text = GetValue<string>(type, "Text", source, true);
                button.Clicked += GetValue<Action<object, object, int>>(type, "Clicked", source, true);
                return button;
            }
            else if(controlName == "ToggleButton")
            {
                ToggleButton button = new ToggleButton();
                button.Metric = GetValue<int>(type, "Metric", source, true);
                button.TextOn = GetValue<string>(type, "TextOn", source, true);
                button.TextOff = GetValue<string>(type, "TextOff", source, true);
                button.Getter = GetValue<Func<object, bool>>(type, "Getter", source, false);
                button.Setter = GetValue<Action<object, bool>>(type, "Setter", source, false);
                return button;
            }
            else if(controlName == "VRMenu")
            {
                VRMenu menu = parent.AddComponent<VRMenu>();
                return initVRMenu(menu, parent, source);
            }
            else if(controlName == "RepeatButtonsMenu")
            {
                RepeatButtonsMenu menu = parent.AddComponent<RepeatButtonsMenu>();
                initVRMenu(menu, parent, source);
                string tickMode = GetValue<string>(type, "TickMode", source, true);
                if(tickMode != null)
                {
                    try
                    {
                        menu.TickMode = (RepeatButtonTickMode)Enum.Parse(
                            typeof(RepeatButtonTickMode), tickMode);
                    }
                    catch(Exception e)
                    {
                        throw new Exception("メニューソース " + "TickMode" +
                            "フィールドの値が不正です(" + e.Message + "): " + source.ToString());
                    }
                }
                menu.OnTick += GetValue<Action<object, int, int>>(type, "OnTick", source, true);
                return menu;
            }
            else
            {
                throw new Exception("不明なコントロールです: " + controlName);
            }
        }

        public static object InstantiateMenu(GameObject parent, object source)
        {
            return CreateMenuInternal(parent, source);
        }
    }
}
