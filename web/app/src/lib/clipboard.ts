// clipboard.ts - Copy a History line or result (the long-press menu)
//
// navigator.clipboard needs a secure page (https, localhost, the APK);
// over plain http on the local network the old copy command still works.

// A password copied (owner 2026-11-20): cleared after 30 s while SoftCalc is on screen, unless something else
// was copied here since. A phone's clipboard history cannot be kept out of on the web (desktop: it is).
let secretTimer: ReturnType<typeof setTimeout> | undefined;
let lastCopied = "";

export async function copySecret(text: string): Promise<boolean>
{
	const done = await copyText(text);
	clearTimeout(secretTimer);
	if (done)
	{
		secretTimer = setTimeout(() =>
		{
			if (lastCopied === text && document.visibilityState === "visible")
			{
				void copyText("").then(() => { lastCopied = ""; });
			}
		}, 30000);
	}
	return done;
}

export async function copyText(text: string): Promise<boolean>
{
	lastCopied = text;
	try
	{
		await navigator.clipboard.writeText(text);
		return true;
	}
	catch
	{
		const box = document.createElement("textarea");
		box.value = text;
		box.setAttribute("readonly", "");
		box.style.position = "fixed";
		box.style.opacity = "0";
		document.body.append(box);
		box.select();
		const done = document.execCommand("copy");
		box.remove();
		return done;
	}
}
