using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CM3D2.VRMenuPlugin
{
    // Vive用のメニュー表示の実体
    public class ViveMenuButtons : VRMenuButtons
    {
        public SteamVR_TrackedObject TrackedObject;
        private SteamVR_Controller.Device Device
        {
            get
            {
                return SteamVR_Controller.Input((int)TrackedObject.index);
            }
        }

        public Controller ControllerId;

        public float TouchPadRadius;
        public float TextDepth;
        public float TextCircleRadius;
        public float LineWidth;
        public float BoldLineWidth;

        private int NumButtons_;
        private float TextDepth_;
        private string Caption_;

        private Canvas canvas;
        private Image image;
        private Text text;

        private Font font;

        private GuideButton[] buttons;

        private int selectedIndex;

        private readonly static float UI_SCALE = 2000;
        private readonly static int FONT_SIZE = 30;
        private readonly static int CIRCLE_SPLITS = 48;

        public Color NormalColor = new Color(1, 1, 1, 0.5f);
        public Color NormalSelectedColor = new Color(0.5f, 1, 1, 1);
        public Color NormalForeColor = Color.black;
        public Color ToggledColor = new Color(0, 0, 1, 0.8f);
        public Color ToggledSelectedColor = new Color(0, 0, 1, 1);
        public Color ToggledForeColor = Color.white;

        void Start()
        {
            font = Resources.FindObjectsOfTypeAll<Font>()[0];

            GameObject textCanvas = new GameObject("Canvas");
            textCanvas.transform.SetParent(transform, false);
            canvas = textCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.localScale = Vector3.one * (1f / UI_SCALE);

            // キャプションを作成
            var imageObject = new GameObject("Image");
            image = imageObject.AddComponent<Image>();
            image.transform.SetParent(canvas.transform, false);
            image.rectTransform.pivot = new Vector2(0.5f, 0); ;
            image.rectTransform.anchoredPosition = new Vector2(0, TextCircleRadius * 1.6f) * UI_SCALE;
            image.color = NormalColor;
            image.material = new Material(Asset.Instance.UIDefaultShader);

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(image.transform, false);
            text = textObject.AddComponent<Text>();
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.text = "";
            text.font = font;
            text.fontSize = FONT_SIZE;
            text.color = NormalForeColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.material = new Material(Asset.Instance.UIFontShader);

            image.gameObject.SetActive(false);

            selectedIndex = -1;
        }

        void Update()
        {
            try
            {
                Update_();
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }

        void Update_() {
            // ボタンの数が変わっているか見る
            int numButtons = (MenuData != null) ? MenuData.Texts.Length : 0;

            if (NumButtons_ != numButtons)
            {
                NumButtons_ = numButtons;

                if (buttons != null)
                {
                    // ボタンを削除
                    foreach (var button in buttons)
                    {
                        button.Destroy();
                    }
                    buttons = null;
                }
            }

            if(numButtons == 0)
            {
                setSelection(-1);
                return;
            }

            string[] labels = MenuData.Texts;
            bool[] toggles = MenuData.ToggleState;
            bool showLabel = MenuData.ShowLabel;

            int togglesLength = (toggles == null) ? 0 : toggles.Length;

            // ボタンがなかったら生成
            if (buttons == null)
            {
                buttons = new GuideButton[numButtons];

                for (int i = 0; i < numButtons; ++i)
                {
                    buttons[i] = new GuideButton();
                }
            }

            if (TextDepth_ != TextDepth)
            {
                TextDepth_ = TextDepth;
                canvas.transform.localPosition = new Vector3(0, 0, -TextDepth);
            }

            // ボタンの角度を計算しておく
            double arcAngle = (2 * Math.PI) / numButtons;
            double angleStart = (MenuData.AngleOffset * (Math.PI/180)) + (0.5 * Math.PI) - (arcAngle / 2);

            // ユーザ操作を処理
            Vector2 position = Device.GetAxis();
            if (position.x == 0 && position.y == 0)
            {
                setSelection(-1);
            }
            else
            {
                double angle = angleFromPosition(position);

                angle = (angle - angleStart) % (2 * Math.PI);
                if (angle < 0)
                {
                    angle += 2 * Math.PI;
                }

                int index = (int)(angle / arcAngle);
                if (index >= labels.Length || labels[index] == null)
                {
                    index = -1;
                }
                setSelection(index);

                if(index >= 0)
                {
                    if (Device.GetPressUp(SteamVR_Controller.ButtonMask.Touchpad))
                    {
                        MenuData.OnClicked(ControllerId, index);
                    }
                    else if (Device.GetPress(SteamVR_Controller.ButtonMask.Touchpad))
                    {
                        MenuData.OnPress(ControllerId, index);
                    }
                }
            }

            // ボタンが更新されていたらメッシュを更新
            for (int i = 0; i < numButtons; ++i)
            {
                GuideButton button = buttons[i];

                double angleOffset = angleStart + arcAngle * i;
                double lineWidth = LineWidth;
                double splitLineWidth = LineWidth / 2;
                bool toggle = (i < togglesLength && toggles[i]);

                if (i == selectedIndex)
                {
                    lineWidth = BoldLineWidth;
                    splitLineWidth = (BoldLineWidth - LineWidth) + splitLineWidth;
                }

                Color main = (i == selectedIndex) ? NormalSelectedColor : NormalColor;
                Color fore;
                Color back;
                if(toggle)
                {
                    fore = ToggledForeColor;
                    back = (i == selectedIndex) ? ToggledSelectedColor : ToggledColor;
                }
                else
                {
                    fore = NormalForeColor;
                    back = (i == selectedIndex) ? NormalSelectedColor : NormalColor;
                }

                string label = null;
                if (i < labels.Length)
                {
                    label = labels[i];
                }

                if (button.AngleOffset != angleOffset ||
                    button.ArcAngle != arcAngle ||
                    button.TouchPadRadius != TouchPadRadius ||
                    button.TextDepth != TextDepth ||
                    button.TextCircleRadius != TextCircleRadius ||
                    button.LineWidth != lineWidth ||
                    button.SplitLineWidth != splitLineWidth ||
                    button.MainColor != main ||
                    button.ForeColor != fore ||
                    button.BackColor != back ||
                    button.Label != label ||
                    button.ShowLabel != showLabel)
                {
                    // パラメータが変わっていたら作り直す
                    button.AngleOffset = angleOffset;
                    button.ArcAngle = arcAngle;
                    button.TouchPadRadius = TouchPadRadius;
                    button.TextDepth = TextDepth;
                    button.TextCircleRadius = TextCircleRadius;
                    button.LineWidth = lineWidth;
                    button.SplitLineWidth = splitLineWidth;
                    button.MainColor = main;
                    button.ForeColor = fore;
                    button.BackColor = back;
                    button.Label = label;
                    button.ShowLabel = showLabel;

                    button.Init(gameObject, canvas, font);
                    button.Rebuild();
                }
            }

            // キャプションを更新
            if(Caption_ != MenuData.Caption)
            {
                Caption_ = MenuData.Caption;
                if(Caption_ != null)
                {
                    image.color = NormalColor;
                    text.text = Caption_;
                    Vector2 preferedSize = new Vector2(text.preferredWidth, text.preferredHeight);
                    text.rectTransform.sizeDelta = preferedSize;
                    image.rectTransform.sizeDelta = new Vector2(preferedSize.x + 20, FONT_SIZE);
                    image.gameObject.SetActive(true);
                }
            }
        }

        private static double angleFromPosition(Vector2 position)
        {
            double angle;
            if (position.x == 0)
            {
                angle = (position.y > 0) ? (Math.PI / 2) : -(Math.PI / 2);
            }
            else
            {
                angle = Math.Atan(position.y / position.x);
                if (position.x < 0)
                {
                    angle += Math.PI;
                }
            }
            return angle;
        }

        private void setSelection(int index)
        {
            if (selectedIndex != index)
            {
                selectedIndex = index;
                MenuData.OnSelectionChanged(ControllerId, index);
            }
        }

        private class GuideButton
        {
            public double AngleOffset;
            public double ArcAngle;
            public double TouchPadRadius;
            public double TextDepth;
            public double TextCircleRadius;
            public double LineWidth;
            public double SplitLineWidth;
            public string Label;
            public Color MainColor = Color.white;
            public Color BackColor = Color.white;
            public Color ForeColor = Color.black;
            public bool ShowLabel;

            public bool Initialized = false;

            private GameObject guideObject;
            private GameObject arrowObject;

            private Mesh guideMesh;
            private Mesh arrowMesh;
            private Image image;
            private Text text;

            private void initMeshObject(GameObject go, Shader shader, Mesh mesh)
            {
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();

                // デフォルトシェーダーを設定
                MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = new Material(shader);
                meshRenderer.sharedMaterial.color = MainColor;

                // メッシュオブジェクト作成
                go.GetComponent<MeshFilter>().mesh = mesh;
            }

            public void Init(GameObject parent, Canvas canvas, Font font)
            {
                if (Initialized) return;

                Shader shader = Asset.Instance.LabelShader;

                // ボタンのガイド
                guideObject = new GameObject("VRMenu - ButtonGuide");
                guideObject.transform.SetParent(parent.transform, false);
                guideMesh = new Mesh();
                guideMesh.name = "GuideButtonMesh";
                initMeshObject(guideObject, shader, guideMesh);

                // ラベルに向けた矢印部分
                arrowObject = new GameObject("VRMenu - LabelArrow");
                arrowObject.transform.SetParent(parent.transform, false);
                arrowMesh = new Mesh();
                arrowMesh.name = "LabelArrowMesh";
                initMeshObject(arrowObject, shader, arrowMesh);

                // ラベルを作成
                double angle = AngleOffset + (ArcAngle / 2);
                float x = (float)(TextCircleRadius * Math.Cos(angle));
                float y = (float)(TextCircleRadius * Math.Sin(angle));

                Vector2 pivot;
                if (Math.Abs(Math.Cos(angle)) < 0.1)
                {
                    if (Math.Sin(angle) > 0)
                    {
                        // 上
                        pivot = new Vector2(0.5f, 0);
                    }
                    else
                    {
                        // 下
                        pivot = new Vector2(0.5f, 1);
                    }
                }
                else
                {
                    if (Math.Cos(angle) > 0)
                    {
                        // 右
                        pivot = new Vector2(0, 0.5f);
                    }
                    else
                    {
                        // 左
                        pivot = new Vector2(1, 0.5f);
                    }
                }

                var imageObject = new GameObject("Image");
                image = imageObject.AddComponent<Image>();
                image.transform.SetParent(canvas.transform, false);
                image.rectTransform.pivot = pivot;
                image.rectTransform.anchoredPosition = new Vector2(x, y) * UI_SCALE;
                image.color = BackColor;
                image.material = new Material(Asset.Instance.UIDefaultShader);

                var textObject = new GameObject("Text");
                textObject.transform.SetParent(image.transform, false);
                text = textObject.AddComponent<Text>();
                text.rectTransform.anchoredPosition = Vector2.zero;
                text.text = "";
                text.font = font;
                text.fontSize = FONT_SIZE;
                text.color = ForeColor;
                text.alignment = TextAnchor.MiddleCenter;
                text.material = new Material(Asset.Instance.UIFontShader);

                Initialized = true;
            }

            public void Destroy()
            {
                GameObject.Destroy(guideObject);
                GameObject.Destroy(arrowObject);
                GameObject.Destroy(image.gameObject);
            }

            private void updateSize()
            {
                Vector2 preferedSize = new Vector2(text.preferredWidth, text.preferredHeight);
                text.rectTransform.sizeDelta = preferedSize;
                image.rectTransform.sizeDelta = new Vector2(preferedSize.x + 20, FONT_SIZE);
            }

            // 状態が変わったら呼び出す
            public void Rebuild()
            {
                RebuidMesh();

                // 色を反映
                guideObject.GetComponent<MeshRenderer>().sharedMaterial.color = MainColor;
                arrowObject.GetComponent<MeshRenderer>().sharedMaterial.color = MainColor;
                image.color = BackColor;
                text.color = ForeColor;

                // テキストを反映
                if (Label != null && ShowLabel)
                {
                    image.gameObject.SetActive(true);
                    arrowObject.SetActive(true);
                    text.text = Label;
                    updateSize();
                }
                else
                {
                    arrowObject.SetActive(false);
                    image.gameObject.SetActive(false);
                }
            }

            private void RebuidMesh()
            {
                // Arc=ボタンの半分の弧
                bool isCircle = (Math.Abs(ArcAngle - (2 * Math.PI)) < 0.01);

                int numArcSplit = (int)(CIRCLE_SPLITS * (ArcAngle / 2) / (2 * Math.PI));
                int numTris = 3 + 2 + 2 * numArcSplit * 2;
                int numVertices = numTris + 2;

                Vector3[] vertices = new Vector3[numVertices];
                Vector2[] uvs = new Vector2[numVertices];
                int[] tris = new int[numTris * 3];

                double innerLineAngle = isCircle ? 0 : Math.Asin(SplitLineWidth / (TouchPadRadius - LineWidth));
                double outerLineAngle = Math.Asin((LineWidth / 2) / TouchPadRadius);
                double innerSplitAngle = (ArcAngle - innerLineAngle * 2) / (2 * numArcSplit);
                double outerSplitAngle = (ArcAngle - outerLineAngle * 2) / (2 * numArcSplit);

                double halfAngle = ArcAngle / 2;
                double centerAngle = AngleOffset + halfAngle;

                double outerRadius = TouchPadRadius;
                double innerRadius = outerRadius - LineWidth;

                int verticesOffset = 0;
                int trisOffset = 0;

                // 中心角より後ろ
                for (int i = 0; i <= numArcSplit; ++i)
                {
                    vertices[verticesOffset++] = fromPolar(outerRadius, centerAngle + outerLineAngle + outerSplitAngle * i);
                    vertices[verticesOffset++] = fromPolar(innerRadius, centerAngle + innerSplitAngle * i);
                }

                if(isCircle)
                {
                    // これは必要ないので頂点数だけ合わせる
                    Vector3 last = vertices[verticesOffset - 1];
                    vertices[verticesOffset++] = last;
                    vertices[verticesOffset++] = last;
                }
                else
                {
                    // 中心の2点
                    vertices[verticesOffset++] = Vector3.zero;
                    vertices[verticesOffset++] = fromPolar(SplitLineWidth / Math.Sin(halfAngle), centerAngle);
                }

                // 中心角より前
                for (int i = numArcSplit; i >= 0; --i)
                {
                    vertices[verticesOffset++] = fromPolar(outerRadius, centerAngle - outerLineAngle - outerSplitAngle * i);
                    vertices[verticesOffset++] = fromPolar(innerRadius, centerAngle - innerSplitAngle * i);
                }

                // ここまでは全て四角形なのでまとめて三角形インデックスを生成
                for (int i = 2; i < verticesOffset; i += 2)
                {
                    tris[trisOffset++] = i - 2;
                    tris[trisOffset++] = i - 1;
                    tris[trisOffset++] = i + 1;

                    tris[trisOffset++] = i - 2;
                    tris[trisOffset++] = i + 1;
                    tris[trisOffset++] = i + 0;
                }

                // うにょ～ん部分 //

                int vOffset = verticesOffset - 2; // 直前の2個の座標を使う

                // 結合部分の三角形
                vertices[verticesOffset++] = vertices[0]; // ここの座標は最初と同じ
                tris[trisOffset++] = vOffset + 0;
                tris[trisOffset++] = vOffset + 1;
                tris[trisOffset++] = vOffset + 2;

                Vector3[] arrowVertices = new Vector3[4];
                Vector2[] arrowUVs = new Vector2[4];
                int[] arrowTris = new int[6] { 0, 1, 3, 0, 3, 2 };

                // うにょ～んのベクトル
                Vector3 arrowDirection = Quaternion.Euler(0, 0, (float)(centerAngle * (180 / Math.PI))) *
                    new Vector3((float)(TextCircleRadius - TouchPadRadius), 0, -(float)TextDepth);

                // うにょ～んの四角形
                arrowVertices[0] = vertices[vOffset + 0];
                arrowVertices[1] = vertices[vOffset + 2];
                arrowVertices[2] = vertices[vOffset + 0] + arrowDirection;
                arrowVertices[3] = vertices[vOffset + 2] + arrowDirection;

                // uvを計算
                CalcUVs(uvs, vertices);
                CalcUVs(arrowUVs, arrowVertices);

                // Meshに割り当て
                SetVertices(guideMesh, vertices, uvs, tris);
                SetVertices(arrowMesh, arrowVertices, arrowUVs, arrowTris);
            }

            private void CalcUVs(Vector2[] uvs, Vector3[] vertices)
            {
                float scale = 1.0f / (2 * (float)TextCircleRadius);
                for (int i = 0; i < vertices.Length; ++i)
                {
                    Vector3 v = vertices[i];
                    uvs[i] = new Vector2(
                        (v.x + (float)TextCircleRadius) * scale,
                        (v.y + (float)TextCircleRadius) * scale);
                }
            }

            private static void SetVertices(Mesh mesh, Vector3[] vertices, Vector2[] uvs, int[] tris)
            {
                mesh.Clear();
                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.triangles = tris;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }

            private static Vector3 fromPolar(double r, double theta)
            {
                return new Vector3((float)(r * Math.Cos(theta)), (float)(r * Math.Sin(theta)));
            }
        }
    }

}
