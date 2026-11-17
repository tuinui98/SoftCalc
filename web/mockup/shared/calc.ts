// calc.ts - The calculator behind both mockups: Input, History, keys, recall walk
//
// A plain class the two UIs (Ionic + Vue, Svelte) draw from, so they
// behave the same and only their looks differ. Rules follow the desktop
// MainForm / InputRules / KeypadRules / InputRecall (owner 2026-11-16):
// C removes the last number, % only appends %, an operator on an empty
// Input after a result starts with `answer`, Enter on an empty Input
// repeats the last line, tapping a History line recalls it and Up /
// entry walk on from there, tapping it again only takes the highlight off.

import { Engine } from "./engine";

export interface Entry
{
	kind: string;          // result, definition, error, notice
	input: string;
	display: string;       // the History expression line (numbers substituted)
	text: string;
}

export interface Settings
{
	precision: number;
	angle: string;
	base: string;
	[key: string]: unknown;
}

export type Catalog = { functions: Record<string, string[]>; conversions: Record<string, string[]> };

const OPERATORS = "+-*/^!%";

export class Calculator
{
	readonly engine: Engine;
	text = "";
	caret = 0;
	entries: Entry[] = [];
	highlight = -1;            // History index shown by the walk, -1 when not walking
	settings: Settings = { precision: 64, angle: "deg", base: "dec" };
	second = false;
	storeArmed = false;
	status = "Loading engine...";
	ready = false;
	catalog: Catalog = { functions: {}, conversions: {} };
	private lastWasResult = false;
	private hasMemory = false;
	private draft = "";
	private listeners = new Set<() => void>();

	constructor(workerUrl: string)
	{
		this.engine = new Engine(workerUrl);
		const started = performance.now();
		this.engine.ready.then(async version =>
		{
			this.settings = (await this.engine.send("settings", { angle: "deg" })) as unknown as Settings;
			this.catalog = (await this.engine.send("catalog")) as unknown as Catalog;
			this.ready = true;
			this.status = `Engine ${version} ready in ${((performance.now() - started) / 1000).toFixed(1)} s`;
			this.changed();
		}, error =>
		{
			this.status = "Engine failed to load: " + error.message;
			this.changed();
		});
	}

	subscribe(listener: () => void): () => void
	{
		this.listeners.add(listener);
		return () => this.listeners.delete(listener);
	}

	private changed()
	{
		for (const listener of this.listeners)
		{
			listener();
		}
	}

	//==================================================================
	//  Input editing
	//==================================================================
	// Typed with the system keyboard: the walk ends, Input is what was typed
	typed(text: string, caret: number)
	{
		this.text = text;
		this.caret = caret;
		this.highlight = -1;
		this.changed();
	}

	moveCaret(caret: number)
	{
		this.caret = Math.max(0, Math.min(caret, this.text.length));
	}

	private setText(text: string, caret = text.length)
	{
		this.text = text;
		this.caret = caret;
	}

	private insert(piece: string)
	{
		this.setText(this.text.slice(0, this.caret) + piece + this.text.slice(this.caret), this.caret + piece.length);
		this.highlight = -1;
	}

	// An operator on an empty Input right after a result starts with `answer` (InputRules.AutoAnswer)
	private insertTyped(piece: string)
	{
		if (this.text.length === 0 && this.lastWasResult && piece.length > 0 && OPERATORS.includes(piece[0]))
		{
			this.setText("answer" + piece);
			this.highlight = -1;
			return;
		}
		this.insert(piece);
	}

	private async menuInsert(args: Record<string, unknown>)
	{
		const answer = await this.engine.send("menu_insert", { ...args, text: this.text, caret: this.caret });
		if (answer.ok)
		{
			this.setText(answer.text, answer.caret);
			this.highlight = -1;
		}
	}

	async insertFunction(name: string)
	{
		await this.menuInsert(name === "mod" ? { kind: "operator", symbol: " mod " }
			: name.length === 1 && OPERATORS.includes(name) ? { kind: "operator", symbol: name }
			: { kind: "function", name });
		this.changed();
	}

	async insertConversion(source: string, target: string)
	{
		await this.menuInsert({ kind: "conversion", source, target });
		this.changed();
	}

	//==================================================================
	//  Submit and History
	//==================================================================
	async submit(line = this.text)
	{
		const toRun = line.length > 0 ? line : this.entries.at(-1)?.input;
		if (!toRun || !this.ready)
		{
			return;
		}
		const answer = await this.engine.send("eval", { input: toRun });
		this.highlight = -1;
		this.draft = "";
		if (!answer.ok)
		{
			this.entries.push({ kind: "error", input: toRun, display: toRun, text: answer.error ?? "" });
		}
		else if (answer.kind === "rates_needed")
		{
			this.entries.push({ kind: "notice", input: toRun, display: toRun, text: "Currency rates are not in the mockup." });
		}
		else if (answer.kind !== "empty")
		{
			this.entries.push({ kind: answer.kind, input: answer.input, display: answer.display_input, text: answer.text });
			if (/^\s*memory\s*=/i.test(toRun) && answer.kind === "definition")
			{
				this.hasMemory = true;
			}
		}
		this.lastWasResult = answer.ok && answer.kind === "result";
		if (answer.ok && answer.kind !== "error")
		{
			this.setText("");
		}
		this.changed();
	}

