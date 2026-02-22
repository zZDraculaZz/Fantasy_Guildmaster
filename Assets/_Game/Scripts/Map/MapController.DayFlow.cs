using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyGuildmaster.Map
{
    public sealed partial class MapController
    {
        private MissionReportData BuildMissionReport(TravelTask task, SquadData squad, HunterData soloHunter = null)
        {
            var readiness = soloHunter != null ? Mathf.RoundToInt(Mathf.Clamp01(soloHunter.maxHp > 0 ? soloHunter.hp / (float)soloHunter.maxHp : 1f) * 100f) : ComputeReadinessPercent(squad);
            var membersSummary = soloHunter != null ? $"Solo: {soloHunter.name}" : BuildMembersSummary(squad);
            var regionName = ResolveRegionName(task.fromRegionId);
            var contractTitle = ResolveContractTitle(task.fromRegionId, task.contractId);
            var outcome = readiness < 70
                ? "Contract completed. Loot secured. Injuries reported."
                : "Contract completed. Loot secured. Minor injuries.";

            return new MissionReportData
            {
                squadId = squad?.id,
                soloHunterId = soloHunter?.id,
                squadName = soloHunter != null ? soloHunter.name : squad?.name,
                regionId = task.fromRegionId,
                regionName = regionName,
                contractId = task.contractId,
                contractTitle = contractTitle,
                rewardGold = task.contractReward,
                readinessBeforePercent = readiness,
                readinessAfterPercent = readiness,
                membersSummary = membersSummary,
                outcomeText = outcome
            };
        }

        private void TryShowNextMissionReport()
        {
            EnsureMissionReportPanel();
            EnsureGuildHallPanel();
            EnsureEndDayButton();
            EnsureEndDayConfirmPanel();
            if (missionReportPanel == null || _reportOpen || missionReportPanel.IsOpen || _pendingReports.Count == 0)
            {
                return;
            }

            var report = _pendingReports.Peek();
            Debug.Log($"[Report] Showing: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");
            _reportOpen = true;
            var shown = missionReportPanel.Show(report, () => OnMissionReportContinue(report));
            if (!shown)
            {
                _reportOpen = false;
                Debug.LogError("[Report] MissionReport failed to show; applying fallback continuation. [TODO REMOVE]");
                OnMissionReportContinue(report);
            }
        }

        private void OnMissionReportContinue(MissionReportData report)
        {
            Debug.Log($"[Report] onContinue invoked: squad={report?.squadId} contract={report?.contractId} reward={report?.rewardGold} [TODO REMOVE]");

            if (_pendingReports.Count > 0)
            {
                _pendingReports.Dequeue();
            }

            AddGold(report.rewardGold);
            CompleteContract(report.regionId, report.contractId);

            var reportSquad = FindSquad(report.squadId);
            if (reportSquad != null)
            {
                reportSquad.exhausted = true;
                reportSquad.exhaustedReason = "Needs rest";
                reportSquad.contractsDoneToday++;
                Debug.Log($"[Squad] Exhausted after contract: squad={reportSquad.id} contract={report.contractId} [TODO REMOVE]");

                var cohesionDelta = reportSquad.lastRosterChangeDay == _dayIndex ? 1 : 3;
                reportSquad.cohesion = Mathf.Clamp(reportSquad.cohesion + cohesionDelta, 0, 100);
                Debug.Log($"[Cohesion] After mission squad={reportSquad.id} cohesion={reportSquad.cohesion} delta={cohesionDelta} [TODO REMOVE]");
            }

            if (!string.IsNullOrEmpty(report.soloHunterId) && hunterRoster != null)
            {
                var hunter = hunterRoster.GetById(report.soloHunterId);
                if (hunter != null)
                {
                    hunter.exhaustedToday = true;
                }
            }

            missionReportPanel?.Hide();
            _reportOpen = false;
            Debug.Log($"[Report] Applied+Closed: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");

            RefreshSquadStatusHud();
            UpdateEndDayUiState();
            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            TryAdvanceDayFlow("MissionReportClosed");
            if (_pendingReports.Count > 0)
            {
                TryShowNextMissionReport();
            }
        }

        private void OnEndDayButtonClicked()
        {
            if (IsEndDayBlocked(out var reason))
            {
                Debug.Log($"[EndDay] blocked reason={reason} [TODO REMOVE]");
                UpdateEndDayUiState();
                return;
            }

            if (IsEndDayWarning(out var warning))
            {
                Debug.Log($"[EndDay] warning shown {warning} [TODO REMOVE]");
                ShowEndDayConfirm(warning);
                return;
            }

            _endDayRequested = true;
            Debug.Log("[EndDay] confirmed -> request end day [TODO REMOVE]");
            TryAdvanceDayFlow("EndDay");
            UpdateEndDayUiState();
        }

        private void EnsureEndDayButton()
        {
            EnsureEndDayHeaderLayout();
            if (endDayButton == null)
            {
                endDayButton = transform.Find("MapCanvas/OverlayLayer/RegionDetailsPanel/HeaderContainer/EndDayButton")?.GetComponent<Button>();
            }

            if (endDayButton == null)
            {
                endDayButton = transform.Find("MapCanvas/OverlayLayer/EndDayButton")?.GetComponent<Button>();
            }

            if (endDayButton == null)
            {
                var canvas = EnsureCanvas();
                if (canvas == null)
                {
                    return;
                }

                var button = CreateButton(canvas.transform, "EndDayButton", "End Day");
                endDayButton = button;
            }

            EnsureEndDayHeaderLayout();

            if (endDayButton != null)
            {
                endDayButton.onClick.RemoveListener(OnEndDayButtonClicked);
                endDayButton.onClick.AddListener(OnEndDayButtonClicked);
            }

            UpdateEndDayUiState();
        }

        private void EnsureEndDayHeaderLayout()
        {
            if (detailsPanel == null)
            {
                return;
            }

            var panel = detailsPanel.transform as RectTransform;
            if (panel == null)
            {
                return;
            }

            var header = panel.Find("HeaderContainer") as RectTransform;
            if (header == null)
            {
                var headerGo = new GameObject("HeaderContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                headerGo.transform.SetParent(panel, false);
                header = headerGo.GetComponent<RectTransform>();
                header.SetSiblingIndex(0);

                var headerLayout = headerGo.GetComponent<HorizontalLayoutGroup>();
                headerLayout.childControlWidth = true;
                headerLayout.childControlHeight = true;
                headerLayout.childForceExpandWidth = false;
                headerLayout.childForceExpandHeight = false;
                headerLayout.spacing = 10f;
                headerLayout.padding = new RectOffset(0, 0, 0, 0);

                var headerElement = headerGo.GetComponent<LayoutElement>();
                headerElement.minHeight = 40f;
                headerElement.preferredHeight = 44f;
            }

            var regionName = panel.Find("RegionName")?.GetComponent<TMP_Text>();
            if (regionName != null && regionName.transform.parent != header)
            {
                regionName.transform.SetParent(header, false);
            }

            if (regionName != null)
            {
                regionName.textWrappingMode = TextWrappingModes.NoWrap;
                regionName.overflowMode = TextOverflowModes.Ellipsis;
                var nameLayout = regionName.GetComponent<LayoutElement>() ?? regionName.gameObject.AddComponent<LayoutElement>();
                nameLayout.flexibleWidth = 1f;
                nameLayout.minWidth = 120f;
            }

            if (endDayButton != null && endDayButton.transform.parent != header)
            {
                endDayButton.transform.SetParent(header, false);
            }

            if (endDayButton != null)
            {
                var buttonLayout = endDayButton.GetComponent<LayoutElement>() ?? endDayButton.gameObject.AddComponent<LayoutElement>();
                buttonLayout.preferredWidth = 160f;
                buttonLayout.minWidth = 140f;
                buttonLayout.flexibleWidth = 0f;
                var buttonRect = endDayButton.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.sizeDelta = new Vector2(160f, 36f);
                }
            }

            if (endDayHintText == null)
            {
                var hint = panel.Find("EndDayHint")?.GetComponent<TMP_Text>();
                if (hint == null)
                {
                    var hintGo = new GameObject("EndDayHint", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                    hintGo.transform.SetParent(panel, false);
                    hintGo.transform.SetSiblingIndex(Mathf.Min(1, panel.childCount - 1));
                    hint = hintGo.GetComponent<TextMeshProUGUI>();
                    hint.fontSize = 16f;
                    hint.alignment = TextAlignmentOptions.Left;
                    hint.color = new Color(1f, 0.85f, 0.55f, 1f);
                    hint.textWrappingMode = TextWrappingModes.Normal;
                    var hintLayout = hintGo.GetComponent<LayoutElement>();
                    hintLayout.minHeight = 20f;
                    hintLayout.preferredHeight = 24f;
                }

                endDayHintText = hint;
            }
        }

        private bool IsEndDayBlocked(out string reason)
        {
            if (_travelTasks.Count > 0)
            {
                reason = "Squads are still on missions.";
                return true;
            }

            var encounterQueued = encounterManager != null && encounterManager.PendingEncounterCount > 0;
            var encounterActive = encounterManager != null && encounterManager.IsEncounterActive;
            if (encounterQueued || encounterActive)
            {
                reason = "Resolve encounters/reports first.";
                return true;
            }

            var reportActive = missionReportPanel != null && missionReportPanel.IsOpen;
            if (_pendingReports.Count > 0 || reportActive)
            {
                reason = "Resolve encounters/reports first.";
                return true;
            }

            if (IsAnyContextModalActive())
            {
                reason = "Close active windows first.";
                return true;
            }

            reason = null;
            return false;
        }

        private bool IsEndDayWarning(out string warning)
        {
            warning = null;
            if (IsEndDayBlocked(out _))
            {
                return false;
            }

            var availableContracts = 0;
            foreach (var pair in _contractsByRegion)
            {
                if (pair.Key == GuildHqId || pair.Value == null)
                {
                    continue;
                }

                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var contract = pair.Value[i];
                    if (contract == null || contract.IsExpired || IsContractAssigned(contract.id))
                    {
                        continue;
                    }

                    availableContracts++;
                }
            }

            var readyIdleSquads = 0;
            var squads = GetRosterSquads();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.IsDestroyed)
                {
                    continue;
                }

                if (squad.state == SquadState.IdleAtHQ && !squad.exhausted)
                {
                    readyIdleSquads++;
                }
            }

            if (availableContracts > 0 && readyIdleSquads > 0)
            {
                warning = $"You still have {availableContracts} available contracts and {readyIdleSquads} ready squads. End the day anyway?";
                return true;
            }

            return false;
        }

        private bool IsContractAssigned(string contractId)
        {
            if (string.IsNullOrEmpty(contractId))
            {
                return false;
            }

            for (var i = 0; i < _travelTasks.Count; i++)
            {
                if (_travelTasks[i] != null && _travelTasks[i].contractId == contractId)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateEndDayUiState()
        {
            if (endDayButton == null)
            {
                return;
            }

            if (IsEndDayBlocked(out var reason))
            {
                endDayButton.interactable = false;
                var label = endDayButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = "End Day";
                }

                if (endDayHintText != null)
                {
                    endDayHintText.text = $"Cannot end day: {reason}";
                }

                return;
            }

            endDayButton.interactable = true;
            if (endDayHintText != null)
            {
                endDayHintText.text = string.Empty;
            }

            var buttonLabel = endDayButton.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null)
            {
                buttonLabel.text = IsEndDayWarning(out _) ? "End Day (Confirm)" : "End Day";
            }
        }

        private void EnsureEndDayConfirmPanel()
        {
            if (endDayConfirmPanel == null)
            {
                var canvas = EnsureCanvas();
                if (canvas == null)
                {
                    return;
                }

                var root = new GameObject("EndDayConfirmPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                root.transform.SetParent(canvas.transform, false);
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var bg = root.GetComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.55f);

                var content = new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                content.transform.SetParent(root.transform, false);
                var contentRect = content.GetComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0.5f, 0.5f);
                contentRect.anchorMax = new Vector2(0.5f, 0.5f);
                contentRect.pivot = new Vector2(0.5f, 0.5f);
                contentRect.sizeDelta = new Vector2(720f, 280f);
                var contentImage = content.GetComponent<Image>();
                contentImage.color = new Color(0.08f, 0.1f, 0.14f, 0.98f);
                var contentLayoutElement = content.GetComponent<LayoutElement>() ?? content.AddComponent<LayoutElement>();
                contentLayoutElement.minWidth = 420f;
                contentLayoutElement.preferredWidth = 560f;
                contentLayoutElement.minHeight = 260f;
                contentLayoutElement.preferredHeight = 280f;
                var v = content.GetComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(18, 18, 18, 18);
                v.spacing = 12f;
                v.childControlHeight = true;
                v.childControlWidth = true;
                v.childForceExpandHeight = false;

                var title = CreateText(content.transform, "Title", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
                title.text = "End Day?";
                var titleLayout = title.gameObject.AddComponent<LayoutElement>();
                titleLayout.preferredHeight = 40f;

                var body = CreateText(content.transform, "Body", 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                body.textWrappingMode = TextWrappingModes.Normal;
                body.overflowMode = TextOverflowModes.Overflow;
                body.text = "";
                var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
                bodyLayout.preferredHeight = 120f;

                var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(content.transform, false);
                var h = row.GetComponent<HorizontalLayoutGroup>();
                h.spacing = 12f;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandHeight = false;
                h.childForceExpandWidth = false;

                var rowLayout = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
                rowLayout.minHeight = 56f;
                rowLayout.preferredHeight = 60f;

                var yes = CreateButton(row.transform, "ConfirmButton", "Confirm");
                var no = CreateButton(row.transform, "CancelButton", "Cancel");
                ConfigureEndDayConfirmButtonVisuals(yes);
                ConfigureEndDayConfirmButtonVisuals(no);

                endDayConfirmPanel = root;
                endDayConfirmBodyText = body;
                endDayConfirmYesButton = yes;
                endDayConfirmNoButton = no;
            }

            if (endDayConfirmYesButton != null)
            {
                endDayConfirmYesButton.onClick.RemoveAllListeners();
                endDayConfirmYesButton.onClick.AddListener(OnEndDayConfirmYes);
            }

            if (endDayConfirmNoButton != null)
            {
                endDayConfirmNoButton.onClick.RemoveAllListeners();
                endDayConfirmNoButton.onClick.AddListener(HideEndDayConfirm);
            }

            if (endDayConfirmPanel != null)
            {
                endDayConfirmPanel.SetActive(false);
            }
        }

        private void ConfigureEndDayConfirmButtonVisuals(Button button)
        {
            if (button == null)
            {
                return;
            }

            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 160f;
            layout.preferredWidth = 180f;
            layout.minHeight = 44f;

            var label = button.GetComponentInChildren<TMP_Text>();
            if (label == null)
            {
                return;
            }

            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 20f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private void ShowEndDayConfirm(string warning)
        {
            EnsureEndDayConfirmPanel();
            if (endDayConfirmPanel == null)
            {
                return;
            }

            if (endDayConfirmBodyText != null)
            {
                endDayConfirmBodyText.text = warning;
            }

            endDayConfirmPanel.SetActive(true);
            if (!_endDayConfirmPauseHeld)
            {
                GamePauseService.Push("EndDayConfirm");
                _endDayConfirmPauseHeld = true;
            }
        }

        private void HideEndDayConfirm()
        {
            if (endDayConfirmPanel != null)
            {
                endDayConfirmPanel.SetActive(false);
            }

            if (_endDayConfirmPauseHeld)
            {
                GamePauseService.Pop("EndDayConfirm");
                _endDayConfirmPauseHeld = false;
            }
        }

        private void OnEndDayConfirmYes()
        {
            HideEndDayConfirm();
            _endDayRequested = true;
            Debug.Log("[EndDay] confirmed -> request end day [TODO REMOVE]");
            TryAdvanceDayFlow("EndDayConfirm");
            UpdateEndDayUiState();
        }

        private void TryAdvanceDayFlow(string trigger)
        {
            if (_dayState == DayState.EveningGuildHall)
            {
                LogDayFlow(trigger, IsAnyContextModalActive());
                return;
            }

            var reportActive = missionReportPanel != null && missionReportPanel.IsOpen;
            var encounterQueued = encounterManager != null && encounterManager.PendingEncounterCount > 0;
            var encounterActive = encounterManager != null && encounterManager.IsEncounterActive;
            if (_pendingReports.Count > 0 || reportActive)
            {
                _dayState = DayState.ShowingReports;
                LogDayFlow(trigger, IsAnyContextModalActive());
                return;
            }

            if (encounterQueued || encounterActive)
            {
                _dayState = DayState.ResolvingEvents;
                LogDayFlow(trigger, IsAnyContextModalActive());
                return;
            }

            var anyModal = IsAnyContextModalActive();
            if (_endDayRequested && CanEnterEveningNow(anyModal))
            {
                _dayState = DayState.EveningGuildHall;
                LogDayFlow(trigger, anyModal);
                EnterGuildHallEvening();
                return;
            }

            _dayState = DayState.MapActive;
            LogDayFlow(trigger, anyModal);
        }

        private bool CanEnterEveningNow(bool anyModal)
        {
            if (!_endDayRequested)
            {
                return false;
            }

            if (_travelTasks.Count > 0)
            {
                return false;
            }

            var encounterQueued = encounterManager != null && encounterManager.PendingEncounterCount > 0;
            var encounterActive = encounterManager != null && encounterManager.IsEncounterActive;
            if (encounterQueued || encounterActive)
            {
                return false;
            }

            var reportActive = missionReportPanel != null && missionReportPanel.IsOpen;
            if (_pendingReports.Count > 0 || reportActive)
            {
                return false;
            }

            if (anyModal)
            {
                return false;
            }

            return GamePauseService.Count == 0;
        }

        private void LogDayFlow(string trigger, bool anyModal)
        {
            Debug.Log($"[DayFlow] reason={trigger} endDayRequested={_endDayRequested} travelActive={_travelTasks.Count} encounters={(encounterManager != null ? encounterManager.PendingEncounterCount : 0)}/{(encounterManager != null && encounterManager.IsEncounterActive)} reports={_pendingReports.Count}/{(missionReportPanel != null && missionReportPanel.IsOpen)} modals={anyModal} [TODO REMOVE]");
        }

        private void EnterGuildHallEvening()
        {
            Debug.Log("[GuildHall] Enter evening [TODO REMOVE]");
            EnsureGuildHallPanel();
            EnsureEndDayButton();
            EnsureEndDayConfirmPanel();
            if (guildHallPanel == null)
            {
                Debug.LogWarning("[GuildHall] Panel missing, skipping evening. [TODO REMOVE]");
                OnGuildHallNextDay();
                return;
            }

            if (_guildHallEveningData == null)
            {
                _guildHallEveningData = GuildHallEveningLoader.Load();
            }

            _endDayRequested = false;
            EnsureEventSystem();
            guildHallPanel.ShowEvening(_guildHallEveningData, _dayIndex, OnGuildHallNextDay, ApplyRestEveningEffect);
        }

        private void ApplyRestEveningEffect()
        {
            var squads = GetRosterSquads();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.IsDestroyed)
                {
                    continue;
                }

                if (squad.members == null)
                {
                    continue;
                }

                for (var m = 0; m < squad.members.Count; m++)
                {
                    var member = squad.members[m];
                    if (member == null || member.maxHp <= 0)
                    {
                        continue;
                    }

                    member.hp = Mathf.Min(member.maxHp, member.hp + 5);
                }
            }

            RefreshSquadStatusHud();
            UpdateEndDayUiState();
            squadDetailsPanel?.Refresh();
        }

        private void OnGuildHallNextDay()
        {
            guildHallPanel?.Hide();
            _endDayRequested = false;
            _dayIndex++;
            var squads = GetRosterSquads();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null)
                {
                    continue;
                }

                squad.exhausted = false;
                squad.exhaustedReason = null;
                squad.contractsDoneToday = 0;
                if (squad.lastRosterChangeDay != _dayIndex - 1)
                {
                    squad.cohesion = Mathf.Clamp(squad.cohesion + 1, 0, 100);
                }
            }

            if (hunterRoster != null)
            {
                for (var i = 0; i < hunterRoster.Hunters.Count; i++)
                {
                    if (hunterRoster.Hunters[i] != null)
                    {
                        hunterRoster.Hunters[i].exhaustedToday = false;
                    }
                }
            }

            RefreshContractsForNextDay();
            SyncAllContractIcons();

            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            Debug.Log($"[Day] Reset squad exhaustion, day={_dayIndex} [TODO REMOVE]");
            UpdateEndDayUiState();
        }

        private void RefreshContractsForNextDay()
        {
            foreach (var region in _gameData.regions)
            {
                if (region == null || region.id == GuildHqId)
                {
                    continue;
                }

                var seed = (region.id != null ? region.id.GetHashCode() : 0) ^ (_dayIndex * 397);
                var random = new System.Random(seed);
                if (!_contractsByRegion.TryGetValue(region.id, out var contracts) || contracts == null)
                {
                    contracts = new List<ContractData>();
                    _contractsByRegion[region.id] = contracts;
                }

                if (contracts.Count == 0)
                {
                    var count = random.Next(2, 5);
                    for (var i = 0; i < count; i++)
                    {
                        contracts.Add(new ContractData
                        {
                            id = $"{region.id}_day{_dayIndex}_contract_{i}",
                            title = $"Contract #{i + 1}: {region.name}",
                            remainingSeconds = random.Next(45, 300),
                            reward = random.Next(50, 250),
                            iconKey = PickContractIconKey(i),
                            minRank = i % 3 == 0 ? HunterRank.C : HunterRank.D,
                            allowSquad = i % 5 != 0,
                            allowSolo = true
                        });
                    }
                }
                else
                {
                    for (var i = 0; i < contracts.Count; i++)
                    {
                        contracts[i].remainingSeconds = random.Next(45, 300);
                        contracts[i].reward = random.Next(50, 250);
                    }
                }
            }
        }
    }
}
