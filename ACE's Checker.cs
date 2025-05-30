using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace ACE
{
    [BepInPlugin("com.ace.gorillatag.modchecker", "ACE's Checker", "2.2.22")]
    public class AcesModChecker : BaseUnityPlugin
    {
        private const bool ShowAllCosmetics = false;
        private const string OverrideFullCosmeticsUserId = "1879DA7F4096C99A";
        private const string Version = "2.2.22";
        private static readonly Vector3 PanelPosition = new Vector3(-66.5f, 12.2f, -82.5f);

        private readonly Dictionary<string, string> _specials = new Dictionary<string, string> {
            {"LBAGS","Illustrator Badge"},
            {"LBAAD","Administrator Badge"},
            {"LBAAK","Mod Stick"},
            {"LBADE","Finger Painter Badge"},
            {"LMAPY", "FOREST GUIDE MOD STICK"}
        };

        private Sprite _steamIcon, _metaIcon;
        private readonly Dictionary<string, Sprite> _specialSprites = new Dictionary<string, Sprite>();

        private Sprite _bgSprite;

        private GameObject _canvasGO;
        private Canvas _canvas;
        private RectTransform _panelRT, _leftCol, _rightCol;
        private TextMeshProUGUI _titleText;
        private RectTransform _titlePanel;
        private Camera _cam;
        private bool _isVr;
        private float _lastDelta;

        void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            StartCoroutine(Init());
        }

        IEnumerator Init()
        {
            yield return new WaitUntil(() => Camera.main != null);
            _cam = Camera.main;
            _isVr = XRSettings.enabled && XRSettings.isDeviceActive;

            // load platform icons
            yield return LoadSprite("https://raw.githubusercontent.com/HanSolo1OOOFalcon/WhoIsThatMonke/refs/heads/main/Assets/SteamIcon.png", spr => _steamIcon = spr);
            yield return LoadSprite("https://raw.githubusercontent.com/HanSolo1OOOFalcon/WhoIsThatMonke/refs/heads/main/Assets/MetaIcon.png", spr => _metaIcon = spr);
            // load special cosmetics
            yield return LoadSprite("https://static.wikia.nocookie.net/gorillatag/images/5/56/IllustratorBadgeSprite.png/revision/latest?cb=20240720174852", spr => _specialSprites["LBAGS"] = spr);
            yield return LoadSprite("https://static.wikia.nocookie.net/gorillatag/images/4/40/Adminbadge.png/revision/latest?cb=20220223233745", spr => _specialSprites["LBAAD"] = spr);
            yield return LoadSprite("https://static.wikia.nocookie.net/gorillatag/images/a/aa/Stick.png/revision/latest?cb=20231102195128", spr => _specialSprites["LBAAK"] = spr);
            yield return LoadSprite("https://static.wikia.nocookie.net/gorillatag/images/b/b7/Fingerpaint.png/revision/latest?cb=20231114024321", spr => _specialSprites["LBADE"] = spr);
            yield return LoadSprite("https://static.wikia.nocookie.net/gorillatag/images/d/d8/6767.png/revision/latest?cb=20250507013232", spr => _specialSprites["LMAPY"] = spr);

            // Load a simple background for title and cards (optional, can be null)
            yield return LoadSprite(
                "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTDTZ6IeWDPQc0weVC38ZKfx8QZY6KR1HEDwQ&s",
                spr => _bgSprite = spr);

            CreateUI();
        }

        IEnumerator LoadSprite(string url, Action<Sprite> callback)
        {
            using var uwr = UnityWebRequestTexture.GetTexture(url);
            yield return uwr.SendWebRequest();
            if (uwr.result != UnityWebRequest.Result.Success) yield break;
            var tex = DownloadHandlerTexture.GetContent(uwr);
            callback(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f));
        }

        void Update()
        {
            _lastDelta = Time.deltaTime;
            Refresh();
        }

        void CreateUI()
        {
            _canvasGO = new GameObject("ACE_Canvas");
            _canvasGO.transform.position = PanelPosition;
            DontDestroyOnLoad(_canvasGO);
            _canvas = _canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _cam;
            _canvasGO.AddComponent<GraphicRaycaster>();
            _canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            // 2x scale
            _canvasGO.transform.localScale = Vector3.one * (_isVr ? 0.0005f : 0.0005f);

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(_canvasGO.transform, false);
            _panelRT = panelGO.AddComponent<RectTransform>();

            // Back panel (optional, mostly transparent)
            var bg = panelGO.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.14f, 0.2f, 0.22f);
            bg.raycastTarget = false;

            // TITLE PANEL (rounded card)
            var titlePanelGO = new GameObject("TitlePanel");
            titlePanelGO.transform.SetParent(panelGO.transform, false);
            _titlePanel = titlePanelGO.AddComponent<RectTransform>();
            var titleImg = titlePanelGO.AddComponent<Image>();
            if (_bgSprite != null)
            {
                titleImg.sprite = _bgSprite;
                titleImg.type = Image.Type.Sliced;
            }
            else
            {
                titleImg.color = new Color(0.09f, 0.11f, 0.22f, 0.95f);
            }
            titleImg.raycastTarget = false;
            titleImg.maskable = false;
            titleImg.material = null;
            titleImg.pixelsPerUnitMultiplier = 1f;
            titleImg.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 140); // doubled for looks
            titleImg.GetComponent<Image>().raycastTarget = false;

            // TITLE TEXT
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(titlePanelGO.transform, false);
            _titleText = titleGO.AddComponent<TextMeshProUGUI>();
            _titleText.text = $"ACE’S CHECKER V2  ({Version})";
            _titleText.enableAutoSizing = true;
            _titleText.fontSize = _isVr ? 16 : 8;
            _titleText.color = Color.cyan;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.outlineWidth = 0.3f;
            _titleText.outlineColor = Color.black;
            _titleText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _titleText.rectTransform.anchorMin = new Vector2(0, 0);
            _titleText.rectTransform.anchorMax = new Vector2(1, 1);
            _titleText.rectTransform.offsetMin = Vector2.zero;
            _titleText.rectTransform.offsetMax = Vector2.zero;

            // COLUMNS
            _leftCol = NewColumn("LeftCol", panelGO.transform);
            _rightCol = NewColumn("RightCol", panelGO.transform);
        }

        RectTransform NewColumn(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var vg = go.AddComponent<VerticalLayoutGroup>();
            vg.spacing = 108; // was 54
            vg.childForceExpandWidth = false;
            vg.childForceExpandHeight = false;
            vg.childAlignment = TextAnchor.UpperCenter;
            var csf = go.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rt.pivot = new Vector2(0, 1);
            return rt;
        }

        void Refresh()
        {
            Vector3 direction = (_canvasGO.transform.position - _cam.transform.position).normalized;
            _canvasGO.transform.rotation = Quaternion.LookRotation(direction);

            foreach (Transform t in _leftCol) Destroy(t.gameObject);
            foreach (Transform t in _rightCol) Destroy(t.gameObject);

            var players = PhotonNetwork.PlayerList;
            int count = players.Length;

            // Columns setup
            bool singleCol = count <= 5;
            int leftCount = singleCol ? count : (count + 1) / 2;
            int rightCount = singleCol ? 0 : count - leftCount;

            int leftIndex = 0, rightIndex = 0;
            for (int i = 0; i < count; i++)
            {
                var p = players[i];
                var rig = GorillaGameManager.instance.FindPlayerVRRig(p);
                if (rig == null) continue;

                bool isSteam = rig.concatStringOfCosmeticsAllowed.Contains("FIRST LOGIN");
                Sprite platformIcon = isSteam ? _steamIcon : _metaIcon;

                List<string> owned = (p.UserId == OverrideFullCosmeticsUserId || ShowAllCosmetics)
                  ? _specials.Keys.ToList()
                  : _specials.Keys.Where(k => rig.concatStringOfCosmeticsAllowed.Contains(k)).ToList();

                var c = rig.playerColor;
                int r9 = Mathf.RoundToInt(c.r * 8f) + 1;
                int g9 = Mathf.RoundToInt(c.g * 8f) + 1;
                int b9 = Mathf.RoundToInt(c.b * 8f) + 1;
                string hex = ColorUtility.ToHtmlStringRGB(c);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"<b>{p.NickName}</b>");
                sb.AppendLine($"<color=#{hex}>Color: ({r9},{g9},{b9})</color>");
                sb.AppendLine($"Speed: {rig.LatestVelocity().magnitude:F1}");
                bool has = owned.Any();
                sb.AppendLine($"Specials: <color={(has ? "#88FF88" : "#FF8888")}>{(has ? "YES" : "NO")}</color>");

                // -- PLAYER CARD PARENT --
                var parent = (singleCol || leftIndex < leftCount) ? _leftCol : _rightCol;
                if (parent == _leftCol) leftIndex++; else rightIndex++;

                // --- CARD GO ---
                var cardGO = new GameObject("PlayerCard");
                cardGO.transform.SetParent(parent, false);
                var cardRT = cardGO.AddComponent<RectTransform>();
                var cardImg = cardGO.AddComponent<Image>();
                if (_bgSprite != null)
                {
                    cardImg.sprite = _bgSprite;
                    cardImg.type = Image.Type.Sliced;
                }
                else
                {
                    cardImg.color = new Color(0.13f, 0.16f, 0.18f, 0.98f);
                }
                cardImg.raycastTarget = false;
                cardImg.material = null;
                cardImg.pixelsPerUnitMultiplier = 1f;

                // --- SHADOW EFFECT for Card
                var shadow = cardGO.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.7f);
                shadow.effectDistance = new Vector2(8, -8); // doubled

                // -- PADDING INSIDE THE CARD --
                var cardPadding = cardGO.AddComponent<VerticalLayoutGroup>();
                cardPadding.padding = new RectOffset(48, 48, 36, 36); // doubled
                cardPadding.childAlignment = TextAnchor.MiddleCenter;
                cardPadding.childForceExpandHeight = false;
                cardPadding.childForceExpandWidth = false;
                cardPadding.spacing = 0;

                // Add LayoutElement for consistent card sizing (optional)
                var cardLE = cardGO.AddComponent<LayoutElement>();
                cardLE.preferredWidth = 940; // doubled
                cardLE.minHeight = 200;      // doubled
                cardLE.flexibleHeight = 0;

                // HORIZONTAL CONTENT OF CARD
                var row = new GameObject("Row");
                row.transform.SetParent(cardGO.transform, false);
                var hl = row.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 18; // keep text/icons same spacing for appearance
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;
                hl.childAlignment = TextAnchor.MiddleLeft;
                row.AddComponent<ContentSizeFitter>()
                   .SetLayoutHorizontalFit(ContentSizeFitter.FitMode.PreferredSize)
                   .SetLayoutVerticalFit(ContentSizeFitter.FitMode.PreferredSize);

                // Platform Icon
                var pi = new GameObject("PlatformIcon");
                pi.transform.SetParent(row.transform, false);
                var piImg = pi.AddComponent<Image>();
                piImg.sprite = platformIcon;
                piImg.preserveAspect = true;
                var piLE = pi.AddComponent<LayoutElement>();
                piLE.preferredWidth = 100f; // unchanged
                piLE.preferredHeight = 100f; // unchanged

                // Label
                var txtGO = new GameObject("Label");
                txtGO.transform.SetParent(row.transform, false);
                var tmp = txtGO.AddComponent<TextMeshProUGUI>();
                tmp.text = sb.ToString();
                tmp.enableAutoSizing = true;
                tmp.fontSize = _isVr ? 12 : 7;
                tmp.color = Color.white;
                tmp.outlineWidth = 0.3f;
                tmp.outlineColor = Color.black;
                tmp.alignment = TextAlignmentOptions.Left;
                txtGO.AddComponent<Shadow>().effectColor = new Color32(0, 0, 0, 100);
                txtGO.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);
                txtGO.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 65); // unchanged

                // Cosmetics
                foreach (var key in owned)
                {
                    if (!_specialSprites.TryGetValue(key, out var spr)) continue;
                    var ci = new GameObject("CosmeticIcon");
                    ci.transform.SetParent(row.transform, false);
                    var ciImg = ci.AddComponent<Image>();
                    ciImg.sprite = spr;
                    ciImg.preserveAspect = true;
                    var le = ci.AddComponent<LayoutElement>();
                    le.preferredWidth = 40f; // unchanged
                    le.preferredHeight = 40f; // unchanged
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_leftCol);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rightCol);

            // --- UI Layout Calculation ---
            Vector2 titleSize = _titleText.GetPreferredValues(_titleText.text, 400, 60);
            _titlePanel.sizeDelta = new Vector2(titleSize.x * 2 + 64, titleSize.y * 2 + 56); // 2x size
            _titlePanel.anchoredPosition3D = new Vector3(0, 0, 0);
            _titlePanel.pivot = new Vector2(0.5f, 1);

            Vector2 leftSize = _leftCol.sizeDelta;
            Vector2 rightSize = _rightCol.sizeDelta;
            float pad = 24f, sp = 64f; // doubled
            float colsWidth = leftSize.x + rightSize.x + (rightSize.x > 0 ? sp : 0f);
            float w = Mathf.Max(colsWidth, titleSize.x * 2) + pad * 2; // also use *2 for title width
            float h = _titlePanel.sizeDelta.y + sp + Mathf.Max(leftSize.y, rightSize.y) + pad * 2;
            _panelRT.sizeDelta = new Vector2(w, h);

            _titlePanel.localPosition = new Vector3(0, h / 2 - pad, -0.01f);
            _leftCol.localPosition = new Vector3(-w / 2 + pad, h / 2 - _titlePanel.sizeDelta.y - sp, -0.01f);
            _rightCol.localPosition = new Vector3(-w / 2 + pad + leftSize.x + sp, h / 2 - _titlePanel.sizeDelta.y - sp, -0.01f);
        }
    }

    public static class CSFExtensions
    {
        public static ContentSizeFitter SetLayoutHorizontalFit(this ContentSizeFitter csf, ContentSizeFitter.FitMode m) { csf.horizontalFit = m; return csf; }
        public static ContentSizeFitter SetLayoutVerticalFit(this ContentSizeFitter csf, ContentSizeFitter.FitMode m) { csf.verticalFit = m; return csf; }
    }
}
