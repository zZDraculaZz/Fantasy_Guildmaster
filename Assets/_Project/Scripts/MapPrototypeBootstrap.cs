using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UObject = UnityEngine.Object;

public class MapPrototypeBootstrap : MonoBehaviour
{
    [Header("Optional (can be empty)")]
    [SerializeField] private Sprite mapSprite;

    [Header("UI")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
    [SerializeField] private float rightPanelWidth = 420f;

    private TMP_Text _regionName;
    private TMP_Text _undead;
    private TMP_Text _cult;

    private bool _built;

    [Serializable]
    private class Region
    {
        public string id;
        public string name;
        public int undead;
        public int cult;
        public Vector2 uiPos;
    }

    private readonly List<Region> _regions = new()
    {
        new Region { id="north_march", name="Ñåâåðíàÿ Ìàðêà", undead=35, cult=10, uiPos=new Vector2(-350, 200) },
        new Region { id="ashen_fens",  name="Ïåïåëüíûå Òîïè", undead=60, cult=40, uiPos=new Vector2(-150, -50) },
        new Region { id="iron_coast",  name="Æåëåçíûé Áåðåã", undead=15, cult=25, uiPos=new Vector2(200, 120) }
    };

    private void Awake()
    {
        if (_built) return;
        _built = true;

        EnsureEventSystem();
        var canvas = EnsureCanvas();

        // ×òîáû íå ïëîäèòü UI ïðè ïîâòîðíîì çàïóñêå (åñëè Enter Play Mode áåç domain reload)
        var existing = canvas.transform.Find("MapUI");
        if (existing != null) return;

        BuildUI(canvas.transform);

        // Âûáåðåì ïåðâûé ðåãèîí ïî óìîë÷àíèþ
        if (_regions.Count > 0) SelectRegion(_regions[0]);
    }

    private void EnsureEventSystem()
    {
        // Explicit UnityEngine.Object alias avoids CS0104 ambiguity with System.Object/object.
        if (UObject.FindFirstObjectByType<EventSystem>() != null) return;

        var esGo = new GameObject("EventSystem", typeof(EventSystem));

        // Åñëè óñòàíîâëåí New Input System  äîáàâèì åãî ìîäóëü (åñëè äîñòóïåí),
        // èíà÷å èñïîëüçóåì ñòàðûé StandaloneInputModule.
        var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
            esGo.AddComponent(inputSystemModuleType);
        else
            esGo.AddComponent<StandaloneInputModule>();
    }

    private Canvas EnsureCanvas()
    {
        var existing = GameObject.Find("MapCanvas");
        if (existing != null && existing.TryGetComponent(out Canvas c0))
            return c0;

        var canvasGo = new GameObject("MapCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f; // ñåðåäèíà

        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private void BuildUI(Transform canvas)
    {
        var root = CreateUIObject("MapUI", canvas);
        var rootRt = root.GetComponent<RectTransform>();
        StretchFull(rootRt);

        // Background
        var bg = CreateUIObject("MapBackground", root.transform);
        var bgRt = bg.GetComponent<RectTransform>();
        StretchFull(bgRt);

        var bgImg = bg.AddComponent<Image>();
        // Keep background transparent when no sprite is assigned so it does not hide the scene map.
        bgImg.color = mapSprite != null ? new Color(0.08f, 0.08f, 0.10f, 1f) : Color.clear;
        if (mapSprite != null)
        {
            bgImg.sprite = mapSprite;
            bgImg.preserveAspect = true;
        }

        // Right panel
        var panel = CreateUIObject("RegionDetailsPanel", root.transform);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.offsetMin = new Vector2(-rightPanelWidth, 0);
        panelRt.offsetMax = new Vector2(0, 0);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.55f);

        // Texts inside panel
        _regionName = CreateTMP("RegionNameText", panel.transform, 26, FontStyles.Bold);
        _regionName.rectTransform.anchorMin = new Vector2(0, 1);
        _regionName.rectTransform.anchorMax = new Vector2(1, 1);
        _regionName.rectTransform.pivot = new Vector2(0.5f, 1f);
        _regionName.rectTransform.offsetMin = new Vector2(16, -64);
        _regionName.rectTransform.offsetMax = new Vector2(-16, -16);
        _regionName.alignment = TextAlignmentOptions.TopLeft;

        _undead = CreateTMP("UndeadText", panel.transform, 18, FontStyles.Normal);
        _undead.rectTransform.anchorMin = new Vector2(0, 1);
        _undead.rectTransform.anchorMax = new Vector2(1, 1);
        _undead.rectTransform.pivot = new Vector2(0.5f, 1f);
        _undead.rectTransform.offsetMin = new Vector2(16, -120);
        _undead.rectTransform.offsetMax = new Vector2(-16, -80);
        _undead.alignment = TextAlignmentOptions.TopLeft;

        _cult = CreateTMP("CultText", panel.transform, 18, FontStyles.Normal);
        _cult.rectTransform.anchorMin = new Vector2(0, 1);
        _cult.rectTransform.anchorMax = new Vector2(1, 1);
        _cult.rectTransform.pivot = new Vector2(0.5f, 1f);
        _cult.rectTransform.offsetMin = new Vector2(16, -160);
        _cult.rectTransform.offsetMax = new Vector2(-16, -120);
        _cult.alignment = TextAlignmentOptions.TopLeft;

        // Regions layer (buttons)
        var regionsLayer = CreateUIObject("RegionsLayer", root.transform);
        var regionsRt = regionsLayer.GetComponent<RectTransform>();
        StretchFull(regionsRt);

        foreach (var r in _regions)
        {
            var btnGo = CreateUIObject($"Region_{r.id}", regionsLayer.transform);

            var rt = btnGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220, 44);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = r.uiPos; // êëþ÷åâîé ïàðàìåòð ïîçèöèîíèðîâàíèÿ :contentReference[oaicite:4]{index=4}

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.85f, 0.15f, 0.15f, 0.60f);

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => SelectRegion(r)); // onClick :contentReference[oaicite:5]{index=5}

            var label = CreateTMP("Label", btnGo.transform, 16, FontStyles.Bold);
            label.text = r.name;
            label.alignment = TextAlignmentOptions.Center;
            StretchFull(label.rectTransform, 6);
        }
    }

    private void SelectRegion(Region r)
    {
        if (_regionName == null) return;

        _regionName.text = r.name;
        _undead.text = $"Íåæèòü: {r.undead}";
        _cult.text = $"Êóëüò: {r.cult}";
    }

    // ---------- UI helpers ----------
    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TMP_Text CreateTMP(string name, Transform parent, float size, FontStyles style)
    {
        var go = CreateUIObject(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = Color.white;
        t.text = "";
        return t;
    }

    private static void StretchFull(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }
}
