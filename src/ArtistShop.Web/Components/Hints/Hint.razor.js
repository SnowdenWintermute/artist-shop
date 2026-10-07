// A hint's close button hides it, or for one that collapses leaves only its Show help text, which
// shows it again. Either way it's saved, so the page renders it like that from then on. Nothing waits
// on the save: one that fails only means the next page load shows it as it was
class HintToggle extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    this.addEventListener("click", (event) => this.#onClick(event), { signal: this.#listeners.signal });
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  /** @param {MouseEvent} event */
  #onClick(event) {
    const action = event.target instanceof Element ? event.target.closest("[data-hint-action]") : null;
    const hint = this.closest("[data-hint]");

    if (action === null || !this.contains(action) || !(hint instanceof HTMLElement)) {
      return;
    }

    const dismissed = action.getAttribute("data-hint-action") === "dismiss";

    if (hint.hasAttribute("data-collapses")) {
      hint.toggleAttribute("data-dismissed", dismissed);
    } else {
      // hidden rather than removed, as an island's renderer still owns the element
      hint.toggleAttribute("hidden", dismissed);
    }

    this.#save(hint.dataset.hint, dismissed);
  }

  /**
   * @param {string | undefined} hintName
   * @param {boolean} dismissed
   */
  #save(hintName, dismissed) {
    const { saveUrl, hintField, dismissedField, tokenField, token } = this.dataset;

    if (saveUrl === undefined || hintField === undefined || dismissedField === undefined || hintName === undefined) {
      return;
    }

    const body = new URLSearchParams({ [hintField]: hintName, [dismissedField]: String(dismissed) });

    if (tokenField !== undefined && token !== undefined) {
      body.append(tokenField, token);
    }

    // keepalive lets the save finish if the page is left right after
    fetch(saveUrl, { method: "POST", body, keepalive: true }).catch(() => {});
  }
}

customElements.define("hint-toggle", HintToggle);
