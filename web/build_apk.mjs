// build_apk.mjs - Build the signed Android APK in one go (owner 2026-11-18)
//
// Builds the web app, copies it into the Android project (Capacitor sync), runs Gradle's release
// build, checks the signature and writes dist-apk/SoftCalc-<version>.apk (not in git) with its
// SHA-256. Needs JDK 21 and the Android SDK: JAVA_HOME and ANDROID_HOME, or the places the README
// names under %LOCALAPPDATA%; and android/keystore.local.properties for the release key.
//   npm run apk

import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const web = path.dirname(fileURLToPath(import.meta.url));
const android = path.join(web, "android");
const local = process.env.LOCALAPPDATA ?? "";
const windows = process.platform === "win32";

function fail(message)
{
	console.error(`apk: ${message}`);
	process.exit(1);
}

// The first JDK folder under %LOCALAPPDATA%\Programs\jdk when JAVA_HOME is not set
function findJava()
{
	if (process.env.JAVA_HOME)
		return process.env.JAVA_HOME;
	const root = path.join(local, "Programs", "jdk");
	const found = existsSync(root) ? readdirSync(root).filter(name => name.startsWith("jdk-21")).sort().pop() : undefined;
	return found ? path.join(root, found) : fail("no JDK 21: set JAVA_HOME (web/README.md, Android app)");
}

const javaHome = findJava();
const sdk = process.env.ANDROID_HOME ?? path.join(local, "Android", "Sdk");
if (!existsSync(sdk))
	fail(`no Android SDK at ${sdk}: set ANDROID_HOME`);
if (!existsSync(path.join(android, "keystore.local.properties")))
	fail("no android/keystore.local.properties: the release key is needed (android/keystore.example.properties)");
const env = { ...process.env, JAVA_HOME: javaHome, ANDROID_HOME: sdk };
// npm, npx and the .bat tools need a shell on Windows; a full path in quotes, as cmd does not look in cwd
const quote = file => windows && file.includes(path.sep) ? `"${file}"` : file;
const run = (file, args, cwd = web) => execFileSync(quote(file), args, { cwd, env, stdio: "inherit", shell: windows });

//==================================================================
//  Web app, Capacitor sync, Gradle release build
//==================================================================
run("npm", ["run", "app:build"]);
run("npx", ["cap", "sync", "android"]);
run(path.join(android, windows ? "gradlew.bat" : "gradlew"), ["assembleRelease", "--console=plain"], android);

//==================================================================
//  Signature check, versioned copy, hashes
//==================================================================
const built = path.join(android, "app", "build", "outputs", "apk", "release", "app-release.apk");
const tools = path.join(sdk, "build-tools");
const buildTools = readdirSync(tools).sort().pop();
const signer = execFileSync(quote(path.join(tools, buildTools, windows ? "apksigner.bat" : "apksigner")),
	["verify", "--print-certs", quote(built)], { env, encoding: "utf8", shell: windows });
const certificate = signer.match(/certificate SHA-256 digest: (\w+)/)?.[1] ?? fail("apksigner found no signature");
const metadata = JSON.parse(readFileSync(path.join(path.dirname(built), "output-metadata.json"), "utf8")).elements[0];
const target = path.join(web, "dist-apk");
mkdirSync(target, { recursive: true });
const apk = path.join(target, `SoftCalc-${metadata.versionName}.apk`);
copyFileSync(built, apk);
const hash = createHash("sha256").update(readFileSync(apk)).digest("hex");
console.log(`\n${apk}\n  version ${metadata.versionName} (code ${metadata.versionCode})`);
console.log(`  APK SHA-256          ${hash}\n  signing key SHA-256  ${certificate}`);
