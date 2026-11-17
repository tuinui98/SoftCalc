// page.ts - Page-wide helpers for both mockups: wide layout query and the theme

// The same queries as layout.css. WIDE: Sci beside Basic. SIDE: History left, keys right (a phone
// on its side), Sci from the side over the keys. Owner 2026-11-18: a phone on its side was too flat.
export const WIDE = "(orientation: landscape) and (min-width: 900px) and (min-height: 600px)";
export const SIDE = "(orientation: landscape) and (max-height: 599px)";

// The Sci panel covers exactly the keys box, so History and Input stay in view (owner 2026-11-18)
export function trackKeys(box: HTMLElement): () => void
{
	const update = () =>
	{
		const r = box.getBoundingClientRect();
		const style = document.documentElement.style;
		style.setProperty("--keys-top", `${r.top}px`);
		style.setProperty("--keys-left", `${r.left}px`);
		style.setProperty("--keys-width", `${r.width}px`);
		style.setProperty("--keys-h", `${r.height}px`);
	};
	const watcher = new ResizeObserver(update);
	watcher.observe(box);
	addEventListener("resize", update);
	update();
	return () => { watcher.disconnect(); removeEventListener("resize", update); };
}

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
