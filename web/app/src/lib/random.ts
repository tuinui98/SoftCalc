// random.ts - ⋮ > Random's generators: Simple and Password (owner 2026-11-20, R1)
//
// A port of the desktop's Logic/RandomTools.cs. Everything draws from a
// RandomSource: the browser's crypto.getRandomValues in the app, fixed
// numbers in tests. Draws are unbiased (rejection sampling, never `%` on
// raw bytes). Whole numbers and decimals are worked in BigInt steps, so
// 10^15 and ten decimal places stay exact.

export interface RandomSource
{
	// A uniform whole number from 0 to bound - 1 (bound >= 1)
	below(bound: bigint): bigint;
}

const TOP = 1n << 64n;

// A uniform value below bound from 64-bit draws, dropping the uneven top
export function unbiased(bound: bigint, next: () => bigint): bigint
{
	if (bound < 1n)
	{
		throw new RangeError("bound");
	}
	const limit = TOP - TOP % bound;    // values at or above it would favour small results
	for (;;)
	{
		const value = next();
		if (value < limit)
		{
			return value % bound;
		}
	}
}

// The browser's secure generator (crypto.getRandomValues)
export const secure: RandomSource = {
	below(bound: bigint): bigint
	{
		return unbiased(bound, () =>
		{
			const words = crypto.getRandomValues(new Uint32Array(2));
			return (BigInt(words[0]) << 32n) | BigInt(words[1]);
		});
	},
};

export const MAX_COUNT = 1000;
export const MAX_PLACES = 10;
export const LIMIT = 10n ** 15n;    // |From|, |To| up to 10^15
export const DEFAULT_HEAD = "Head";
export const DEFAULT_TAIL = "Tail";

type Result<T> = { values: T[]; error: null } | { values: null; error: string };

//==================================================================
//  Numbers as text, exact: "-12.5" <-> scaled BigInt
//==================================================================
// The text as a whole number of 10^-places, or null when it is not a plain number
export function scaled(text: string, places: number): bigint | null
{
	const match = /^\s*([+-]?)(\d*)(?:\.(\d*))?\s*$/.exec(text);
	if (!match || (match[2] === "" && (match[3] ?? "") === ""))
	{
		return null;
	}
	const fraction = (match[3] ?? "").padEnd(places, "0");
	if (fraction.length > places && /[1-9]/.test(fraction.slice(places)))
	{
		return null;    // more decimals than asked: not rounded silently
	}
	const value = BigInt((match[2] || "0") + fraction.slice(0, places));
	return match[1] === "-" ? -value : value;
}

export function unscaled(value: bigint, places: number): string
{
	const negative = value < 0n;
	const digits = (negative ? -value : value).toString().padStart(places + 1, "0");
	const text = places === 0 ? digits : `${digits.slice(0, -places)}.${digits.slice(-places)}`;
	return (negative ? "-" : "") + text;
}

//==================================================================
//  Simple tab
//==================================================================
// Whole numbers from min to max, both included
export function integers(source: RandomSource, from: string, to: string, count: number, noRepeats: boolean): Result<string>
{
	let min = scaled(from, 0), max = scaled(to, 0);
	if (min === null || max === null)
	{
		return { values: null, error: "From and To must be whole numbers." };
	}
	if (min > max)
	{
		[min, max] = [max, min];
	}
	if (min < -LIMIT || max > LIMIT)
	{
		return { values: null, error: "From and To must be within 10^15." };
	}
	if (!(count >= 1 && count <= MAX_COUNT))
	{
		return { values: null, error: `Count must be 1 to ${MAX_COUNT}.` };
	}
	const span = max - min + 1n;
	if (noRepeats && BigInt(count) > span)
	{
		return { values: null, error: `Only ${span} different numbers from ${min} to ${max}.` };
	}
	const values: bigint[] = [];
	const seen = new Set<bigint>();
	while (values.length < count)
	{
		const value = min + source.below(span);
		if (noRepeats && seen.has(value))
		{
			continue;
		}
		seen.add(value);
		values.push(value);
	}
	return { values: values.map(String), error: null };
}

