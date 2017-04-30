using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CM3D2.VRMenu.Plugin
{
    /// <summary>
    /// アイテム呼び出しをするクラス
    /// </summary>
    public class ItemManager
    {
        public static readonly string ROOT_OBJECT_NAME = "VRMenu_SpawnItemRoot";

        public struct SlotInfo
        {
            public string name;
            public string displayName;

            public SlotInfo(string name, string displayName)
            {
                this.name = name;
                this.displayName = displayName;
            }
        }
        public readonly static SlotInfo[] SlotArray = new SlotInfo[]
        {
            new SlotInfo("acchat", "帽子"),
            new SlotInfo("headset", "ヘッドドレス"),
            new SlotInfo("wear", "トップス"),
            new SlotInfo("skirt", "ボトムス"),
            new SlotInfo("onepiece", "ワンピース"),
            new SlotInfo("mizugi", "水着"),
            new SlotInfo("bra", "ブラジャー"),
            new SlotInfo("panz", "パンツ"),
            new SlotInfo("stkg", "靴下"),
            new SlotInfo("shoes", "靴"),
            new SlotInfo("acckami", "前髪"),
            new SlotInfo("megane", "メガネ"),
            new SlotInfo("acchead", "アイマスク"),
            new SlotInfo("acchana", "鼻"),
            new SlotInfo("accmimi", "耳"),
            new SlotInfo("glove", "手袋"),
            new SlotInfo("acckubi", "ネックレス"),
            new SlotInfo("acckubiwa", "チョーカー"),
            new SlotInfo("acckamisub", "リボン"),
            new SlotInfo("accnip", "乳首"),
            new SlotInfo("accude", "腕"),
            new SlotInfo("accheso", "へそ"),
            new SlotInfo("accashi", "足首"),
            new SlotInfo("accsenaka", "背中"),
            new SlotInfo("accshippo", "しっぽ"),
            new SlotInfo("accxxx", "前穴")
        };

        public class SpawnedItem
        {
            public ItemData itemData;
            public GameObject itemObject;
            public MeshRenderer boxRenderer;
            public BoxCollider boxCollider;
            public TMorph morph;
            public List<UnityEngine.Object> listDEL;
            public GameObject holder {
                get {
                    return itemObject.transform.parent.gameObject;
                }
            }
        }

        public class ItemData : IComparable<ItemData>
        {
            public string slot;
            public string texfilename;
            public string menu;
            public int order;
            public byte[] rawbytes;

            // テクスチャをロードするので例外が出ることがあることに注意
            private Texture2D tex_;
            public Texture2D tex {
                get {
                    if (tex_ == null)
                    {
                        try
                        {
                            tex_ = ImportCM.LoadTexture(texfilename, true).CreateTexture2D();
                        }
                        catch(Exception)
                        {
                            Log.Print("[PhotoModeVRMenu]" + menu + "のテクスチャロードでエラーが発生しました");
                            tex_ = Texture2D.whiteTexture;
                        }
                    }
                    return tex_;
                }
            }

            public int CompareTo(ItemData other)
            {
                if (order != other.order)
                {
                    return order.CompareTo(other.order);
                }
                return texfilename.CompareTo(other.texfilename);
            }
        }

        private Material boxMaterial;

        // アイテムデータ（1回読み込んだらずっと保持）
        private List<ItemData> itemList;

        // 呼び出すアイテムのルートゲームオブジェクト
        private GameObject spawnItemRoot;

        private ItemLoader itemLoader;

        public List<SpawnedItem> CurrentItems {
            get; private set;
        }

        // CurrentItemsが増えたり減ったりするとカウントがアップされる
        public int UpdateCounter {
            get; private set;
        }

        private bool cubeVisible_;
        private bool CubeVisible {
            get { return cubeVisible_; }
            set {
                if(cubeVisible_ != value)
                {
                    cubeVisible_ = value;

                    foreach(var item in CurrentItems)
                    {
                        item.boxRenderer.enabled = value;
                    }
                }
            }
        }

        private int handleVisibleCount = 0;

        // ShowHandleはtrueとfalseを必ず対で呼び出すこと
        public void ShowHandle(bool show)
        {
            if(show)
            {
                if(++handleVisibleCount > 0)
                {
                    CubeVisible = true;
                }
            }
            else
            {
                if(--handleVisibleCount <= 0)
                {
                    CubeVisible = false;
                }
            }
        }

        public ItemManager(Component plugin)
        {
            itemLoader = new ItemLoader(plugin);
            CurrentItems = new List<SpawnedItem>();
        }

        public void Init()
        {
            CurrentItems.Clear();

            if (boxMaterial == null)
            {
                boxMaterial = new Material(Asset.Instance.HandleBox);
                boxMaterial.color = new Color(0.4f, 0.4f, 1f, 0.8f);
            }
            if(spawnItemRoot == null)
            {
                spawnItemRoot = new GameObject("VRMenu_SpawnItemRoot");
            }
            if(itemList == null)
            {
                CreateItemData();
            }
        }

        public void EnumCurrentItems(Action<string, GameObject, TMorph> callback)
        {
            foreach(var item in CurrentItems)
            {
                callback(item.itemData.menu, item.itemObject, item.morph);
            }
        }

        private ItemData ReadMenuFile(BinaryReader reader)
        {
            if (reader.ReadString() != "CM3D2_MENU")
            {
                return null;
            }

            try
            {
                reader.ReadInt32();
                reader.ReadString();
                reader.ReadString();
                string slot = reader.ReadString();
                reader.ReadString();
                reader.ReadInt32();

                string texfilename = "";
                string orderstr = "";
                while (true)
                {
                    int count = reader.ReadByte();
                    for (int k = 0; k < count; k++)
                    {
                        string tag = reader.ReadString();
                        if (tag == "icons")
                        {
                            texfilename = reader.ReadString();
                            break;
                        }
                        if (tag == "priority")
                        {
                            orderstr = reader.ReadString();
                            break;
                        }
                    }
                    if (texfilename != "")
                    {
                        break;
                    }
                }
                int order = 0;
                int.TryParse(orderstr, out order);

                return new ItemData() { slot = slot, texfilename = texfilename, order = order };
            }
            catch (Exception)
            {
                // ちゃんとファイル読めばEOF例外にならなくすることができるかもしれないが
                // 面倒なので放置
            }

            return null;
        }

        private void CreateItemData()
        {
            if (itemList != null)
            {
                return;
            }

            itemList = new List<ItemData>();

            var have_item_list = GameMain.Instance.CharacterMgr.GetPlayerParam().status.have_item_list;
            foreach (var path in GameUty.MenuFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(path) + ".menu";
                using (var file = GameUty.FileOpen(filename))
                {
                    var bytes = file.ReadAll();
                    using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
                    {
                        var itemData = ReadMenuFile(reader);
                        if (itemData != null)
                        {
                            using(var texfile = GameUty.FileOpen(itemData.texfilename)) {
                                // テクスチャがOpenできるのだけ
                                if(texfile.IsValid())
                                {
                                    itemData.menu = filename;
                                    itemData.rawbytes = bytes;
                                    itemList.Add(itemData);
                                }
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<ItemData> GetSlotItems(SlotInfo slot)
        {
            return itemList.Where(item => item.slot == slot.name).OrderBy(item => item);
        }

        public bool SpawnItem(ItemData itemData)
        {
            TMorph morph;
            List<UnityEngine.Object> listDEL = new List<UnityEngine.Object>();
            GameObject newItem = itemLoader.LoadCM3D2Item(itemData.rawbytes, out morph, listDEL);
            if (newItem != null)
            {
                // 移動するためのゲームオブジェクト
                GameObject itemHolder = new GameObject("ItemSpawnedByPhotoModeVRMenu");
                itemHolder.transform.parent = spawnItemRoot.transform;

                newItem.transform.parent = itemHolder.transform;
                newItem.transform.localPosition = Vector3.zero;
                newItem.transform.localRotation = Quaternion.identity;

                // 掴むためのボックス
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "box";
                box.transform.parent = itemHolder.transform;
                box.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                box.GetComponent<MeshRenderer>().sharedMaterial = boxMaterial;

                // ボックスの位置
                var bounds = newItem.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
                var center = Vector3.zero;
                if (bounds.size.magnitude < 2.5f)
                {
                    // オブジェクトの実際の中心にボックスを配置
                    //（あまりに大きいオブジェクトの場合、中心がかなり遠くになる可能性があるので除外）
                    center = bounds.center;
                }
                box.transform.localPosition = center;

                box.GetComponent<MeshRenderer>().enabled = cubeVisible_;

                itemHolder.transform.localPosition = Vector3.zero;

                CurrentItems.Add(new SpawnedItem() {
                    itemData = itemData,
                    itemObject = newItem,
                    boxRenderer = box.GetComponent<MeshRenderer>(),
                    boxCollider = box.GetComponent<BoxCollider>(),
                    listDEL = listDEL,
                    morph = morph
                });

                ++UpdateCounter;

                Log.Debug("Spawnしました");
                return true;
            }
            return false;
        }

        public bool RemoveItem(int itemIndex)
        {
            if(itemIndex < CurrentItems.Count)
            {
                // holderから削除
                GameObject.Destroy(CurrentItems[itemIndex].itemObject.transform.parent.gameObject);
                // 関連オブジェクトを削除
                foreach(var obj in CurrentItems[itemIndex].listDEL)
                {
                    GameObject.Destroy(obj);
                }
                CurrentItems.RemoveAt(itemIndex);
                ++UpdateCounter;
                return true;
            }
            return false;
        }

    }

    // アイテム呼び出しに使う
    public class ItemLoader
    {
        private Component plugin;
        private string dummySlotName = "HandItemR";
        private TBody dummyBody;
        private TBodySkin bodySlot;

        private GameObject newItem;
        private TMorph newItemMorph;

        public ItemLoader(Component plugin)
        {
            this.plugin = plugin;
        }

        private void CreateDummyBody()
        {
            if(dummyBody != null)
            {
                return;
            }

            dummyBody = new GameObject("PhotoModeVRMenuBody").AddComponent<TBody>();
            dummyBody.transform.parent = plugin.transform;
            dummyBody.Init(null);
            dummyBody.enabled = false;

            int slotNo = (int)TBody.hashSlotName[dummySlotName];
            bodySlot = dummyBody.GetSlot(slotNo);
        }

        private void AddItem(string modelFilename)
        {
            if (newItem != null)
            {
                GameObject.Destroy(newItem);
            }
            int layer = 0;
            newItemMorph = new TMorph(bodySlot);
            newItem = ImportCM.LoadSkinMesh_R(modelFilename, newItemMorph, dummySlotName, bodySlot, layer);
            newItemMorph.InitGameObject(newItem);
        }

        private void ChangeMaterial(int f_nMatNo, string f_strFileName)
        {
            if (newItem != null)
            {
                foreach (var child in newItem.transform.GetComponentsInChildren<Transform>(true))
                {
                    Renderer component = child.GetComponent<Renderer>();
                    if (component != null && component.material != null && f_nMatNo < component.materials.Length)
                    {
                        ImportCM.LoadMaterial(f_strFileName, bodySlot, component.materials[f_nMatNo]);
                    }
                }
            }
        }

        private void LoadCM3D2ItemInternal(byte[] menuData)
        {
            using (var reader = new BinaryReader(new MemoryStream(menuData), Encoding.UTF8))
            {
                string header = reader.ReadString();
                NDebug.Assert(header == "CM3D2_MENU", "LoadCM3D2Item 例外 : ヘッダーファイルが不正です。" + header);
                reader.ReadInt32();
                string path = reader.ReadString();
                reader.ReadString();
                reader.ReadString();
                reader.ReadString();
                reader.ReadInt32();
                try
                {
                    int fragcount;
                    while ((fragcount = reader.ReadByte()) != 0)
                    {
                        string text = "";
                        for (int i = 0; i < fragcount; i++)
                        {
                            text += "\"" + reader.ReadString() + "\" ";
                        }
                        if (text != "")
                        {
                            string stringCom = UTY.GetStringCom(text);
                            string[] stringList = UTY.GetStringList(text);
                            if (stringCom == "additem")
                            {
                                AddItem(stringList[1]);
                            }
                            else if (stringCom == "マテリアル変更")
                            {
                                int f_nMatNo = int.Parse(stringList[2]);
                                string f_strFileName = stringList[3];
                                ChangeMaterial(f_nMatNo, f_strFileName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    NDebug.Assert("メニューファイル処理中にエラーが発生しました。" + Path.GetFileName(path));
                    throw ex;
                }
            }
        }

        public GameObject LoadCM3D2Item(byte[] menuData, out TMorph morph, List<UnityEngine.Object> listDEL)
        {
            newItem = null;
            
            // アイテムは体に付けるものなのでその体を作っておく
            CreateDummyBody();

            bodySlot.listDEL = listDEL;
            LoadCM3D2ItemInternal(menuData);

            if(newItem != null)
            {
                morph = newItemMorph;
            }
            else
            {
                morph = null;
            }

            return newItem;
        }
    }
}
