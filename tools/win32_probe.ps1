#==================================================================
#  win32_probe.ps1 - Shared Win32 helpers for probing PowerCalc
#==================================================================
# Dot-source this file. Provides window-tree dumps, menu-command
# posting, and window screenshots for the live PowerCalc process.

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Win
{
	public delegate bool EnumProc(IntPtr hwnd, IntPtr lparam);

	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left, Top, Right, Bottom;
	}

	[DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr lparam);
	[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lparam);
	[DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
	[DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
	[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hwnd, StringBuilder s, int n);
	[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hwnd, StringBuilder s, int n);
	[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, StringBuilder l);
	[DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
	[DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
	[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
	[DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr hwnd);
	[DllImport("user32.dll")] public static extern int GetWindowLongW(IntPtr hwnd, int index);
	[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
	[DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hwnd);
	[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
	[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
	[DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr hwnd);

	public static List<IntPtr> Children(IntPtr parent)
	{
		var list = new List<IntPtr>();
		EnumChildWindows(parent, (h, l) => { if (GetParent(h) == parent) list.Add(h); return true; }, IntPtr.Zero);
		return list;
	}

	public static List<IntPtr> TopWindowsOf(uint pid)
	{
		var list = new List<IntPtr>();
		EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
		return list;
	}

	public static string Text(IntPtr hwnd)
	{
		// WM_GETTEXT reads control text across processes; GetWindowText does not
		var sb = new StringBuilder(1024);
		SendMessageW(hwnd, 0x000D, (IntPtr)1024, sb);
		return sb.ToString();
	}

	public static string ClassOf(IntPtr hwnd)
	{
		var sb = new StringBuilder(256);
		GetClassNameW(hwnd, sb, 256);
		return sb.ToString();
	}
}
"@

$WM_COMMAND = 0x0111
$WM_CLOSE = 0x0010
$BM_CLICK = 0x00F5

function Get-PowerCalc
{
	$proc = Get-Process -Name PowerCalc -ErrorAction SilentlyContinue | Select-Object -First 1
	if (-not $proc)
	{
		throw "PowerCalc is not running"
	}
	return $proc
}

function Send-MenuCommand([IntPtr]$hwnd, [int]$id)
{
	# Same message a menu click produces; posted so a modal dialog cannot block us
	[void][Win]::PostMessageW($hwnd, $WM_COMMAND, [IntPtr]$id, [IntPtr]::Zero)
}

function Format-Window([IntPtr]$hwnd, [int]$depth, [System.Collections.Generic.List[string]]$lines)
{
	$r = New-Object Win+RECT
	[void][Win]::GetWindowRect($hwnd, [ref]$r)
	$style = [Win]::GetWindowLongW($hwnd, -16)
	$text = [Win]::Text($hwnd) -replace "`r?`n", "\n"
	if ($text.Length -gt 80)
	{
		$text = $text.Substring(0, 80) + "..."
	}
	$flags = @()
	if (-not [Win]::IsWindowVisible($hwnd))
	{
		$flags += "hidden"
	}
	if (-not [Win]::IsWindowEnabled($hwnd))
	{
		$flags += "disabled"
	}
	$indent = "    " * $depth
	$line = "{0}{1} id={2} '{3}' {4}x{5} @({6},{7}) style=0x{8:X8}" -f $indent, [Win]::ClassOf($hwnd), [Win]::GetDlgCtrlID($hwnd), $text, ($r.Right - $r.Left), ($r.Bottom - $r.Top), $r.Left, $r.Top, $style
	if ($flags.Count -gt 0)
	{
		$line += " [" + ($flags -join ",") + "]"
	}
	$lines.Add($line)
	foreach ($child in [Win]::Children($hwnd))
	{
		Format-Window $child ($depth + 1) $lines
	}
}

function Get-WindowTree([IntPtr]$hwnd)
{
	$lines = New-Object System.Collections.Generic.List[string]
	Format-Window $hwnd 0 $lines
	return $lines
}

function Save-WindowShot([IntPtr]$hwnd, [string]$path)
{
	$r = New-Object Win+RECT
	[void][Win]::GetWindowRect($hwnd, [ref]$r)
	$bmp = New-Object System.Drawing.Bitmap(($r.Right - $r.Left), ($r.Bottom - $r.Top))
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$hdc = $g.GetHdc()
	[void][Win]::PrintWindow($hwnd, $hdc, 2)    # PW_RENDERFULLCONTENT
	$g.ReleaseHdc($hdc)
	$g.Dispose()
	$dir = Split-Path -Parent $path
	if ($dir -and -not (Test-Path $dir))
	{
		New-Item -ItemType Directory -Force $dir | Out-Null
	}
	$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
	$bmp.Dispose()
}
