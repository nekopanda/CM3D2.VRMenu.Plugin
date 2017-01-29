using PluginExt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityInjector.Attributes;

namespace CM3D2.VRMenu.Plugin
{
    public class SpawnItemUI : MonoBehaviour
    {
        private readonly static int WINDOW_ID = 241;

        private bool isVR;

        private bool isPhotoMode;

        private MyStyles styles;
        private MyWindow mainWindow;
        private DropdownMenu dropdownMenu;

        private bool handleVisibleByKey_;
        private bool HandleVisibleByKey {
            get { return handleVisibleByKey_; }
            set {
                if(handleVisibleByKey_ != value)
                {
                    handleVisibleByKey_ = value;
                    ItemManager.ShowHandle(value);
                }
            }
        }

        private bool handleVisibleByOther_;
        private bool HandleVisibleByOther {
            get { return handleVisibleByOther_; }
            set {
                if (handleVisibleByOther_ != value)
                {
                    handleVisibleByOther_ = value;
                    ItemManager.ShowHandle(value);
                }
            }
        }

        public bool IsGUIVisible { get; private set; }

        public void ToggleGUI()
        {
            IsGUIVisible = !IsGUIVisible;
        }

        public void ShowGUI(bool show)
        {
            IsGUIVisible = show;
        }

        public void ToggleShowHandle()
        {
            HandleVisibleByOther = !HandleVisibleByOther;
        }

        public void ShowHandle(bool show)
        {
            HandleVisibleByOther = show;
        }

        public bool IsEnabledScene {
            get {
                //return (GameMain.Instance.GetNowSceneName() == "ScenePhotoMode");
                return true;
            }
        }

        public ItemManager ItemManager { get; private set; }

        public void Initialize()
        {
            isVR = (Application.dataPath.Contains("CM3D2OHVR") || Application.dataPath.Contains("CM3D2VR"));

            styles = new MyStyles();
            mainWindow = new MyWindow(WINDOW_ID, doMyWindow, styles);
            dropdownMenu = new DropdownMenu(WINDOW_ID + 1, styles);

            mainWindow.Width = 100;
            mainWindow.Title = "アイテム呼び出し";

            ItemManager = new ItemManager(this);
        }

        private void OnLevelWasLoaded()
        {
            isPhotoMode = IsEnabledScene;

            if (isPhotoMode)
            {
                ItemManager.Init();
            }
        }

        private bool showSidePanel;
        private int currentSelected = -1;
        private Vector2 currentItemViewVec;
        private int currentSlotPage;
        private ItemManager.SlotInfo currentSlot;
        private ItemManager.ItemData[] currentSlotItems;

