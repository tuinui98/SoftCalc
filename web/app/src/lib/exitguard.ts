// exitguard.ts - Back on the main screen leaves only when pressed again soon (owner 2026-11-19)
//
// A swipe from the screen's edge is the system's Back, and it closed the app
// by mistake. The first Back on the main screen shows a note; a second one
// within EXIT_MS leaves. The Android app asks here from its backButton
// listener (main.ts); the browser from a guard entry in its history (App.svelte).

export const EXIT_MS = 2000;

export class ExitGuard
{
	onshow: () => void = () => {};
	private shownAt = -Infinity;

	// true: this Back leaves; false: the note is shown and the next Back leaves
	press(now = performance.now()): boolean
	{
		if (now - this.shownAt < EXIT_MS)
		{
			return true;
		}
		this.shownAt = now;
		this.onshow();
		return false;
	}

	// A tap in between: the next Back asks again
	reset()
	{
		this.shownAt = -Infinity;
	}
}

export const exitGuard = new ExitGuard();
