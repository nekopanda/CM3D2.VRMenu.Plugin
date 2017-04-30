using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CM3D2.VRMenu.Plugin
{
    // 掴むロジック追加用インターフェース
    public interface IGripTriggerHandler
    {
        void OnTriggerEnter(Collider other);
        void OnTriggerStay(Collider other);
        void OnTriggerExit(Collider other);
        bool OnGripTest();
        void OnGripStart();
        void OnGripReleased();
    }

    public enum GripTarget
    {
        None,
        GUI,
        Maid,
        Object,
        Handler,
        World,
        SpawnItem
    }

    // 各コントローラに最初にアタッチされるコンポーネント
    // コントローラの調整やメニューの表示などの実際の作業のトップコンポーネント
    public abstract class VRMenuController : MonoBehaviour
    {
        private class PointerTriggerDelegator : MonoBehaviour
        {
            public VRMenuController Menuctrl;

            private void OnTriggerEnter(Collider other)
            {
                Menuctrl.OnTriggerEnterPointer(other);
            }

            private void OnTriggerStay(Collider other)
            {
                Menuctrl.OnTriggerStayPointer(other);
            }

            private void OnTriggerExit(Collider other)
            {
                Menuctrl.OnTriggerExitPointer(other);
            }
        }

        public Controller ControllerId;

        public bool isEnabled_ = true;
        public bool IsEnabled {
            get {
                return isEnabled_;
            }
            set {
                if(isEnabled_ != value)
                {
                    pointerObj.SetActive(value);
                    guiQuad.Visible = value;
                    isEnabled_ = value;
                }
            }
        }

        private ControllerMode mode_;
        public ControllerMode Mode {
            get {
                if (mode_ == null)
                {
                    mode_ = new ControllerMode(this);
                }
                return mode_;
            }
        }

        private ControllerDevice device_;
        public ControllerDevice Device
        {
            get
            {
                if (device_ == null)
                {
                    device_ = CreateControllerDevice();
                }
                return device_;
            }
        }

        private ControllerMenu menu_;
        public ControllerMenu Menu
        {
            get
            {
                if (menu_ == null)
                {
                    menu_ = CreateControllerMenu();
                }
                return menu_;
            }
        }

        private ContollerHacker hacker_;
        public ContollerHacker Hacker
        {
            get
            {
                if (hacker_ == null)
                {
                    hacker_ = new ContollerHacker(gameObject);
                }
                return hacker_;
            }
        }

        private VRMenuPlugin.ControllerConfig controllerConfig_;
        public VRMenuPlugin.ControllerConfig ControllerConfig {
            get {
                if(controllerConfig_ == null)
                {
                    controllerConfig_ = ((ControllerId == Controller.Left)
                        ? VRMenuPlugin.Instance.ConfigLeft
                        : VRMenuPlugin.Instance.ConfigRight);
                }
                return controllerConfig_;
            }
        }

        private ControllerModel model_;
        public ControllerModel Model {
            get {
                if(model_ == null)
                {
                    model_ = new ControllerModel(this);
                }
                return model_;
            }
        }

        private GUIQuad guiQuad;

        private GameObject pointerObj;
        private MeshRenderer pointerRenderer;

        private Color PointerColor
        {
            get
            {
                if (pointerRenderer != null)
                {
                    return pointerRenderer.sharedMaterial.color;
                }
                return Color.white;
            }
            set
            {
                if (pointerRenderer != null)
                {
                    pointerRenderer.sharedMaterial.color = value;
                }
            }
        }

        private bool PointerEnabled {
            get {
                if (pointerObj == null)
                {
                    return false;
                }
                return pointerObj.activeSelf;
            }
            set {
                if (pointerObj == null)
                {
                    return;
                }
                pointerObj.SetActive(value);
            }
        }

        public GameObject Pointer {
            get {
                return pointerObj;
            }
        }

        private int supressCount;
        public void SuppressPointer(bool suppress)
        {
            // 表示させたくない人が複数いるのでカウントしておく
            if(suppress)
            {
                //Log.Out("Supress count incremented");
                if(supressCount++ == 0)
                {
                    //Log.Out("Pointer DISBLED");
                    PointerEnabled = false;
                }
            }
            else
            {
                //Log.Out("Supress count decremented");
                if (--supressCount == 0)
                {
                    //Log.Out("Pointer ENABLED");
                    PointerEnabled = true;
                }
            }
        }

        // trueにするとシステムメニュー表示中以外は常にクリックする
        public bool AlwaysClickableOnGUI = false;

        private enum TouchPadState
        {
            Free, SystemMenu, GUI
        }
        private TouchPadState touchPadState = TouchPadState.Free;

        private static readonly Color[] GripTargetColorMap = new Color[] {
            new Color(0,0,1,0.2f), // None: blue
            new Color(1,1,1,0.3f), // GUI: white
            new Color(1,1,0,0.2f), // Maid: yellow
            new Color(0,1,0,0.2f), // Object: green
            new Color(1,1,0,0.5f), // Handler: yellow
            new Color(0,0,1,0.2f), // World: blue
            new Color(1,1,0,0.5f)  // SpawnItem: green
        };
        private static readonly float NormalPointerAlpha = 0.2f;
        private static readonly float TransparentPointerAlpha = 0.0f;

        private GripTarget nextGripTarget;
        private object nextGripObject;

        private GripTarget currentGripTarget;
        private object currentGripObject;
        private bool isLockAxis;

        // 登録されてるIGripTriggerHandler
        private List<IGripTriggerHandler> gripHandlers = new List<IGripTriggerHandler>();

        // 今、全コントローラで掴んでいるオブジェクトのリスト
        // 重複させたくない場合に使用する
        private static List<Transform> grippingList = new List<Transform>();

        // カーソルがウィンドウ上にあるか
        //（更新されるのはisTouchingGUIの時だけでisTouchingGUIがfalseのときは常にfalseになる）
        private bool isCursorInWindow_;
        private bool isTouchingGUI;

        // GUIタッチステートまとめ
        // isTouchingGUI: ポインタがGUI範囲内にあるか？
        // pressedWithTouchingGUI: ドラッグ中なのでGUIモード継続中
        // touchingOffGUICo != null: ポインタはGUIから離れたがまだGUIモード中
        // 
        // ポインタの色は isTouchingGUI

        // GUIモードか？実際には色々な状態があるので、GUIモードとはどういう状態なのか分からない
        public bool IsGUIMode {
            get {
                return IsRawGUIMode && !Menu.SystemMenuActive;
            }
        }

        private bool ShouldMoveMouse {
            get {
                // GUIにタッチしていなくてもドラッグ中は移動を継続する
                return isTouchingGUI || pressedInGUIMode;
            }
        }

        private bool IsRawGUIMode {
            get {
                // ポインタ移動中に加えて、離れて間もない場合は、準GUIモード
                return isTouchingGUI || pressedInGUIMode || touchingOffGUICo != null;
            }
        }

        // トラックパッドクリックを画面クリックにすべき状態か？
        public bool IsClickEnabledState {
            get {
                // システムメニューが表示されていないことが前提、その上で準GUIモードまたは常にクリックなら
                return !Menu.SystemMenuActive && (IsRawGUIMode || AlwaysClickableOnGUI);
            }
        }

        private ReferenceCountSet<Transform> touchingObjects =
            new ReferenceCountSet<Transform>();

        private bool isGrippingGUI { get { return currentGripTarget == GripTarget.GUI; } }
        private bool isGrippingWorld { get { return currentGripTarget == GripTarget.World; } }

        // 掴んで移動させるときに使うダミーオブジェクト
        private GameObject markerObj;
        private GameObject handleObj;

        //
        protected abstract ControllerDevice CreateControllerDevice();
        protected abstract ControllerMenu CreateControllerMenu();

        private void Start()
        {
            try
            {
                // GUIQuadを表示（左右のコントローラでココは2回呼ばれるがGUIQuadは1枚しか作らないことに注意）
                if (GUIQuad.Instance == null)
                {
                    Log.Debug("Call GUIQuad.Create");
                    guiQuad = GUIQuad.Create();
                    guiQuad.gameObject.transform.parent = VRMenuPlugin.Instance.PlayRoom.transform;

                    // 30cm前に表示
                    var head = VRMenuPlugin.Instance.Head;
                    guiQuad.transform.position = head.TransformPoint(new Vector3(0, 0, 0.3f));
                    guiQuad.transform.rotation = Quaternion.LookRotation(head.forward);

                    //printCollisionMatrix();
                }
                else
                {
                    guiQuad = GUIQuad.Instance;
                }

                // コントローラにポインタを追加
                CreatePointer();

                // マーカーはプレイルームに対して固定する
                markerObj = new GameObject("VRMenu Marker " + ControllerId.ToString());
                markerObj.transform.parent = VRMenuPlugin.Instance.PlayRoom.transform.parent;

                handleObj = new GameObject("VRMenu Handle " + ControllerId.ToString());
                handleObj.transform.parent = gameObject.transform;

                // 関連システム初期化
                ModeImpl.InitBuiltinMode(Mode, gameObject);
                Menu.SetSystemMenu(Mode.SystemMenuTop);

                // コントローラ初期状態を設定
                Hacker.ChangeMode(ContollerHacker.Mode.None);
                Mode.SwitchToDefaultMode();

                UICamera.InputEnable = true;
            }
            catch(Exception e)
            {
                Log.Debug(e);
            }
        }

        private void printCollisionMatrix()
        {
            for(int i = 0; i < 32; ++i)
            {
                for(int j = 0; j < 32; ++j)
                {
                    bool ignore = Physics.GetIgnoreLayerCollision(i, j);
                    if(ignore)
                    {
                        Log.Debug("IGNORE " + i + " -> " + j);
                    }
                }
            }
        }

        private void CreatePointer()
        {
            if (pointerObj == null)
            {
                pointerObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pointerObj.name = "VRMenu Pointer";
                pointerObj.transform.parent = gameObject.transform;
                pointerObj.transform.localRotation = Quaternion.identity;
                UpdatePointerSize();
                UpdatePointerDistance();

                // Kinematic Rigidbody Trigger はすべてのコライダーとの接触でTriggerイベントが発生する
                //（Layer Collision Matrix で接触判定が無効化されている場合を除く）
                pointerObj.GetComponent<SphereCollider>().isTrigger = true;
                pointerObj.AddComponent<Rigidbody>().isKinematic = true;

                pointerObj.AddComponent<PointerTriggerDelegator>().Menuctrl = this;

                pointerRenderer = pointerObj.GetComponent<MeshRenderer>();
                pointerRenderer.sharedMaterial = new Material(Asset.Instance.PointerShader);
                UpdatePointerAlwaysVisible();

                // デフォルトの0だとメイドに当たらない
                pointerObj.layer = 11;
            }
        }

        public void UpdatePointerSize()
        {
            if (pointerObj != null)
            {
                pointerObj.transform.localScale = Vector3.one * VRMenuPlugin.Instance.Config.PointerSize;
            }
        }

        public void UpdatePointerDistance()
        {
            if (pointerObj != null)
            {
                pointerObj.transform.localPosition = new Vector3(
                    0f, 0, VRMenuPlugin.Instance.Config.PointerDistance);
            }
        }

        public void UpdatePointerAlwaysVisible()
        {
            if (pointerRenderer != null)
            {
                Shader shader = VRMenuPlugin.Instance.Config.PointerAlwaysVisible
                    ? Asset.Instance.PointerShader
                    : Asset.Instance.ColorShader;
                pointerRenderer.sharedMaterial.shader = shader;
            }
        }
        /*
        public void ResetWorldXZ()
        {
            var trOffset = VRMenuPlugin.Instance.PlayRoomHandle.transform;
            var angles = trOffset.rotation.eulerAngles;
            trOffset.rotation = Quaternion.Euler(0, angles.y, 0);
        }
        */
        public void ResetCamOffset()
        {
            releaseGrip();

            // XZ回転しているときはそれだけ戻す
            var trOffset = VRMenuPlugin.Instance.PlayRoomHandle.transform;
            var angles = trOffset.rotation.eulerAngles;
            if(Math.Abs(angles.x) > 0.3 || Math.Abs(angles.z) > 0.3)
            {
                trOffset.rotation = Quaternion.Euler(0, angles.y, 0);
            }
            else
            {
                GameMain.Instance.OvrMgr.OvrCamera.ReCallcOffset();

                var headOffset = VRMenuPlugin.Instance.Config.HeadOffset;
                var pos = trOffset.position;
                trOffset.position = new Vector3(pos.x, pos.y + headOffset, pos.z);
            }
        }

        private void OnLevelWasLoaded(int level)
        {
            Mode.UpdateForNewScene();

            if (pointerObj != null)
            {
                // UIにタッチしたままだとEnterが再度走らなくてタッチ状態が認識されないので
                // コライダーの有効/無効を1回トグルさせる
                Collider collider = pointerObj.GetComponent<Collider>();
                collider.enabled = false;
                collider.enabled = true;
            }

            // 状態を初期化
            grippingList.Clear();
            releaseGrip();
            nextGripTarget = GripTarget.None;
            nextGripObject = null;
            isTouchingGUI = false;
            touchingObjects.Clear();
        }

        private void OnTriggerEnterPointer(Collider other)
        {
            GUIQuad guiQuad = other.gameObject.GetComponent<GUIQuad>();
            if(guiQuad != null)
            {
                isTouchingGUI = true;
            }
            else
            {
                GripTarget targetType;
                Transform target = Util.GetGripTarget(other.gameObject, out targetType);
                if(target != null)
                {
                    // １つのtargetに対して複数の子GameObjectが接触することがある
                    // いま接触してるtargetのリストを保持したいので、
                    // いくつの子GameObjectと接触してるかをカウントする

                    touchingObjects.Add(target);
                    Log.Debug("TriggerEnter " + target.name);
                }

                foreach (var handler in gripHandlers)
                {
                    handler.OnTriggerEnter(other);
                }
            }

        }

        private void OnTriggerStayPointer(Collider other)
        {
            GUIQuad guiQuad = other.gameObject.GetComponent<GUIQuad>();
            if (guiQuad != null && pointerObj != null)
            {
                //
            }
            else
            {
                foreach (var handler in gripHandlers)
                {
                    handler.OnTriggerStay(other);
                }
            }
        }

        private Coroutine touchingOffGUICo;
        private void OnTriggerExitPointer(Collider other)
        {
            GUIQuad guiQuad = other.gameObject.GetComponent<GUIQuad>();
            if (guiQuad != null)
            {
                if (touchingOffGUICo != null)
                {
                    StopCoroutine(touchingOffGUICo);
                }
                touchingOffGUICo = StartCoroutine(touchingOffGUI());
                isTouchingGUI = false;
            }
            else
            {
                GripTarget targetType;
                Transform target = Util.GetGripTarget(other.gameObject, out targetType);
                if (target != null)
                {
                    touchingObjects.Remove(target);
                    Log.Debug("TriggerExit " + target.name);
                }

                foreach (var handler in gripHandlers)
                {
                    handler.OnTriggerExit(other);
                }
            }
        }

        private IEnumerator touchingOffGUI()
        {
            yield return new WaitForSeconds(0.5f);
            touchingOffGUICo = null;
        }

        private void Update()
        {
            try
            {
                Hacker.Update();

                //beam();

                // GUI操作中はユーザメニューを非表示にする
                Menu.UserMenuVisible = !IsRawGUIMode;
                Menu.updateMenu();

                if(IsEnabled)
                {
                    updatePointerPosition();

                    updateGripTarget();

                    updateGrip();

                    updatePointerState();

                    updateTouchPadClick();

                    updateTouchPadState();
                }
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }

        private void updatePointerPosition()
        {
            // GUIにタッチしてなくてもドラック中はGUIモードを維持するのでポインタを移動させる
            if(ShouldMoveMouse)
            {
                // スクリーン平面はguiQuadのローカル座標でz=0なので、ローカル座標に変換すればxyが計算される
                //（しかもスケールも考慮されるのでxyは-0.5～+0.5に正規化された値になる）
                Vector3 pos = guiQuad.transform.InverseTransformPoint(pointerObj.transform.position);

                float x = (pos.x + 0.5f) * 1280;
                float y = (pos.y + 0.5f) * 720;
                var uipos = new Vector3(x, y, 0);

                if (pressBegining)
                {
                    if(Vector3.Distance(uipos, UICamera.OvrVirtualMousePos) < 20)
                    {
                        // クリックし始めは一定距離以上でないと動かさない
                        return;
                    }
                    pressBegining = false;
                }

                UICamera.OvrVirtualMousePos = uipos;

                // UIで実際に移動した位置に更新
                uipos = UICamera.OvrVirtualMousePos;

                Vector4 imguiVirtualRect = guiQuad.IMGUIVirtualScreenRect;
                float ix = (uipos.x / 1280) * imguiVirtualRect.z + imguiVirtualRect.x;
                float iy = (uipos.y / 720) * imguiVirtualRect.w + imguiVirtualRect.y;
                guiQuad.MoveCursorTo((int)ix, (int)iy);

                //Log.Out("Stay: [" + (int)x + "," + (int)y + "]");
            }
        }

        private void updateTouchPadState()
        {
            TouchPadState newState;

            if (IsGUIMode)
            {
                newState = TouchPadState.GUI;
            }
            else if (Menu.SystemMenuActive)
            {
                newState = TouchPadState.SystemMenu;
            }
            else
            {
                newState = TouchPadState.Free;
            }
            if(touchPadState != newState)
            {
                if (newState == TouchPadState.Free)
                {
                    // Free以外からFreeになったので通知
                    Mode.ChangeTouchPadState(true);
                }
                else if(touchPadState == TouchPadState.Free)
                {
                    // FreeからFree以外になったので通知
                    Mode.ChangeTouchPadState(false);
                }

                touchPadState = newState;
            }
        }

        private GripTarget tmp;
        private void updatePointerState()
        {
            Color color = GripTargetColorMap[(int)nextGripTarget];

            if(tmp != nextGripTarget)
            {
                tmp = nextGripTarget;
            }

            if (IsClickEnabledState)
            {
                isCursorInWindow_ = isCursorInWindow();
                if (isCursorInWindow_ == false && isTouchingGUI)
                {
                    color = Color.red;
                }
            }
            else
            {
                isCursorInWindow_ = false;
            }

            PointerColor = color;
        }

        public void ResetGUIPosition()
        {
            if(guiQuad != null)
            {
                Log.Debug("GUI位置をリセット");
                // コントローラの20cm前
                guiQuad.transform.position = transform.TransformPoint(new Vector3(0, 0, 0.2f));
                // 60度上を向かせる
                guiQuad.transform.rotation = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(60,0,0);
                // 非表示になっていたら表示する
                guiQuad.Visible = true;
            }
        }

        private void emitMouseEvent(int flags)
        {
            /*
            switch(flags)
            {
                case WinAPI.MOUSEEVENTF_LEFTDOWN:
                    Log.Out("Mouse Left Down");
                    break;
                case WinAPI.MOUSEEVENTF_LEFTUP:
                    Log.Out("Mouse Left Up");
                    break;
                case WinAPI.MOUSEEVENTF_RIGHTDOWN:
                    Log.Out("Mouse Right Down");
                    break;
                case WinAPI.MOUSEEVENTF_RIGHTUP:
                    Log.Out("Mouse Right Up");
                    break;
            }
             */
            WinAPI.mouse_event(flags, 0, 0, 0, 0);
        }

        private bool leftRightPressed;
        private bool pressedInGUIMode;
        private bool leftPressed;
        private bool pressBegining;
        private Coroutine pressWaitAMomentCo;
        private void updateTouchPadClick()
        {
            bool leftDown = Device.GetPressDown(Button.Left);
            bool rightDown = Device.GetPressDown(Button.Right);
            bool leftPress = Device.GetPress(Button.Left);
            bool rightPress = Device.GetPress(Button.Right);
            bool leftUp = Device.GetPressUp(Button.Left);
            bool rightUp = Device.GetPressUp(Button.Right);

            if (leftDown || rightDown)
            {
                if (leftRightPressed)
                {
                    emurateButtonUp();
                }
                // GUIモードかつマウスが画面内にあるときだけ
                if (isCursorInWindow_)
                {
                    if (leftDown)
                    {
                        emitMouseEvent(WinAPI.MOUSEEVENTF_LEFTDOWN);
                        leftPressed = true;
                    }
                    else
                    {
                        emitMouseEvent(WinAPI.MOUSEEVENTF_RIGHTDOWN);
                        leftPressed = false;
                    }
                    leftRightPressed = true;
                    pressedInGUIMode = (isTouchingGUI && !Menu.SystemMenuActive);
                    pressBegining = true;
                    if (pressWaitAMomentCo != null) {
                        StopCoroutine(pressWaitAMomentCo);
                    }
                    pressWaitAMomentCo = StartCoroutine(pressWaitAMoment());
                }
                else if(IsClickEnabledState)
                {
                    Log.Debug("マウスカーソルがゲームウィンドウ上にないためクリックできません！");
                }
            }
            else if (leftUp || rightUp)
            {
                if(leftRightPressed)
                {
                    emurateButtonUp();
                }
            }
            else if (leftPress == false && rightPress == false)
            {
                // どのボタンも押して無い
                if (leftRightPressed)
                {
                    emurateButtonUp();
                }
            }
        }

        private IEnumerator pressWaitAMoment()
        {
            yield return new WaitForSeconds(0.2f);
            pressBegining = false;
            pressWaitAMomentCo = null;
        }

        // ゲームウィンドウ上にカーソルがあるか？
        private bool isCursorInWindow()
        {
            WinAPI.POINT mpos = new WinAPI.POINT();
            WinAPI.GetCursorPos(out mpos);
            IntPtr window = WinAPI.WindowFromPoint(mpos);
            if (window == WinAPI.WindowHandle)
            {
                // ウィンドウの枠もゲームウィンドウと認識されるので、クライアント領域にいるかも見る
                if(!GameMain.Instance.CMSystem.FullScreen)
                {
                    // クライアント領域の縦横取得
                    WinAPI.RECT rect;
                    WinAPI.GetClientRect(window, out rect);

                    // クライアント領域左上のスクリーン座標を取得
                    WinAPI.POINT topleft = new WinAPI.POINT() { X = 0, Y = 0 };
                    WinAPI.ClientToScreen(window, ref topleft);

                    if( mpos.X >= topleft.X &&
                        mpos.X < topleft.X + rect.Right &&
                        mpos.Y >= topleft.Y &&
                        mpos.Y < topleft.Y + rect.Bottom)
                    {
                        return true;
                    }

                    return false;
                }

                return true;
            }
            return false;
        }

        private void emurateButtonUp()
        {
            if (leftPressed)
            {
                emitMouseEvent(WinAPI.MOUSEEVENTF_LEFTUP);
            }
            else
            {
                emitMouseEvent(WinAPI.MOUSEEVENTF_RIGHTUP);
            }
            leftRightPressed = false;
            pressedInGUIMode = false;
            pressBegining = false;
        }

        /// <summary>
        /// targetにコントローラの移動・回転を適用
        /// markerObjとコントローラ位置の差分を使って移動するので、markerObjコントローラの位置に合わせておく必要がある
        /// </summary>
        /// <param name="target">移動するターゲット</param>
        /// <param name="inverse">移動・回転を逆方向に適用する場合（世界を掴む場合はオフセットに対して適用するので）</param>
        /// <param name="lockRotXZ">XZ軸に沿った回転を無効化するか</param>
        /// <param name="lockY">Y軸方向の移動を無効化するか</param>
        private void applyMove(Transform target, bool inverse, bool lockRotXZ, bool lockY)
        {
            // ポインタの移動・回転を算出
            // 回転の差はクォータニオンの逆数を掛けることで計算できることに注意
            var move = pointerObj.transform.position - markerObj.transform.position;
            var rot = pointerObj.transform.rotation * Quaternion.Inverse(markerObj.transform.rotation);

            if(inverse)
            {
                move = -move;
                rot = Quaternion.Inverse(rot);
            }

            if (lockY)
            {
                // 高さ方向の移動をゼロにする
                move.y = 0;
            }

            if (lockRotXZ)
            {
                // 回転はy軸以外をゼロにする
                rot = Quaternion.Euler(0, rot.eulerAngles.y, 0);
            }

            // ポインタを中心に回転させるため
            // ポインタの位置にセットされたハンドルをターゲットの親に設定して
            // ハンドルに対して移動・回転を適用する

            var parent = target.transform.parent; // あとで戻すため親を記憶しておく

            handleObj.transform.position = pointerObj.transform.position;
            handleObj.transform.rotation = pointerObj.transform.rotation;

            target.transform.parent = handleObj.transform;

            handleObj.transform.position += move;
            handleObj.transform.rotation = rot * handleObj.transform.rotation;

            target.transform.parent = parent;

            markerObj.transform.position = pointerObj.transform.position;
            markerObj.transform.rotation = pointerObj.transform.rotation;
        }

        private void updateGripTarget()
        {
            if (currentGripTarget != GripTarget.None)
            {
                // 掴んでいるときは変更しない
                return;
            }

            // 掴む優先順 GUI -> オブジェクト -> 世界
            var config = VRMenuPlugin.Instance.Config;

            if (isTouchingGUI && ControllerConfig.EnableGripGUI)
            {
                // GUIを掴む
                nextGripTarget = GripTarget.GUI;
                nextGripObject = null;
                return;
            }

            foreach (var handler in gripHandlers)
            {
                if (handler.OnGripTest())
                {
                    nextGripTarget = GripTarget.Handler;
                    nextGripObject = handler;
                    return;
                }
            }

            if (ControllerConfig.EnableGripMaid || ControllerConfig.EnableGripObject)
            {
                foreach (var t in touchingObjects.Items)
                {
                    if (grippingList.Contains(t))
                    {
                        // 既に掴んでいるオブジェクトは除外
                        continue;
                    }

                    // タイプを取得
                    Util.GetGripTarget(t.gameObject, out nextGripTarget);
                    if(nextGripTarget == GripTarget.Maid)
                    {
                        if(ControllerConfig.EnableGripMaid == false)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if(ControllerConfig.EnableGripObject == false)
                        {
                            continue;
                        }
                    }

                    nextGripObject = t;
                    return;
                }
            }

            if (ControllerConfig.EnableGripWorld)
            {
                // 何にも触れていなかったら世界を掴む
                nextGripTarget = GripTarget.World;
                nextGripObject = null;
                return;
            }

            nextGripTarget = GripTarget.None;
            nextGripObject = null;
        }

        private void updateGrip()
        {
            try
            {
                // 掴んでいたら移動する
                switch (currentGripTarget)
                {
                    case GripTarget.GUI:
                        applyMove(guiQuad.gameObject.transform, false, false, false);
                        break;
                    case GripTarget.Object:
                    case GripTarget.Maid:
                    case GripTarget.SpawnItem:
                        applyMove(currentGripObject as Transform, false, isLockAxis, false);
                        break;
                    case GripTarget.World:
                        if (isLockAxis)
                        {
                            applyMove(VRMenuPlugin.Instance.PlayRoomHandle.transform, true,
                                true, !VRMenuPlugin.Instance.Config.EnableWorldYMove);
                        }
                        else
                        {
                            applyMove(VRMenuPlugin.Instance.PlayRoomHandle.transform, true, 
                                !VRMenuPlugin.Instance.Config.EnableWorldXZRotation, false);
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Debug("EXC 1");
                Log.Debug(e);
            }

            // 掴む状態を更新
            bool isTriggerDown = Device.GetPressDown(Button.Trigger);
            bool isGripDown = Device.GetPressDown(Button.Grip);
            bool startByTrigger = false;
            bool startByGrip = false;

            // 現在のモードを考慮
            if(isTriggerDown || isGripDown) {
                var mode = Mode.GetCurrentMode();
                if (mode != null)
                {
                    if(isTriggerDown) {
                        startByTrigger = (mode.IsDiableGripByTrigger == false);
                    }
                    if(isGripDown) {
                        startByGrip = (mode.IsDiableGripByGrip == false);
                    }
                }
                if (isTouchingGUI)
                {
                    // GUI操作は優先させる
                    startByTrigger = isTriggerDown;
                    startByGrip = isGripDown;
                }
                if(Menu.SystemMenuActive)
                {
                    // システムメニュー表示中はトリガーを無効化
                    startByTrigger = false;
                }
            }

            try
            {
                if (startByTrigger || startByGrip)
                {
                    releaseGrip();

                    // グリップでない場合はロックが有効
                    isLockAxis = !startByGrip;

                    currentGripTarget = nextGripTarget;
                    currentGripObject = nextGripObject;

                    Log.Debug("currentGripTarget=" + currentGripTarget);

                    switch (currentGripTarget)
                    {
                        case GripTarget.Handler:
                            //Log.Out("Call OnGripStart");
                            (currentGripObject as IGripTriggerHandler).OnGripStart();
                            break;
                        case GripTarget.Object:
                        case GripTarget.Maid:
                        case GripTarget.SpawnItem:
                            grippingList.Add(currentGripObject as Transform);
                            break;
                        case GripTarget.World:
                            // 世界を掴むはマーカーをプレイルームに対して固定する
                            //（世界に対して固定して世界を動かすとおかしくなるので）
                            markerObj.transform.parent = VRMenuPlugin.Instance.PlayRoom.transform;
                            break;
                    }

                    if (currentGripTarget != GripTarget.None)
                    {
                        // 開始（マーカーをポインタ位置にセット）
                        markerObj.transform.position = pointerObj.transform.position;
                        markerObj.transform.rotation = pointerObj.transform.rotation;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Debug("EXC 2");
                Log.Debug(e);
            }

            if (isTriggerDown == false &&
                isGripDown == false &&
                Device.GetPress(Button.Trigger) == false &&
                Device.GetPress(Button.Grip) == false)
            {
                // 離している
                releaseGrip();
            }
        }

        private void releaseGrip()
        {
            switch(currentGripTarget)
            {
                case GripTarget.Handler:
                    (currentGripObject as IGripTriggerHandler).OnGripReleased();
                    break;
                case GripTarget.Object:
                case GripTarget.Maid:
                case GripTarget.SpawnItem:
                    grippingList.Remove(currentGripObject as Transform);
                    break;
                case GripTarget.World:
                    // マーカーの親を戻す
                    markerObj.transform.parent = VRMenuPlugin.Instance.PlayRoom.transform.parent;
                    break;
            }
            currentGripTarget = GripTarget.None;
            currentGripObject = null;
        }

        private GameObject targetMark;
        private void beam()
        {
            RaycastHit hitinfo;
            if (Physics.Raycast(transform.position + transform.forward * 0.3f, transform.forward,
                out hitinfo))
            {
                if (targetMark == null)
                {
                    targetMark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    targetMark.name = "WarpPosition";
                    targetMark.transform.localScale = Vector3.one * 0.1f;
                    targetMark.transform.parent = VRMenuPlugin.Instance.PlayRoom.transform;
                    targetMark.GetComponent<MeshRenderer>().material.color = Color.cyan;
                    targetMark.GetComponent<Collider>().enabled = false;
                }
                targetMark.SetActive(true);
                targetMark.transform.position = hitinfo.point;

                if(Device.GetPressDown(Button.Menu) && hitinfo.collider != null)
                {
                    Log.Debug("HIT " + hitinfo.collider.gameObject.name + " [" + hitinfo.collider.gameObject.layer + "]");
                }

                return;
            }
            if(targetMark != null)
            {
                targetMark.SetActive(false);
            }
        }

        public void AddGripHandler(IGripTriggerHandler gripHandler)
        {
            gripHandlers.Remove(gripHandler);
            gripHandlers.Add(gripHandler);
        }

        public bool RemoveGripHandler(IGripTriggerHandler gripHandler)
        {
            return gripHandlers.Remove(gripHandler);
        }
    }

    public class ViveVRMenuController : VRMenuController
    {
        private SteamVR_TrackedObject trackedObject_;
        public SteamVR_TrackedObject TrackedObject {
            get {
                if (trackedObject_ == null)
                {
                    trackedObject_ = GetComponent<SteamVR_TrackedObject>();
                }
                return trackedObject_;
            }
        }

        private static readonly ulong[] buttonMask = new ulong[] {
            SteamVR_Controller.ButtonMask.Trigger,
            SteamVR_Controller.ButtonMask.Grip,
            SteamVR_Controller.ButtonMask.ApplicationMenu,
            0,
            0
        };

        protected override ControllerDevice CreateControllerDevice()
        {
            return new ViveControllerDevice(TrackedObject);
        }

        protected override ControllerMenu CreateControllerMenu()
        {
            return new ViveControllerMenu(this);
        }
    }
}
