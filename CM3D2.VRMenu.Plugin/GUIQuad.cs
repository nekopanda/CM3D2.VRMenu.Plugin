using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CM3D2.VRMenu.Plugin
{
    public class GUIQuad : MonoBehaviour
    {
        class GUIHook : MonoBehaviour
        {
            public int Depth;
            public Action OnGUICalled;

            private void OnGUI()
            {
                GUI.depth = Depth;
                OnGUICalled();
            }
        }

        private GameObject ovr_screen;

        private RenderTexture rtIMGUI;
        private RenderTexture rtOVRUI;
        private Material uiMaterial;

        private int screenWidth;
        private int screenHeight;

        private bool visible_;
        public bool Visible {
            get {
                return visible_;
            }
            set {
                if(visible_ != value)
                {
                    visible_ = value;
                    switchVisiblity(visible_);
                }
            }
        }

        private static GUIQuad instance_;
        public static GUIQuad Instance {
            get {
                return instance_;
            }
        }

        public static GUIQuad Create()
        {
            if (instance_ == null)
            {
                Log.Debug("GUIQuad Create !!!");
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "VRMenu GUIQuad";
                go.GetComponent<MeshCollider>().enabled = false;
                BoxCollider collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(1, 1, 0.07f);
                // raycast使わないけど一応・・・
                go.layer = LayerMask.NameToLayer("Ignore Raycast");
                instance_ = go.AddComponent<GUIQuad>();
            }
            return instance_;
        }

        private void Start()
        {
            Log.Debug("GUIQuad Start ...");
            screenWidth = Screen.width;
            screenHeight = Screen.height;

            UpdateGuiQuadScale();

            Shader shader = Asset.Instance.UIBlendShader;
            uiMaterial = new Material(shader);
            gameObject.GetComponent<MeshRenderer>().material = uiMaterial;
            updateScreenSize();

            // GUIフック
            GUIHook beforeHook = gameObject.AddComponent<GUIHook>();
            beforeHook.Depth = 2000;
            beforeHook.OnGUICalled = OnBeforeGUI;
            GUIHook afterHook = gameObject.AddComponent<GUIHook>();
            afterHook.Depth = -2000;
            afterHook.OnGUICalled = OnAfterGUI;
        }

        public void UpdateGuiQuadScale()
        {
            float quadSize = VRMenuPlugin.Instance.Config.GUISize;
            float y = quadSize;
            float x = quadSize * ((float)1280 / 720);
            gameObject.transform.localScale = new Vector3(x, y, 1);
        }

        #region IMGUIをrtIMGUIにレンダリング

        private RenderTexture prevRenderTexture;
        private void OnBeforeGUI()
        {
            if (Event.current.type == EventType.Repaint)
            {
                prevRenderTexture = RenderTexture.active;
                RenderTexture.active = rtIMGUI;
                GL.Clear(true, true, Color.clear);
            }
        }

        private void OnAfterGUI()
        {
            if (Event.current.type == EventType.Repaint)
            {
                // 戻す
                RenderTexture.active = prevRenderTexture;
            }
        }
        
        #endregion

        private void Update()
        {
            if (uiMaterial == null)
            {
                // シェーダがないと何も出来ない
                Log.Debug("No shader ...");
                return;
            }
            if (rtOVRUI == null)
            {
                if(ovr_screen == null)
                {
                    //ovr_screen = GameObject.Find("ovr_screen");
                    ovr_screen = VRMenuPlugin.Instance.OVRScreen;
                    Log.Debug("ovr_screen found !!!");
                }
                if(ovr_screen != null)
                {
                    rtOVRUI = ovr_screen.GetComponentInChildren<MeshRenderer>().material.mainTexture as RenderTexture;
                    uiMaterial.SetTexture("_SecondTex", rtOVRUI);
                    
                    switchVisiblity(visible_);
                }
            }
            if(rtOVRUI != null)
            {
                if (screenWidth != Screen.width || screenHeight != Screen.height)
                {
                    screenWidth = Screen.width;
                    screenHeight = Screen.height;
                    updateScreenSize();
                }

                if (rtIMGUI.IsCreated() == false)
                {
                    rtIMGUI.Create();
                }

                if(gameObject.activeSelf == ovr_screen.activeSelf)
                {
                    // 整合性を取る
                    ovr_screen.SetActive(!gameObject.activeSelf);
                }
            }
        }

        private void updateScreenSize()
        {
            if(rtIMGUI != null)
            {
                rtIMGUI.Release();
            }
            rtIMGUI = new RenderTexture(screenWidth, screenHeight, 16, RenderTextureFormat.ARGB32);
            uiMaterial.SetTexture("_MainTex", rtIMGUI);

            var virtualRect = IMGUIVirtualScreenRect;

            uiMaterial.SetTextureOffset("_MainTex", new Vector2(virtualRect.x / screenWidth, virtualRect.y / screenHeight));
            uiMaterial.SetTextureScale("_MainTex", new Vector2(virtualRect.z / screenWidth, virtualRect.w / screenHeight));
        }

        public void MoveCursorTo(int x, int y)
        {
            if(WinAPI.WindowHandle == IntPtr.Zero)
            {
                return;
            }

            // UI上では移動して実際には移動しないのは整合性が取れないのでコメントアウト
            //if(x >= 0 && x < screenWidth && y >= 0 && y < screenHeight)
            //{
                WinAPI.POINT screenPos = new WinAPI.POINT();
                if (GameMain.Instance.CMSystem.FullScreen)
                {
                    WinAPI.RECT rect;
                    WinAPI.GetWindowRect(WinAPI.WindowHandle, out rect);
                    float w = rect.Right - rect.Left;
                    float h = rect.Bottom - rect.Top;
                    screenPos.X = (int)(x * (w / screenWidth));
                    screenPos.Y = (int)(h - y * (h / screenHeight));
                }
                else
                {
                    screenPos.X = x;
                    screenPos.Y = screenHeight - y;
                }
                WinAPI.ClientToScreen(WinAPI.WindowHandle, ref screenPos);
                WinAPI.SetCursorPos(screenPos.X, screenPos.Y);
            //}
        }

        // IMGUIの仮想的な画面サイズ
        // Quadのサイズは16:9固定なので、実際にはこの画面サイズより小さいので注意
        // (左上x,左上y,幅,高さ)
        public Vector4 IMGUIVirtualScreenRect {
            get {
                float aspect = (float)screenWidth / screenHeight;
                float quadAspect = (1280.0f / 720.0f);

                float x, y, w, h;
                if (aspect < quadAspect)
                {
                    // 画面が縦長
                    h = screenHeight;
                    w = quadAspect * h;
                    x = -(w - screenWidth) / 2;
                    y = 0;
                }
                else
                {
                    // 画面が横長
                    w = screenWidth;
                    h = w / quadAspect;
                    x = 0;
                    y = -(h - screenHeight) / 2;
                }

                return new Vector4(x, y, w, h);
            }
        }

        private void switchVisiblity(bool visilbe)
        {
            if (ovr_screen != null)
            {
                gameObject.SetActive(visilbe);
                ovr_screen.SetActive(!visilbe);
            }
        }
    }
}