        private void doMyWindow(MyWindow window)
        {
            int mainWidth = 160;
            int sideWidth = 365;
            int height = 575;

            window.Width = showSidePanel ? styles.GetPix(mainWidth + sideWidth) : styles.GetPix(mainWidth);
            window.Height = styles.GetPix(height);

            float fs = styles.gsWin.fontSize;


            { // 表示中のアイテムリスト
                Rect currentItemRect = new Rect(
                    styles.GetPix(5),
                    fs * 3,
                    styles.GetPix(mainWidth - 10),
                    styles.GetPix(height) - fs * 5);

                Rect currentItemView = new Rect(0, 0,
                    styles.GetPix(mainWidth - 20),
                    ItemManager.CurrentItems.Count * styles.GetPix(45));

                currentItemViewVec = GUI.BeginScrollView(currentItemRect, currentItemViewVec, currentItemView);
                int y = 0;
                int index = 0;
                foreach(var item in ItemManager.CurrentItems)
                {
                    try
                    {
                        GUI.DrawTexture(new Rect(
                            styles.GetPix(1), styles.GetPix(y + 1), styles.GetPix(44), styles.GetPix(44)),
                            item.itemData.tex);
                    }
                    catch (Exception) { }

                    if (GUI.Button(new Rect(
                        styles.GetPix(50), styles.GetPix(y + 1), styles.GetPix(40), styles.GetPix(20)),
                        "削除", styles.gsButton))
                    {
                        currentSelected = index;
                    }
                    if (GUI.Button(new Rect(
                        styles.GetPix(95), styles.GetPix(y + 1), styles.GetPix(40), styles.GetPix(20)),
                        "回転R", styles.gsButton))
                    {
                        currentSelected = -1;
                        item.holder.transform.localRotation = Quaternion.identity;
                    }

                    GUI.Label(new Rect(
                        styles.GetPix(50), styles.GetPix(y + 25), styles.GetPix(85), styles.GetPix(20)),
                        item.holder.transform.position.ToString(), styles.gsLabel);

                    y += 45;
                    index++;
                }
                GUI.EndScrollView();
            }

            // 削除選択
            if(currentSelected >= 0)
            {
                float y = styles.GetPix(height) - fs * 1.8f;
                float x = styles.GetPix(5);
                float labelWidth = fs * 3;
                float btnWidth = fs * 4;

                GUI.Label(new Rect(x, y, labelWidth, fs * 1.5f), "削除？", styles.gsLabel);
                x += labelWidth + styles.GetPix(5);
                if(GUI.Button(new Rect(x, y, btnWidth, fs * 1.5f), "はい", styles.gsButton))
                {
                    ItemManager.RemoveItem(currentSelected);
                    currentSelected = -1;
                }
                x += btnWidth + styles.GetPix(5);
                if (GUI.Button(new Rect(x, y, btnWidth, fs * 1.5f), "いいえ", styles.gsButton))
                {
                    currentSelected = -1;
                }
            }

            if(showSidePanel)
            {
                // サイドパネル上部
                Rect itemRect = new Rect(styles.GetPix(mainWidth), fs * 0.3f, styles.GetPix(110), fs * 1.8f);
                if (GUI.Button(itemRect, currentSlot.displayName, styles.gsButton))
                {
                    List<string> slots = new List<string>();
                    slots.Add(""); // 空アイテム
                    slots.AddRange(ItemManager.SlotArray.Select(s => s.displayName));
                    dropdownMenu.Show(
                        new Vector2(window.rectWin.x + itemRect.x, window.rectWin.y + itemRect.y + itemRect.height),
                        styles.GetPix(100), slots.ToArray(), slotItemSelected);
                }

                if (currentSlotItems != null)
                {
                    int numX = 8;
                    int numY = 12;
                    int itemsPerPage = numX * numY;
                    int numPages = (currentSlotItems.Length + itemsPerPage - 1) / itemsPerPage;

                    itemRect.x += itemRect.width + styles.GetPix(55);
                    GUI.Label(itemRect, "ページ: " + (currentSlotPage + 1) + "/" + numPages, styles.gsLabel);

                    itemRect.x = styles.GetPix(mainWidth + sideWidth - 60);
                    itemRect.width = styles.GetPix(25);
                    if (GUI.Button(itemRect, "<", styles.gsButton))
                    {
                        if(currentSlotPage > 0)
                        {
                            --currentSlotPage;
                        }
                    }

                    itemRect.x += itemRect.width + styles.GetPix(5);
                    if (GUI.Button(itemRect, ">", styles.gsButton))
                    {
                        if (currentSlotPage + 1 < numPages)
                        {
                            ++currentSlotPage;
                        }
                    }

                    // サイドパネルコンテンツ
                    GUI.BeginGroup(new Rect(
                        styles.GetPix(mainWidth),
                        fs * 2.5f,
                        styles.GetPix(sideWidth - 5),
                        styles.GetPix(height) - fs * 2.5f));

                    var tmpgs = new GUIStyle();

                    for (int y = 0; y < numY; ++y)
                    {
                        for (int x = 0; x < numX; ++x)
                        {
                            int index = itemsPerPage * currentSlotPage + x + y * numX;
                            if (index >= currentSlotItems.Length)
                            {
                                break;
                            }
                            var item = currentSlotItems[index];
                            Rect imageRect = new Rect(
                                styles.GetPix(x * 45),
                                styles.GetPix(y * 45),
                                styles.GetPix(44),
                                styles.GetPix(44));
                            try
                            {
                                GUI.DrawTexture(imageRect, item.tex);
                            }
                            catch (Exception) { }
                            if(GUI.Button(imageRect, "", tmpgs))
                            {
                                Log.Debug("クリックされた: " + item.menu);
                                ItemManager.SpawnItem(item);
                                currentSelected = -1;
                            }
                        }
                    }

                    GUI.EndGroup();
                }
            }

            // サイドパネルの表示ON/OFF
            Rect sideButtonRect = new Rect(styles.GetPix(mainWidth - 30), fs * 0.3f, styles.GetPix(25), fs * 1.5f);
            if(GUI.Button(sideButtonRect, showSidePanel ? "<" : ">", styles.gsButton))
            {
                showSidePanel = !showSidePanel;
            }

            GUI.DragWindow();
        }

