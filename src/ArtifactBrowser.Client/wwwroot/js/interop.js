export function applyUiScale(multiplier) {
  document.documentElement.style.setProperty("--ui-scale", String(multiplier));
}

export function getItem(key) {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function setItem(key, value) {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // Ignore storage failures (e.g. private browsing quota).
  }
}

export function focusElement(selector) {
  const el = document.querySelector(selector);
  if (el) {
    el.focus();
  }
}

export async function copyText(text) {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return;
    } catch {
      // Fall through to the execCommand path when the Clipboard API is denied.
    }
  }

  const textarea = document.createElement("textarea");
  textarea.value = text;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.left = "-9999px";
  document.body.appendChild(textarea);
  textarea.select();
  const ok = document.execCommand("copy");
  textarea.remove();
  if (!ok) {
    throw new Error("Copy failed");
  }
}

export function getActiveElementRect() {
  const el = document.activeElement;
  if (!el || el === document.body || el === document.documentElement) {
    return { x: 8, y: 8 };
  }

  const rect = el.getBoundingClientRect();
  return { x: rect.left, y: rect.bottom };
}

export function placeContextMenu(el, x, y) {
  if (!el) {
    return;
  }

  const pad = 8;
  el.style.left = `${x}px`;
  el.style.top = `${y}px`;
  const rect = el.getBoundingClientRect();
  const left = Math.max(pad, Math.min(x, window.innerWidth - rect.width - pad));
  const top = Math.max(pad, Math.min(y, window.innerHeight - rect.height - pad));
  el.style.left = `${left}px`;
  el.style.top = `${top}px`;
}

export function subscribeCloseOnScroll(dotNetHelper) {
  const handler = () => {
    cleanup();
    dotNetHelper.invokeMethodAsync("CloseFromJs");
  };

  function cleanup() {
    window.removeEventListener("scroll", handler, true);
    window.removeEventListener("resize", handler);
  }

  window.addEventListener("scroll", handler, true);
  window.addEventListener("resize", handler);
  return { dispose: cleanup };
}

export async function downloadViaPost(url, jsonBody, filename) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: jsonBody,
  });

  if (!response.ok) {
    throw new Error(`Download failed with status ${response.status}`);
  }

  const blob = await response.blob();
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = objectUrl;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(objectUrl);
}
