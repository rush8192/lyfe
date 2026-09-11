import { spawn } from "node:child_process";
import { access, mkdtemp, rm } from "node:fs/promises";
import { arch, cpus, platform, release, tmpdir, totalmem } from "node:os";
import { join } from "node:path";
import process from "node:process";
import { createServer } from "vite";

const candidates = [
  process.env.LYFE_CHROME,
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
  "/usr/bin/google-chrome",
  "/usr/bin/chromium",
  "/usr/bin/chromium-browser",
].filter(Boolean);
const chrome = await firstExisting(candidates);
if (chrome === null) {
  throw new Error("Chrome or Chromium was not found. Set LYFE_CHROME to its executable path.");
}

const server = await createServer({
  server: { host: "127.0.0.1", port: 0 },
  logLevel: "error",
});
await server.listen();
const address = server.httpServer?.address();
if (address === null || typeof address === "string" || address === undefined) {
  await server.close();
  throw new Error("The benchmark server did not expose a local TCP port.");
}

try {
  const url = `http://127.0.0.1:${address.port}/render-benchmark.html`;
  const result = await runChrome(chrome, url);
  result.host = {
    platform: platform(),
    release: release(),
    architecture: arch(),
    cpu: cpus()[0]?.model ?? "unknown",
    logicalCores: cpus().length,
    totalMemoryGiB: Number((totalmem() / 1024 ** 3).toFixed(1)),
  };
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
} finally {
  await server.close();
}

async function firstExisting(paths) {
  for (const path of paths) {
    try {
      await access(path);
      return path;
    } catch {
      // Continue to the next supported browser location.
    }
  }
  return null;
}

async function runChrome(executable, url) {
  const profile = await mkdtemp(join(tmpdir(), "lyfe-render-benchmark-"));
  const child = spawn(executable, [
      "--headless=new",
      "--disable-background-networking",
      "--disable-breakpad",
      "--disable-component-update",
      "--disable-default-apps",
      "--disable-dev-shm-usage",
      "--disable-extensions",
      "--disable-sync",
      "--enable-unsafe-swiftshader",
      "--hide-scrollbars",
      "--no-first-run",
      "--no-sandbox",
      "--remote-debugging-port=0",
      `--user-data-dir=${profile}`,
      "about:blank",
    ], { stdio: ["ignore", "pipe", "pipe"] });
  let stderr = "";
  try {
    child.stderr.setEncoding("utf8");
    child.stderr.on("data", (chunk) => { stderr += chunk; });
    const endpoint = await waitForDevTools(child, () => stderr);
    const cdp = await connectCdp(endpoint);
    try {
      const target = await cdp.send("Target.createTarget", { url });
      const attached = await cdp.send("Target.attachToTarget", {
        targetId: target.targetId,
        flatten: true,
      });
      await cdp.send("Runtime.enable", {}, attached.sessionId);
      const deadline = Date.now() + 120_000;
      let title = "";
      while (Date.now() < deadline) {
        title = await evaluate(cdp, attached.sessionId, "document.title");
        if (title === "LYFE_RENDER_BENCHMARK_COMPLETE" || title === "LYFE_RENDER_BENCHMARK_FAILED") break;
        await delay(100);
      }
      const text = await evaluate(
        cdp,
        attached.sessionId,
        "document.querySelector('#benchmark-results')?.textContent ?? ''",
      );
      if (title !== "LYFE_RENDER_BENCHMARK_COMPLETE") {
        throw new Error(text || `Benchmark timed out with title ${title}.`);
      }
      return JSON.parse(text);
    } finally {
      await cdp.send("Browser.close").catch(() => undefined);
      cdp.close();
    }
  } finally {
    if (child.exitCode === null) child.kill("SIGTERM");
    await rm(profile, { recursive: true, force: true });
  }
}

function waitForDevTools(child, stderr) {
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error(`Chrome DevTools did not start: ${stderr()}`)), 15_000);
    const inspect = (chunk) => {
      const match = String(chunk).match(/DevTools listening on (ws:\/\/[^\s]+)/);
      if (match === null) return;
      clearTimeout(timeout);
      child.stderr.off("data", inspect);
      resolve(match[1]);
    };
    child.stderr.on("data", inspect);
    child.once("error", (error) => {
      clearTimeout(timeout);
      reject(error);
    });
    child.once("exit", (code) => {
      clearTimeout(timeout);
      reject(new Error(`Chrome exited with ${code}: ${stderr()}`));
    });
  });
}

async function connectCdp(endpoint) {
  const socket = new WebSocket(endpoint);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });
  let nextId = 1;
  const pending = new Map();
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(String(event.data));
    if (message.id === undefined) return;
    const promise = pending.get(message.id);
    if (promise === undefined) return;
    pending.delete(message.id);
    if (message.error === undefined) promise.resolve(message.result);
    else promise.reject(new Error(message.error.message));
  });
  return {
    send(method, params = {}, sessionId) {
      const id = nextId++;
      return new Promise((resolve, reject) => {
        pending.set(id, { resolve, reject });
        socket.send(JSON.stringify({ id, method, params, ...(sessionId === undefined ? {} : { sessionId }) }));
      });
    },
    close() { socket.close(); },
  };
}

async function evaluate(cdp, sessionId, expression) {
  const response = await cdp.send("Runtime.evaluate", {
    expression,
    awaitPromise: true,
    returnByValue: true,
  }, sessionId);
  if (response.exceptionDetails !== undefined) {
    throw new Error(response.exceptionDetails.text);
  }
  return response.result.value;
}

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}
