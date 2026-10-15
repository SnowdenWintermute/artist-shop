// While leaving would lose work, asks first: the browser's own question when the tab closes or
// reloads (it shows its own words), and a confirm before a link in the page, which Blazor's
// enhanced navigation follows without unloading the page. Back isn't asked about: enhanced
// navigation handles it without asking NavigationLock, and working around that meant faking a
// history entry. Returns the function that stops asking
/** @param {string} question */
export function guardLeaving(question) {
  /** @param {BeforeUnloadEvent} event */
  function onBeforeUnload(event) {
    event.preventDefault();
  }

  /** @param {MouseEvent} event */
  function onClick(event) {
    const link = event.target instanceof Element ? event.target.closest("a[href]") : null;

    // a new tab, or a jump within the page, leaves this one as it is
    if (
      !(link instanceof HTMLAnchorElement) ||
      link.target === "_blank" ||
      link.getAttribute("href")?.startsWith("#") ||
      event.button !== 0 ||
      event.ctrlKey ||
      event.metaKey ||
      event.shiftKey
    ) {
      return;
    }

    if (window.confirm(question)) {
      // asked already, so the browser's own question doesn't follow it
      release();
      return;
    }

    // capture on window runs before Blazor's own click handler on the document
    event.preventDefault();
    event.stopImmediatePropagation();
  }

  function release() {
    window.removeEventListener("beforeunload", onBeforeUnload);
    window.removeEventListener("click", onClick, { capture: true });
  }

  window.addEventListener("beforeunload", onBeforeUnload);
  window.addEventListener("click", onClick, { capture: true });

  return release;
}
