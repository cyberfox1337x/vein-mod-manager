using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.ComponentModel;

namespace VEIN_Item_And_Container_Modifier;

public sealed partial class MainForm : Form
{
    private static readonly Color AppBack = Color.FromArgb(5, 10, 18);
    private static readonly Color PanelBack = Color.FromArgb(10, 18, 30);
    private static readonly Color InnerBack = Color.FromArgb(3, 11, 20);
    private static readonly Color Border = Color.FromArgb(34, 58, 90);
    private static readonly Color BorderSoft = Color.FromArgb(28, 49, 78);
    private static readonly Color TextMain = Color.White;
    private static readonly Color TextMuted = Color.FromArgb(177, 207, 242);
    private static readonly Color Purple = Color.FromArgb(126, 58, 242);
    private static readonly Color PurpleLight = Color.FromArgb(154, 85, 255);
    private static readonly Color Green = Color.FromArgb(0, 255, 102);
    private static readonly Color Orange = Color.FromArgb(255, 112, 0);
    private static readonly Color Red = Color.FromArgb(255, 87, 87);
    private static readonly Color Cyan = Color.FromArgb(18, 223, 213);
    private const int MaxVisibleComboRows = 14;
    private static readonly string[] BoolChoices = { "Game Default", "True", "False" };
    private const string SettingsDirectoryName = "VeinModManager";
    private const string SettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions SettingsJsonOptions = new() { WriteIndented = true };

