// files.ts - Files out of and into the app: download in a browser, the share sheet in the Android app
//
// A page in a browser saves a file by downloading it. The Android app's
// web view does not download: there the files are written to the app's
// cache (@capacitor/filesystem) and handed to Android's share sheet
// (@capacitor/share), which saves them to Files or Drive or sends them on.
// Picking a file is the page's own file chooser in both.

export interface OutFile
{
	name: string;
	type: string;
	bytes: Uint8Array;
}

export function inApp(): boolean
{
	return (window as { Capacitor?: { isNativePlatform?: () => boolean } }).Capacitor?.isNativePlatform?.() === true;
}

export async function saveFiles(files: OutFile[], title: string)
{
	if (inApp())
	{
		await shareFiles(files, title);
		return;
	}
	for (const file of files)
	{
		const url = URL.createObjectURL(new Blob([file.bytes as BlobPart], { type: file.type }));
		const link = document.createElement("a");
		link.href = url;
		link.download = file.name;
		document.body.append(link);
		link.click();
		link.remove();
		// The download has taken the blob by the next turn; a second file waits a moment, as
		// Chrome drops a download that follows another at once
		await new Promise(resolve => setTimeout(resolve, 300));
		URL.revokeObjectURL(url);
	}
}

async function shareFiles(files: OutFile[], title: string)
{
	const { Filesystem, Directory } = await import("@capacitor/filesystem");
	const { Share } = await import("@capacitor/share");
	const uris: string[] = [];
	for (const file of files)
	{
		const written = await Filesystem.writeFile({ path: file.name, data: base64(file.bytes), directory: Directory.Cache });
		uris.push(written.uri);
	}
	try
	{
		await Share.share({ title, files: uris, dialogTitle: title });
	}
	catch (error)
	{
		// Closing the share sheet without a choice is not a failure
		if (!/cancel/i.test(String(error)))
		{
			throw error;
		}
	}
}

function base64(bytes: Uint8Array): string
{
	let binary = "";
	for (let i = 0; i < bytes.length; i += 0x8000)
	{
		binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
	}
	return btoa(binary);
}

// The files the user picks; none when the chooser is closed
export function pickFiles(): Promise<File[]>
{
	return new Promise(resolve =>
	{
		const input = document.createElement("input");
		input.type = "file";
		input.multiple = true;
		input.addEventListener("change", () => resolve([...input.files ?? []]));
		input.addEventListener("cancel", () => resolve([]));
		input.click();
	});
}
