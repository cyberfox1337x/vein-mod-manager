using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace VEIN_Item_And_Container_Modifier;

public sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 14;
    public Color FillColor { get; set; } = Color.FromArgb(10, 18, 30);
    public Color BorderColor { get; set; } = Color.FromArgb(32, 53, 82);

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        using var path = RoundedRect(new Rectangle(1, 1, Width - 3, Height - 3), Radius);
        using var fill = new SolidBrush(FillColor);
        using var pen = new Pen(BorderColor, 1f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }

    internal static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class RoundedButton : Button
{
    public int Radius { get; set; } = 10;
    public Color FillColor { get; set; } = Color.FromArgb(32, 49, 75);
    public Color HoverColor { get; set; } = Color.FromArgb(43, 65, 99);
    public Color BorderColor { get; set; } = Color.FromArgb(54, 78, 116);
    private bool _hover;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.FromArgb(32, 49, 75);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackColor);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPanel.RoundedRect(rect, Radius);
        using var fill = new SolidBrush(_hover ? HoverColor : FillColor);
        using var pen = new Pen(BorderColor, 1f);
        graphics.FillPath(fill, path);
        graphics.DrawPath(pen, path);
        TextRenderer.DrawText(graphics, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

public sealed partial class ThemedTextBox : TextBox
{
    private const int WmPaint = 0x000F;
    private const int WmNcPaint = 0x0085;
    private const int EmSetMargins = 0x00D3;
    private const int EcLeftMargin = 0x0001;
    private const int EcRightMargin = 0x0002;

    public Color FillColor { get; set; } = Color.FromArgb(3, 11, 20);
    public Color BorderColor { get; set; } = Color.FromArgb(54, 78, 116);
    public Color FocusedBorderColor { get; set; } = Color.FromArgb(126, 58, 242);
    public Color TextColor { get; set; } = Color.White;
    public int Radius { get; set; } = 8;

    public ThemedTextBox()
    {
        AutoSize = false;
        BorderStyle = BorderStyle.None;
        BackColor = FillColor;
        ForeColor = TextColor;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTextMargins();
        UpdateRoundedRegion();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRoundedRegion();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is WmPaint or WmNcPaint)
        {
            PaintChrome();
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    private void PaintChrome()
    {
        var hdc = GetWindowDC(Handle);
        if (hdc == IntPtr.Zero) return;

        try
        {
            using var graphics = Graphics.FromHdc(hdc);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var border = new Pen(Focused ? FocusedBorderColor : BorderColor, 1f);
            using var path = RoundedPanel.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
            graphics.DrawPath(border, path);
        }
        finally
        {
            _ = ReleaseDC(Handle, hdc);
        }
    }

    private void UpdateRoundedRegion()
    {
        if (Width <= 0 || Height <= 0) return;

        using var path = RoundedPanel.RoundedRect(new Rectangle(0, 0, Width, Height), Radius);
        var previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }

    private void ApplyTextMargins()
    {
        if (!IsHandleCreated) return;

        var margin = 10;
        SendMessage(Handle, EmSetMargins, (IntPtr)(EcLeftMargin | EcRightMargin), (IntPtr)((margin << 16) | margin));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr handle, IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
}

public sealed partial class ThemedComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmNcPaint = 0x0085;
    private SmoothComboListHook? _listHook;

    public Color FillColor { get; set; } = Color.FromArgb(3, 11, 20);
    public Color BorderColor { get; set; } = Color.FromArgb(54, 78, 116);
    public Color SelectedColor { get; set; } = Color.FromArgb(126, 58, 242);
    public Color TextColor { get; set; } = Color.White;
    public Color MutedColor { get; set; } = Color.FromArgb(177, 207, 242);

    public ThemedComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = FillColor;
        ForeColor = TextColor;
        ItemHeight = 22;
        IntegralHeight = false;
        MaxDropDownItems = 40;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDarkThemeToNativeWindows();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;

        var isEditArea = e.State.HasFlag(DrawItemState.ComboBoxEdit);
        var isSelected = e.State.HasFlag(DrawItemState.Selected) && !isEditArea;
        var fillColor = isSelected ? SelectedColor : FillColor;
        var text = e.Index >= 0 ? Convert.ToString(Items[e.Index]) ?? string.Empty : Text;

        using (var fill = new SolidBrush(fillColor))
        {
            e.Graphics.FillRectangle(fill, e.Bounds);
        }

        var textBounds = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            text,
            Font,
            textBounds,
            TextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is WmPaint or WmNcPaint)
        {
            PaintChrome();
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnDropDown(EventArgs e)
    {
        base.OnDropDown(e);
        HookNativeList(ApplyDarkThemeToNativeWindows());
        BeginInvoke(PaintChrome);
    }

    protected override void OnDropDownClosed(EventArgs e)
    {
        base.OnDropDownClosed(e);
        _listHook?.Release();
        BeginInvoke(PaintChrome);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _listHook?.Release();
            _listHook = null;
        }

        base.Dispose(disposing);
    }

    private void PaintChrome()
    {
        var hdc = GetWindowDC(Handle);
        if (hdc == IntPtr.Zero) return;

        try
        {
            using var graphics = Graphics.FromHdc(hdc);
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            var arrowBounds = new Rectangle(Math.Max(0, Width - 28), 1, 27, Math.Max(0, Height - 2));

            using (var fill = new SolidBrush(FillColor))
            using (var arrowFill = new SolidBrush(Color.FromArgb(14, 24, 40)))
            using (var border = new Pen(Focused ? SelectedColor : BorderColor, 1f))
            using (var divider = new Pen(BorderColor, 1f))
            {
                graphics.FillRectangle(fill, bounds);
                graphics.FillRectangle(arrowFill, arrowBounds);
                graphics.DrawLine(divider, arrowBounds.Left, 3, arrowBounds.Left, Height - 4);
                graphics.DrawRectangle(border, bounds);
            }

            var textBounds = new Rectangle(7, 1, Math.Max(0, Width - 38), Math.Max(0, Height - 2));
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textBounds,
                TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            DrawChevron(graphics, arrowBounds);
        }
        finally
        {
            _ = ReleaseDC(Handle, hdc);
        }
    }

    private IntPtr ApplyDarkThemeToNativeWindows()
    {
        if (!IsHandleCreated) return IntPtr.Zero;

        _ = SetWindowTheme(Handle, "DarkMode_Explorer", null);

        var info = new ComboBoxInfo
        {
            cbSize = Marshal.SizeOf<ComboBoxInfo>()
        };

        if (!GetComboBoxInfo(Handle, ref info)) return IntPtr.Zero;
        if (info.hwndCombo != IntPtr.Zero) _ = SetWindowTheme(info.hwndCombo, "DarkMode_Explorer", null);
        if (info.hwndEdit != IntPtr.Zero) _ = SetWindowTheme(info.hwndEdit, "DarkMode_Explorer", null);
        if (info.hwndList != IntPtr.Zero) _ = SetWindowTheme(info.hwndList, "DarkMode_Explorer", null);
        return info.hwndList;
    }

    private void HookNativeList(IntPtr listHandle)
    {
        if (listHandle == IntPtr.Zero) return;
        _listHook ??= new SmoothComboListHook();
        _listHook.Assign(listHandle);
    }

    private void DrawChevron(Graphics graphics, Rectangle bounds)
    {
        var centerX = bounds.Left + bounds.Width / 2;
        var centerY = bounds.Top + bounds.Height / 2 + 1;
        using var pen = new Pen(MutedColor, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawLine(pen, centerX - 5, centerY - 2, centerX, centerY + 3);
        graphics.DrawLine(pen, centerX, centerY + 3, centerX + 5, centerY - 2);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr handle, IntPtr hdc);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetComboBoxInfo(IntPtr hwndCombo, ref ComboBoxInfo info);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    [StructLayout(LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int cbSize;
        public Rect rcItem;
        public Rect rcButton;
        public int stateButton;
        public IntPtr hwndCombo;
        public IntPtr hwndEdit;
        public IntPtr hwndList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private sealed partial class SmoothComboListHook : NativeWindow
    {
        private const int WmMouseWheel = 0x020A;
        private const int LbGetTopIndex = 0x018E;
        private const int LbSetTopIndex = 0x0197;

        public void Assign(IntPtr handle)
        {
            if (Handle == handle) return;
            Release();
            AssignHandle(handle);
        }

        public void Release()
        {
            if (Handle != IntPtr.Zero) ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseWheel)
            {
                var delta = unchecked((short)((m.WParam.ToInt64() >> 16) & 0xFFFF));
                var direction = Math.Sign(delta);
                if (direction != 0)
                {
                    var currentTop = (int)SendMessage(Handle, LbGetTopIndex, IntPtr.Zero, IntPtr.Zero);
                    var nextTop = Math.Max(0, currentTop - direction);
                    _ = SendMessage(Handle, LbSetTopIndex, (IntPtr)nextTop, IntPtr.Zero);
                    return;
                }
            }

            base.WndProc(ref m);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}

public sealed class ThemedCheckBox : CheckBox
{
    public Color FillColor { get; set; } = Color.FromArgb(3, 11, 20);
    public Color BorderColor { get; set; } = Color.FromArgb(54, 78, 116);
    public Color CheckedColor { get; set; } = Color.FromArgb(126, 58, 242);
    public Color CheckColor { get; set; } = Color.White;
    public Color TextColor { get; set; } = Color.FromArgb(177, 207, 242);
    private bool _hover;

    public ThemedCheckBox()
    {
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        var boxSize = 16;
        var boxY = (Height - boxSize) / 2;
        var box = new Rectangle(0, boxY, boxSize, boxSize);
        var radius = 4;

        using (var path = RoundedPanel.RoundedRect(box, radius))
        using (var fill = new SolidBrush(Checked ? CheckedColor : FillColor))
        using (var border = new Pen(Checked ? CheckedColor : (_hover ? Color.FromArgb(78, 114, 166) : BorderColor), 1.2f))
        {
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        if (Checked)
        {
            using var pen = new Pen(CheckColor, 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            e.Graphics.DrawLine(pen, 4, boxY + 8, 7, boxY + 11);
            e.Graphics.DrawLine(pen, 7, boxY + 11, 12, boxY + 5);
        }

        var textBounds = new Rectangle(box.Right + 8, 0, Math.Max(0, Width - box.Right - 8), Height);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            TextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

public sealed class ToggleSwitch : Control
{
    private bool _checked;
    private bool _hover;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Color OnColor { get; set; } = Color.FromArgb(126, 58, 242);
    public Color OnColor2 { get; set; } = Color.FromArgb(21, 220, 210);
    public Color OffColor { get; set; } = Color.FromArgb(14, 24, 40);
    public Color OffColor2 { get; set; } = Color.FromArgb(28, 44, 70);
    public Color KnobColor { get; set; } = Color.White;
    public event EventHandler? CheckedChanged;

    public ToggleSwitch()
    {
        Cursor = Cursors.Hand;
        BackColor = Color.FromArgb(3, 11, 20);
        MinimumSize = new Size(64, 28);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        var track = new Rectangle(1, 3, Width - 3, Height - 7);
        var borderColor = Checked
            ? Color.FromArgb(_hover ? 235 : 205, OnColor2)
            : Color.FromArgb(_hover ? 210 : 150, OffColor2);
        using (var path = RoundedPanel.RoundedRect(track, track.Height / 2))
        using (var fill = new LinearGradientBrush(track, Checked ? OnColor : OffColor, Checked ? OnColor2 : OffColor2, LinearGradientMode.Horizontal))
        using (var border = new Pen(borderColor, 1.2f))
        using (var shine = new Pen(Color.FromArgb(Checked ? 55 : 28, Color.White), 1f))
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(border, path);

            var shineY = track.Top + 5;
            graphics.DrawLine(shine, track.Left + track.Height / 2, shineY, track.Right - track.Height / 2, shineY);
        }

        var knob = Math.Max(18, Height - 12);
        var knobX = Checked ? Width - knob - 7 : 7;
        var knobY = (Height - knob) / 2;

        using (var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
        {
            graphics.FillEllipse(shadow, knobX + 1, knobY + 2, knob, knob);
        }

        var knobBounds = new Rectangle(knobX, knobY, knob, knob);
        using var knobBrush = new LinearGradientBrush(knobBounds, Color.White, Color.FromArgb(200, 222, 250), LinearGradientMode.Vertical);
        using var knobBorder = new Pen(Color.FromArgb(195, 218, 244), 1f);
        graphics.FillEllipse(knobBrush, knobBounds);
        graphics.DrawEllipse(knobBorder, knobBounds);

        var glintBounds = new Rectangle(knobX + 5, knobY + 4, Math.Max(4, knob / 3), Math.Max(3, knob / 4));
        using var glint = new SolidBrush(Color.FromArgb(135, Color.White));
        graphics.FillEllipse(glint, glintBounds);
    }
}