        private void slotItemSelected(int selectedIndex)
        {
            if(selectedIndex >= 1)
            {
                currentSlot = ItemManager.SlotArray[selectedIndex - 1];
                currentSlotItems = ItemManager.GetSlotItems(currentSlot).ToArray();
                currentSlotPage = 0;
            }
            else
            {
                currentSlot = new ItemManager.SlotInfo();
                currentSlotItems = null;
            }
        }


        private void Update()
        {
            if(isPhotoMode)
            {
                if(isVR == false)
                {
                    if(Input.GetKeyUp(KeyCode.F8))
                    {
                        ToggleGUI();
                    }
                }
                if(Input.GetKey(KeyCode.Z))
                {
                    HandleVisibleByKey = true;
                }
                else
                {
                    HandleVisibleByKey = false;
                }
            }
        }

        private void OnGUI()
        {
            if(isPhotoMode && IsGUIVisible )
            {
                styles.Update();
                mainWindow.Update();
                dropdownMenu.Update();
                ChkMouseClick();
            }
        }

        private bool IsMouseOnGUI()
        {
            Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (mainWindow.Contains(mousePos))
                return true;
            if (dropdownMenu.Contains(mousePos))
                return true;

            return false;
        }

        private void ChkMouseClick()
        {
            if ((Input.GetMouseButtonUp(0) || Input.GetMouseButtonDown(0)) && IsMouseOnGUI())
            {
                Input.ResetInputAxes();
            }
        }
    }

    public class MyStyles
    {
        public GUIStyle gsWin;
        public GUIStyle gsLabel;
        public GUIStyle gsButton;
        public GUIStyle gsSelectionGrid;

        private float fontSize_;
        public float FontSize {
            get { return fontSize_; }
            set {
                if(fontSize_ != value)
                {
                    fontSize_ = value;
                    UpdateFontSize();
                }
            }
        }

        private float scaleFactor_;
        public float ScaleFactor {
            get { return scaleFactor_; }
            set {
                if (scaleFactor_ != value)
                {
                    scaleFactor_ = value;
                    UpdateFontSize();
                }
            }
        }

        public MyStyles()
        {
            gsWin = new GUIStyle("box");
            gsWin.alignment = TextAnchor.UpperLeft;

            gsLabel = new GUIStyle("label");
            gsLabel.alignment = TextAnchor.MiddleCenter;

            gsButton = new GUIStyle("button");
            gsButton.alignment = TextAnchor.MiddleCenter;

            gsSelectionGrid = new GUIStyle();

            GUIStyleState gssNormal = new GUIStyleState();
            gssNormal.textColor = Color.white;
            gssNormal.background = Texture2D.blackTexture;

            GUIStyleState gssHover = new GUIStyleState();
            gssHover.textColor = Color.black;
            gssHover.background = Texture2D.whiteTexture;

            gsSelectionGrid.normal = gssNormal;
            gsSelectionGrid.hover = gssHover;

            fontSize_ = 12;

            Update();
        }