	private recallLines(): number[]
	{
		return this.entries.map((entry, index) => entry.kind === "notice" ? -1 : index).filter(index => index >= 0);
	}

	private show(index: number)
	{
		this.highlight = index;
		this.setText(index >= 0 ? this.entries[index].input : this.draft);
	}

	// Tap on a History line: recall it; a tap on the highlighted line keeps it and drops the highlight
	tapEntry(index: number)
	{
		if (this.entries[index]?.kind === "notice")
		{
			return;
		}
		if (this.highlight === index)
		{
			this.highlight = -1;
		}
		else
		{
			if (this.highlight < 0)
			{
				this.draft = this.text;
			}
			this.show(index);
		}
		this.changed();
	}

	// Up (entry key): one line older; from no highlight, the newest
	walk(up: boolean)
	{
		const lines = this.recallLines();
		if (lines.length === 0)
		{
			return;
		}
		const at = lines.indexOf(this.highlight);
		if (up)
		{
			if (at < 0)
			{
				this.draft = this.text;
				this.show(lines.at(-1)!);
			}
			else if (at > 0)
			{
				this.show(lines[at - 1]);
			}
		}
		else if (at >= 0)
		{
			this.show(at + 1 < lines.length ? lines[at + 1] : -1);
		}
		this.changed();
	}

	// Esc: the highlight goes and the draft comes back
	cancelWalk()
	{
		if (this.highlight >= 0)
		{
			this.show(-1);
			this.changed();
		}
	}

	clearHistory()
	{
		this.entries = [];
		this.highlight = -1;
		this.engine.send("clear_history");
		this.changed();
	}

	//==================================================================
	//  Settings
	//==================================================================
	async setSetting(name: string, value: unknown)
	{
		const answer = await this.engine.send("settings", { [name]: value });
		if (answer.ok)
		{
			const { id, ok, ...settings } = answer;
			this.settings = settings as Settings;
		}
		this.changed();
	}

	//==================================================================
	//  Keys (ids from keys.ts)
	//==================================================================
	// Returns "func" or "units" when the UI should open that menu
	async press(id: string): Promise<"func" | "units" | null>
	{
		if (id === "2nd")
		{
			this.second = !this.second;
			this.changed();
			return null;
		}
		if (this.storeArmed)
		{
			this.storeArmed = false;
			if (id === "x" || id === "y")
			{
				const value = this.text.trim();
				await this.submit(`${id}=${value.length === 0 ? "ans" : value}`);
				return null;
			}
		}
		let menu: "func" | "units" | null = null;
		switch (id)
		{
			case "=":
				await this.submit();
				return null;
			case "AC":
				this.setText("");
				this.highlight = -1;
				break;
			case "C":
				this.setText(clearEntry(this.text));
				this.highlight = -1;
				break;
			case "M+":
			case "M-":
				await this.submit(memoryAdd(this.text, this.hasMemory, id === "M-"));
				return null;
			case "MC":
				await this.submit("memory=0");
				return null;
			case "MR":
				this.insert("memory");
				break;
			case "sto":
				this.storeArmed = true;
				break;
			case "entry":
				this.walk(true);
				return null;
			case "base":
			{
				const order = ["dec", "hex", "oct", "bin"];
				await this.setSetting("base", order[(order.indexOf(this.settings.base) + 1) % order.length]);
				const line = this.text;
				if (line.trim().length > 0)
				{
					await this.submit(line);
					this.setText(line);    // the line stays for the next base (owner 2026-11-16)
				}
				break;
			}
			case "angle":
				await this.setSetting("angle", this.settings.angle === "deg" ? "rad" : "deg");
				break;
			case "func":
			case "units":
				menu = id;
				break;
			default:
				if (id.startsWith("fn:"))
				{
					await this.insertFunction(id.slice(3));
					return null;
				}
				this.insertTyped(id.startsWith("ins:") ? id.slice(4) : id);
				break;
		}
		this.changed();
		return menu;
	}

	// The id a key sends now (2nd applied), and 2nd is used up
	resolve(key: { id: string; second?: string }): string
	{
		if (this.second && key.id !== "2nd")
		{
			this.second = false;
			return key.second ?? key.id;
		}
		return key.id;
	}
}

// C: the last number or name (or the last other character) goes, with spaces before it (KeypadRules.ClearEntry)
export function clearEntry(text: string): string
{
	const trimmed = text.trimEnd();
	let start = trimmed.length;
	while (start > 0 && /[\p{L}\p{N}._]/u.test(trimmed[start - 1]))
	{
		start--;
	}
	if (start === trimmed.length && start > 0)
	{
		start--;    // an operator or a bracket
	}
	return trimmed.slice(0, start).trimEnd();
}

// M+ / M-: an empty Input stands for `answer`; a missing memory counts as 0 (InputRules.MemoryAdd)
export function memoryAdd(input: string, hasMemory: boolean, subtract: boolean): string
{
	const trimmed = input.trim();
	const operand = trimmed.length === 0 ? "answer" : `(${trimmed})`;
	const sign = subtract ? "-" : "+";
	if (hasMemory)
	{
		return `memory=memory${sign}${operand}`;
	}
	return subtract ? `memory=-${operand}` : `memory=${operand}`;
}
