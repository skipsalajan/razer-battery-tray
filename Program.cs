using System.ComponentModel;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using HidSharp;
using Microsoft.Win32.SafeHandles;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Only allow one copy of the app at a time.
        using var mutex = new Mutex(true, "RazerBatteryTray_SingleInstance", out bool isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}

internal sealed class TrayContext : ApplicationContext
{
    private const int RefreshMinutes = 5;

    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private Icon? _currentIcon;
    private bool _busy;

    public TrayContext()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Refresh", null, (_, _) => RefreshBattery());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) RefreshBattery();
        };

        SetState(null);

        _timer = new System.Windows.Forms.Timer { Interval = RefreshMinutes * 60 * 1000 };
        _timer.Tick += (_, _) => RefreshBattery();
        _timer.Start();

        RefreshBattery();
    }

    private async void RefreshBattery()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            int? level = await Task.Run(BatteryReader.TryRead);
            SetState(level); // null => "Unavailable", never a stale number
        }
        finally
        {
            _busy = false;
        }
    }

    private void SetState(int? level)
    {
        _tray.Text = level is int p
            ? $"DeathAdder V2 X: {p}%"
            : "DeathAdder V2 X: Unavailable";

        Icon? old = _currentIcon;
        _currentIcon = IconFactory.Create(level);
        _tray.Icon = _currentIcon;
        old?.Dispose();
    }

    private void ExitApp()
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _currentIcon?.Dispose();
        ExitThread();
    }
}

internal static class IconFactory
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(int? level)
    {
        Color background = level switch
        {
            null => Color.FromArgb(110, 110, 110),   // unavailable: grey
            <= 15 => Color.FromArgb(200, 40, 40),    // low: red
            <= 30 => Color.FromArgb(200, 130, 0),    // getting low: amber
            _ => Color.FromArgb(0, 140, 70),         // fine: green
        };

        string text = level?.ToString() ?? "?";

        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var brush = new SolidBrush(background);
            g.FillRectangle(brush, 0, 0, size, size);

            // Typographic format = no built-in padding and no wrapping.
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
            };

            // Start big and shrink until the text fits the square with a 1px margin.
            float fontSize = 30f;
            Font font = new("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            while (fontSize > 8f)
            {
                SizeF measured = g.MeasureString(text, font, PointF.Empty, format);
                if (measured.Width <= size - 2 && measured.Height <= size - 2) break;
                font.Dispose();
                fontSize -= 1f;
                font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            }

            g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, size, size), format);
            font.Dispose();
        }

        IntPtr handle = bmp.GetHicon();
        Icon icon = (Icon)Icon.FromHandle(handle).Clone();
        DestroyIcon(handle);
        return icon;
    }
}

internal static class BatteryReader
{
    private const int VendorId = 0x1532;
    private const int ProductId = 0x009C;
    private const int ReportLength = 91;
    private const byte TransactionId = 0x1F;

    // Windows buffer: byte 0 is the HID report ID, so Razer fields sit one position later.
    private const int Status = 1, Tid = 2, Protocol = 5, DataSize = 6,
                      CmdClass = 7, CmdId = 8, Args = 9, Crc = 89;

    // Returns the battery percentage, or null if no trustworthy reading is available.
    public static int? TryRead()
    {
        try
        {
            var device = DeviceList.Local
                .GetHidDevices(VendorId, ProductId)
                .FirstOrDefault(d =>
                    d.DevicePath.Contains("mi_00", StringComparison.OrdinalIgnoreCase) &&
                    d.GetMaxFeatureReportLength() == ReportLength);

            if (device is null) return null;

            using var handle = NativeMethods.CreateFile(
                device.DevicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) return null;

            byte[] request = new byte[ReportLength];
            request[Tid] = TransactionId;
            request[Protocol] = 0x00;
            request[DataSize] = 0x02;
            request[CmdClass] = 0x07;
            request[CmdId] = 0x80;
            request[Crc] = Checksum(request);

            if (!NativeMethods.HidD_SetFeature(handle, request, request.Length))
                return null;

            byte[] reply = new byte[ReportLength];
            for (int attempt = 1; attempt <= 6; attempt++)
            {
                Thread.Sleep(100);
                Array.Clear(reply);

                if (!NativeMethods.HidD_GetFeature(handle, reply, reply.Length))
                    return null;

                if (reply[Status] != 0x01) break; // 0x01 = busy, keep waiting
            }

            if (reply[Status] != 0x02) return null;
            if (reply[CmdClass] != 0x07 || reply[CmdId] != 0x80) return null;
            if (reply[Crc] != Checksum(reply)) return null;

            int raw = reply[Args + 1];
            if (raw == 0) return null; // 0 is treated as "no real reading" (mouse asleep/off)

            return (int)Math.Round(raw * 100.0 / 255);
        }
        catch
        {
            return null;
        }
    }

    private static byte Checksum(byte[] report)
    {
        byte result = 0;
        for (int i = 3; i <= 88; i++)
            result ^= report[i];
        return result;
    }
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition,
        uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_SetFeature(
        SafeFileHandle handle, [In] byte[] reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetFeature(
        SafeFileHandle handle, [In, Out] byte[] reportBuffer, int reportBufferLength);
}