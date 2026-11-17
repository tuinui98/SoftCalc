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

// A backdrop that closes on the first touch (owner 2026-11-18: a tap outside the menu took two
// tries on a phone). It closes when the finger lifts, and the click that a phone may still send
// after that is dropped, so the touch does not also press the key that was under the backdrop.
export function closeOnTouch(backdrop: HTMLElement, close: () => void): () => void
{
	let downOnIt = false;
	const down = (event: PointerEvent) =>
	{
		downOnIt = event.target === backdrop;
	};
	const up = (event: PointerEvent) =>
	{
		if (!downOnIt || event.target !== backdrop)
		{
			return;
		}
		downOnIt = false;
		event.preventDefault();
		const swallow = (click: MouseEvent) => { click.stopPropagation(); click.preventDefault(); };
		window.addEventListener("click", swallow, true);
		setTimeout(() => window.removeEventListener("click", swallow, true), 400);
		close();
	};
	backdrop.addEventListener("pointerdown", down);
	backdrop.addEventListener("pointerup", up);
	return () =>
	{
		backdrop.removeEventListener("pointerdown", down);
		backdrop.removeEventListener("pointerup", up);
	};
}
