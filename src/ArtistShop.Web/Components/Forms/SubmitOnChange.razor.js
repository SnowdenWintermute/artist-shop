import { submitOnChangeDelayMilliseconds } from "/js/app-consts.js";

// A filter bar posts its form as soon as a control changes, which keeps the page's whole state
// in the URL with no island. A text input fires "change" when it loses focus or takes Enter,
// never per keystroke, so the search box needs no separate handling.
// From the change until the page has caught up it carries data-pending, which a form marked
// data-waits-for-pending waits on (static-form-submit.js)
class SubmitOnChange extends HTMLElement {
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

  // A load that landed while a later change is still waiting to submit hasn't caught up with it.
  // Set again either way, as patching the page in may have taken the attribute off
  caughtUp() {
    this.toggleAttribute("data-pending", this.#pending !== undefined);
  }

  #submitSoon() {
    clearTimeout(this.#pending);
    this.setAttribute("data-pending", "");
    this.#pending = setTimeout(() => {
      this.#pending = undefined;
      this.closest("form")?.requestSubmit();
    }, submitOnChangeDelayMilliseconds);
  }
}

customElements.define("submit-on-change", SubmitOnChange);

Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("submit-on-change").forEach((element) => {
    if (element instanceof SubmitOnChange) {
      element.caughtUp();
    }
  })
);
