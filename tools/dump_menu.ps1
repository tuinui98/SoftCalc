#==================================================================
#  dump_menu.ps1 - Read-only dump of the running PowerCalc menu tree
#==================================================================
# Reads the menu bar of the live PowerCalc window through Win32 menu
# APIs. Never sends messages, clicks, or keystrokes to the target.
#
# Usage: .\tools\dump_menu.ps1 [-OutFile spec\inventory\menu_tree.txt] [-Hwnd <window>]
# -Hwnd dumps another PowerCalc window (e.g. the Graph window) instead
# of the main window.

param
(
	[string]$OutFile = "",
	[long]$Hwnd = 0
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class MenuApi
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct MENUITEMINFOW
	{
		public uint cbSize;
		public uint fMask;
		public uint fType;
		public uint fState;
		public uint wID;
		public IntPtr hSubMenu;
		public IntPtr hbmpChecked;
		public IntPtr hbmpUnchecked;
		public IntPtr dwItemData;
		public IntPtr dwTypeData;
		public uint cch;
		public IntPtr hbmpItem;
	}

	[DllImport("user32.dll")]
	public static extern IntPtr GetMenu(IntPtr hwnd);

	[DllImport("user32.dll")]
	public static extern int GetMenuItemCount(IntPtr menu);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern bool GetMenuItemInfoW(IntPtr menu, uint item, bool by_pos, ref MENUITEMINFOW info);
}
"@

$MIIM_STATE   = 0x0001
$MIIM_ID      = 0x0002
$MIIM_SUBMENU = 0x0004
$MIIM_STRING  = 0x0040
$MIIM_FTYPE   = 0x0100
$MFT_SEPARATOR  = 0x0800
$MFT_RADIOCHECK = 0x0200
$MFS_GRAYED  = 0x0003
$MFS_CHECKED = 0x0008
$MFS_DEFAULT = 0x1000

function Get-ItemInfo([IntPtr]$menu, [int]$pos)
{
	$info = New-Object MenuApi+MENUITEMINFOW
	$info.cbSize = [Runtime.InteropServices.Marshal]::SizeOf($info)
	$info.fMask = $MIIM_STATE -bor $MIIM_ID -bor $MIIM_SUBMENU -bor $MIIM_STRING -bor $MIIM_FTYPE
	[void][MenuApi]::GetMenuItemInfoW($menu, $pos, $true, [ref]$info)

	$text = ""
	if ($info.cch -gt 0)
	{
		$buf = [Runtime.InteropServices.Marshal]::AllocHGlobal(($info.cch + 1) * 2)
		try
		{
			$info.dwTypeData = $buf
			$info.cch = $info.cch + 1
			$info.fMask = $MIIM_STRING
			[void][MenuApi]::GetMenuItemInfoW($menu, $pos, $true, [ref]$info)
			$text = [Runtime.InteropServices.Marshal]::PtrToStringUni($buf)
		}
		finally
		{
			[Runtime.InteropServices.Marshal]::FreeHGlobal($buf)
		}
		$info.fMask = $MIIM_STATE -bor $MIIM_ID -bor $MIIM_SUBMENU -bor $MIIM_FTYPE
		$info.dwTypeData = [IntPtr]::Zero
		$info.cch = 0
		[void][MenuApi]::GetMenuItemInfoW($menu, $pos, $true, [ref]$info)
	}
	return @{ Info = $info; Text = $text }
}

function Write-Menu([IntPtr]$menu, [int]$depth, [System.Collections.Generic.List[string]]$lines)
{
	$count = [MenuApi]::GetMenuItemCount($menu)
	for ($pos = 0; $pos -lt $count; $pos++)
	{
		$item = Get-ItemInfo $menu $pos
		$info = $item.Info
		$indent = "    " * $depth

		if ($info.fType -band $MFT_SEPARATOR)
		{
			$lines.Add("$indent----")
			continue
		}

		# Menu text uses TAB to separate the label from its accelerator hint
		$parts = $item.Text -split "`t", 2
		$label = $parts[0]
		$flags = @()
		if ($parts.Count -gt 1)
		{
			$flags += "key=" + $parts[1]
		}
		if (($info.fState -band $MFS_GRAYED) -ne 0)
		{
			$flags += "grayed"
		}
		if (($info.fState -band $MFS_CHECKED) -ne 0)
		{
			$flags += $(if ($info.fType -band $MFT_RADIOCHECK) { "radio-on" } else { "checked" })
		}
		if (($info.fState -band $MFS_DEFAULT) -ne 0)
		{
			$flags += "default"
		}
		if ($info.hSubMenu -eq [IntPtr]::Zero)
		{
			$flags += "id=$($info.wID)"
		}

		$suffix = ""
		if ($flags.Count -gt 0)
		{
			$suffix = "  [" + ($flags -join ", ") + "]"
		}
		$lines.Add("$indent$label$suffix")

		if ($info.hSubMenu -ne [IntPtr]::Zero)
		{
			Write-Menu $info.hSubMenu ($depth + 1) $lines
		}
	}
}

#==================================================================
#  BLOCK 1 - Locate target : find the live PowerCalc window
#==================================================================
$proc = Get-Process -Name PowerCalc -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc)
{
	Write-Error "PowerCalc is not running"
	exit 1
}
$target = $proc.MainWindowHandle
if ($Hwnd -ne 0)
{
	$target = [IntPtr]$Hwnd
}
$menu_bar = [MenuApi]::GetMenu($target)
if ($menu_bar -eq [IntPtr]::Zero)
{
	Write-Error "Window has no menu bar"
	exit 1
}

#==================================================================
#  BLOCK 2 - Walk and emit : recursive dump of every menu level
#==================================================================
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# PowerCalc menu tree")
$lines.Add("# captured: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + "  window: '" + $proc.MainWindowTitle + "'")
$lines.Add("# '&' marks the Alt access key; key= is the accelerator hint")
$lines.Add("")
Write-Menu $menu_bar 0 $lines

if ($OutFile)
{
	$dir = Split-Path -Parent $OutFile
	if ($dir -and -not (Test-Path $dir))
	{
		New-Item -ItemType Directory -Force $dir | Out-Null
	}
	$lines | Out-File -FilePath $OutFile -Encoding utf8
	"Wrote $($lines.Count) lines to $OutFile"
}
else
{
	$lines
}
