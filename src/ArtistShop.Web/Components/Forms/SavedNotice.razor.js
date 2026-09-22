// "Saved." stops being true once anything on the page changes. An input event bubbles from every
// field, a checkbox, a select and Quill's editor alike, per keystroke, where an island's form hears
// of a text field only when it loses focus.
// Hidden rather than removed: inside an island the element is Blazor's, and removing it would break
// Blazor's next change there. On a static page, the patch after the next save puts the server's
// attributes back, which clears hidden
customElements.define(
  "saved-notice",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      document.addEventListener("input", () => (this.hidden = true), { signal: this.#listeners.signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }
  }
);
