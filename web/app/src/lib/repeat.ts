// repeat.ts - Press and hold: act once, then again and again while the finger stays down (⌫)

export function holdToRepeat(button: HTMLElement, act: () => void): () => void
{
	let timer: ReturnType<typeof setTimeout> | undefined;
	const stop = () => { clearTimeout(timer); timer = undefined; };
	const next = (wait: number) => { timer = setTimeout(() => { act(); next(70); }, wait); };
	const start = (event: PointerEvent) =>
	{
		event.preventDefault();    // Input keeps the focus, the system keyboard does not jump
		stop();
		act();
		next(450);
	};
	button.addEventListener("pointerdown", start);
	for (const name of ["pointerup", "pointerleave", "pointercancel"])
	{
		button.addEventListener(name, stop);
	}
	return () =>
	{
		stop();
		button.removeEventListener("pointerdown", start);
	};
}

// Acts on the first touch (owner 2026-11-18: a tap outside the menu, and on Cancel, took two
// tries on a phone). It acts when the finger lifts on the same element, and the click that a
// phone may still send after that is dropped, so the touch does not also press what was under
// it. A click with no touch before it (keyboard, mouse) still acts once.
export function tapOn(element: HTMLElement, act: () => void, selfOnly = false): () => void
{
	let downOnIt = false;
	let done = false;
	const inside = (target: EventTarget | null) => selfOnly ? target === element : element.contains(target as Node);
	const down = (event: PointerEvent) =>
	{
		downOnIt = inside(event.target);
	};
	const click = (event: MouseEvent) =>
	{
		if (!done && inside(event.target))
		{
			act();
		}
	};
	const up = (event: PointerEvent) =>
	{
		if (!downOnIt || !inside(event.target))
		{
			return;
		}
		downOnIt = false;
		event.preventDefault();
		// Only the touch's own click: a script's click (Export's download link, Import's file
		// chooser) right after the tap must go through (owner 2026-11-19: History never downloaded)
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
		done = true;
		setTimeout(() => { done = false; }, 400);
		act();
	};
	element.addEventListener("pointerdown", down);
	element.addEventListener("pointerup", up);
	element.addEventListener("click", click);
	return () =>
	{
		element.removeEventListener("pointerdown", down);
		element.removeEventListener("pointerup", up);
		element.removeEventListener("click", click);
	};
}

// A tap acts on lift; a press held HOLD_MS without moving acts as a hold instead (the Graph's
// lines: tap selects, hold opens the line's menu). A mouse's right button also holds.
const HOLD_MS = 500;

export function tapOrHold(element: HTMLElement, tap: () => void, hold: () => void): () => void
{
	let timer: ReturnType<typeof setTimeout> | undefined;
	let start = { x: 0, y: 0 };
	let down = false;
	let heldAt = -Infinity;
	const stop = () => { clearTimeout(timer); timer = undefined; };
	const swallowClick = () =>
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
	};
	const onDown = (event: PointerEvent) =>
	{
		if (event.button > 0)
		{
			return;
		}
		down = true;
		start = { x: event.clientX, y: event.clientY };
		stop();
		timer = setTimeout(() => { timer = undefined; down = false; heldAt = performance.now(); swallowClick(); hold(); }, HOLD_MS);
	};
	const onMove = (event: PointerEvent) =>
	{
		if (down && Math.hypot(event.clientX - start.x, event.clientY - start.y) > 10)
		{
			down = false;    // a scroll, not a press
			stop();
		}
	};
	const onUp = (event: PointerEvent) =>
	{
		if (down && timer !== undefined)
		{
			stop();
			down = false;
			event.preventDefault();
			swallowClick();
			tap();
		}
	};
	const onCancel = () => { down = false; stop(); };
	const onMenu = (event: Event) =>
	{
		event.preventDefault();
		if (timer === undefined && !down && performance.now() - heldAt > 1000)
		{
			hold();    // a mouse's right button (a long touch has held already)
		}
	};
	element.addEventListener("pointerdown", onDown);
	element.addEventListener("pointermove", onMove);
	element.addEventListener("pointerup", onUp);
	element.addEventListener("pointercancel", onCancel);
	element.addEventListener("pointerleave", onCancel);
	element.addEventListener("contextmenu", onMenu);
	return () =>
	{
		stop();
		element.removeEventListener("pointerdown", onDown);
		element.removeEventListener("pointermove", onMove);
		element.removeEventListener("pointerup", onUp);
		element.removeEventListener("pointercancel", onCancel);
		element.removeEventListener("pointerleave", onCancel);
		element.removeEventListener("contextmenu", onMenu);
	};
}

// A backdrop: only a touch on the backdrop itself closes it
export function closeOnTouch(backdrop: HTMLElement, close: () => void): () => void
{
	return tapOn(backdrop, close, true);
}
