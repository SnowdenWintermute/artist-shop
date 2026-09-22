// Keeps an edit form's Save disabled while the form still holds what was loaded. It compares the
// form's data, which on a static page names every field. Typing raises input, and a hidden input
// changed by code raises nothing, but its value is an attribute, so the observer sees it: the image
// list's inputs, rendered by an island, and whatever an editor writes back
customElements.define(
  "enable-save-on-change",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    /** @type {MutationObserver | null} */
    #observer = null;
    #loadedState = "";

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
      this.#loadedState = this.#formState();
      this.#update();
    }

    // Not FormData, which leaves out disabled controls: an island's controls are disabled until its
    // circuit connects, so the state would change by itself a moment after the page loads
    #formState() {
      const form = this.querySelector("form");

      if (form === null) {
        return "";
      }

      const entries = new URLSearchParams();

      for (const control of form.elements) {
        if (control instanceof HTMLSelectElement) {
          [...control.selectedOptions].forEach((option) => entries.append(control.name, option.value));
        } else if (control instanceof HTMLTextAreaElement) {
          entries.append(control.name, control.value);
        } else if (control instanceof HTMLInputElement && control.name !== "") {
          const isUncheckedChoice = (control.type === "checkbox" || control.type === "radio") && !control.checked;

          if (control.type !== "file" && !isUncheckedChoice) {
            entries.append(control.name, control.value);
          }
        }
      }

      return entries.toString();
    }

    #update() {
      const isUnchanged =
        !this.hasAttribute("data-holds-unsaved-changes") && this.#formState() === this.#loadedState;

      this.querySelectorAll("[data-waits-for-change]").forEach((button) => {
        button.disabled = isUnchanged;
      });
    }
  }
);

// after a save, the enhanced navigation patches the form in place with the saved values without
// connecting it again, and puts back the server's attributes, which re-enables the buttons
Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("enable-save-on-change").forEach((element) => element.reset())
);