        public void Update()
        {
            ScaleFactor = 1f + (Screen.width / 1280f - 1f) * 0.6f;
        }

        public int GetPix(int i)
        {
            return (int)(scaleFactor_ * i);
        }

        private void UpdateFontSize()
        {
            gsWin.fontSize = (int)(scaleFactor_ * fontSize_);
            gsLabel.fontSize = (int)(scaleFactor_ * fontSize_);
            gsButton.fontSize = (int)(scaleFactor_ * fontSize_);
            gsSelectionGrid.fontSize = (int)(scaleFactor_ * fontSize_);
        }
    }

    public class MyWindow
    {
        public int WINDOW_ID;
        public Action<MyWindow> guiFunc;
        public MyStyles styles;

        public float Height;
        public float Width;
        public string Title;

        public Rect rectWin;
        
        public Rect InnerRect {
            get {
                return new Rect(
                    styles.gsWin.fontSize / 2,
                    styles.gsWin.fontSize * 1.5f,
                    rectWin.width - styles.gsWin.fontSize,
                    rectWin.height - styles.gsWin.fontSize * 2);
            }
        }

        public MyWindow(int window_id, Action<MyWindow> guiFunc, MyStyles styles)
        {
            WINDOW_ID = window_id;
            this.guiFunc = guiFunc;
            this.styles = styles;
        }

        public bool Contains(Vector2 pos)
        {
            return rectWin.Contains(pos);
        }

        public void Update()
        {
            if (rectWin.width < 1)
            {
                rectWin.Set(Screen.width - Width - 100, 100, Width, Height);
            }

            rectWin.width = Width;
            rectWin.height = Height;

            if (rectWin.x < 0 - rectWin.width * 0.9f)
            {
                rectWin.x = 0;
            }
            else if (rectWin.x > Screen.width - rectWin.width * 0.1f)
            {
                rectWin.x = Screen.width - rectWin.width;
            }
            else if (rectWin.y < 0 - rectWin.height * 0.9f)
            {
                rectWin.y = 0;
            }
            else if (rectWin.y > Screen.height - rectWin.height * 0.1f)
            {
                rectWin.y = Screen.height - rectWin.height;
            }

            rectWin = GUI.Window(WINDOW_ID, rectWin, invokeGuiFunc, Title, styles.gsWin);
        }

        private void invokeGuiFunc(int id)
        {
            guiFunc(this);
        }
    }

    public class DropdownMenu
    {
        public int WINDOW_ID;
        public MyStyles styles;

        public Action<int> itemSelected;

        private Rect rectWin;
        private bool visilbe;
        private string[] items;

        public DropdownMenu(int window_id, MyStyles styles)
        {
            WINDOW_ID = window_id;
            this.styles = styles;
        }

        public bool Contains(Vector2 pos)
        {
            if (visilbe == false) return false;
            return rectWin.Contains(pos);
        }

        public void Show(Vector2 pos, int width, string[] items, Action<int> itemSelected)
        {
            this.rectWin = new Rect(pos, new Vector2(width, 0));
            this.items = items;
            this.itemSelected = itemSelected;
            visilbe = true;
        }

        public void Update()
        {
            if(visilbe)
            {
                rectWin.height = items.Length * styles.gsSelectionGrid.fontSize;
                rectWin = GUI.Window(WINDOW_ID, rectWin, invokeGuiFunc, "", styles.gsWin);

                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                {
                    Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                    if (!rectWin.Contains(mousePos))
                    {
                        visilbe = false;
                    }
                }
            }
        }

        private void invokeGuiFunc(int id)
        {
            int selectedindex = GUI.SelectionGrid(
                new Rect(0f, 0f, rectWin.width, rectWin.height), -1, items, 1, styles.gsSelectionGrid);
            if (selectedindex >= 0)
            {
                itemSelected(selectedindex);
                visilbe = false;
            }
        }
    }
}
