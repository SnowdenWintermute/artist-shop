// A filter bar posts its form as soon as a control changes, which keeps the page's whole state
// in the URL with no island. A text input fires "change" when it loses focus or takes Enter,
// never per keystroke, so the search box needs no separate handling.
// It submits at once rather than after a pause: a page load landing during a pause would put the
// boxes back as they were before the later clicks, and the delayed submit would then send that.
// Each submit cancels the load before it, so the last one's page is the one that lands.
// From the submit until the page has caught up it carries data-pending, which a form marked
// data-waits-for-pending waits on (static-form-submit.js)
class SubmitOnChange extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    this.addEventListener("change", () => this.#submit(), { signal: this.#listeners.signal });
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  #submit() {
    this.setAttribute("data-pending", "");
    this.closest("form")?.requestSubmit();
  }
}

customElements.define("submit-on-change", SubmitOnChange);

Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("submit-on-change").forEach((element) => element.removeAttribute("data-pending"))
);
