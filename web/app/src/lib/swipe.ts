// swipe.ts - Swipes on the keys (owner 2026-11-18): left / right flips Basic <-> Sci and the
// keys follow the finger; up opens the menu. A tap stays a key press.
//
// The system's own gestures (edge swipe = Back, swipe from the bottom =
// app list) are left alone: nothing here listens to the screen edges or
// to the Back button.

export interface SwipeHandlers
{
	move: (dx: number) => void;                       // horizontal drag, in pixels
	end: (dx: number, velocity: number) => void;      // finger up after a horizontal drag; velocity in px/ms
	up: () => void;                                   // a swipe up
}

const DECIDE = 12;     // px before a move counts as a swipe, not a tap
const UP_MIN = 50;     // px up for the menu

export function swipeOn(box: HTMLElement, on: SwipeHandlers): () => void
{
	let id = -1, x0 = 0, y0 = 0, axis: "" | "x" | "y" = "";
	let lastX = 0, lastT = 0, speed = 0;
	let swallowClick = false;

	const down = (event: PointerEvent) =>
	{
		if (id !== -1 || !event.isPrimary)
		{
			return;
		}
		id = event.pointerId;
		x0 = lastX = event.clientX;
		y0 = event.clientY;
		lastT = event.timeStamp;
		speed = 0;
		axis = "";
	};

	const move = (event: PointerEvent) =>
	{
		if (event.pointerId !== id)
		{
			return;
		}
		const dx = event.clientX - x0, dy = event.clientY - y0;
		if (axis === "")
		{
			if (Math.abs(dx) < DECIDE && Math.abs(dy) < DECIDE)
			{
				return;
			}
			axis = Math.abs(dx) > Math.abs(dy) ? "x" : "y";
			swallowClick = true;    // a swipe that ends on a key does not press it
			box.setPointerCapture(id);
		}
		if (axis === "x")
		{
			const dt = event.timeStamp - lastT;
			if (dt > 0)
			{
				speed = (event.clientX - lastX) / dt;
			}
			lastX = event.clientX;
			lastT = event.timeStamp;
			on.move(dx);
		}
	};

	const up = (event: PointerEvent) =>
	{
		if (event.pointerId !== id)
		{
			return;
		}
		const dx = event.clientX - x0, dy = event.clientY - y0;
		if (axis === "x")
		{
			on.end(dx, event.timeStamp - lastT > 80 ? 0 : speed);
		}
		else if (axis === "y" && dy < -UP_MIN)
		{
			on.up();
		}
		id = -1;
		axis = "";
		setTimeout(() => { swallowClick = false; }, 0);    // after the click that may follow
	};

	const cancel = (event: PointerEvent) =>
	{
		if (event.pointerId === id && axis === "x")
		{
			on.end(0, 0);
		}
		if (event.pointerId === id)
		{
			id = -1;
			axis = "";
			swallowClick = false;
		}
	};

	const click = (event: MouseEvent) =>
	{
		if (swallowClick)
		{
			event.stopPropagation();
			event.preventDefault();
			swallowClick = false;
		}
	};

	box.addEventListener("pointerdown", down);
	box.addEventListener("pointermove", move);
	box.addEventListener("pointerup", up);
	box.addEventListener("pointercancel", cancel);
	box.addEventListener("click", click, true);
	return () =>
	{
		box.removeEventListener("pointerdown", down);
		box.removeEventListener("pointermove", move);
		box.removeEventListener("pointerup", up);
		box.removeEventListener("pointercancel", cancel);
		box.removeEventListener("click", click, true);
	};
}
