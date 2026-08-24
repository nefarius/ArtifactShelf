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
