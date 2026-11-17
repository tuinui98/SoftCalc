// page.ts - Page-wide helpers for both mockups: wide layout query and the theme

// The same query as layout.css: Sci beside Basic instead of from the side
export const WIDE = "(min-width: 900px) and (orientation: landscape)";

const DARK = matchMedia("(prefers-color-scheme: dark)");

export function loadTheme(): string
{
	try
	{
		return localStorage.getItem("softcalc-theme") ?? "system";
	}
	catch
	{
		return "system";
	}
}

export function saveTheme(value: string)
{
	try
	{
		localStorage.setItem("softcalc-theme", value);
	}
	catch
	{
		// private mode: the theme just is not remembered
	}
}

// "dark" for our CSS, "ion-palette-dark" for Ionic's own colours
export function applyTheme(value: string)
{
	const dark = value === "dark" || (value === "system" && DARK.matches);
	document.documentElement.classList.toggle("dark", dark);
	document.documentElement.classList.toggle("ion-palette-dark", dark);
	DARK.onchange = () => applyTheme(loadTheme());
}
