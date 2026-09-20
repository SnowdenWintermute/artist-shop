import { submitOnChangeDelayMilliseconds } from "/js/app-consts.js";

// A filter bar posts its form as soon as a control changes, which keeps the page's whole state
// in the URL with no island. A text input fires "change" when it loses focus or takes Enter,
// never per keystroke, so the search box needs no separate handling
customElements.define(
  "submit-on-change",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    /** @type {ReturnType<typeof setTimeout> | undefined} */
    #pending;

    connectedCallback() {
      this.#listeners = new AbortController();
      this.addEventListener("change", () => this.#submitSoon(), { signal: this.#listeners.signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
      clearTimeout(this.#pending);
    }

    #submitSoon() {
      clearTimeout(this.#pending);
      this.#pending = setTimeout(
        () => this.closest("form")?.requestSubmit(),
        submitOnChangeDelayMilliseconds
      );
    }
  }
);