// Decimals from min to max in steps of 10^-places, both ends included
export function decimals(source: RandomSource, from: string, to: string, places: number, count: number): Result<string>
{
	if (!(places >= 0 && places <= MAX_PLACES))
	{
		return { values: null, error: `Decimal places must be 0 to ${MAX_PLACES}.` };
	}
	let min = scaled(from, places), max = scaled(to, places);
	if (min === null || max === null)
	{
		return { values: null, error: `From and To must be numbers with at most ${places} decimal places.` };
	}
	if (min > max)
	{
		[min, max] = [max, min];
	}
	const unit = 10n ** BigInt(places);
	if (min < -LIMIT * unit || max > LIMIT * unit)
	{
		return { values: null, error: "From and To must be within 10^15." };
	}
	if (!(count >= 1 && count <= MAX_COUNT))
	{
		return { values: null, error: `Count must be 1 to ${MAX_COUNT}.` };
	}
	const values: string[] = [];
	for (let i = 0; i < count; i++)
	{
		values.push(unscaled(min + source.below(max - min + 1n), places));
	}
	return { values, error: null };
}

// True = heads; headsPercent 0-100 in steps of 0.01 (desktop: P50 or a bias)
export function coins(source: RandomSource, headsPercent: number, count: number): boolean[]
{
	const threshold = BigInt(Math.round(Math.min(100, Math.max(0, headsPercent)) * 100));
	return Array.from({ length: count }, () => source.below(10000n) < threshold);
}

export function dice(source: RandomSource, sides: number, count: number): number[]
{
	return Array.from({ length: count }, () => 1 + Number(source.below(BigInt(sides))));
}

// Insert Command: one draw as a rand() line for Input (desktop RandomTools.Command)
export function command(kind: string, from: string, to: string, places: number, headsPercent: number, sides: number): string
{
	let low = Number(from), high = Number(to);
	if (low > high)
	{
		[low, high] = [high, low];
	}
	const plain = (value: number) => String(Number(value.toPrecision(15)));
	switch (kind)
	{
		case "decimal":
		{
			const start = plain(low);
			return `round(rand(${start.includes(".") ? start : start + ".0"}, ${plain(high)}), ${places})`;
		}
		case "coin":
		{
			const p = Math.min(100, Math.max(0, headsPercent)) / 100;
			return p === 0.5 ? "round(rand())" : `floor(rand()+${plain(p)})`;
		}
		case "dice":
			return `rand(1, ${sides})`;
		default:
			return `rand(${plain(Math.trunc(low))}, ${plain(Math.trunc(high))})`;
	}
}

//==================================================================
//  Password tab
//==================================================================
export const UPPER_SET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
export const LOWER_SET = "abcdefghijklmnopqrstuvwxyz";
export const DIGIT_SET = "0123456789";
// Symbol groups (owner 2026-11-12); the last two are the ones sites most often refuse, so they start unticked
export const SYMBOL_GROUPS: [string, string][] = [
	["Common", "!@#$%&*?"], ["Dash", "-_"], ["Maths", "+=^~"], ["Brackets", "()[]{}<>"], ["Punctuation", ".,:;"],
	["Quotes", "'\"`"], ["Slashes", "/\\|"],
];
export const DEFAULT_SYMBOLS = SYMBOL_GROUPS.slice(0, 5).map(([, chars]) => chars).join("");
export const LOOK_ALIKES = "Il1O0o";
export const PLACES = ["end", "start", "anywhere"] as const;

export interface PasswordOptions
{
	length: number;
	upper: boolean;
	lower: boolean;
	digits: boolean;
	symbols: boolean;
	symbolSet: string;
	noLookAlikes: boolean;
}

// A group ticked or unticked: its symbols out of the text, then back at the end when ticked
export function toggleSymbols(text: string, chars: string, on: boolean): string
{
	const kept = [...text].filter(c => !chars.includes(c)).join("");
	return on ? kept + chars : kept;
}

// The character sets chosen, look-alikes and doubles left out
export function kinds(options: PasswordOptions): string[]
{
	const sets: string[] = [];
	const add = (on: boolean, set: string) =>
	{
		if (!on)
		{
			return;
		}
		const chars = [...new Set([...set].filter(c => !/\s/.test(c) && !(options.noLookAlikes && LOOK_ALIKES.includes(c))))].join("");
		if (chars.length > 0)
		{
			sets.push(chars);
		}
	};
	add(options.upper, UPPER_SET);
	add(options.lower, LOWER_SET);
	add(options.digits, DIGIT_SET);
	add(options.symbols, options.symbolSet);
	return sets;
}

