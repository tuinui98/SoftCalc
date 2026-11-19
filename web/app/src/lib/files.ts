// files.ts - Files out of and into the app: a download in a browser, Download/SoftCalc in the Android app
//
// A page in a browser saves a file by downloading it to the browser's
// Downloads. The Android app's web view does not download: there the app's
// own plugin (DownloadsPlugin.java) writes the files to Download/SoftCalc,
// where the system's file chooser finds them for Import; sharing them is
// left to the Files app (owner 2026-11-19: save, not share). Picking a file
// is the page's own file chooser in both.

import { registerPlugin } from "@capacitor/core";

export interface OutFile
{
	name: string;
	type: string;
	bytes: Uint8Array;
}

interface DownloadsPlugin
{
	save(options: { name: string; type: string; data: string }): Promise<{ folder: string }>;
}

const Downloads = registerPlugin<DownloadsPlugin>("Downloads");

export function inApp(): boolean
{
	return (window as { Capacitor?: { isNativePlatform?: () => boolean } }).Capacitor?.isNativePlatform?.() === true;
}

// Saves the files and gives the folder they went to, for the status line
export async function saveFiles(files: OutFile[]): Promise<string>
{
	if (inApp())
	{
		let folder = "Download/SoftCalc";
		for (const file of files)
		{
			folder = (await Downloads.save({ name: file.name, type: file.type, data: base64(file.bytes) })).folder;
		}
		return folder;
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
		// Chrome on Android reads the blob some time after the click, so it stays a minute;
		// a second file waits a moment, as Chrome drops a download that follows another at once
		setTimeout(() => URL.revokeObjectURL(url), 60_000);
		await new Promise(resolve => setTimeout(resolve, 500));
	}
	return "Downloads";
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

// What Export saves and older Android exports were saved as; .jsonl and .pcf are octet-stream to Android
const PICK_TYPES = ".jsonl,.pcf,.txt,application/octet-stream,application/x-ndjson,application/json,text/plain";

// The files the user picks; none when the chooser is closed
export function pickFiles(): Promise<File[]>
{
	return new Promise(resolve =>
	{
		const input = document.createElement("input");
		input.type = "file";
		input.multiple = true;
		if (!inApp())
		{
			// Without types Chrome on Android also offers the camera and asks for camera and
			// microphone; a list without image or video opens the file chooser alone. The app's
			// web view opens it alone already, with no filter (owner 2026-11-19)
			input.accept = PICK_TYPES;
		}
		input.addEventListener("change", () => resolve([...input.files ?? []]));
		input.addEventListener("cancel", () => resolve([]));
		input.click();
	});
}