    private readonly UiConfigState _state = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 1000 };

    private TextBox _gameFolderBox = null!;
    private TextBox _modFolderBox = null!;
    private Label _gameStatus = null!;
    private Label _ue4ssStatus = null!;
    private Label _modStatus = null!;
    private Label _unsavedStatus = null!;
    private RichTextBox _log = null!;
    private readonly ToolTip _toolTip = new();
    private ToggleSwitch _toolTipsToggle = null!;
    private Label _toolTipsStatus = null!;
    private RoundedPanel _importDropZone = null!;
    private Label _importConfigPathLabel = null!;
    private readonly Dictionary<string, Control> _tabPages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoundedButton> _tabButtons = new(StringComparer.Ordinal);
    private RoundedButton? _settingsButton;

    private ComboBox _categoryCombo = null!;
    private ToggleSwitch _categoryEnabled = null!;
    private CheckBox _categoryAdvanced = null!;
    private FlowLayoutPanel _categoryFields = null!;
    private readonly Dictionary<string, Control> _categoryInputs = new(StringComparer.OrdinalIgnoreCase);

    private ComboBox _itemCategoryCombo = null!;
    private TextBox _itemSearchBox = null!;
    private ComboBox _itemCombo = null!;
    private CheckBox _itemAdvanced = null!;
    private Label _itemClassLabel = null!;
    private Label _itemCdoLabel = null!;
    private FlowLayoutPanel _itemFields = null!;
    private readonly Dictionary<string, (CheckBox DefaultCheck, Control Input)> _itemInputs = new(StringComparer.OrdinalIgnoreCase);

    private ModData? _modData;
    private bool _loadingUi;
    private bool _hasUnsavedChanges;

    public MainForm()
    {
        InitializeComponent();
        if (IsDesignerHosted) return;

        ConfigureToolTips();
        if (LoadWindowIcon() is { } windowIcon) Icon = windowIcon;
        BuildUi();

        LoadPathSettings();
        AutoDetectPaths(log: true);
        LoadModFromPath();

        _statusTimer.Tick += (_, _) => UpdateStatuses();
        _statusTimer.Start();
        UpdateStatuses();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusTimer.Stop();
            _statusTimer.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "Vein Mod Manager";
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(1296, 839);
        MaximumSize = new Size(1296, 839);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppBack;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 11F, FontStyle.Regular);
        DoubleBuffered = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (IsDesignerHosted) return;

        UseDarkTitleBar(Handle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!e.Cancel && _hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                this,
                "You have unsaved config changes. Choose Yes to save, No to discard them, or Cancel to keep editing.",
                "Save changes before closing?",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
            }
            else if (result == DialogResult.Yes && (!TrySaveConfig() || _hasUnsavedChanges))
            {
                e.Cancel = true;
                MessageBox.Show(
                    this,
                    "The config could not be saved, so Vein Mod Manager will stay open. Check the log for details.",
                    "Save failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        base.OnFormClosing(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!IsDesignerHosted || Controls.Count > 0) return;

        DrawDesignerPreview(e.Graphics);
    }

    private static readonly bool IsDesignerHosted = DetectDesignerHosted();

    private static bool DetectDesignerHosted()
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return true;

        var processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "";
        return processName.Contains("devenv", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase);
    }

    private void BuildUi()
    {
        Controls.Clear();
        BuildSidebar();
        BuildHeader();
        BuildStatusCards();
        BuildTabs();
    }

    private void DrawDesignerPreview(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(AppBack);

        DrawPreviewPanel(graphics, new Rectangle(20, 20, 172, 740), 14, PanelBack, BorderSoft);
        DrawPreviewLogo(graphics, new Rectangle(46, 74, 122, 122));
        DrawPreviewLine(graphics, 38, 230, 136);
        DrawPreviewText(graphics, "Steps", 46, 260, 14, FontStyle.Bold, TextMuted);
        DrawPreviewText(graphics, "1. Select folder\n2. Pick category\n3. Change values\n4. Save config", 46, 296, 12, FontStyle.Regular, TextMuted);

        DrawPreviewText(graphics, "Vein Manager", 220, 72, 28, FontStyle.Bold, TextMain);
        DrawPreviewText(graphics, "Modify VEIN item, backpack, vehicle, and container values without editing config files.", 222, 116, 13, FontStyle.Regular, TextMuted);
        DrawPreviewPanel(graphics, new Rectangle(1196, 58, 52, 52), 12, InnerBack, BorderSoft);
        DrawPreviewText(graphics, "\uE713", 1211, 72, 17, FontStyle.Regular, TextMain, "Segoe MDL2 Assets");

        DrawPreviewStatusCard(graphics, "Game", "Closed", "VEIN process", Orange, 220, 148);
        DrawPreviewStatusCard(graphics, "UE4SS", "Found", "Detected in game folder", Green, 498, 148);
        DrawPreviewStatusCard(graphics, "Mod", "Found", "ItemAndContainerModifier", Green, 776, 148);

        DrawPreviewTab(graphics, "Setup", 220, 252, 136, selected: true);
        DrawPreviewTab(graphics, "Category Defaults", 364, 252, 170, selected: false);
        DrawPreviewTab(graphics, "Item Overrides", 542, 252, 170, selected: false);
        DrawPreviewTab(graphics, "Presets", 720, 252, 136, selected: false);
        DrawPreviewTab(graphics, "Log", 864, 252, 120, selected: false);

        DrawPreviewPanel(graphics, new Rectangle(220, 308, 1020, 484), 12, PanelBack, Border);
        DrawPreviewText(graphics, "Setup", 256, 340, 20, FontStyle.Bold, TextMain);
        DrawPreviewText(graphics, "Select your VEIN install and the UE4SS mod folder. The editor writes generated overrides only.", 256, 378, 13, FontStyle.Regular, TextMuted);
        DrawPreviewText(graphics, "Game folder", 256, 428, 13, FontStyle.Bold, TextMuted);
        DrawPreviewTextBox(graphics, @"C:\Program Files (x86)\Steam\steamapps\common\Vein", 256, 452, 660);
        DrawPreviewButton(graphics, "Browse", 930, 448, 110, 44, main: false);
        DrawPreviewButton(graphics, "Auto Detect", 1054, 448, 126, 44, main: false);
        DrawPreviewText(graphics, "Mod folder", 256, 524, 13, FontStyle.Bold, TextMuted);
        DrawPreviewTextBox(graphics, @"C:\Program Files (x86)\Steam\steamapps\common\Vein\Vein\Binaries\Win64\ue4ss\Mods\ItemAndContainerModifier", 256, 548, 660);
        DrawPreviewButton(graphics, "Browse", 930, 544, 110, 44, main: false);
        DrawPreviewButton(graphics, "Open Folder", 1054, 544, 126, 44, main: false);
        DrawPreviewText(graphics, "No unsaved changes (7 edits)", 256, 658, 13, FontStyle.Bold, Cyan);
        DrawPreviewButton(graphics, "Save Config", 256, 694, 190, 54, main: true);
        DrawPreviewButton(graphics, "Backup Now", 462, 694, 170, 54, main: false);
        DrawPreviewButton(graphics, "Launch VEIN", 652, 694, 170, 54, main: false);
    }

    private static void DrawPreviewLogo(Graphics graphics, Rectangle bounds)
    {
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "vein-logo.png");
        if (!File.Exists(logoPath))
        {
            logoPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "vein-logo.png"));
        }

        if (File.Exists(logoPath))
        {
            using var image = Image.FromFile(logoPath);
            graphics.DrawImage(image, bounds);
            return;
        }

        DrawPreviewPanel(graphics, bounds, 2, Color.Black, BorderSoft);
        DrawPreviewText(graphics, "VEIN", bounds.Left + 18, bounds.Top + 42, 24, FontStyle.Bold, TextMain);
    }

    private static void DrawPreviewStatusCard(Graphics graphics, string title, string value, string subtitle, Color valueColor, int x, int y)
    {
        DrawPreviewPanel(graphics, new Rectangle(x, y, 260, 92), 10, PanelBack, BorderSoft);
        DrawPreviewText(graphics, title, x + 24, y + 14, 12, FontStyle.Regular, TextMuted);
        DrawPreviewText(graphics, value, x + 24, y + 34, 20, FontStyle.Regular, valueColor);
        DrawPreviewText(graphics, subtitle, x + 24, y + 62, 9, FontStyle.Regular, TextMuted);
    }

    private static void DrawPreviewTab(Graphics graphics, string text, int x, int y, int width, bool selected)
    {
        DrawPreviewPanel(graphics, new Rectangle(x, y, width, 44), 14, selected ? Purple : InnerBack, selected ? PurpleLight : BorderSoft);
        DrawPreviewText(graphics, text, x, y + 13, 11, FontStyle.Bold, TextMain, width: width, alignment: StringAlignment.Center);
    }

    private static void DrawPreviewTextBox(Graphics graphics, string text, int x, int y, int width)
    {
        DrawPreviewPanel(graphics, new Rectangle(x, y, width, 36), 8, InnerBack, Border);
        DrawPreviewText(graphics, text, x + 10, y + 8, 11, FontStyle.Regular, TextMain, width: width - 20);
    }

    private static void DrawPreviewButton(Graphics graphics, string text, int x, int y, int width, int height, bool main)
    {
        DrawPreviewPanel(graphics, new Rectangle(x, y, width, height), 10, main ? Purple : Color.FromArgb(32, 49, 75), main ? PurpleLight : Color.FromArgb(54, 78, 116));
        DrawPreviewText(graphics, text, x, y + (height - 18) / 2, 11, FontStyle.Bold, TextMain, width: width, alignment: StringAlignment.Center);
    }

    private static void DrawPreviewLine(Graphics graphics, int x, int y, int width)
    {
        using var pen = new Pen(BorderSoft);
        graphics.DrawLine(pen, x, y, x + width, y);
    }

    private static void DrawPreviewPanel(Graphics graphics, Rectangle bounds, int radius, Color fillColor, Color borderColor)
    {
        using var path = RoundedPanel.RoundedRect(bounds, radius);
        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(borderColor);
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);
    }

    private static void DrawPreviewText(
        Graphics graphics,
        string text,
        int x,
        int y,
        float size,
        FontStyle style,
        Color color,
        string family = "Segoe UI",
        int width = 900,
        StringAlignment alignment = StringAlignment.Near)
    {
        using var font = new Font(family, size, style);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = alignment,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter
        };
        graphics.DrawString(text, font, brush, new RectangleF(x, y, width, 120), format);
    }

    private void ConfigureToolTips()
    {
        _toolTip.Active = true;
        _toolTip.AutoPopDelay = 12000;
        _toolTip.InitialDelay = 450;
        _toolTip.ReshowDelay = 120;
        _toolTip.ShowAlways = true;
        _toolTip.OwnerDraw = true;
        _toolTip.BackColor = Color.Black;
        _toolTip.ForeColor = TextMain;
        _toolTip.Popup += ToolTip_Popup;
        _toolTip.Draw += ToolTip_Draw;
    }

    private void ToolTip_Popup(object? sender, PopupEventArgs e)
    {
        const int maxWidth = 420;
        const int horizontalPadding = 18;
        const int verticalPadding = 12;
        var text = e.AssociatedControl == null ? string.Empty : _toolTip.GetToolTip(e.AssociatedControl);
        var proposedSize = new Size(maxWidth - horizontalPadding, int.MaxValue);
        var measured = TextRenderer.MeasureText(text, Font, proposedSize, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        e.ToolTipSize = new Size(measured.Width + horizontalPadding, measured.Height + verticalPadding);
    }

    private void ToolTip_Draw(object? sender, DrawToolTipEventArgs e)
    {
        using var background = new SolidBrush(Color.Black);
        using var border = new Pen(Border);
        e.Graphics.FillRectangle(background, e.Bounds);
        e.Graphics.DrawRectangle(border, new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1));

        var textBounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, e.Bounds.Width - 16, e.Bounds.Height - 12);
        TextRenderer.DrawText(e.Graphics, e.ToolTipText, Font, textBounds, Color.White, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }

    private void AddTip(Control control, string text)
    {
        _toolTip.SetToolTip(control, text);
    }

    private void SetToolTipsEnabled(bool enabled)
    {
        _toolTip.Active = enabled;

        if (_toolTipsStatus != null)
        {
            _toolTipsStatus.Text = enabled ? "ON" : "OFF";
            _toolTipsStatus.ForeColor = enabled ? Green : Red;
        }

        if (_toolTipsToggle != null)
        {
            _toolTipsToggle.Checked = enabled;
        }

        Log("Tooltips " + (enabled ? "enabled." : "disabled."));
    }

    private void BuildSidebar()
    {
        var sidebar = NewPanel(18, 20, 20, 172, 740);
        Controls.Add(sidebar);

        var logo = new PictureBox
        {
            Left = 24,
            Top = 24,
            Width = 124,
            Height = 124,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = InnerBack
        };

        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "vein-logo.png");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "vein-logo.png");
            }
            if (File.Exists(logoPath)) logo.Image = Image.FromFile(logoPath);
        }
        catch
        {
        }

        sidebar.Controls.Add(logo);
        sidebar.Controls.Add(Line(18, 178, 136));
        sidebar.Controls.Add(MakeLabel("Steps", 20, 204, 120, 24, 14, FontStyle.Bold, TextMuted, ContentAlignment.MiddleLeft, PanelBack));
        sidebar.Controls.Add(MakeLabel("1. Select folder\n2. Pick category\n3. Change values\n4. Save config", 20, 240, 132, 118, 12, FontStyle.Regular, TextMuted, ContentAlignment.TopLeft, PanelBack));
    }

    private static Icon? LoadWindowIcon()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "vein-logo.ico"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "vein-logo.ico"))
        };

        var iconPath = candidates.FirstOrDefault(File.Exists);
        return iconPath == null ? null : new Icon(iconPath);
    }

    private void BuildHeader()
    {
        Controls.Add(MakeLabel("Vein Manager", 220, 30, 610, 42, 28, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, AppBack));
        Controls.Add(MakeLabel("Modify VEIN item, backpack, vehicle, and container values without editing config files.", 222, 74, 880, 28, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, AppBack));

        _settingsButton = MakeButton("\uE713", 1196, 28, 52, 52, () => ShowTab("Settings"));
        _settingsButton.AccessibleName = "Settings";
        _settingsButton.Font = new Font("Segoe MDL2 Assets", 17F, FontStyle.Regular);
        _settingsButton.FillColor = InnerBack;
        _settingsButton.HoverColor = Color.FromArgb(18, 31, 50);
        _settingsButton.BorderColor = BorderSoft;
        AddTip(_settingsButton, "Open settings, config import, and tooltip options.");
        Controls.Add(_settingsButton);
    }

    private void BuildStatusCards()
    {
        var x = 220;
        var y = 116;
        var w = 260;
        var h = 92;
        var gap = 18;

        var game = NewStatCard("Game", "Closed", "VEIN process", Orange, x, y, w, h);
        _gameStatus = (Label)game.Tag!;
        Controls.Add(game);

        var ue4ss = NewStatCard("UE4SS", "Missing", "Detected in game folder", Orange, x + w + gap, y, w, h);
        _ue4ssStatus = (Label)ue4ss.Tag!;
        Controls.Add(ue4ss);

        var mod = NewStatCard("Mod", "Missing", "ItemAndContainerModifier", Orange, x + (w + gap) * 2, y, w, h);
        _modStatus = (Label)mod.Tag!;
        Controls.Add(mod);
    }

    private void BuildTabs()
    {
        _tabPages.Clear();
        _tabButtons.Clear();

        var shell = new Panel
        {
            Left = 220,
            Top = 222,
            Width = 1020,
            Height = 538,
            BackColor = AppBack
        };

        _tabPages["Setup"] = BuildSetupTab();
        _tabPages["Settings"] = BuildSettingsTab();
        _tabPages["Category Defaults"] = BuildCategoryTab();
        _tabPages["Item Overrides"] = BuildItemTab();
        _tabPages["Presets"] = BuildPresetTab();
        _tabPages["Log"] = BuildLogTab();

        var tabWidths = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Setup"] = 136,
            ["Category Defaults"] = 170,
            ["Item Overrides"] = 170,
            ["Presets"] = 136,
            ["Log"] = 120
        };

        var x = 0;
        const int tabGap = 8;
        foreach (var title in tabWidths.Keys)
        {
            var button = NewTabButton(title, x, 0, tabWidths[title]);
            button.Click += (_, _) => ShowTab(title);
            _tabButtons[title] = button;
            shell.Controls.Add(button);
            x += button.Width + tabGap;
        }

        foreach (var page in _tabPages.Values)
        {
            page.Visible = false;
            shell.Controls.Add(page);
        }

        Controls.Add(shell);
        ShowTab("Setup");
    }

    private void ShowTab(string title)
    {
        foreach (var (name, page) in _tabPages)
        {
            var selected = name.Equals(title, StringComparison.Ordinal);
            page.Visible = selected;
            if (selected)
            {
                page.BringToFront();
            }
        }

        foreach (var (name, button) in _tabButtons)
        {
            var selected = name.Equals(title, StringComparison.Ordinal);
            button.FillColor = selected ? Purple : InnerBack;
            button.HoverColor = selected ? PurpleLight : Color.FromArgb(18, 31, 50);
            button.BorderColor = selected ? PurpleLight : BorderSoft;
            button.Invalidate();
        }

        if (_settingsButton != null)
        {
            var settingsSelected = title.Equals("Settings", StringComparison.Ordinal);
            _settingsButton.FillColor = settingsSelected ? Purple : InnerBack;
            _settingsButton.HoverColor = settingsSelected ? PurpleLight : Color.FromArgb(18, 31, 50);
            _settingsButton.BorderColor = settingsSelected ? PurpleLight : BorderSoft;
            _settingsButton.Invalidate();
        }
    }

    private RoundedPanel BuildSetupTab()
    {
        var panel = NewContentPanel();
        panel.Controls.Add(MakeLabel("Setup", 28, 24, 300, 34, 20, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        panel.Controls.Add(MakeLabel("Select your VEIN install and the UE4SS mod folder. The editor writes generated overrides only.", 30, 62, 760, 28, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));

        panel.Controls.Add(MakeLabel("Game folder", 30, 112, 180, 28, 13, FontStyle.Bold, TextMuted, ContentAlignment.MiddleLeft, PanelBack));
        _gameFolderBox = NewTextBox(30, 144, 660, 36);
        panel.Controls.Add(_gameFolderBox);
        var browseGame = MakeButton("Browse", 710, 140, 110, 44, BrowseGameFolder);
        var autoDetect = MakeButton("Auto Detect", 834, 140, 126, 44, () => AutoDetectPaths(log: true));
        AddTip(_gameFolderBox, "The main VEIN Steam folder. Auto Detect usually finds this for you.");
        AddTip(browseGame, "Pick the VEIN Steam folder manually if Auto Detect cannot find it.");
        AddTip(autoDetect, "Search Steam libraries for VEIN and fill the expected UE4SS mod path.");
        panel.Controls.Add(browseGame);
        panel.Controls.Add(autoDetect);

        panel.Controls.Add(MakeLabel("Mod folder", 30, 208, 180, 28, 13, FontStyle.Bold, TextMuted, ContentAlignment.MiddleLeft, PanelBack));
        _modFolderBox = NewTextBox(30, 240, 660, 36);
        panel.Controls.Add(_modFolderBox);
        var browseMod = MakeButton("Browse", 710, 236, 110, 44, BrowseModFolder);
        var openMod = MakeButton("Open Folder", 834, 236, 126, 44, OpenModFolder);
        AddTip(_modFolderBox, "The UE4SS ItemAndContainerModifier folder where Scripts\\ui_config.lua is saved.");
        AddTip(browseMod, "Pick the ItemAndContainerModifier folder manually.");
        AddTip(openMod, "Open the selected mod folder in File Explorer.");
        panel.Controls.Add(browseMod);
        panel.Controls.Add(openMod);

        _unsavedStatus = MakeLabel("No unsaved changes", 30, 340, 420, 32, 13, FontStyle.Bold, Cyan, ContentAlignment.MiddleLeft, PanelBack);
        panel.Controls.Add(_unsavedStatus);
        var saveConfig = MakeButton("Save Config", 30, 386, 190, 54, SaveConfig, main: true);
        var backupNow = MakeButton("Backup Now", 240, 386, 170, 54, BackupNow);
        var launchVein = MakeButton("Launch VEIN", 430, 386, 170, 54, LaunchVein);
        AddTip(saveConfig, "Write your generated values to Scripts\\ui_config.lua. Restart VEIN after saving.");
        AddTip(backupNow, "Create a backup of config.lua, ui_config.lua, and category files.");
        AddTip(launchVein, "Start VEIN from the detected Steam folder.");
        panel.Controls.Add(saveConfig);
        panel.Controls.Add(backupNow);
        panel.Controls.Add(launchVein);

        return panel;
    }

    private RoundedPanel BuildSettingsTab()
    {
        var panel = NewContentPanel();
        panel.Controls.Add(MakeLabel("Settings", 28, 24, 300, 34, 20, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        panel.Controls.Add(MakeLabel("Import a generated config file, open the saved config folder, and control beginner help.", 30, 62, 820, 28, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));

        var configCard = NewPanel(14, 30, 112, 930, 248);
        configCard.BackColor = PanelBack;
        WireConfigImportDrop(configCard);
        panel.Controls.Add(configCard);

        configCard.Controls.Add(MakeLabel("Configuration file", 28, 22, 260, 28, 15, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        configCard.Controls.Add(MakeLabel("Drop ui_config.lua below. The manager installs it into the correct Scripts folder automatically.", 30, 54, 820, 24, 12, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));

        _importDropZone = NewPanel(12, 30, 90, 870, 92);
        _importDropZone.FillColor = InnerBack;
        _importDropZone.BorderColor = Border;
        _importDropZone.BackColor = PanelBack;
        WireConfigImportDrop(_importDropZone);
        configCard.Controls.Add(_importDropZone);

        var dropTitle = MakeLabel("Drop ui_config.lua here", 20, 16, 830, 28, 14, FontStyle.Bold, TextMain, ContentAlignment.MiddleCenter, InnerBack);
        var dropHint = MakeLabel("It will be copied to ItemAndContainerModifier\\Scripts\\ui_config.lua and the old file is backed up first.", 20, 44, 830, 22, 10.5F, FontStyle.Regular, TextMuted, ContentAlignment.MiddleCenter, InnerBack);
        _importConfigPathLabel = MakeLabel("Waiting for file", 20, 66, 830, 18, 8.5F, FontStyle.Regular, TextMuted, ContentAlignment.MiddleCenter, InnerBack);
        WireConfigImportDrop(dropTitle);
        WireConfigImportDrop(dropHint);
        WireConfigImportDrop(_importConfigPathLabel);
        _importDropZone.Controls.Add(dropTitle);
        _importDropZone.Controls.Add(dropHint);
        _importDropZone.Controls.Add(_importConfigPathLabel);

        var loadCurrent = MakeButton("Load Current", 30, 192, 150, 42, LoadCurrentConfigFile);
        var openScripts = MakeButton("Open Config Folder", 200, 192, 190, 42, OpenConfigFolder);
        AddTip(loadCurrent, "Reload the ui_config.lua already installed in the selected mod folder.");
        AddTip(openScripts, "Open the Scripts folder that contains ui_config.lua.");
        AddTip(_importDropZone, "Drop ui_config.lua here. It installs automatically to the selected ItemAndContainerModifier\\Scripts folder.");
        configCard.Controls.Add(loadCurrent);
        configCard.Controls.Add(openScripts);

        var helpCard = NewPanel(14, 30, 376, 930, 104);
        helpCard.BackColor = PanelBack;
        panel.Controls.Add(helpCard);

        helpCard.Controls.Add(MakeLabel("Tooltips", 28, 24, 180, 34, 16, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        _toolTipsToggle = new ToggleSwitch
        {
            Left = 768,
            Top = 24,
            Width = 72,
            Height = 32,
            BackColor = PanelBack,
            Checked = true,
            OnColor = Color.FromArgb(8, 84, 54),
            OnColor2 = Color.FromArgb(10, 135, 82),
            OffColor = Color.FromArgb(82, 20, 28),
            OffColor2 = Color.FromArgb(122, 32, 42)
        };
        _toolTipsToggle.CheckedChanged += (_, _) => SetToolTipsEnabled(_toolTipsToggle.Checked);
        helpCard.Controls.Add(_toolTipsToggle);

        _toolTipsStatus = MakeLabel("ON", 852, 24, 54, 32, 11, FontStyle.Bold, Green, ContentAlignment.MiddleCenter, PanelBack);
        helpCard.Controls.Add(_toolTipsStatus);
        helpCard.Controls.Add(MakeLabel("Turn hover help on for first-time modders, or off once you know the workflow.", 30, 62, 760, 24, 12, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));
        AddTip(_toolTipsToggle, "Green means tooltips are on. Red means tooltips are off.");

        return panel;
    }

    private RoundedPanel BuildCategoryTab()
    {
        var panel = NewContentPanel();
        panel.Controls.Add(MakeLabel("Category Defaults", 28, 24, 300, 34, 20, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        panel.Controls.Add(MakeLabel("Set simple defaults for a whole category. Fields change based on what the category supports.", 30, 62, 760, 28, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));

        _categoryCombo = NewCombo(30, 112, 280, CategoryNames.Ordered);
        _categoryCombo.SelectedIndexChanged += (_, _) => RefreshCategoryEditor();
        AddTip(_categoryCombo, "Choose the group you want to edit. Vehicles and containers use Max Weight; item groups use item properties.");
        panel.Controls.Add(_categoryCombo);

        _categoryAdvanced = NewCheckBox("Advanced", 330, 116, 120);
        _categoryAdvanced.CheckedChanged += (_, _) => RefreshCategoryEditor();
        AddTip(_categoryAdvanced, "Show extra/raw information for advanced troubleshooting.");
        panel.Controls.Add(_categoryAdvanced);

        var card = NewPanel(14, 30, 150, 930, 330);
        card.BackColor = PanelBack;
        panel.Controls.Add(card);

        card.Controls.Add(MakeLabel("Enable this category", 28, 24, 280, 34, 16, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        _categoryEnabled = new ToggleSwitch { Left = 324, Top = 26, Width = 84, Height = 34, BackColor = PanelBack };
        _categoryEnabled.CheckedChanged += (_, _) => MarkUnsaved();
        AddTip(_categoryEnabled, "Turn this whole category on or off in the generated config.");
        card.Controls.Add(_categoryEnabled);

        _categoryFields = NewFieldFlow(28, 80, 876, 198);
        _categoryFields.AutoScroll = false;
        card.Controls.Add(_categoryFields);

        card.Controls.Add(MakeButton("Save Category Default", 28, 280, 210, 42, SaveCategoryDefault, main: true));
        card.Controls.Add(MakeButton("Reset This Category", 258, 280, 190, 42, ResetCategory));

        _categoryCombo.SelectedIndex = 0;
        return panel;
    }

    private RoundedPanel BuildItemTab()
    {
        var panel = NewContentPanel();
        panel.Controls.Add(MakeLabel("Item Overrides", 28, 24, 300, 34, 20, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        panel.Controls.Add(MakeLabel("Pick one item or container and override only the values you need.", 30, 62, 760, 28, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));

        _itemCategoryCombo = NewCombo(30, 112, 220, CategoryNames.Ordered);
        _itemCategoryCombo.SelectedIndexChanged += (_, _) => RefreshItemList();
        AddTip(_itemCategoryCombo, "Pick the item group to search inside.");
        panel.Controls.Add(_itemCategoryCombo);

        _itemSearchBox = NewTextBox(270, 112, 230, 28);
        _itemSearchBox.PlaceholderText = "Search item name";
        _itemSearchBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
        _itemSearchBox.TextChanged += (_, _) => RefreshItemList();
        AddTip(_itemSearchBox, "Type part of an item name or raw class name to filter the list.");
        panel.Controls.Add(_itemSearchBox);

        _itemCombo = NewCombo(520, 112, 320, Array.Empty<string>());
        _itemCombo.SelectedIndexChanged += (_, _) => RefreshItemEditor();
        AddTip(_itemCombo, "Pick the exact item, vehicle, backpack, or container to override.");
        panel.Controls.Add(_itemCombo);

        _itemAdvanced = NewCheckBox("Advanced", 858, 118, 120);
        _itemAdvanced.CheckedChanged += (_, _) => RefreshItemEditor();
        AddTip(_itemAdvanced, "Show raw Unreal class names and CDO paths for advanced users.");
        panel.Controls.Add(_itemAdvanced);

        var card = NewPanel(14, 30, 150, 930, 330);
        card.BackColor = PanelBack;
        panel.Controls.Add(card);

        _itemClassLabel = MakeLabel("Selected item: -", 28, 24, 850, 30, 14, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack);
        _itemCdoLabel = MakeLabel("CDO path: -", 28, 58, 850, 30, 11, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack);
        card.Controls.Add(_itemClassLabel);
        card.Controls.Add(_itemCdoLabel);

        _itemFields = NewFieldFlow(28, 72, 876, 204);
        _itemFields.AutoScroll = false;
        card.Controls.Add(_itemFields);

        card.Controls.Add(MakeButton("Save Item Override", 28, 280, 190, 42, SaveItemOverride, main: true));
        card.Controls.Add(MakeButton("Clear Item Override", 238, 280, 190, 42, ClearItemOverride));

        _itemCategoryCombo.SelectedIndex = 0;
        return panel;
    }

    private RoundedPanel BuildPresetTab()
    {
        var panel = NewContentPanel();
        panel.Controls.Add(MakeLabel("Presets", 28, 24, 300, 34, 20, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        panel.Controls.Add(MakeLabel("Use presets as quick starting points, then fine tune categories or individual items.", 30, 62, 760, 28, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));

        (string Title, string Description, Action Apply)[] presets =
        {
            ("Lightweight Items", "Set normal item category weight defaults to 0.1.", PresetLightweightItems),
            ("Big Stacks", "Set normal item categories to stack up to 999.", PresetBigStacks),
            ("Huge Containers", "Set container Max Weight to 999999.", PresetHugeContainers),
            ("Huge Vehicles", "Set vehicle Max Weight to 999999.", PresetHugeVehicles),
            ("Backpacks Boosted", "Set backpacks Extra Weight Capacity to 999999.", PresetBackpacksBoosted),
            ("Reset To Game Defaults", "Clear generated UI defaults and overrides.", PresetResetToDefaults)
        };

        for (var i = 0; i < presets.Length; i++)
        {
            var x = 30 + (i % 2) * 465;
            var y = 118 + (i / 2) * 112;
            panel.Controls.Add(NewPresetCard(presets[i].Title, presets[i].Description, x, y, presets[i].Apply));
        }

        return panel;
    }

    private RoundedPanel BuildLogTab()
    {
        var panel = NewContentPanel();
        panel.Controls.Add(MakeLabel("Status Log", 28, 24, 300, 34, 20, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        panel.Controls.Add(MakeLabel("Copy friendly status messages, backup paths, and errors from here.", 30, 62, 760, 28, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));

        var logShell = NewPanel(12, 30, 112, 930, 330);
        logShell.BackColor = PanelBack;
        logShell.FillColor = InnerBack;
        logShell.BorderColor = Border;
        panel.Controls.Add(logShell);

        _log = new RichTextBox
        {
            Left = 14,
            Top = 12,
            Width = 902,
            Height = 306,
            BackColor = InnerBack,
            ForeColor = TextMuted,
            Font = new Font("Consolas", 10.5F, FontStyle.Regular),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true
        };
        logShell.Controls.Add(_log);
        Log("VEIN Item And Container Modifier loaded.");
        return panel;
    }

    private void AutoDetectPaths(bool log)
    {
        var savedGameFolder = _gameFolderBox.Text.Trim();
        var savedModFolder = _modFolderBox.Text.Trim();
        var hasSavedGameFolder = IsValidGameFolder(savedGameFolder);
        var hasSavedModFolder = LuaModService.IsValidModFolder(savedModFolder);

        if (hasSavedGameFolder)
        {
            var modFolder = hasSavedModFolder
                ? savedModFolder
                : LuaModService.DetectModFolder(savedGameFolder) ?? LuaModService.GetExpectedModFolder(savedGameFolder);
            _gameFolderBox.Text = savedGameFolder;
            _modFolderBox.Text = modFolder;

            TryInstallBundledMod(savedGameFolder, modFolder, log);

            if (log) Log("Using saved VEIN path: " + savedGameFolder);
            if (log) Log("Using mod path: " + modFolder);
            SavePathSettings();
            LoadModFromPath(loadExistingState: true);
            UpdateStatuses();
            return;
        }

        var gameFolder = LuaModService.DetectGameFolder();
        if (!string.IsNullOrWhiteSpace(gameFolder))
        {
            _gameFolderBox.Text = gameFolder;
            var modFolder = hasSavedModFolder
                ? savedModFolder
                : LuaModService.DetectModFolder(gameFolder) ?? LuaModService.GetExpectedModFolder(gameFolder);
            _modFolderBox.Text = modFolder;

            TryInstallBundledMod(gameFolder, modFolder, log);

            if (log) Log("Detected VEIN path: " + gameFolder);
            if (log) Log("Using mod path: " + modFolder);
            LoadModFromPath(loadExistingState: true);
        }
        else if (log)
        {
            Log(hasSavedModFolder
                ? "VEIN path was not auto-detected. Keeping the saved mod folder."
                : "VEIN path was not auto-detected. Use Browse.");
        }

        SavePathSettings();
        UpdateStatuses();
    }

    private void BrowseGameFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select the VEIN Steam folder" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _gameFolderBox.Text = dlg.SelectedPath;
        var expected = LuaModService.DetectModFolder(dlg.SelectedPath) ?? LuaModService.GetExpectedModFolder(dlg.SelectedPath);
        _modFolderBox.Text = expected;
        TryInstallBundledMod(dlg.SelectedPath, expected, log: true);
        SavePathSettings();
        LoadModFromPath(loadExistingState: true);
        MarkUnsaved(false);
        Log("Selected VEIN path: " + dlg.SelectedPath);
    }

    private void TryInstallBundledMod(string gameFolder, string modFolder, bool log)
    {
        if (LuaModService.IsValidModFolder(modFolder)) return;
        if (!LuaModService.HasUe4ss(gameFolder))
        {
            if (log) Log("UE4SS was not found yet. Install UE4SS, then Auto Detect will install the bundled mod template.");
            return;
        }

        try
        {
            if (LuaModService.EnsureBundledModInstalled(modFolder) && log)
            {
                Log("Installed bundled ItemAndContainerModifier template: " + modFolder);
            }
        }
        catch (Exception ex)
        {
            if (log) LogError("Could not auto-install bundled mod: " + ex.Message);
        }
    }

    private void LoadPathSettings()
    {
        var path = GetLocalSettingsPath();
        if (path == null || !File.Exists(path)) return;

        try
        {
            var settings = JsonSerializer.Deserialize<LocalPathSettings>(File.ReadAllText(path));
            if (settings == null) return;

            var gameFolder = settings.GameFolder;
            if (!string.IsNullOrWhiteSpace(gameFolder) && IsValidGameFolder(gameFolder))
            {
                _gameFolderBox.Text = gameFolder;
            }

            var modFolder = settings.ModFolder;
            if (!string.IsNullOrWhiteSpace(modFolder) && LuaModService.IsValidModFolder(modFolder))
            {
                _modFolderBox.Text = modFolder;
            }
        }
        catch (Exception ex)
        {
            LogError("Saved path settings could not be loaded. Auto Detect will be used instead. " + ex.Message);
        }
    }

    private void SavePathSettings()
    {
        var path = GetLocalSettingsPath();
        if (path == null) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var settings = new LocalPathSettings
            {
                GameFolder = _gameFolderBox.Text.Trim(),
                ModFolder = _modFolderBox.Text.Trim()
            };
            File.WriteAllText(path, JsonSerializer.Serialize(settings, SettingsJsonOptions));
        }
        catch (Exception ex)
        {
            LogError("Path settings could not be saved, but the app can continue. " + ex.Message);
        }
    }

    private static string? GetLocalSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? null
            : Path.Combine(localAppData, SettingsDirectoryName, SettingsFileName);
    }

    private static bool IsValidGameFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
        return File.Exists(Path.Combine(path, "Vein", "Binaries", "Win64", "Vein-Win64-Test.exe"))
            || File.Exists(Path.Combine(path, "Vein", "Binaries", "Win64", "Vein.exe"))
            || Directory.Exists(Path.Combine(path, "Vein", "Content", "Paks"));
    }

    private void BrowseModFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Select ItemAndContainerModifier folder" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _modFolderBox.Text = dlg.SelectedPath;
        LoadModFromPath(loadExistingState: true);
        SavePathSettings();
        Log("Selected mod folder: " + dlg.SelectedPath);
    }

    private void OpenModFolder()
    {
        var path = _modFolderBox.Text.Trim();
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            Log("Opened mod folder.");
        }
        else
        {
            LogError("Mod folder does not exist.");
        }
    }

    private bool ImportConfigFile(string path, bool markUnsaved)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            LogError("Select or drop a ui_config.lua file first.");
            return false;
        }

        if (!File.Exists(path))
        {
            LogError("Config file does not exist: " + path);
            return false;
        }

        try
        {
            SetImportConfigStatus(path);
            var imported = LuaModService.LoadUiConfigStateFromFile(path);
            _state.CopyFrom(imported);

            if (_modData == null && LuaModService.IsValidModFolder(_modFolderBox.Text.Trim()))
            {
                _modData = LuaModService.LoadModData(_modFolderBox.Text.Trim());
            }

            MarkUnsaved(markUnsaved);
            RefreshCategoryEditor();
            RefreshItemList();
            UpdateStatuses();

            var editCount = _state.CountEdits(_modData);
            Log(markUnsaved
                ? $"Imported {editCount} generated edits into the editor."
                : $"Loaded {editCount} generated edits from the current mod folder.");
            SavePathSettings();
            return true;
        }
        catch (Exception ex)
        {
            LogError("Config import failed: " + ex.Message);
            return false;
        }
    }

    private void InstallConfigFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            LogError("Select or drop a ui_config.lua file first.");
            return;
        }

        if (!File.Exists(path))
        {
            LogError("Config file does not exist: " + path);
            return;
        }

        if (!Path.GetFileName(path).Equals("ui_config.lua", StringComparison.OrdinalIgnoreCase))
        {
            LogError("Drop or install a file named ui_config.lua.");
            return;
        }

        var modFolder = ResolveConfigInstallModFolder();
        if (modFolder == null) return;

        try
        {
            var install = LuaModService.InstallUiConfig(modFolder, path);
            _state.CopyFrom(install.State);
            MarkUnsaved(false);
            SetImportConfigStatus("Installed to Scripts\\ui_config.lua");
            Log("Backup created: " + install.BackupPath);
            Log("Installed ui_config.lua to: " + install.InstalledPath);
            LoadModFromPath(loadExistingState: true);
            SavePathSettings();
        }
        catch (Exception ex)
        {
            LogError("Config install failed: " + ex.Message);
        }
    }

    private string? ResolveConfigInstallModFolder()
    {
        var modFolder = _modFolderBox.Text.Trim();
        if (LuaModService.IsValidModFolder(modFolder)) return modFolder;

        Log("Mod folder was not ready. Trying Auto Detect before installing the dropped config.");
        AutoDetectPaths(log: true);

        modFolder = _modFolderBox.Text.Trim();
        if (LuaModService.IsValidModFolder(modFolder)) return modFolder;

        LogError("Could not find ItemAndContainerModifier automatically. Use Setup > Auto Detect once, then drop ui_config.lua again.");
        return null;
    }

    private void LoadCurrentConfigFile()
    {
        var modFolder = _modFolderBox.Text.Trim();
        if (!LuaModService.IsValidModFolder(modFolder))
        {
            LogError("Select a valid ItemAndContainerModifier folder first.");
            return;
        }

        var path = Path.Combine(modFolder, "Scripts", "ui_config.lua");
        SetImportConfigStatus(path);
        ImportConfigFile(path, markUnsaved: false);
    }

    private void OpenConfigFolder()
    {
        var modFolder = _modFolderBox.Text.Trim();
        var scripts = Path.Combine(modFolder, "Scripts");
        if (!Directory.Exists(scripts))
        {
            LogError("Scripts folder does not exist. Select a valid mod folder first.");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = scripts, UseShellExecute = true });
        Log("Opened config folder.");
    }

    private void ConfigImport_DragEnter(object? sender, DragEventArgs e)
    {
        var canDrop = e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
        e.Effect = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        if (canDrop && _importDropZone != null)
        {
            _importDropZone.BorderColor = PurpleLight;
            _importDropZone.Invalidate();
        }
    }

    private void ConfigImport_DragLeave(object? sender, EventArgs e)
    {
        ResetImportDropZoneBorder();
    }

    private void ConfigImport_DragDrop(object? sender, DragEventArgs e)
    {
        ResetImportDropZoneBorder();
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;

        SetImportConfigStatus(files[0]);
        InstallConfigFile(files[0]);
    }

    private void SetImportConfigStatus(string message)
    {
        if (_importConfigPathLabel == null) return;

        _importConfigPathLabel.Text = string.IsNullOrWhiteSpace(message)
            ? "Waiting for file"
            : message;
    }

    private void WireConfigImportDrop(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += ConfigImport_DragEnter;
        control.DragLeave += ConfigImport_DragLeave;
        control.DragDrop += ConfigImport_DragDrop;
    }

    private void ResetImportDropZoneBorder()
    {
        if (_importDropZone == null) return;

        _importDropZone.BorderColor = Border;
        _importDropZone.Invalidate();
    }

    private void LoadModFromPath(bool loadExistingState = true)
    {
        var path = _modFolderBox.Text.Trim();
        if (!LuaModService.IsValidModFolder(path))
        {
            _modData = null;
            if (loadExistingState) _state.Clear();
            RefreshCategoryEditor();
            RefreshItemList();
            UpdateStatuses();
            Log("Setup needed: select a valid ItemAndContainerModifier folder.");
            return;
        }

        try
        {
            _modData = LuaModService.LoadModData(path);
            if (loadExistingState)
            {
                try
                {
                    _state.CopyFrom(LuaModService.LoadUiConfigState(path));
                    MarkUnsaved(false);
                    var editCount = _state.CountEdits(_modData);
                    if (editCount > 0) Log($"Loaded {editCount} generated UI config edits.");
                }
                catch (Exception ex)
                {
                    _state.Clear();
                    MarkUnsaved(false);
                    LogError("Failed loading generated ui_config.lua: " + ex.Message);
                }
            }
            Log($"Loaded {_modData.Categories.Count} categories and {_modData.ItemCount} entries.");
            foreach (var category in _modData.Categories.Values.Where(category => category.ParseError != null))
            {
                LogError($"{category.Name}: {category.ParseError}");
            }
            RefreshCategoryEditor();
            RefreshItemList();
        }
        catch (Exception ex)
        {
            _modData = null;
            LogError("Failed loading mod: " + ex.Message);
        }
        UpdateStatuses();
    }

    private void SaveConfig()
    {
        _ = TrySaveConfig();
    }

    private bool TrySaveConfig()
    {
        LoadModFromPath(loadExistingState: false);
        var modFolder = _modFolderBox.Text.Trim();
        if (!LuaModService.IsValidModFolder(modFolder))
        {
            LogError("Cannot save. Select a valid ItemAndContainerModifier folder first.");
            return false;
        }

        try
        {
            var backup = LuaModService.CreateBackup(modFolder);
            Log("Backup created: " + backup);
            LuaModService.ApplyConfig(modFolder, _state);
            MarkUnsaved(false);
            SavePathSettings();
            Log("Config saved. Restart VEIN if the game is open.");
            LoadModFromPath(loadExistingState: true);
            return !_hasUnsavedChanges;
        }
        catch (Exception ex)
        {
            LogError("Save failed. Your unsaved changes are still open in the editor. " + ex.Message);
            return false;
        }
    }

    private void BackupNow()
    {
        var modFolder = _modFolderBox.Text.Trim();
        if (!LuaModService.IsValidModFolder(modFolder))
        {
            LogError("Cannot backup. Select the ItemAndContainerModifier folder first.");
            return;
        }

        try
        {
            Log("Backup created: " + LuaModService.CreateBackup(modFolder));
        }
        catch (Exception ex)
        {
            LogError("Backup failed: " + ex.Message);
        }
    }

    private void LaunchVein()
    {
        try
        {
            using var process = LuaModService.LaunchVein(_gameFolderBox.Text.Trim());
            Log(process == null ? "VEIN executable was not found." : "Launched VEIN.");
        }
        catch (Exception ex)
        {
            LogError("Launch failed: " + ex.Message);
        }
    }

    private void SaveCategoryDefault()
    {
        var category = CurrentCategory();
        if (category == null) return;

        var values = new Dictionary<string, LuaValue>(StringComparer.OrdinalIgnoreCase);
        var inputsValid = true;
        inputsValid &= AddCategoryNumber(values, "Weight");
        inputsValid &= AddCategoryNumber(values, "MaxStack", allowNegative: false);
        AddCategoryBool(values, "bStackable");
        inputsValid &= AddCategoryNumber(values, "MaxWeight");
        inputsValid &= AddCategoryNumber(values, "ExtraWeightCapacity");
        inputsValid &= AddCategoryNumber(values, "RunSpeedMultiplier");
        if (!inputsValid) return;

        _state.EnabledCategories[category] = _categoryEnabled.Checked;
        if (values.Count == 0) _state.CategoryDefaults.Remove(category);
        else _state.CategoryDefaults[category] = values;

        MarkUnsaved();
        Log("Saved category defaults in the editor for " + category + ".");
        RefreshCategoryEditor();
        UpdateStatuses();
    }

    private void ResetCategory()
    {
        var category = CurrentCategory();
        if (category == null) return;
        _state.EnabledCategories.Remove(category);
        _state.CategoryDefaults.Remove(category);
        _state.ItemOverrides.Remove(category);
        _state.ContainerWeightOverrides.Remove(category);
        MarkUnsaved();
        Log("Reset generated overrides for " + category + ".");
        RefreshCategoryEditor();
        RefreshItemEditor();
        UpdateStatuses();
    }

    private void SaveItemOverride()
    {
        var item = CurrentItem();
        if (item == null)
        {
            LogError("Pick an item first.");
            return;
        }

        if (CategoryNames.ContainerLike.Contains(item.Category))
        {
            if (_itemInputs.TryGetValue("MaxWeight", out var field) && field.DefaultCheck.Checked)
            {
                if (_state.ContainerWeightOverrides.TryGetValue(item.Category, out var existing)) existing.Remove(item.ClassName);
            }
            else
            {
                if (!ReadItemNumber("MaxWeight", "Max Weight", out var maxWeight, out var hasMaxWeight)) return;
                if (hasMaxWeight) GetContainerOverrides(item.Category)[item.ClassName] = maxWeight;
            }
        }
        else
        {
            var values = new Dictionary<string, LuaValue>(StringComparer.OrdinalIgnoreCase);
            var inputsValid = true;
            inputsValid &= AddItemNumber(values, "Weight", "Weight");
            inputsValid &= AddItemNumber(values, "MaxStack", "Max Stack", allowNegative: false);
            AddItemBool(values, "bStackable");
            inputsValid &= AddItemNumber(values, "ExtraWeightCapacity", "Extra Weight Capacity");
            inputsValid &= AddItemNumber(values, "RunSpeedMultiplier", "Run Speed Multiplier");
            if (!inputsValid) return;

            if (values.Count == 0)
            {
                if (_state.ItemOverrides.TryGetValue(item.Category, out var existing)) existing.Remove(item.ClassName);
            }
            else
            {
                GetItemOverrides(item.Category)[item.ClassName] = values;
            }
        }

        MarkUnsaved();
        Log("Saved item override in the editor for " + item.ClassName + ".");
        RefreshItemEditor();
        UpdateStatuses();
    }

    private void ClearItemOverride()
    {
        var item = CurrentItem();
        if (item == null) return;

        if (CategoryNames.ContainerLike.Contains(item.Category))
        {
            if (_state.ContainerWeightOverrides.TryGetValue(item.Category, out var values)) values.Remove(item.ClassName);
        }
        else if (_state.ItemOverrides.TryGetValue(item.Category, out var items))
        {
            items.Remove(item.ClassName);
        }

        MarkUnsaved();
        Log("Cleared generated override for " + item.ClassName + ".");
        RefreshItemEditor();
        UpdateStatuses();
    }

    private void ClearDynamicFieldControls(Control parent)
    {
        foreach (Control control in parent.Controls.Cast<Control>().ToArray())
        {
            ClearToolTipTree(control);
            control.Dispose();
        }

        parent.Controls.Clear();
    }

    private void ClearToolTipTree(Control control)
    {
        _toolTip.SetToolTip(control, null);
        foreach (Control child in control.Controls)
        {
            ClearToolTipTree(child);
        }
    }

    private void RefreshCategoryEditor()
    {
        if (_categoryCombo == null || _categoryFields == null) return;
        var category = CurrentCategory();
        ClearDynamicFieldControls(_categoryFields);
        _categoryInputs.Clear();

        if (string.IsNullOrWhiteSpace(category))
        {
            AddSetupMessage(_categoryFields, "Select a category to edit defaults.");
            return;
        }

        _loadingUi = true;
        _categoryEnabled.Checked = _state.EnabledCategories.TryGetValue(category, out var enabled)
            ? enabled
            : _modData?.BaseEnabledCategories.TryGetValue(category, out var baseEnabled) == true && baseEnabled;

        var fields = GetFieldsForCategory(category);
        foreach (var field in fields)
        {
            AddCategoryField(field.Key, field.Label, field.Kind);
        }

        if (_state.CategoryDefaults.TryGetValue(category, out var values))
        {
            foreach (var pair in values)
            {
                SetInputValue(_categoryInputs, pair.Key, pair.Value);
            }
        }

        _loadingUi = false;
    }

    private void RefreshItemList()
    {
        if (_itemCombo == null || _itemCategoryCombo == null) return;
        var category = Convert.ToString(_itemCategoryCombo.SelectedItem) ?? CategoryNames.Ordered[0];
        var search = _itemSearchBox?.Text.Trim() ?? "";
        var items = _modData?.Categories.TryGetValue(category, out var data) == true
            ? data.Items
            : new List<CategoryItem>();

        var filtered = items
            .Select(item => new ItemChoice(item.ClassName, FriendlyClassName(item.ClassName)))
            .Where(item => string.IsNullOrWhiteSpace(search)
                || item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.ClassName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Cast<object>()
            .ToArray();

        _loadingUi = true;
        _itemCombo.Items.Clear();
        _itemCombo.Items.AddRange(filtered);
        ConfigureComboDropDown(_itemCombo);
        if (_itemCombo.Items.Count > 0) _itemCombo.SelectedIndex = 0;
        _loadingUi = false;
        RefreshItemEditor();
    }

    private void RefreshItemEditor()
    {
        if (_loadingUi || _itemFields == null) return;
        var item = CurrentItem();
        ClearDynamicFieldControls(_itemFields);
        _itemInputs.Clear();

        if (item == null)
        {
            _itemClassLabel.Text = _modData == null
                ? "Setup needed: select a valid mod folder first."
                : "Selected item: -";
            _itemCdoLabel.Visible = false;
            AddSetupMessage(_itemFields, "Pick a category and item to edit overrides.");
            return;
        }

        _itemClassLabel.Text = _itemAdvanced.Checked ? "Raw class: " + item.ClassName : "Selected item: " + FriendlyClassName(item.ClassName);
        _itemCdoLabel.Text = "CDO path: " + (item.CdoPath ?? "-");
        _itemCdoLabel.Visible = _itemAdvanced.Checked;
        _itemFields.Top = _itemAdvanced.Checked ? 88 : 72;
        _itemFields.Height = _itemAdvanced.Checked ? 188 : 204;

        foreach (var field in GetFieldsForCategory(item.Category))
        {
            AddItemField(field.Key, field.Label, field.Kind);
        }

        if (CategoryNames.ContainerLike.Contains(item.Category))
        {
            if (_state.ContainerWeightOverrides.TryGetValue(item.Category, out var values) && values.TryGetValue(item.ClassName, out var value))
            {
                SetItemInputValue("MaxWeight", value);
            }
        }
        else if (_state.ItemOverrides.TryGetValue(item.Category, out var items) && items.TryGetValue(item.ClassName, out var props))
        {
            foreach (var pair in props)
            {
                SetItemInputValue(pair.Key, pair.Value);
            }
        }
    }

    private void PresetLightweightItems()
    {
        foreach (var category in CategoryNames.ItemLike)
        {
            GetCategoryDefaults(category)["Weight"] = new LuaValue(0.1m);
        }
        MarkUnsaved();
        Log("Preset applied: Lightweight Items.");
        RefreshCategoryEditor();
        UpdateStatuses();
    }

    private void PresetBigStacks()
    {
        foreach (var category in CategoryNames.ItemLike)
        {
            var defaults = GetCategoryDefaults(category);
            defaults["MaxStack"] = new LuaValue(999m);
            defaults["bStackable"] = new LuaValue(true);
        }
        MarkUnsaved();
        Log("Preset applied: Big Stacks.");
        RefreshCategoryEditor();
        UpdateStatuses();
    }

    private void PresetHugeContainers()
    {
        GetCategoryDefaults("containers")["MaxWeight"] = new LuaValue(999999m);
        MarkUnsaved();
        Log("Preset applied: Huge Containers.");
        RefreshCategoryEditor();
        UpdateStatuses();
    }

    private void PresetHugeVehicles()
    {
        GetCategoryDefaults("vehicles")["MaxWeight"] = new LuaValue(999999m);
        MarkUnsaved();
        Log("Preset applied: Huge Vehicles.");
        RefreshCategoryEditor();
        UpdateStatuses();
    }

    private void PresetBackpacksBoosted()
    {
        GetCategoryDefaults("backpacks")["ExtraWeightCapacity"] = new LuaValue(999999m);
        MarkUnsaved();
        Log("Preset applied: Backpacks Boosted.");
        RefreshCategoryEditor();
        UpdateStatuses();
    }

    private void PresetResetToDefaults()
    {
        _state.Clear();
        MarkUnsaved();
        Log("Preset applied: Reset To Game Defaults.");
        RefreshCategoryEditor();
        RefreshItemEditor();
        UpdateStatuses();
    }

    private static bool IsAnyProcessRunning(params string[] processNames)
    {
        foreach (var processName in processNames)
        {
            var processes = Process.GetProcessesByName(processName);
            try
            {
                if (processes.Length > 0) return true;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return false;
    }

    private void UpdateStatuses()
    {
        var gameRunning = IsAnyProcessRunning(
            "Vein-Win64-Test",
            "Vein",
            "Vein-Win64-Shipping");
        _gameStatus.Text = gameRunning ? "Open" : "Closed";
        _gameStatus.ForeColor = gameRunning ? Green : Orange;

        var ue4ssFound = LuaModService.HasUe4ss(_gameFolderBox.Text.Trim());
        _ue4ssStatus.Text = ue4ssFound ? "Found" : "Missing";
        _ue4ssStatus.ForeColor = ue4ssFound ? Green : Orange;

        var modFound = LuaModService.IsValidModFolder(_modFolderBox.Text.Trim());
        _modStatus.Text = modFound ? "Found" : "Missing";
        _modStatus.ForeColor = modFound ? Green : Orange;
    }

    private void MarkUnsaved(bool unsaved = true)
    {
        if (_loadingUi) return;
        _hasUnsavedChanges = unsaved;
        var edits = _state.CountEdits(_modData);
        _unsavedStatus.Text = unsaved
            ? $"Unsaved changes ({edits})"
            : $"No unsaved changes ({edits} edits)";
        _unsavedStatus.ForeColor = unsaved ? Orange : Cyan;
    }

    private string? CurrentCategory() => Convert.ToString(_categoryCombo.SelectedItem);

    private CategoryItem? CurrentItem()
    {
        var category = Convert.ToString(_itemCategoryCombo.SelectedItem);
        var className = _itemCombo.SelectedItem is ItemChoice choice
            ? choice.ClassName
            : Convert.ToString(_itemCombo.SelectedItem);
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(className) || _modData == null) return null;
        return _modData.Categories.TryGetValue(category, out var data)
            ? data.Items.FirstOrDefault(item => item.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static string FriendlyClassName(string className)
    {
        var name = className;
        if (name.StartsWith("BP_", StringComparison.OrdinalIgnoreCase)) name = name[3..];
        if (name.EndsWith("_C", StringComparison.OrdinalIgnoreCase)) name = name[..^2];
        name = name.Replace('_', ' ');
        name = LowerToUpperOrDigitPattern.Replace(name, " ");
        name = AcronymBoundaryPattern.Replace(name, " ");
        name = NumberBoundaryPattern.Replace(name, " ");
        name = WhitespacePattern.Replace(name, " ").Trim();
        return string.IsNullOrWhiteSpace(name) ? className : name;
    }

    private static (string Key, string Label, FieldKind Kind)[] GetFieldsForCategory(string category)
    {
        if (category.Equals("vehicles", StringComparison.OrdinalIgnoreCase) || category.Equals("containers", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { ("MaxWeight", "Max Weight", FieldKind.Number) };
        }

        if (category.Equals("backpacks", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                ("Weight", "Weight", FieldKind.Number),
                ("MaxStack", "Max Stack", FieldKind.Number),
                ("bStackable", "Stackable", FieldKind.Bool),
                ("ExtraWeightCapacity", "Extra Weight Capacity", FieldKind.Number),
                ("RunSpeedMultiplier", "Run Speed Multiplier", FieldKind.Number)
            };
        }

        return new[]
        {
            ("Weight", "Weight", FieldKind.Number),
            ("MaxStack", "Max Stack", FieldKind.Number),
            ("bStackable", "Stackable", FieldKind.Bool)
        };
    }

    private void AddCategoryField(string key, string label, FieldKind kind)
    {
        var field = NewFieldPanel(label, 260, 94);
        field.Margin = new Padding(0, 0, 18, 8);
        Control input = kind == FieldKind.Bool
            ? NewCombo(14, 40, 220, BoolChoices)
            : NewTextBox(14, 40, 220, 36);
        AddTip(input, kind == FieldKind.Bool
            ? $"Choose a category-wide default for {label}, or leave Game Default."
            : $"Enter a category-wide number for {label}, or leave blank for game default.");
        input.TextChanged += (_, _) => MarkUnsaved();
        if (input is ComboBox combo) combo.SelectedIndexChanged += (_, _) => MarkUnsaved();
        field.Controls.Add(input);
        _categoryInputs[key] = input;
        _categoryFields.Controls.Add(field);
    }

    private void AddItemField(string key, string label, FieldKind kind)
    {
        var field = NewFieldPanel(label, 270, 98);
        field.Margin = new Padding(0, 0, 18, 8);
        var check = NewCheckBox("Use Game Default", 50, 38, 170);
        check.Checked = true;
        check.BackColor = InnerBack;
        AddTip(check, "Leave checked to keep the game's value. Uncheck to type a generated override.");
        Control input = kind == FieldKind.Bool
            ? NewCombo(25, 64, 220, BoolChoices)
            : NewTextBox(25, 68, 220, 28);
        AddTip(input, kind == FieldKind.Bool
            ? $"Choose an override for {label}, or keep Game Default."
            : $"Type the {label} override for this one item.");
        SetItemFieldEditMode(field, input, editMode: false);
        check.CheckedChanged += (_, _) =>
        {
            SetItemFieldEditMode(field, input, editMode: !check.Checked);
            _itemFields?.PerformLayout();
            MarkUnsaved();
        };
        if (input is ComboBox combo) combo.SelectedIndexChanged += (_, _) => MarkUnsaved();
        else input.TextChanged += (_, _) => MarkUnsaved();
        field.Controls.Add(check);
        field.Controls.Add(input);
        _itemInputs[key] = (check, input);
        _itemFields.Controls.Add(field);
    }

    private static void SetItemFieldEditMode(Control field, Control input, bool editMode)
    {
        input.Enabled = editMode;
        input.Visible = editMode;
        field.Height = editMode ? 104 : 78;
    }

    private bool AddCategoryNumber(Dictionary<string, LuaValue> values, string key, bool allowNegative = true)
    {
        if (!_categoryInputs.TryGetValue(key, out var input) || input is not TextBox box) return true;
        if (!TryReadOptionalNumber(box.Text, key, out var value, out var hasValue, allowNegative)) return false;
        if (hasValue) values[key] = value;
        return true;
    }

    private void AddCategoryBool(Dictionary<string, LuaValue> values, string key)
    {
        if (!_categoryInputs.TryGetValue(key, out var input) || input is not ComboBox combo) return;
        var value = ReadBoolCombo(combo);
        if (!value.IsNil) values[key] = value;
    }

    private bool AddItemNumber(Dictionary<string, LuaValue> values, string key, string label, bool allowNegative = true)
    {
        if (!ReadItemNumber(key, label, out var value, out var hasValue, allowNegative)) return false;
        if (hasValue) values[key] = value;
        return true;
    }

    private void AddItemBool(Dictionary<string, LuaValue> values, string key)
    {
        if (!_itemInputs.TryGetValue(key, out var field) || field.DefaultCheck.Checked || field.Input is not ComboBox combo) return;
        var value = ReadBoolCombo(combo);
        if (!value.IsNil) values[key] = value;
    }

    private bool ReadItemNumber(string key, string label, out LuaValue value, out bool hasValue, bool allowNegative = true)
    {
        value = LuaValue.Nil;
        hasValue = false;
        if (!_itemInputs.TryGetValue(key, out var field) || field.DefaultCheck.Checked || field.Input is not TextBox box) return true;

        var raw = box.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("nil", StringComparison.OrdinalIgnoreCase))
        {
            LogError(label + " must be a number or check Use Game Default.");
            return false;
        }

        return TryReadOptionalNumber(raw, label, out value, out hasValue, allowNegative);
    }

    private bool TryReadOptionalNumber(string raw, string label, out LuaValue value, out bool hasValue, bool allowNegative = true)
    {
        value = LuaValue.Nil;
        hasValue = false;
        raw = raw.Trim();
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("nil", StringComparison.OrdinalIgnoreCase)) return true;

        if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            LogError(label + " must be a number or blank.");
            return false;
        }

        if (!allowNegative && number < 0)
        {
            LogError(label + " cannot be negative.");
            return false;
        }

        if (number >= 999999) Log(label + " is massive. Allowed for private testing.");
        value = new LuaValue(number);
        hasValue = true;
        return true;
    }

    private static LuaValue ReadBoolCombo(ComboBox combo)
    {
        return combo.SelectedIndex switch
        {
            1 => new LuaValue(true),
            2 => new LuaValue(false),
            _ => LuaValue.Nil
        };
    }

    private static void SetInputValue(Dictionary<string, Control> inputs, string key, LuaValue value)
    {
        if (!inputs.TryGetValue(key, out var input)) return;
        if (input is TextBox box) box.Text = value.ToLua();
        if (input is ComboBox combo && value.Value is bool b) combo.SelectedIndex = b ? 1 : 2;
    }

    private void SetItemInputValue(string key, LuaValue value)
    {
        if (!_itemInputs.TryGetValue(key, out var field)) return;
        field.DefaultCheck.Checked = false;
        SetItemFieldEditMode(field.DefaultCheck.Parent!, field.Input, editMode: true);
        if (field.Input is TextBox box) box.Text = value.ToLua();
        if (field.Input is ComboBox combo && value.Value is bool b) combo.SelectedIndex = b ? 1 : 2;
    }

    private Dictionary<string, LuaValue> GetCategoryDefaults(string category)
    {
        if (!_state.CategoryDefaults.TryGetValue(category, out var values))
        {
            values = new Dictionary<string, LuaValue>(StringComparer.OrdinalIgnoreCase);
            _state.CategoryDefaults[category] = values;
        }
        return values;
    }

    private Dictionary<string, Dictionary<string, LuaValue>> GetItemOverrides(string category)
    {
        if (!_state.ItemOverrides.TryGetValue(category, out var values))
        {
            values = new Dictionary<string, Dictionary<string, LuaValue>>(StringComparer.OrdinalIgnoreCase);
            _state.ItemOverrides[category] = values;
        }
        return values;
    }

    private Dictionary<string, LuaValue> GetContainerOverrides(string category)
    {
        if (!_state.ContainerWeightOverrides.TryGetValue(category, out var values))
        {
            values = new Dictionary<string, LuaValue>(StringComparer.OrdinalIgnoreCase);
            _state.ContainerWeightOverrides[category] = values;
        }
        return values;
    }

    private void Log(string msg)
    {
        if (_log == null) return;
        AppendLog(msg, TextMuted);
    }

    private void LogError(string msg)
    {
        if (_log == null) return;
        AppendLog("ERROR: " + msg, Red);
    }

    private void AppendLog(string msg, Color color)
    {
        var line = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " | " + msg + Environment.NewLine;
        _log.SelectionStart = _log.TextLength;
        _log.SelectionColor = color;
        _log.AppendText(line);
        _log.SelectionColor = _log.ForeColor;
        _log.ScrollToCaret();
    }

    private static RoundedPanel NewContentPanel()
    {
        return NewPanel(12, 0, 54, 1020, 484);
    }

    private static FlowLayoutPanel NewFieldFlow(int x, int y, int w, int h)
    {
        return new FlowLayoutPanel
        {
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            BackColor = PanelBack,
            AutoScroll = true,
            WrapContents = true
        };
    }

    private static RoundedPanel NewFieldPanel(string label, int width, int height)
    {
        var panel = new RoundedPanel
        {
            Width = width,
            Height = height,
            Margin = new Padding(0, 0, 18, 14),
            Radius = 12,
            FillColor = InnerBack,
            BorderColor = BorderSoft,
            BackColor = PanelBack
        };
        panel.Controls.Add(MakeLabel(label, 0, 8, width, 26, 12, FontStyle.Bold, TextMain, ContentAlignment.MiddleCenter, InnerBack));
        return panel;
    }

    private static RoundedPanel NewPresetCard(string title, string description, int x, int y, Action action)
    {
        var card = NewPanel(14, x, y, 430, 92);
        card.BackColor = PanelBack;
        card.Controls.Add(MakeLabel(title, 20, 14, 240, 28, 15, FontStyle.Bold, TextMain, ContentAlignment.MiddleLeft, PanelBack));
        card.Controls.Add(MakeWrappedLabel(description, 20, 44, 276, 38, 10.5F, FontStyle.Regular, TextMuted, PanelBack));
        var button = NewSmallButton("Apply", 318, 26, 86, 40, main: true);
        button.Click += (_, _) => action();
        card.Controls.Add(button);
        return card;
    }

    private static void AddSetupMessage(Control parent, string text)
    {
        parent.Controls.Add(MakeLabel(text, 8, 8, Math.Max(260, parent.Width - 20), 36, 13, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, parent.BackColor));
    }

    private static ThemedTextBox NewTextBox(int x, int y, int w, int h)
    {
        return new ThemedTextBox
        {
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            BackColor = InnerBack,
            ForeColor = TextMain,
            FillColor = InnerBack,
            BorderColor = Border,
            FocusedBorderColor = Purple,
            TextColor = TextMain,
            Radius = 8,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 12F, FontStyle.Regular)
        };
    }

    private static ThemedComboBox NewCombo(int x, int y, int w, IEnumerable<string> items)
    {
        var combo = new ThemedComboBox
        {
            Left = x,
            Top = y,
            Width = w,
            Height = 36,
            BackColor = InnerBack,
            ForeColor = TextMain,
            FillColor = InnerBack,
            BorderColor = Border,
            SelectedColor = Purple,
            TextColor = TextMain,
            MutedColor = TextMuted,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular)
        };
        combo.Items.AddRange(items.Cast<object>().ToArray());
        ConfigureComboDropDown(combo);
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }

    private static void ConfigureComboDropDown(ComboBox combo)
    {
        combo.MaxDropDownItems = Math.Clamp(combo.Items.Count, 1, MaxVisibleComboRows);
        combo.DropDownWidth = combo.Width;
        combo.DropDownHeight = combo.ItemHeight * combo.MaxDropDownItems + 2;
    }

    private static ThemedCheckBox NewCheckBox(string text, int x, int y, int w)
    {
        return new ThemedCheckBox
        {
            Text = text,
            Left = x,
            Top = y,
            Width = w,
            Height = 30,
            ForeColor = TextMuted,
            BackColor = PanelBack,
            FillColor = InnerBack,
            BorderColor = Border,
            CheckedColor = Purple,
            TextColor = TextMuted,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular)
        };
    }

    private static RoundedButton MakeButton(string text, int x, int y, int w, int h, Action action, bool main = false)
    {
        var button = NewSmallButton(text, x, y, w, h, main);
        button.Click += (_, _) => action();
        return button;
    }

    private static RoundedPanel NewPanel(int radius, int x, int y, int w, int h)
    {
        return new RoundedPanel
        {
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            Radius = radius,
            FillColor = PanelBack,
            BorderColor = Border,
            BackColor = AppBack
        };
    }

    private static RoundedPanel NewStatCard(string title, string value, string sub, Color valueColor, int x, int y, int w, int h)
    {
        var panel = NewPanel(12, x, y, w, h);
        const int textLeft = 20;
        const int valueLeft = 16;
        panel.Controls.Add(MakeLabel(title, textLeft, 12, w - 40, 20, 11, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));
        var valueLabel = MakeLabel(value, valueLeft, 28, w - 38, 30, 20, FontStyle.Regular, valueColor, ContentAlignment.MiddleLeft, PanelBack);
        panel.Controls.Add(valueLabel);
        panel.Controls.Add(MakeLabel(sub, textLeft, 58, w - 40, 18, 8.5F, FontStyle.Regular, TextMuted, ContentAlignment.MiddleLeft, PanelBack));
        panel.Tag = valueLabel;
        return panel;
    }

    private static RoundedPanel NewPill(string text, int x, int y, int w, int h, Color color)
    {
        var panel = new RoundedPanel
        {
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            Radius = 12,
            FillColor = color,
            BorderColor = PurpleLight,
            BackColor = PanelBack
        };
        panel.Controls.Add(MakeLabel(text, 0, 0, w, h, 9, FontStyle.Bold, TextMain, ContentAlignment.MiddleCenter, color));
        return panel;
    }

    private static Panel Line(int x, int y, int w)
    {
        return new Panel { Left = x, Top = y, Width = w, Height = 1, BackColor = BorderSoft };
    }

    private static Label MakeLabel(string text, int left, int top, int width, int height, float size, FontStyle style, Color color, ContentAlignment align, Color back)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            BackColor = back,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = align,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
    }

    private static Label MakeWrappedLabel(string text, int left, int top, int width, int height, float size, FontStyle style, Color color, Color back)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            BackColor = back,
            AutoSize = false,
            AutoEllipsis = false,
            TextAlign = ContentAlignment.TopLeft,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
    }

    private static RoundedButton NewSmallButton(string text, int x, int y, int w, int h, bool main = false)
    {
        return new RoundedButton
        {
            Text = text,
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            Radius = 10,
            FillColor = main ? Purple : Color.FromArgb(32, 49, 75),
            HoverColor = main ? PurpleLight : Color.FromArgb(43, 65, 99),
            BorderColor = Color.FromArgb(54, 78, 116),
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TabStop = false,
            FlatStyle = FlatStyle.Flat
        };
    }

    private static RoundedButton NewTabButton(string text, int x, int y, int width)
    {
        return new RoundedButton
        {
            Text = text,
            Left = x,
            Top = y,
            Width = width,
            Height = 44,
            Radius = 14,
            FillColor = InnerBack,
            HoverColor = Color.FromArgb(18, 31, 50),
            BorderColor = BorderSoft,
            ForeColor = TextMain,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
    }

    private static void UseDarkTitleBar(IntPtr handle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;

        var enabled = 1;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, Marshal.SizeOf<int>()) != 0)
        {
            _ = DwmSetWindowAttribute(handle, 19, ref enabled, Marshal.SizeOf<int>());
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static readonly Regex LowerToUpperOrDigitPattern = new(@"(?<=[a-z])(?=[A-Z0-9])", RegexOptions.Compiled);
    private static readonly Regex AcronymBoundaryPattern = new(@"(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);
    private static readonly Regex NumberBoundaryPattern = new(@"(?<=\D)(?=\d)", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    private sealed class LocalPathSettings
    {
        public string? GameFolder { get; set; }
        public string? ModFolder { get; set; }
    }

    private enum FieldKind
    {
        Number,
        Bool
    }

    private sealed record ItemChoice(string ClassName, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
