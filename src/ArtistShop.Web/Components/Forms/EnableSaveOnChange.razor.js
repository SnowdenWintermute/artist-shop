import { FormChanges } from "/js/form-changes.js";

// Keeps an edit form's Save disabled while the form still holds what was loaded (FormChanges).
// Typing raises input, and a hidden input changed by code raises nothing, but its value is an
// attribute, so the observer sees it: the image list's inputs, rendered by an island, and whatever
// an editor writes back
class EnableSaveOnChange extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  /** @type {MutationObserver | null} */
  #observer = null;
  #changes = new FormChanges(() => this.querySelector("form"));

  connectedCallback() {
    this.#listeners = new AbortController();
    this.addEventListener("input", () => this.#update(), { signal: this.#listeners.signal });
    this.addEventListener("change", () => this.#update(), { signal: this.#listeners.signal });
    this.#observer = new MutationObserver(() => this.#update());
    this.#observer.observe(this, {
      subtree: true,
      childList: true,
      attributes: true,
      attributeFilter: ["value"],
    });
    this.reset();
  }

  disconnectedCallback() {
    this.#listeners?.abort();
    this.#observer?.disconnect();
  }

  reset() {
    this.#changes.reset();
    this.#update();
  }

  #update() {
    const isUnchanged = !this.#changes.hasChanges;

    this.querySelectorAll("[data-waits-for-change]").forEach((button) => {
      button.toggleAttribute("disabled", isUnchanged);
    });
  }
}

customElements.define("enable-save-on-change", EnableSaveOnChange);

// after a save, the enhanced navigation patches the form in place with the saved values without
// connecting it again, and puts back the server's attributes, which re-enables the buttons
Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("enable-save-on-change").forEach((element) => {
    if (element instanceof EnableSaveOnChange) {
      element.reset();
    }
  })
);
