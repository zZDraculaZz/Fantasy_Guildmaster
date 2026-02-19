#if UNITY_EDITOR
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Encounter;
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
        private const string ContractIconPrefabPath = PrefabsDir + "/ContractIcon.prefab";
        private const string TravelTokenPrefabPath = PrefabsDir + "/TravelToken.prefab";
        private const string SquadStatusRowPrefabPath = PrefabsDir + "/SquadStatusRow.prefab";

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
            var contractIconPrefab = EnsureContractIconPrefab();
            var travelTokenPrefab = EnsureTravelTokenPrefab();
            var squadStatusRowPrefab = EnsureSquadStatusRowPrefab();

            var root = FindOrCreate("MapSceneRoot");
            var clock = root.GetComponent<GameClock>() ?? root.AddComponent<GameClock>();
            var gameManager = root.GetComponent<GameManager>() ?? root.AddComponent<GameManager>();
            var gameState = root.GetComponent<GameState>() ?? root.AddComponent<GameState>();
            var squadRoster = root.GetComponent<SquadRoster>() ?? root.AddComponent<SquadRoster>();
            var controller = root.GetComponent<MapController>() ?? root.AddComponent<MapController>();

            var canvas = EnsureCanvas(root.transform);
            var mapLayer = EnsureLayer(canvas.transform, "MapLayer");
            var overlayLayer = EnsureLayer(canvas.transform, "OverlayLayer");

            var detailsPanel = EnsureDetailsPanel(overlayLayer, contractPrefab);
            var squadSelectPanel = EnsureSquadSelectPanel(overlayLayer);
            var squadStatusHud = EnsureSquadStatusHud(overlayLayer, squadStatusRowPrefab, gameState);
            var encounterPanel = EnsureEncounterPanel(overlayLayer);
            var encounterManager = root.GetComponent<EncounterManager>() ?? root.AddComponent<EncounterManager>();
            var mapRect = EnsureMapArea(mapLayer, out var markersRoot, out var contractIconsRoot, out var travelTokensRoot);
            EnsureGuildHqMarker(markersRoot);

            AssignEncounterManager(encounterManager, encounterPanel);
            AssignMapController(controller, mapRect, markersRoot, contractIconsRoot, travelTokensRoot, markerPrefab, contractIconPrefab, travelTokenPrefab, detailsPanel, squadSelectPanel, squadStatusHud, encounterManager, gameManager, gameState, squadRoster, clock);

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
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        private static Transform EnsureLayer(Transform parent, string layerName)
        {
            var layerGo = FindOrCreateUI(layerName, parent);
            var rect = layerGo.GetComponent<RectTransform>() ?? layerGo.AddComponent<RectTransform>();
            Stretch(rect);
            rect.SetAsLastSibling();
            return rect;
        }

        private static Canvas EnsureCanvas(Transform parent)
        {
            var existing = GameObject.Find("MapCanvas");
            if (existing != null && existing.TryGetComponent<Canvas>(out var canvasExisting))
            {
                if (canvasExisting.GetComponent<GraphicRaycaster>() == null)
                {
                    canvasExisting.gameObject.AddComponent<GraphicRaycaster>();
                }

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

        private static ContractIcon EnsureContractIconPrefab()
        {
            var source = BuildContractIconSource();
            PrefabUtility.SaveAsPrefabAsset(source, ContractIconPrefabPath);
            Object.DestroyImmediate(source);
            return AssetDatabase.LoadAssetAtPath<ContractIcon>(ContractIconPrefabPath);
        }

        private static TravelToken EnsureTravelTokenPrefab()
        {
            var source = BuildTravelTokenSource();
            PrefabUtility.SaveAsPrefabAsset(source, TravelTokenPrefabPath);
            Object.DestroyImmediate(source);
            return AssetDatabase.LoadAssetAtPath<TravelToken>(TravelTokenPrefabPath);
        }

        private static SquadStatusRow EnsureSquadStatusRowPrefab()
        {
            var source = BuildSquadStatusRowSource();
            PrefabUtility.SaveAsPrefabAsset(source, SquadStatusRowPrefabPath);
            Object.DestroyImmediate(source);
            return AssetDatabase.LoadAssetAtPath<SquadStatusRow>(SquadStatusRowPrefabPath);
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
            var root = new GameObject("ContractRow", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(ContractRow));
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

        private static GameObject BuildContractIconSource()
        {
            var root = new GameObject("ContractIcon", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContractIcon));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 58f);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0f);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var icon = CreateImage("Icon", root.transform, new Vector2(28f, 28f));
            icon.preserveAspect = true;
            icon.color = Color.white;

            var timer = CreateText("Timer", root.transform, "00:00", 12f, TextAlignmentOptions.Center);
            timer.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            var contractIcon = root.GetComponent<ContractIcon>();
            AssignContractIcon(contractIcon, icon, timer);
            return root;
        }

        private static GameObject BuildTravelTokenSource()
        {
            var root = new GameObject("TravelToken", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(TravelToken));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(74f, 62f);

            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var icon = CreateImage("Icon", root.transform, new Vector2(24f, 24f));
            icon.color = Color.white;
            icon.preserveAspect = true;

            var timer = CreateText("Timer", root.transform, "00:00", 12f, TextAlignmentOptions.Center);
            var squad = CreateText("Squad", root.transform, "Squad", 11f, TextAlignmentOptions.Center);

            var token = root.GetComponent<TravelToken>();
            AssignTravelToken(token, icon, timer, squad);
            return root;
        }


        private static GameObject BuildSquadStatusRowSource()
        {
            var root = new GameObject("SquadStatusRow", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(SquadStatusRow));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
            root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var rowLayoutElement = root.GetComponent<LayoutElement>();
            rowLayoutElement.minHeight = 32f;
            rowLayoutElement.preferredHeight = 32f;

            var squadName = CreateText("NameText", root.transform, "Iron Hawks", 14f, TextAlignmentOptions.Left);
            squadName.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;

            var statusTimer = CreateText("StatusText", root.transform, "OUT 00:12", 14f, TextAlignmentOptions.Left);
            statusTimer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var hp = CreateText("HpText", root.transform, "100/100", 14f, TextAlignmentOptions.Right);
            hp.gameObject.AddComponent<LayoutElement>().preferredWidth = 72f;

            var row = root.GetComponent<SquadStatusRow>();
            AssignSquadStatusRow(row, squadName, statusTimer, hp);
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
            threats.textWrappingMode = TextWrappingModes.Normal;

            var contractsHeader = EnsurePanelText(panel.transform, "ContractsHeader", "Contracts", 22f);
            contractsHeader.fontStyle = FontStyles.Bold;

            var assignButtonGo = FindOrCreateUI("AssignSquadButton", panel.transform);
            var assignButtonImage = assignButtonGo.GetComponent<Image>() ?? assignButtonGo.AddComponent<Image>();
            assignButtonImage.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
            var assignButton = assignButtonGo.GetComponent<Button>() ?? assignButtonGo.AddComponent<Button>();
            var assignButtonText = assignButtonGo.transform.Find("Label") != null
                ? assignButtonGo.transform.Find("Label").GetComponent<TextMeshProUGUI>()
                : CreateText("Label", assignButtonGo.transform, "Assign Squad", 16f, TextAlignmentOptions.Center);
            Stretch((RectTransform)assignButtonText.transform, 4f);
            var assignLayoutElement = assignButtonGo.GetComponent<LayoutElement>() ?? assignButtonGo.AddComponent<LayoutElement>();
            assignLayoutElement.minHeight = 36f;

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
            AssignDetailsPanel(details, regionName, danger, faction, travel, threats, travelIcon, contentRect, contractPrefab, assignButton);
            return details;
        }

        private static SquadSelectPanel EnsureSquadSelectPanel(Transform parent)
        {
            var panelGo = FindOrCreateUI("SquadSelectPanel", parent);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(360f, 420f);

            var image = panelGo.GetComponent<Image>() ?? panelGo.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.82f);

            var layout = panelGo.GetComponent<VerticalLayoutGroup>() ?? panelGo.AddComponent<VerticalLayoutGroup>();
            var panelFitter = panelGo.GetComponent<ContentSizeFitter>();
            if (panelFitter != null)
            {
                Object.DestroyImmediate(panelFitter);
            }
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = EnsurePanelText(panelGo.transform, "Title", "Assign Squad", 24f);

            var list = FindOrCreateUI("List", panelGo.transform);
            var listRect = (RectTransform)list.transform;
            listRect.sizeDelta = new Vector2(0f, 280f);
            var listLayout = list.GetComponent<VerticalLayoutGroup>() ?? list.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 6f;
            listLayout.childControlHeight = true;
            listLayout.childControlWidth = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            var buttonPrefab = FindOrCreateUI("SquadButtonPrefab", panelGo.transform);
            buttonPrefab.SetActive(false);
            var buttonImage = buttonPrefab.GetComponent<Image>() ?? buttonPrefab.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            var button = buttonPrefab.GetComponent<Button>() ?? buttonPrefab.AddComponent<Button>();
            var label = buttonPrefab.transform.Find("Label") != null
                ? buttonPrefab.transform.Find("Label").GetComponent<TextMeshProUGUI>()
                : CreateText("Label", buttonPrefab.transform, "Squad", 16f, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 4f);
            var buttonLayout = buttonPrefab.GetComponent<LayoutElement>() ?? buttonPrefab.AddComponent<LayoutElement>();
            buttonLayout.minHeight = 34f;

            var closeGo = FindOrCreateUI("CloseButton", panelGo.transform);
            var closeImage = closeGo.GetComponent<Image>() ?? closeGo.AddComponent<Image>();
            closeImage.color = new Color(0.35f, 0.1f, 0.1f, 0.9f);
            var closeButton = closeGo.GetComponent<Button>() ?? closeGo.AddComponent<Button>();
            var closeLabel = closeGo.transform.Find("Label") != null
                ? closeGo.transform.Find("Label").GetComponent<TextMeshProUGUI>()
                : CreateText("Label", closeGo.transform, "Close", 16f, TextAlignmentOptions.Center);
            Stretch((RectTransform)closeLabel.transform, 4f);
            var closeLayout = closeGo.GetComponent<LayoutElement>() ?? closeGo.AddComponent<LayoutElement>();
            closeLayout.minHeight = 34f;

            var panel = panelGo.GetComponent<SquadSelectPanel>() ?? panelGo.AddComponent<SquadSelectPanel>();
            AssignSquadSelectPanel(panel, title, listRect, button, closeButton);
            panelGo.SetActive(false);
            return panel;
        }

        private static SquadStatusHUD EnsureSquadStatusHud(Transform parent, SquadStatusRow rowPrefab, GameState gameState)
        {
            var panelGo = FindOrCreateUI("SquadStatusHUD", parent);
            var panelRect = (RectTransform)panelGo.transform;
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(16f, -16f);
            panelRect.sizeDelta = new Vector2(300f, 260f);

            var image = panelGo.GetComponent<Image>() ?? panelGo.AddComponent<Image>();
            image.color = new Color(0.05f, 0.09f, 0.16f, 0.84f);

            var layout = panelGo.GetComponent<VerticalLayoutGroup>() ?? panelGo.AddComponent<VerticalLayoutGroup>();
            var panelFitter = panelGo.GetComponent<ContentSizeFitter>();
            if (panelFitter != null)
            {
                Object.DestroyImmediate(panelFitter);
            }
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = panelGo.transform.Find("Title") != null
                ? panelGo.transform.Find("Title").GetComponent<TextMeshProUGUI>()
                : CreateText("Title", panelGo.transform, "Squads", 20f, TextAlignmentOptions.Left);
            var titleLayout = title.GetComponent<LayoutElement>() ?? title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = 28f;
            titleLayout.preferredHeight = 28f;
            titleLayout.flexibleHeight = 0f;

            var goldText = panelGo.transform.Find("GoldText") != null
                ? panelGo.transform.Find("GoldText").GetComponent<TextMeshProUGUI>()
                : CreateText("GoldText", panelGo.transform, "Gold: 100", 16f, TextAlignmentOptions.Left);
            var goldLayout = goldText.GetComponent<LayoutElement>() ?? goldText.gameObject.AddComponent<LayoutElement>();
            goldLayout.minHeight = 22f;
            goldLayout.preferredHeight = 22f;
            goldLayout.flexibleHeight = 0f;

            var scrollGo = FindOrCreateUI("RowsScrollRect", panelGo.transform);
            var scrollRect = scrollGo.GetComponent<ScrollRect>() ?? scrollGo.AddComponent<ScrollRect>();
            var scrollFitter = scrollGo.GetComponent<ContentSizeFitter>();
            if (scrollFitter != null)
            {
                Object.DestroyImmediate(scrollFitter);
            }
            var scrollImage = scrollGo.GetComponent<Image>() ?? scrollGo.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);
            var scrollRectTransform = (RectTransform)scrollGo.transform;
            scrollRectTransform.anchorMin = new Vector2(0f, 1f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.pivot = new Vector2(0.5f, 1f);
            scrollRectTransform.anchoredPosition = Vector2.zero;
            scrollRectTransform.sizeDelta = Vector2.zero;
            var viewportLayout = scrollGo.GetComponent<LayoutElement>() ?? scrollGo.AddComponent<LayoutElement>();
            viewportLayout.preferredHeight = 120f;
            viewportLayout.minHeight = 120f;
            viewportLayout.flexibleHeight = 1f;

            var viewport = FindOrCreateUI("Viewport", scrollGo.transform);
            var viewportRect = (RectTransform)viewport.transform;
            Stretch(viewportRect);
            var viewportImage = viewport.GetComponent<Image>() ?? viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            var viewportMask = viewport.GetComponent<Mask>() ?? viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            _ = viewport.GetComponent<RectMask2D>() ?? viewport.AddComponent<RectMask2D>();

            var content = FindOrCreateUI("Content", viewport.transform);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var contentLayout = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(8, 8, 8, 8);
            contentLayout.spacing = 6f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var hud = panelGo.GetComponent<SquadStatusHUD>() ?? panelGo.AddComponent<SquadStatusHUD>();
            AssignSquadStatusHud(hud, title, goldText, panelRect, scrollRect, viewportRect, contentRect, viewportLayout, rowPrefab, gameState);
            return hud;
        }

        private static EncounterPanel EnsureEncounterPanel(Transform parent)
        {
            var panelGo = FindOrCreateUI("EncounterPanel", parent);
            panelGo.transform.SetAsLastSibling();
            var panelRect = (RectTransform)panelGo.transform;
            Stretch(panelRect);

            var canvasGroup = panelGo.GetComponent<CanvasGroup>() ?? panelGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            var bg = panelGo.GetComponent<Image>() ?? panelGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.86f);

            var layout = panelGo.GetComponent<VerticalLayoutGroup>() ?? panelGo.AddComponent<VerticalLayoutGroup>();
            var panelFitter = panelGo.GetComponent<ContentSizeFitter>();
            if (panelFitter != null)
            {
                Object.DestroyImmediate(panelFitter);
            }
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = EnsurePanelText(panelGo.transform, "Title", "Encounter", 28f);
            var description = EnsurePanelText(panelGo.transform, "Description", "Description", 18f);
            description.textWrappingMode = TextWrappingModes.Normal;
            var descriptionLayout = description.GetComponent<LayoutElement>() ?? description.gameObject.AddComponent<LayoutElement>();
            descriptionLayout.minHeight = 120f;

            var options = FindOrCreateUI("Options", panelGo.transform);
            var optionsRect = (RectTransform)options.transform;
            var optionsLayout = options.GetComponent<VerticalLayoutGroup>() ?? options.AddComponent<VerticalLayoutGroup>();
            optionsLayout.spacing = 6f;
            optionsLayout.childControlWidth = true;
            optionsLayout.childControlHeight = true;
            optionsLayout.childForceExpandWidth = true;
            optionsLayout.childForceExpandHeight = false;
            var optionsElement = options.GetComponent<LayoutElement>() ?? options.AddComponent<LayoutElement>();
            optionsElement.minHeight = 180f;

            var optionButtonPrefabGo = FindOrCreateUI("OptionButtonPrefab", panelGo.transform);
            optionButtonPrefabGo.SetActive(false);
            var optionImage = optionButtonPrefabGo.GetComponent<Image>() ?? optionButtonPrefabGo.AddComponent<Image>();
            optionImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            var optionButton = optionButtonPrefabGo.GetComponent<Button>() ?? optionButtonPrefabGo.AddComponent<Button>();
            var optionLabel = optionButtonPrefabGo.transform.Find("Label") != null
                ? optionButtonPrefabGo.transform.Find("Label").GetComponent<TextMeshProUGUI>()
                : CreateText("Label", optionButtonPrefabGo.transform, "Option", 16f, TextAlignmentOptions.Center);
            Stretch((RectTransform)optionLabel.transform, 4f);
            var optionLayoutElement = optionButtonPrefabGo.GetComponent<LayoutElement>() ?? optionButtonPrefabGo.AddComponent<LayoutElement>();
            optionLayoutElement.minHeight = 36f;

            var continueGo = FindOrCreateUI("ContinueButton", panelGo.transform);
            var continueImage = continueGo.GetComponent<Image>() ?? continueGo.AddComponent<Image>();
            continueImage.color = new Color(0.15f, 0.35f, 0.18f, 0.9f);
            var continueButton = continueGo.GetComponent<Button>() ?? continueGo.AddComponent<Button>();
            var continueLabel = continueGo.transform.Find("Label") != null
                ? continueGo.transform.Find("Label").GetComponent<TextMeshProUGUI>()
                : CreateText("Label", continueGo.transform, "Continue", 16f, TextAlignmentOptions.Center);
            Stretch((RectTransform)continueLabel.transform, 4f);
            var continueLayout = continueGo.GetComponent<LayoutElement>() ?? continueGo.AddComponent<LayoutElement>();
            continueLayout.minHeight = 36f;

            var panel = panelGo.GetComponent<EncounterPanel>() ?? panelGo.AddComponent<EncounterPanel>();
            AssignEncounterPanel(panel, title, description, optionsRect, optionButton, continueButton);
            panelGo.SetActive(false);
            return panel;
        }

        private static void EnsureGuildHqMarker(RectTransform markersRoot)
        {
            var markerGo = FindOrCreateUI("GuildHQMarker", markersRoot);
            markerGo.transform.SetAsFirstSibling();
            var markerRect = (RectTransform)markerGo.transform;
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = new Vector2(120f, 40f);

            var image = markerGo.GetComponent<Image>() ?? markerGo.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f, 0.75f);
            image.raycastTarget = false;
            var sprite = Resources.Load<Sprite>("Icons/UI/guild_hq");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }

            var label = markerGo.transform.Find("Label") != null
                ? markerGo.transform.Find("Label").GetComponent<TextMeshProUGUI>()
                : CreateText("Label", markerGo.transform, "Guild HQ", 14f, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 2f);
            label.color = Color.white;

            var canvasGroup = markerGo.GetComponent<CanvasGroup>() ?? markerGo.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private static RectTransform EnsureMapArea(Transform parent, out RectTransform markersRoot, out RectTransform contractIconsRoot, out RectTransform travelTokensRoot)
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
            _ = viewport.GetComponent<RectMask2D>() ?? viewport.AddComponent<RectMask2D>();

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

            var contractIcons = FindOrCreateUI("ContractIconsRoot", mapImageGo.transform);
            contractIconsRoot = (RectTransform)contractIcons.transform;
            Stretch(contractIconsRoot);

            var travelTokens = FindOrCreateUI("TravelTokensRoot", mapImageGo.transform);
            travelTokensRoot = (RectTransform)travelTokens.transform;
            Stretch(travelTokensRoot);

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

        private static void AssignMapController(MapController controller, RectTransform mapRect, RectTransform markersRoot, RectTransform contractIconsRoot, RectTransform travelTokensRoot, RegionMarker markerPrefab, ContractIcon contractIconPrefab, TravelToken travelTokenPrefab, RegionDetailsPanel detailsPanel, SquadSelectPanel squadSelectPanel, SquadStatusHUD squadStatusHud, EncounterManager encounterManager, GameManager gameManager, GameState gameState, SquadRoster squadRoster, GameClock clock)
        {
            var so = new SerializedObject(controller);
            so.FindProperty("mapRect").objectReferenceValue = mapRect;
            so.FindProperty("markersRoot").objectReferenceValue = markersRoot;
            so.FindProperty("regionMarkerPrefab").objectReferenceValue = markerPrefab;
            so.FindProperty("contractIconsRoot").objectReferenceValue = contractIconsRoot;
            so.FindProperty("travelTokensRoot").objectReferenceValue = travelTokensRoot;
            so.FindProperty("contractIconPrefab").objectReferenceValue = contractIconPrefab;
            so.FindProperty("travelTokenPrefab").objectReferenceValue = travelTokenPrefab;
            so.FindProperty("detailsPanel").objectReferenceValue = detailsPanel;
            so.FindProperty("squadSelectPanel").objectReferenceValue = squadSelectPanel;
            so.FindProperty("squadStatusHud").objectReferenceValue = squadStatusHud;
            so.FindProperty("encounterManager").objectReferenceValue = encounterManager;
            so.FindProperty("gameManager").objectReferenceValue = gameManager;
            so.FindProperty("gameState").objectReferenceValue = gameState;
            so.FindProperty("squadRoster").objectReferenceValue = squadRoster;
            so.FindProperty("gameClock").objectReferenceValue = clock;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignDetailsPanel(RegionDetailsPanel detailsPanel, TMP_Text regionName, TMP_Text danger, TMP_Text faction, TMP_Text travel, TMP_Text threats, Image travelIcon, RectTransform contractsRoot, ContractRow rowPrefab, Button assignButton)
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
            so.FindProperty("assignSquadButton").objectReferenceValue = assignButton;
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

        private static void AssignContractIcon(ContractIcon contractIcon, Image icon, TMP_Text timer)
        {
            var so = new SerializedObject(contractIcon);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignTravelToken(TravelToken token, Image icon, TMP_Text timer, TMP_Text squad)
        {
            var so = new SerializedObject(token);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("timerText").objectReferenceValue = timer;
            so.FindProperty("squadNameText").objectReferenceValue = squad;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSquadStatusRow(SquadStatusRow row, TMP_Text squadName, TMP_Text statusTimer, TMP_Text hp)
        {
            var so = new SerializedObject(row);
            so.FindProperty("squadNameText").objectReferenceValue = squadName;
            so.FindProperty("statusTimerText").objectReferenceValue = statusTimer;
            so.FindProperty("hpText").objectReferenceValue = hp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSquadStatusHud(SquadStatusHUD hud, TMP_Text title, TMP_Text goldText, RectTransform rootRect, ScrollRect scrollRect, RectTransform viewportRect, RectTransform rowsRoot, LayoutElement viewportLayout, SquadStatusRow rowPrefab, GameState gameState)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("goldText").objectReferenceValue = goldText;
            so.FindProperty("rootRect").objectReferenceValue = rootRect;
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("viewportRect").objectReferenceValue = viewportRect;
            so.FindProperty("rowsRoot").objectReferenceValue = rowsRoot;
            so.FindProperty("viewportLayoutElement").objectReferenceValue = viewportLayout;
            so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            so.FindProperty("gameState").objectReferenceValue = gameState;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignSquadSelectPanel(SquadSelectPanel panel, TMP_Text title, RectTransform listRoot, Button squadButtonPrefab, Button closeButton)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("listRoot").objectReferenceValue = listRoot;
            so.FindProperty("squadButtonPrefab").objectReferenceValue = squadButtonPrefab;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignEncounterPanel(EncounterPanel panel, TMP_Text title, TMP_Text description, RectTransform optionsRoot, Button optionButtonPrefab, Button continueButton)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("optionsRoot").objectReferenceValue = optionsRoot;
            so.FindProperty("optionButtonPrefab").objectReferenceValue = optionButtonPrefab;
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignEncounterManager(EncounterManager manager, EncounterPanel panel)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("encounterPanel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
#endif
