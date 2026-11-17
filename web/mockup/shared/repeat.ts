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
