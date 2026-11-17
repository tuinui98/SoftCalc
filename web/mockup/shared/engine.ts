// engine.ts - Talk to the engine worker: one promise per request (design/protocol.md)

export type Answer = Record<string, any> & { id: number; ok: boolean; error?: string };

export class Engine
{
	private worker: Worker;
	private waiting = new Map<number, (answer: Answer) => void>();
	private nextId = 1;
	readonly ready: Promise<string>;

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
				if (message.op === "ready")
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
