using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erenshor_WeaponSets
{
    [BepInPlugin(ModGUID, ModDescription, ModVersion)]
    public class WeaponSets : BaseUnityPlugin
    {
        internal const string ModName = "WeaponSets";
        internal const string ModVersion = "1.0.0";
        internal const string ModDescription = "Weapon Sets";
        internal const string Author = "Brad522";
        private const string ModGUID = Author + "." + ModName;

        private ConfigEntry<KeyboardShortcut> _swapKey;
        private bool _swapKeyDownLastFrame;

        internal static ManualLogSource Log;

        public static bool modInitialized;

        public void Awake()
        {
            Log = Logger;
            modInitialized = false;

            _swapKey = Config.Bind(
                "General",
                "Hotkey",
                new KeyboardShortcut(KeyCode.Z),
                "Hotkey used to swap weapon sets. Default is Z.");
            _swapKeyDownLastFrame = false;
        }

        public void OnDestroy()
        {
            if (GameData.PlayerInv != null)
            {
                var weaponSetManager = GameData.PlayerInv.gameObject.GetComponent<WeaponSetManager>();
                if (weaponSetManager != null)
                    GameObject.DestroyImmediate(weaponSetManager);
            }

            Log = null;
            modInitialized = false;
            _swapKey = null;
            _swapKeyDownLastFrame = false;
            
        }

        public void Update()
        {
            if (!modInitialized)
            {
                modInitialized = TryInit();

                if (!modInitialized)
                    return;

                if (!String.IsNullOrEmpty(WeaponSetManager.Instance.SaveFolderPath) && File.Exists(WeaponSetManager.Instance.SaveFolderPath))
                    WeaponSetManager.Instance.LoadSavedData();
            }

            if (modInitialized && WasJustPressed(_swapKey.Value, ref _swapKeyDownLastFrame))
                WeaponSetManager.Instance.SwapSets();
        }

        private bool TryInit()
        {
            if (GameData.InCharSelect)
                return false;

            if (GameData.PlayerInv == null || GameData.PlayerStats == null)
                return false;

            if (GameData.PlayerInv.gameObject.GetComponent<WeaponSetManager>() == null)
                GameData.PlayerInv.gameObject.AddComponent<WeaponSetManager>();

            if (WeaponSetManager.Instance.SaveFolderPath == null)
                WeaponSetManager.Instance.SaveFolderPath = GetSavePath(GameData.PlayerStats.MyName);

            if (WeaponSetManager.Instance.PrimarySlot == null)
                WeaponSetManager.Instance.PrimarySlot = FindEquipmentSlot(Item.SlotType.Primary);

            if (WeaponSetManager.Instance.OffhandSlot == null)
                WeaponSetManager.Instance.OffhandSlot = FindEquipmentSlot(Item.SlotType.Secondary);

            return CheckInit();
        }

        private bool CheckInit()
        {
            return WeaponSetManager.Instance.SaveFolderPath != null &&
                WeaponSetManager.Instance.PrimarySlot != null &&
                WeaponSetManager.Instance.OffhandSlot != null;
        }

        public class WeaponSetManager : MonoBehaviour
        {
            public static WeaponSetManager Instance { get; private set; }

            public string SaveFolderPath;

            public WeaponSetType ActiveSet { get; private set; } = WeaponSetType.SetA;
            private WeaponSet _inactiveSet;

            public ItemIcon PrimarySlot;
            public ItemIcon OffhandSlot;

            private GameObject playerInvUI;
            private GameObject primarySlotUI;

            private GameObject _tagGroup;
            private Button _tagButtonSetA;
            private Button _tagButtonSetB;

            private bool uiInit;

            void Awake()
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(this);
                    return;
                }

                _inactiveSet = new WeaponSet(GameData.PlayerInv.Empty, 1, GameData.PlayerInv.Empty, 1);
                uiInit = false;

                Instance = this;
            }

            void OnDestroy()
            {
                SaveData();

                if (_tagButtonSetA != null)
                {
                    _tagButtonSetA.onClick.RemoveAllListeners();
                    Destroy(_tagButtonSetA.gameObject);
                }

                if (_tagButtonSetB != null)
                {
                    _tagButtonSetB.onClick.RemoveAllListeners();
                    Destroy(_tagButtonSetB.gameObject);
                }

                if (_tagGroup != null)
                    Destroy(_tagGroup);

                SaveFolderPath = null;
                PrimarySlot = null;
                OffhandSlot = null;
                Instance = null;
                WeaponSets.modInitialized = false;
                playerInvUI = null;
                primarySlotUI = null;
                _tagGroup = null;
                _tagButtonSetA = null;
                _tagButtonSetB = null;
                _inactiveSet = null;
                uiInit = false;
            }

            void Update()
            {
                if (!uiInit)
                {
                    if (playerInvUI == null)
                        playerInvUI = GameObject.Find("UI/UIElements/InvPar/PlayerInv");

                    if (primarySlotUI == null)
                        primarySlotUI = GameObject.Find("UI/UIElements/InvPar/PlayerInv/Primary");

                    if (playerInvUI != null && primarySlotUI != null && _tagGroup == null)
                    {
                        CreateSetToggleUI(primarySlotUI.transform.parent);
                        uiInit = true;
                    }
                }

                if (uiInit)
                {
                    if (playerInvUI.activeSelf)
                    {
                        if (!_tagGroup.activeSelf)
                            _tagGroup.SetActive(true);
                    } else
                    {
                        if (_tagGroup.activeSelf)
                            _tagGroup.SetActive(false);
                    }
                }
                
            }

            void CreateSetToggleUI(Transform parent)
            {
                _tagGroup = new GameObject("WeaponSetToggleUI", typeof(RectTransform));
                _tagGroup.transform.SetParent(parent, false);

                var layout = _tagGroup.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 0;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlHeight = false;
                layout.childControlWidth = false;
                layout.childForceExpandHeight = false;
                layout.childForceExpandWidth = false;

                LayoutElement le = _tagGroup.AddComponent<LayoutElement>();
                le.preferredWidth = 20;
                le.preferredHeight = 25;
                le.flexibleWidth = 0;
                le.flexibleHeight = 0;


                var anchor = primarySlotUI.GetComponent<RectTransform>().anchorMin;
                var rt = _tagGroup.GetComponent<RectTransform>();
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.sizeDelta = new Vector2(50, 20);
                rt.anchoredPosition = primarySlotUI.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, 45f);

                _tagButtonSetA = CreateSetButton("1", (int)WeaponSetType.SetA, _tagGroup.transform);
                _tagButtonSetB = CreateSetButton("2", (int)WeaponSetType.SetB, _tagGroup.transform);

                UpdateToggleHighlight();
            }

            private Button CreateSetButton(string label, int index, Transform parent)
            {
                var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);

                var slotImg = primarySlotUI.GetComponent<Image>();
                var img = go.GetComponent<Image>();
                img.material = slotImg.material;
                img.color = slotImg.color;
                img.sprite = slotImg.sprite;

                var textGO = new GameObject("Text", typeof(TextMeshProUGUI));
                textGO.transform.SetParent(go.transform, false);
                var text = textGO.GetComponent<TextMeshProUGUI>();
                text.rectTransform.sizeDelta = new Vector2(20, 25);
                text.raycastTarget = false;
                text.text = label;
                text.fontSize = 14;
                text.alignment = TextAlignmentOptions.Center;

                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(20, 25);

                if (index == 0)
                {
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                }
                else
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot = new Vector2(0f, 0f);
                }

                WeaponSetType targetSet = (WeaponSetType)index;
                var btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (ActiveSet != targetSet)
                        SwapSets();
                });

                return btn;
            }

            private void UpdateToggleHighlight()
            {
                if (_tagButtonSetA != null)
                    _tagButtonSetA.transform.localScale = ActiveSet == WeaponSetType.SetA ? new Vector3(1.2f, 1.2f, 1f) : Vector3.one;

                if (_tagButtonSetB != null)
                    _tagButtonSetB.transform.localScale = ActiveSet == WeaponSetType.SetB ? new Vector3(1.2f, 1.2f, 1f) : Vector3.one;
            }

            public void SwapSets()
            {
                if (PrimarySlot == null || OffhandSlot == null)
                    return;

                var currentPrimary = PrimarySlot.MyItem;
                var currentPrimaryQuality = PrimarySlot.Quantity;

                var currentOffhand = OffhandSlot.MyItem;
                var currentOffhandQuality = OffhandSlot.Quantity;

                PrimarySlot.MyItem = _inactiveSet.PrimaryWeapon;
                if (PrimarySlot.MyItem != null)
                    PrimarySlot.Quantity = _inactiveSet.PrimaryQuality;

                OffhandSlot.MyItem = _inactiveSet.OffhandItem;
                if (OffhandSlot.MyItem != null)
                    OffhandSlot.Quantity = _inactiveSet.OffhandQuality;

                _inactiveSet.PrimaryWeapon = currentPrimary;
                _inactiveSet.PrimaryQuality = currentPrimaryQuality;
                _inactiveSet.OffhandItem = currentOffhand;
                _inactiveSet.OffhandQuality = currentOffhandQuality;

                PrimarySlot.UpdateSlotImage();
                OffhandSlot.UpdateSlotImage();
                GameData.PlayerInv.UpdatePlayerInventory();

                SaveData();

                ActiveSet = ActiveSet == WeaponSetType.SetA ? WeaponSetType.SetB : WeaponSetType.SetA;
                UpdateToggleHighlight();
                UpdateSocialLog.LogAdd($"Equipped weapon set {(int)ActiveSet + 1}", "yellow");
            }

            public void SaveData()
            {
                var saveData = new WeaponSetSaveData
                {
                    ActiveSet = ActiveSet,
                    PrimaryItemID = _inactiveSet.PrimaryWeapon?.Id ?? string.Empty,
                    PrimaryQuality = _inactiveSet.PrimaryQuality,
                    OffhandItemID = _inactiveSet.OffhandItem?.Id ?? string.Empty,
                    OffhandQuality = _inactiveSet.OffhandQuality
                };

                try
                {
                    string json = JsonUtility.ToJson(saveData, prettyPrint: true);
                    File.WriteAllText(SaveFolderPath, json);
                }
                catch (Exception ex)
                {
                    Log.LogError($"[WeaponSets] Failed to save weapon set data: {ex.Message}");
                }
            }

            public void LoadSavedData()
            {
                try
                {
                    string json = File.ReadAllText(SaveFolderPath);
                    var saveData = JsonUtility.FromJson<WeaponSetSaveData>(json);
                    if (saveData == null)
                        Log.LogError("[WeaponSets] Failed to parse saved data.");

                    _inactiveSet = new WeaponSet(
                        GameData.ItemDB.GetItemByID(saveData.PrimaryItemID),
                        saveData.PrimaryQuality,
                        GameData.ItemDB.GetItemByID(saveData.OffhandItemID),
                        saveData.OffhandQuality
                    );

                    ActiveSet = saveData.ActiveSet;
                }
                catch (Exception ex)
                {
                    Log.LogError($"[WeaponSets] Error loading saved data: {ex.Message}");
                }
            }

            public class WeaponSet
            {
                public Item PrimaryWeapon { get; set; }
                public Item OffhandItem { get; set; }
                public int PrimaryQuality { get; set; }
                public int OffhandQuality { get; set; }

                public WeaponSet(Item primaryWeapon, int primaryQuality, Item offhandItem, int offhandQuality)
                {
                    PrimaryWeapon = primaryWeapon;
                    PrimaryQuality = primaryQuality;
                    OffhandItem = offhandItem;
                    OffhandQuality = offhandQuality;
                }
            }

            public class WeaponSetSaveData
            {
                public WeaponSetType ActiveSet;
                public string PrimaryItemID;
                public int PrimaryQuality;
                public string OffhandItemID;
                public int OffhandQuality;
            }

            public enum WeaponSetType
            {
                SetA = 0,
                SetB = 1
            }
        }

        public ItemIcon FindEquipmentSlot(Item.SlotType slotType)
        {
            foreach (ItemIcon icon in GameData.PlayerInv.EquipmentSlots)
            {
                if (icon.ThisSlotType == slotType)
                    return icon;
            }

            return null;
        }

        string GetSavePath(string characterName)
        {
            string modFolder = Path.GetDirectoryName(Info.Location);
            string savePath = Path.Combine(modFolder, "SaveData");

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            string fileName = $"{characterName}_WeaponSet.json";
            return Path.Combine(savePath, fileName);
        }

        bool WasJustPressed(KeyboardShortcut shortcut, ref bool lastState)
        {
            bool isDown = shortcut.IsDown();
            bool wasJustPressed = isDown && !lastState;
            lastState = isDown;
            return wasJustPressed;
        }
    }
}
