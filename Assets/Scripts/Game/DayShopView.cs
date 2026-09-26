using System.Collections.Generic;
using NightShift.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The day-phase build panel: the shop (Сервер / Firewall / IDS / Honeypot with prices), the
    /// selected node's upgrade button, a status line, and «Начать смену». Implements Story 003
    /// acceptance criteria 1, 4, 5 and the panel half of 6 of
    /// `production/epics/night-shift/story-003-day-build-phase.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>Prices are never authored here.</b> Every figure on the panel is read from
    /// <see cref="GameData"/> through the simulation - <see cref="GameData.GetNodeCost"/>,
    /// <see cref="GameData.GetUpgradeCost"/>, <see cref="GameData.MaxLinkLength"/> - so re-balancing
    /// Core re-labels this panel with no edit here.</para>
    ///
    /// <para><b>Affordability is a display state, not a rule.</b> A row the player cannot pay for is
    /// dimmed and <see cref="VisualElement.SetEnabled"/>-disabled so it cannot even be clicked
    /// (criterion 6), but that is the presentation of a decision
    /// <see cref="NetworkSimulation.TryPlaceNode"/> makes for itself: if the two ever disagreed, the
    /// simulation would still refuse the spend, and credits still cannot go negative.</para>
    ///
    /// <para><b>Built in code, with no theme.</b> A runtime <see cref="PanelSettings"/> has no theme
    /// style sheet (see <see cref="UiRoot"/>), so every colour, size, border and padding below is set
    /// explicitly - including on <see cref="Button"/>, which without a theme would otherwise render
    /// as unstyled text. Same idiom as <see cref="NightHudView"/> and
    /// <see cref="NightReportView"/>.</para>
    ///
    /// <para><b>Why the panel tracks the pointer.</b> The map is picked with raw
    /// <see cref="Input"/> in <see cref="DayBuildController"/>, which knows nothing about UI Toolkit's
    /// hit-testing, so a click on this panel would otherwise also place a node on the cell behind it.
    /// <c>PointerEnterEvent</c> / <c>PointerLeaveEvent</c> on the panel root cover the panel and all
    /// its children, and feed <see cref="DayBuildController.PointerOverUi"/>.</para>
    /// </remarks>
    public sealed class DayShopView : MonoBehaviour
    {
        private const int PanelPaddingPx = 12;
        private const int RowSpacingPx = 6;
        private const int SectionSpacingPx = 12;
        private const int ButtonPaddingPx = 6;
        private const float BorderWidthPx = 1f;

        /// <summary>Shop rows, in panel order. Gateway and Core are fixed and deliberately absent.</summary>
        private static readonly NodeType[] ShopTypes =
        {
            NodeType.Server,
            NodeType.Firewall,
            NodeType.Ids,
            NodeType.Honeypot,
        };

        private NetworkSimulation _simulation;
        private GameRunner _runner;
        private DayBuildController _controller;
        private ViewConfig _config;

        private VisualElement _panel;
        private Label _titleLabel;
        private Label _creditsLabel;
        private Label _integrityLabel;
        private Label _maxLinkLabel;
        private Label _selectedLabel;
        private Label _statusLabel;
        private Button _upgradeButton;
        private Button _startShiftButton;

        private readonly Dictionary<NodeType, Button> _shopButtons = new Dictionary<NodeType, Button>();

        /// <summary>
        /// Builds the panel into <paramref name="parent"/> and subscribes to the simulation, the
        /// runner and the build controller. Call before the first day begins.
        /// </summary>
        /// <param name="simulation">Source of credits, integrity and every price.</param>
        /// <param name="runner">Phase owner; the panel shows only during <see cref="GamePhase.Day"/>.</param>
        /// <param name="controller">Receives the panel's commands and reports selection and status back.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer from <see cref="UiRoot.CreateLayer"/>.</param>
        public void Initialize(
            NetworkSimulation simulation,
            GameRunner runner,
            DayBuildController controller,
            ViewConfig config,
            VisualElement parent)
        {
            _simulation = simulation;
            _runner = runner;
            _controller = controller;
            _config = config;

            if (parent == null)
            {
                Debug.LogError("[NightShift] DayShopView.Initialize got a null UI layer; the day panel will not appear.");
                return;
            }

            BuildPanel(parent);

            _simulation.OnCreditsChanged += HandleCreditsChanged;
            _simulation.OnCoreDamaged += HandleCoreDamaged;
            _runner.OnPhaseChanged += HandlePhaseChanged;
            _controller.OnTypeSelectionChanged += Refresh;
            _controller.OnNodeSelectionChanged += HandleNodeSelectionChanged;
            _controller.OnBuildChanged += Refresh;
            _controller.OnStatus += SetStatus;

            SetStatus(UiStrings.DayHintIdle);
            ApplyPhase(_runner.Phase);
        }

        private void OnDestroy()
        {
            if (_simulation != null)
            {
                _simulation.OnCreditsChanged -= HandleCreditsChanged;
                _simulation.OnCoreDamaged -= HandleCoreDamaged;
            }

            if (_runner != null)
            {
                _runner.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (_controller != null)
            {
                _controller.OnTypeSelectionChanged -= Refresh;
                _controller.OnNodeSelectionChanged -= HandleNodeSelectionChanged;
                _controller.OnBuildChanged -= Refresh;
                _controller.OnStatus -= SetStatus;
            }
        }

        // ------------------------------------------------------------------
        // State -> panel
        // ------------------------------------------------------------------

        private void HandleCreditsChanged(float credits) => Refresh();

        private void HandleCoreDamaged(int damage, int remaining) => Refresh();

        private void HandleNodeSelectionChanged(Node node) => Refresh();

        private void HandlePhaseChanged(GamePhase phase) => ApplyPhase(phase);

        private void ApplyPhase(GamePhase phase)
        {
            if (_panel == null)
            {
                return;
            }

            bool isDay = phase == GamePhase.Day;
            _panel.style.display = isDay ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isDay)
            {
                // The pointer can never be "over" a hidden panel; without this the map would stay
                // blocked if the night started while the cursor sat on the panel.
                _controller.PointerOverUi = false;
                return;
            }

            Refresh();
        }

        /// <summary>Re-reads every value the panel shows. Cheap: a handful of labels on player actions only.</summary>
        private void Refresh()
        {
            if (_panel == null)
            {
                return;
            }

            int credits = Mathf.FloorToInt(_simulation.Credits);

            _titleLabel.text = string.Format(UiStrings.DayTitleFormat, Mathf.Max(1, _runner.DayNumber));
            _creditsLabel.text = string.Format(UiStrings.CreditsFormat, credits);
            _integrityLabel.text = string.Format(
                UiStrings.CoreIntegrityFormat,
                _simulation.CoreIntegrity,
                _simulation.Data.CoreStartingIntegrity);
            _maxLinkLabel.text = string.Format(UiStrings.MaxLinkLengthFormat, _simulation.Data.MaxLinkLength);

            RefreshShopButtons(credits);
            RefreshUpgradeBlock(credits);
        }

        private void RefreshShopButtons(int credits)
        {
            for (int i = 0; i < ShopTypes.Length; i++)
            {
                NodeType type = ShopTypes[i];
                if (!_shopButtons.TryGetValue(type, out Button button))
                {
                    continue;
                }

                int cost = _simulation.Data.GetNodeCost(type);
                bool affordable = credits >= cost;
                bool selected = _controller.HasTypeSelection && _controller.SelectedType == type;

                button.text = string.Format(UiStrings.ShopItemFormat, UiStrings.GetNodeName(type), cost);
                button.SetEnabled(affordable);

                StyleActionButton(button, affordable, selected, _config.GetNodeColor(type));
            }
        }

        private void RefreshUpgradeBlock(int credits)
        {
            Node selected = _controller.SelectedNode;

            if (selected == null)
            {
                _selectedLabel.style.display = DisplayStyle.None;
                _upgradeButton.style.display = DisplayStyle.None;
                return;
            }

            _selectedLabel.style.display = DisplayStyle.Flex;
            _selectedLabel.text = string.Format(
                UiStrings.SelectedNodeFormat,
                UiStrings.GetNodeName(selected.Type),
                selected.Level);

            // Only the three security tools have an upgrade path; GetUpgradeCost throws for the rest,
            // so the type test here mirrors Core's own contract rather than duplicating a rule.
            bool upgradable = selected.Type == NodeType.Firewall ||
                              selected.Type == NodeType.Ids ||
                              selected.Type == NodeType.Honeypot;

            if (!upgradable)
            {
                _upgradeButton.style.display = DisplayStyle.None;
                return;
            }

            _upgradeButton.style.display = DisplayStyle.Flex;

            if (selected.Level >= 2)
            {
                _upgradeButton.text = UiStrings.UpgradeMaxLevel;
                _upgradeButton.SetEnabled(false);
                StyleActionButton(_upgradeButton, false, false, _config.HudTextColor);
                return;
            }

            int cost = _simulation.Data.GetUpgradeCost(selected.Type);
            bool affordable = credits >= cost;

            _upgradeButton.text = string.Format(UiStrings.UpgradeButtonFormat, cost);
            _upgradeButton.SetEnabled(affordable);
            StyleActionButton(_upgradeButton, affordable, false, _config.GetNodeColor(selected.Type));
        }

        private void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = status ?? string.Empty;
            }
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void BuildPanel(VisualElement parent)
        {
            _panel = new VisualElement { name = "day-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = _config.DayPanelMarginPx;
            _panel.style.top = _config.DayPanelMarginPx;
            _panel.style.width = _config.DayPanelWidthPx;
            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.alignItems = Align.Stretch;

            // flexShrink 0 for the same reason NightReportView documents: in a container whose height
            // resolved to zero, UI Toolkit's default flex-shrink of 1 squeezes every child to nothing
            // and they overprint each other.
            _panel.style.flexShrink = 0f;

            _panel.style.backgroundColor = _config.ReportBoxColor;
            SetBorder(_panel, _config.ReportBoxBorderColor, BorderWidthPx);
            SetPadding(_panel, PanelPaddingPx);
            _panel.style.display = DisplayStyle.None;

            // The map is picked with raw Input, so the panel has to say when it owns the pointer.
            _panel.RegisterCallback<PointerEnterEvent>(_ => _controller.PointerOverUi = true);
            _panel.RegisterCallback<PointerLeaveEvent>(_ => _controller.PointerOverUi = false);

            parent.Add(_panel);

            _titleLabel = AddLabel(_panel, "day-title", _config.DayTitleFontSize, _config.HudTextColor);
            _creditsLabel = AddLabel(_panel, "day-credits", _config.DayFontSize, _config.HudTextColor);
            _integrityLabel = AddLabel(_panel, "day-integrity", _config.DayFontSize, _config.HudTextColor);
            _maxLinkLabel = AddLabel(_panel, "day-maxlink", _config.DayFontSize, _config.DisabledTextColor);

            Label shopHeader = AddLabel(_panel, "day-shop-header", _config.DayFontSize, _config.HudTextColor);
            shopHeader.text = UiStrings.ShopHeader;
            shopHeader.style.marginTop = SectionSpacingPx;

            for (int i = 0; i < ShopTypes.Length; i++)
            {
                NodeType type = ShopTypes[i];
                Button button = AddButton(_panel, "day-shop-" + type);
                button.clicked += () => _controller.ToggleNodeType(type);
                _shopButtons[type] = button;
            }

            _selectedLabel = AddLabel(_panel, "day-selected", _config.DayFontSize, _config.HudTextColor);
            _selectedLabel.style.marginTop = SectionSpacingPx;
            _selectedLabel.style.display = DisplayStyle.None;

            _upgradeButton = AddButton(_panel, "day-upgrade");
            _upgradeButton.clicked += () => _controller.UpgradeSelectedNode();
            _upgradeButton.style.display = DisplayStyle.None;

            _statusLabel = AddLabel(_panel, "day-status", _config.DayFontSize, _config.DayStatusColor);
            _statusLabel.style.marginTop = SectionSpacingPx;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;

            _startShiftButton = AddButton(_panel, "day-start-shift");
            _startShiftButton.text = UiStrings.StartShiftButton;
            _startShiftButton.style.marginTop = SectionSpacingPx;
            _startShiftButton.clicked += () => _runner.StartNight();
            StyleActionButton(_startShiftButton, true, true, _config.HudTextColor);
        }

        private Label AddLabel(VisualElement parent, string labelName, float fontSize, Color color)
        {
            var label = new Label { name = labelName };
            label.style.color = color;
            label.style.fontSize = fontSize;
            label.style.marginBottom = RowSpacingPx;
            label.style.flexShrink = 0f;
            parent.Add(label);
            return label;
        }

        private Button AddButton(VisualElement parent, string buttonName)
        {
            var button = new Button { name = buttonName };
            button.style.fontSize = _config.DayFontSize;
            button.style.marginBottom = RowSpacingPx;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.flexShrink = 0f;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            SetPadding(button, ButtonPaddingPx);
            StyleActionButton(button, true, false, _config.HudTextColor);
            parent.Add(button);
            return button;
        }

        /// <summary>
        /// The single place a button's enabled / selected look is decided, so "unaffordable" reads the
        /// same on every row (criterion 6).
        /// </summary>
        /// <param name="button">Button to style.</param>
        /// <param name="enabled">False for an action the player cannot currently take.</param>
        /// <param name="selected">True for the armed shop row, which gets a bright border.</param>
        /// <param name="accent">Text colour when enabled - the node type's own map colour, so panel and map agree.</param>
        private void StyleActionButton(Button button, bool enabled, bool selected, Color accent)
        {
            button.style.backgroundColor = !enabled
                ? _config.ShopButtonDisabledColor
                : selected
                    ? _config.ShopButtonSelectedColor
                    : _config.ShopButtonColor;

            button.style.color = enabled ? accent : _config.DisabledTextColor;

            SetBorder(
                button,
                selected ? _config.ShopButtonSelectedBorderColor : _config.ReportBoxBorderColor,
                BorderWidthPx);
        }

        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        private static void SetPadding(VisualElement element, float padding)
        {
            element.style.paddingTop = padding;
            element.style.paddingBottom = padding;
            element.style.paddingLeft = padding;
            element.style.paddingRight = padding;
        }
    }
}
