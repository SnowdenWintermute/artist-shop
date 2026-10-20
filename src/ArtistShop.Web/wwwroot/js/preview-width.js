// The admin previews' Desktop and Phone buttons (PreviewWidthButtons), marked data-preview-width. The width chosen
// is kept for the tab and shared by every preview, since a save or a choice loads the page again
const storageKey = "preview-width";

/** @param {string} width */
export function rememberPreviewWidth(width) {
  try {
    sessionStorage.setItem(storageKey, width);
  } catch {
    // a blocked storage only means the next load starts at Desktop
  }
}

// the button for the width last chosen, null when there's none
/** @param {Element} preview */
export function rememberedWidthButton(preview) {
  let width = null;

  try {
    width = sessionStorage.getItem(storageKey);
  } catch {
    return null;
  }

  return [...preview.querySelectorAll("[data-preview-width]")].find((button) => button.getAttribute("data-preview-width") === width) ?? null;
}

/** @param {Element} preview @param {Element} chosen */
export function pressWidthButton(preview, chosen) {
  for (const button of preview.querySelectorAll("[data-preview-width]")) {
    button.setAttribute("aria-pressed", String(button === chosen));
  }
}
