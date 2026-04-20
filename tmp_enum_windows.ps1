Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

public static class W
{
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int c);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int c);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }

    public class Info { public IntPtr Hwnd; public string Title; public string Class; public uint Pid; public bool Topmost; public int X,Y,Width,Height; public int ZOrder; }

    public static List<Info> List()
    {
        var result = new List<Info>();
        int z = 0;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            var t = new StringBuilder(256); GetWindowText(h, t, 256);
            var c = new StringBuilder(256); GetClassName(h, c, 256);
            uint pid; GetWindowThreadProcessId(h, out pid);
            int ex = GetWindowLong(h, -20);
            RECT r; GetWindowRect(h, out r);
            int w = r.R - r.L, hgt = r.B - r.T;
            if (w <= 0 || hgt <= 0) return true;
            result.Add(new Info {
                Hwnd = h,
                Title = t.ToString(),
                Class = c.ToString(),
                Pid = pid,
                Topmost = (ex & 0x8) != 0,
                X = r.L, Y = r.T, Width = w, Height = hgt,
                ZOrder = z++
            });
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
'@

$items = [W]::List()
$rows = foreach ($i in $items) {
    $proc = ''
    try { $proc = (Get-Process -Id $i.Pid -ErrorAction SilentlyContinue).ProcessName } catch { }
    [pscustomobject]@{
        Z        = $i.ZOrder
        Proc     = $proc
        Title    = if ([string]::IsNullOrEmpty($i.Title)) { '(no title)' } else { $i.Title }
        Class    = $i.Class
        Topmost  = $i.Topmost
        Pos      = "$($i.X),$($i.Y) $($i.Width)x$($i.Height)"
        Hwnd     = ('0x{0:X}' -f [int64]$i.Hwnd)
    }
}
$rows | Where-Object { $_.Proc -match 'AutoTimer|ApplicationFrameHost|spacedesk' } | Format-Table -AutoSize -Wrap
