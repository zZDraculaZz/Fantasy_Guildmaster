#if UNITY_EDITOR
using FantasyGuildmaster.Map;
using FantasyGuildmaster.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyGuildmaster.Editor
{
    public static class MapSceneSetup
    {
        private const string PrefabsDir = "Assets/_Game/Prefabs";
        private const string MarkerPrefabPath = PrefabsDir + "/RegionMarkerButton.prefab";
        private const string ContractRowPrefabPath = PrefabsDir + "/ContractRow.prefab";

        [MenuItem("Tools/FantasyGuildmaster/Setup Map Scene")]
        public static void Setup()
        {
            EnsureFolders();
            EnsureEventSystem();

            if (!PlaceholderIconsGenerator.HasRequiredIcons())
            {
                PlaceholderIconsGenerator.GenerateIcons(false);
                Debug.Log("[MapSceneSetup] Placeholder icons were missing and have been generated.");
            }

            var markerPrefab = EnsureRegionMarkerPrefab();
            var contractPrefab = EnsureContractRowPrefab();

            var root = FindOrCreate("MapSceneRoot");
            var clock = root.GetComponent<GameClock>() ?? root.AddComponent<GameClock>();
            var controller = root.GetComponent<MapController>() ?? root.AddComponent<MapController>();

            var canvas = EnsureCanvas(root.transform);
            var layout = FindOrCreateUI("MapLayout", canvas.transform);
            Stretch(layout);

            var detailsPanel = EnsureDetailsPanel(layout.transform, contractPrefab);
            var mapRect = EnsureMapArea(layout.transform, out var markersRoot);

            AssignMapController(controller, mapRect, markersRoot, markerPrefab, detailsPanel, clock);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[MapSceneSetup] Map Scene setup completed.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game")) AssetDatabase.CreateFolder("Assets", "_Game");
            if (!AssetDatabase.IsValidFolder(PrefabsDir)) AssetDatabase.CreateFolder("Assets/_Game", "Prefabs");
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        private static Canvas EnsureCanvas(Transform parent)
        {
            var existing = GameObject.Find("MapCanvas");
            if (existing != null && existing.TryGetComponent<Canvas>(out var canvasExisting))
            {
                return canvasExisting;
            }

            var go = new GameObject("MapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static RegionMarker EnsureRegionMarkerPrefab()
        {
            var source = BuildRegionMarkerSource();
            PrefabUtility.SaveAsPrefabAsset(source, MarkerPrefabPath);
            Object.DestroyImmediate(source);
            return AssetDatabase.LoadAssetAtPath<RegionMarker>(MarkerPrefabPath);
        }

        private static ContractRow EnsureContractRowPrefab()
        {
            var source = BuildContractRowSource();
            PrefabUtility.SaveAsPrefabAsset(source, ContractRowPrefabPath);
            Object.DestroyImmediate(source);
            return AssetDatabase.LoadAssetAtPath<ContractRow>(ContractRowPrefabPath);
        }

        private static GameObject BuildRegionMarkerSource()
        {
            var root = new GameObject("RegionMarkerButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(RegionMarker));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(210f, 48f);

            root.GetComponent<Image>().color = new Color(0.65f, 0.15f, 0.15f, 0.85f);
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var icon = CreateImage("Icon", root.transform, new Vector2(36f, 36f));
            icon.color = Color.white;
            icon.preserveAspect = true;

            var label = CreateText("Label", root.transform, "Region", 18f, TextAlignmentOptions.Left);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var marker = root.GetComponent<RegionMarker>();
            var so = new SerializedObject(marker);
            so.FindProperty("label").objectReferenceValue = label;
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static GameObject BuildContractRowSource()
        {
            var root = new GameObject("ContractRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContractRow));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 40f);
            root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            root.GetComponent<LayoutElement>().minHeight = 40f;

            var icon = CreateImage("Icon", root.transform, new Vector2(28f, 28f));
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.gameObject.AddComponent<LayoutElement>().preferredWidth = 28f;

            var title = CreateText("Title", root.transform, "Contract", 16f, TextAlignmentOptions.Left);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var timer = CreateText("Timer", root.transform, "00:00", 16f, TextAlignmentOptions.Center);
            timer.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;

            var reward = CreateText("Reward", root.transform, "0g", 16f, TextAlignmentOptions.Right);
            reward.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;

            var contractRow = root.GetComponent<ContractRow>();
            AssignContractRow(contractRow, icon, title, timer, reward);
            return root;
        }

        private static RegionDetailsPanel EnsureDetailsPanel(Transform parent, ContractRow contractPrefab)
        {
            var panel = FindOrCreateUI("RegionDetailsPanel", parent);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.sizeDelta = new Vector2(420f, 0f);
            panelRect.anchoredPosition = Vector2.zero;

            var bg = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            var vLayout = panel.GetComponent<VerticalLayoutGroup>() ?? panel.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(14, 14, 14, 14);
            vLayout.spacing = 8f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var regionName = EnsurePanelText(panel.transform, "RegionName", "Select region", 28f);
            var danger = EnsurePanelText(panel.transform, "Danger", "Danger: -", 18f);
            var faction = EnsurePanelText(panel.transform, "Faction", "Faction: -", 18f);

            var travelRow = FindOrCreateUI("TravelRow", panel.transform);
            var travelLayout = travelRow.GetComponent<HorizontalLayoutGroup>() ?? travelRow.AddComponent<HorizontalLayoutGroup>();
            travelLayout.spacing = 8f;
            travelLayout.childControlHeight = true;
            travelLayout.childControlWidth = false;
            travelLayout.childForceExpandWidth = false;
            travelLayout.childForceExpandHeight = false;
            var travelRowElement = travelRow.GetComponent<LayoutElement>() ?? travelRow.AddComponent<LayoutElement>();
            travelRowElement.minHeight = 28f;

            var travelIcon = FindOrCreateUI("TravelIcon", travelRow.transform).GetComponent<Image>();
            if (travelIcon == null)
            {
                travelIcon = travelRow.transform.Find("TravelIcon").gameObject.AddComponent<Image>();
            }
            var travelIconRect = (RectTransform)travelIcon.transform;
            travelIconRect.sizeDelta = new Vector2(22f, 22f);
            travelIcon.preserveAspect = true;
            travelIcon.color = Color.white;
            var travelIconElement = travelIcon.GetComponent<LayoutElement>() ?? travelIcon.gameObject.AddComponent<LayoutElement>();
            travelIconElement.preferredWidth = 22f;

            var travel = FindOrCreateUI("Travel", travelRow.transform).GetComponent<TextMeshProUGUI>();
            if (travel == null)
            {
                travel = travelRow.transform.Find("Travel").gameObject.AddComponent<TextMeshProUGUI>();
            }
            travel.text = "Travel: -";
            travel.fontSize = 18f;
            travel.color = Color.white;
            travel.alignment = TextAlignmentOptions.Left;
            var travelTextElement = travel.GetComponent<LayoutElement>() ?? travel.gameObject.AddComponent<LayoutElement>();
            travelTextElement.flexibleWidth = 1f;

            var threats = EnsurePanelText(panel.transform, "Threats", "Threats: -", 16f);
            threats.enableWordWrapping = true;

            var contractsHeader = EnsurePanelText(panel.transform, "ContractsHeader", "Contracts", 22f);
            contractsHeader.fontStyle = FontStyles.Bold;

            var contractsScroll = FindOrCreateUI("ContractsScrollRect", panel.transform);
            var scrollRect = contractsScroll.GetComponent<ScrollRect>() ?? contractsScroll.AddComponent<ScrollRect>();
            var scrollImage = contractsScroll.GetComponent<Image>() ?? contractsScroll.AddComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, 0.02f);
            var scrollMask = contractsScroll.GetComponent<Mask>() ?? contractsScroll.AddComponent<Mask>();
            scrollMask.showMaskGraphic = false;
            var scrollRectTransform = (RectTransform)contractsScroll.transform;
            scrollRectTransform.sizeDelta = new Vector2(0f, 420f);

            var contractsContent = FindOrCreateUI("ContractsContent", contractsScroll.transform);
            var contentRect = (RectTransform)contractsContent.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = contractsContent.GetComponent<VerticalLayoutGroup>() ?? contractsContent.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6f;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            var contentFitter = contractsContent.GetComponent<ContentSizeFitter>() ?? contractsContent.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = scrollRectTransform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var details = panel.GetComponent<RegionDetailsPanel>() ?? panel.AddComponent<RegionDetailsPanel>();
            AssignDetailsPanel(details, regionName, danger, faction, travel, threats, travelIcon, contentRect, contractPrefab);
            return details;
        }

        private static RectTransform EnsureMapArea(Transform parent, out RectTransform markersRoot)
        {
            var mapScroll = FindOrCreateUI("MapScrollRect", parent);
            var scrollRect = mapScroll.GetComponent<ScrollRect>() ?? mapScroll.AddComponent<ScrollRect>();

            var mapScrollRect = (RectTransform)mapScroll.transform;
            mapScrollRect.anchorMin = new Vector2(0f, 0f);
            mapScrollRect.anchorMax = new Vector2(1f, 1f);
            mapScrollRect.offsetMin = new Vector2(0f, 0f);
            mapScrollRect.offsetMax = new Vector2(-440f, 0f);

            var viewport = FindOrCreateUI("Viewport", mapScroll.transform);
            var viewportRect = (RectTransform)viewport.transform;
            Stretch(viewportRect);
            var viewportImage = viewport.GetComponent<Image>() ?? viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.10f, 0.10f, 0.12f, 1f);
            var mask = viewport.GetComponent<Mask>() ?? viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var content = FindOrCreateUI("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            Stretch(contentRect);

            var mapImageGo = FindOrCreateUI("MapImage", content.transform);
            var mapImageRect = (RectTransform)mapImageGo.transform;
            Stretch(mapImageRect, 12f);
            var mapImage = mapImageGo.GetComponent<Image>() ?? mapImageGo.AddComponent<Image>();
            mapImage.color = new Color(0.14f, 0.14f, 0.16f, 1f);
            mapImage.raycastTarget = true;

            var markers = FindOrCreateUI("MarkersRoot", mapImageGo.transform);
            markersRoot = (RectTransform)markers.transform;
            Stretch(markersRoot);

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = false;

            return mapImageRect;
        }

        private static TMP_Text EnsurePanelText(Transform parent, string name, string value, float size)
        {
            var go = FindOrCreateUI(name, parent);
            var text = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.text = value;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Left;
            var layoutElement = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            layoutElement.minHeight = size + 8f;
            return text;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            return go.GetComponent<Image>();
        }

        private static GameObject FindOrCreate(string name)
        {
            var found = GameObject.Find(name);
            if (found != null) return found;
            return new GameObject(name);
        }

        private static GameObject FindOrCreateUI(string name, Transform parent)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void AssignMapController(MapController controller, RectTransform mapRect, RectTransform markersRoot, RegionMarker markerPrefab, RegionDetailsPanel detailsPanel, GameClock clock)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("mapRect").objectReferenceValue = mapRect;
            so.FindProperty("markersRoot").objectReferenceValue = markersRoot;
            so.FindProperty("regionMarkerPrefab").objectReferenceValue = markerPrefab;
            so.FindProperty("detailsPanel").objectReferenceValue = detailsPanel;
            so.FindProperty("gameClock").objectReferenceValue = clock;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDetailsPanel(RegionDetailsPanel detailsPanel, TMP_Text regionName, TMP_Text danger, TMP_Text faction, TMP_Text travel, TMP_Text threats, Image travelIcon, RectTransform contractsRoot, ContractRow rowPrefab)
        {
            var so = new SerializedObject(detailsPanel);
            so.FindProperty("regionNameText").objectReferenceValue = regionName;
            so.FindProperty("dangerText").objectReferenceValue = danger;
            so.FindProperty("factionText").objectReferenceValue = faction;
            so.FindProperty("travelDaysText").objectReferenceValue = travel;
            so.FindProperty("threatsText").objectReferenceValue = threats;
            so.FindProperty("travelIconImage").objectReferenceValue = travelIcon;
            so.FindProperty("contractsRoot").objectReferenceValue = contractsRoot;
            so.FindProperty("contractRowPrefab").objectReferenceValue = rowPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignContractRow(ContractRow contractRow, Image icon, TMP_Text title, TMP_Text timer, TMP_Text reward)
        {
            var so = new SerializedObject(contractRow);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("rewardText").objectReferenceValue = reward;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
