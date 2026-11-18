// clipboard.ts - Copy a History line or result (the long-press menu)
//
// navigator.clipboard needs a secure page (https, localhost, the APK);
// over plain http on the local network the old copy command still works.

export async function copyText(text: string): Promise<boolean>
{
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
