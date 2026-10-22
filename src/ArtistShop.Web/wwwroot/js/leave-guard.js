// While leaving would lose work, asks first: the browser's own question when the tab closes or
// reloads (it shows its own words), and a confirm before a link in the page, which Blazor's
// enhanced navigation follows without unloading the page. Back isn't asked about: enhanced
// navigation handles it without asking NavigationLock, and working around that meant faking a
// history entry. A link or submit button marked data-discards-changes leaves without asking.
// Returns the function that stops asking
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

    if (link.hasAttribute("data-discards-changes") || window.confirm(question)) {
      // asked already, or discarding anyway, so the browser's own question doesn't follow it
      release();
      return;
    }

    // capture on window runs before Blazor's own click handler on the document
    event.preventDefault();
    event.stopImmediatePropagation();
  }

  // a form that isn't enhanced loads a new page, which would ask again after the page's own question
  /** @param {SubmitEvent} event */
  function onSubmit(event) {
    if (event.submitter?.hasAttribute("data-discards-changes")) {
      release();
    }
  }

  function release() {
    window.removeEventListener("beforeunload", onBeforeUnload);
    window.removeEventListener("click", onClick, { capture: true });
    window.removeEventListener("submit", onSubmit, { capture: true });
  }

  window.addEventListener("beforeunload", onBeforeUnload);
  window.addEventListener("click", onClick, { capture: true });
  window.addEventListener("submit", onSubmit, { capture: true });

  return release;
}