export function poolSize(options: PasswordOptions): number
{
	return new Set(kinds(options).join("")).size;
}

// Bits of a password drawn uniformly: length * log2(pool)
export function strengthBits(options: PasswordOptions): number
{
	const pool = poolSize(options);
	return pool < 2 ? 0 : options.length * Math.log2(pool);
}

const pick = (source: RandomSource, set: string[]) => set[Number(source.below(BigInt(set.length)))];

// One password with at least one character of each chosen kind
export function password(source: RandomSource, options: PasswordOptions): { password: string | null; error: string | null }
{
	const sets = kinds(options).map(set => [...set]);
	if (sets.length === 0)
	{
		return { password: null, error: "Choose at least one kind of character." };
	}
	if (options.length < sets.length)
	{
		return { password: null, error: `Length must be at least ${sets.length} for ${sets.length} kinds.` };
	}
	const pool = [...new Set(sets.flat())];
	const chars = Array.from({ length: options.length }, (_, i) => i < sets.length ? pick(source, sets[i]) : pick(source, pool));
	for (let i = chars.length - 1; i > 0; i--)    // Fisher-Yates: the sure ones end up anywhere
	{
		const j = Number(source.below(BigInt(i + 1)));
		[chars[i], chars[j]] = [chars[j], chars[i]];
	}
	return { password: chars.join(""), error: null };
}

// Signature (owner 2026-11-12): the owner's own mark written into each password as typed, at the start, the
// end or anywhere; Length counts it, the strength counts only the random characters around it
export function signed(source: RandomSource, options: PasswordOptions, signature: string, place: string)
{
	if (signature.length === 0)
	{
		return password(source, options);
	}
	const count = kinds(options).length;
	const randomLength = options.length - [...signature].length;
	if (count > 0 && randomLength < Math.max(count, 1))
	{
		return { password: null, error: `Length must be at least ${[...signature].length + count}: the signature is ${[...signature].length} long.` };
	}
	const part = password(source, { ...options, length: randomLength });
	if (part.password === null)
	{
		return part;
	}
	const text = part.password;
	if (place === "start")
	{
		return { password: signature + text, error: null };
	}
	if (place === "anywhere")
	{
		const at = Number(source.below(BigInt(text.length + 1)));
		return { password: text.slice(0, at) + signature + text.slice(at), error: null };
	}
	return { password: text + signature, error: null };
}

// The random part's options: the signature's characters are not random
export function randomPart(options: PasswordOptions, signature: string): PasswordOptions
{
	return { ...options, length: Math.max(0, options.length - [...signature].length) };
}

//==================================================================
//  Options kept between visits (never the results, never a signature)
//==================================================================
export interface RandomPrefs
{
	tab: string;
	kind: string;    // whole, decimal, coin, dice
	from: string;
	to: string;
	count: number;
	noRepeats: boolean;
	places: number;
	headsPercent: number;
	sides: number;
	headWord: string;
	tailWord: string;
	passwordLength: number;
	upper: boolean;
	lower: boolean;
	digits: boolean;
	symbols: boolean;
	symbolSet: string;
	noLookAlikes: boolean;
	passwordCount: number;
	signaturePlace: string;
}

// The desktop's first values (SettingsStore.cs RandomPrefs)
export function firstPrefs(): RandomPrefs
{
	return {
		tab: "simple", kind: "whole", from: "1", to: "100", count: 1, noRepeats: false, places: 2, headsPercent: 50, sides: 6,
		headWord: "", tailWord: "", passwordLength: 16, upper: true, lower: true, digits: true, symbols: true,
		symbolSet: DEFAULT_SYMBOLS, noLookAlikes: false, passwordCount: 5, signaturePlace: "end",
	};
}

export function prefsFrom(saved: unknown): RandomPrefs
{
	const prefs = firstPrefs();
	if (typeof saved !== "object" || saved === null)
	{
		return prefs;
	}
	const s = saved as Record<string, unknown>;
	for (const key of Object.keys(prefs) as (keyof RandomPrefs)[])
	{
		if (typeof s[key] === typeof prefs[key])
		{
			(prefs as unknown as Record<string, unknown>)[key] = s[key];
		}
	}
	return prefs;
}
