import { FormChanges } from "/js/form-changes.js";
import { guardLeaving } from "/js/leave-guard.js";

const LEAVE_QUESTION = "You have unsaved changes. Leave anyway?";

// Keeps an edit form's Save disabled while the form still holds what was loaded (FormChanges).
// Typing raises input, and a hidden input changed by code raises nothing, but its value is an
// attribute, so the observer sees it: the image list's inputs, rendered by an island, and whatever
// an editor writes back. Marked data-guards-leaving, it also asks before leaving with changes
class EnableSaveOnChange extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  /** @type {MutationObserver | null} */
  #observer = null;
  #changes = new FormChanges(() => this.querySelector("form"));
  // while it's asking before leaving
  /** @type {(() => void) | null} */
  #stopGuarding = null;

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
    this.#stopGuarding?.();
    this.#stopGuarding = null;
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

    if (isUnchanged || !this.hasAttribute("data-guards-leaving")) {
      this.#stopGuarding?.();
      this.#stopGuarding = null;
    } else if (this.#stopGuarding === null) {
      this.#stopGuarding = guardLeaving(LEAVE_QUESTION);
    }
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
