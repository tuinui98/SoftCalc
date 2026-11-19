// keytap.ts - Key presses on the keys' box: the key the finger went down on, on the page that is
// being shown, pressed when the finger lifts within a few pixels (owner 2026-11-18).
//
// Not the buttons' own click: a click needs the finger to go down and up on the same element, and
// right after a swipe the pages still slide under the finger, so a quick tap after a swipe missed
// or hit a key passing by. The key is found by where it will be once the slide ends: each key's
// box on the shown page, less the page's slide offset at this moment.

const SLOP = 12;    // px; more is a swipe (swipe.ts)

// The click a phone sends after the tap is dropped: func and units open a menu under the finger,
// and that click landed on the menu's backdrop and closed it at once (owner 2026-11-19: only a
// press and hold, which sends no click, kept the menu open). A script's click still goes through.
function swallowClick()
{
	const swallow = (click: MouseEvent) =>
	{
		if (click.isTrusted)
		{
			click.stopPropagation();
			click.preventDefault();
		}
	};
	window.addEventListener("click", swallow, true);
	setTimeout(() => window.removeEventListener("click", swallow, true), 400);
}

// The key on `page` whose settled box holds the point
export function keyAt(page: HTMLElement, x: number, y: number): string
{
	const transform = getComputedStyle(page).transform;
	const offset = transform && transform !== "none" ? new DOMMatrixReadOnly(transform).m41 : 0;
	for (const key of page.querySelectorAll<HTMLElement>("[data-key]"))
	{
		const box = key.getBoundingClientRect();
		if (x >= box.left - offset && x < box.right - offset && y >= box.top && y < box.bottom)
		{
			return key.dataset.key ?? "";
		}
	}
	return "";
}

export function keyTaps(box: HTMLElement, shownPage: () => HTMLElement, press: (id: string) => void): () => void
{
	let id = "";
	let pointer = -1, x0 = 0, y0 = 0;

	const down = (event: PointerEvent) =>
	{
		if (!event.isPrimary || event.button > 0 || !box.contains(event.target as Node))
		{
			return;
		}
		id = keyAt(shownPage(), event.clientX, event.clientY);
		pointer = event.pointerId;
		x0 = event.clientX;
		y0 = event.clientY;
	};

	const up = (event: PointerEvent) =>
	{
		if (event.pointerId !== pointer)
		{
			return;
		}
		const key = id;
		pointer = -1;
		id = "";
		if (key && Math.hypot(event.clientX - x0, event.clientY - y0) < SLOP)
		{
			swallowClick();
			press(key);
		}
	};

	const cancel = (event: PointerEvent) =>
	{
		if (event.pointerId === pointer)
		{
			pointer = -1;
			id = "";
		}
	};

	box.addEventListener("pointerdown", down, true);
	window.addEventListener("pointerup", up);
	window.addEventListener("pointercancel", cancel);
	return () =>
	{
		box.removeEventListener("pointerdown", down, true);
		window.removeEventListener("pointerup", up);
		window.removeEventListener("pointercancel", cancel);
	};
}
