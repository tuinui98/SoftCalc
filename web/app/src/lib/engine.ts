// engine.ts - Talk to the engine worker: one promise per request (design/protocol.md)

export type Answer = Record<string, any> & { id: number; ok: boolean; error?: string };

export class Engine
{
	private worker: Worker;
	private waiting = new Map<number, (answer: Answer) => void>();
	private nextId = 1;
	readonly ready: Promise<string>;
	// When each start step ended, in ms on the page's clock (performance.now):
	// "page" (the page loaded up to here), then the worker's steps
	readonly marks: { name: string; at: number }[] = [{ name: "page", at: performance.now() }];
	onstep: ((name: string) => void) | null = null;

	constructor(url: string)
	{
		this.worker = new Worker(url, { type: "module" });
		let started: (version: string) => void;
		let failed: (error: Error) => void;
		this.ready = new Promise((resolve, reject) => { started = resolve; failed = reject; });
		this.worker.onerror = event => failed(new Error(event.message || "the engine worker could not be loaded"));
		this.worker.onmessage = event =>
		{
			const message = event.data;
			if (message.id === 0)
			{
				if (message.op === "step")
				{
					this.marks.push({ name: message.name, at: message.at - performance.timeOrigin });
					this.onstep?.(message.name);
				}
				else if (message.op === "ready")
				{
					started(message.version);
				}
				else
				{
					failed(new Error(message.error));
				}
				return;
			}
			const resolve = this.waiting.get(message.id);
			this.waiting.delete(message.id);
			resolve?.(message);
		};
	}

	send(op: string, args: Record<string, unknown> = {}): Promise<Answer>
	{
		const id = this.nextId++;
		return new Promise(resolve =>
		{
			this.waiting.set(id, resolve);
			this.worker.postMessage({ id, op, ...args });
		});
	}
}
