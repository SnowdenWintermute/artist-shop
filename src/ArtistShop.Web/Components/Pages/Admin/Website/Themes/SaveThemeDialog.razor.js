// The Theme page's Save as new theme dialog. Its fields are the theme form's, so the colours post
// with the name, and Enter in the name would press the form's first submit button: here it presses
// the dialog's own, marked data-enter-presses. Closing without saving puts the name back, so a name
// typed and cancelled isn't a change to the theme
class SaveThemeDialog extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  // from a save until the page has caught up, when the name typed is still to be posted
  #isSaving = false;

  connectedCallback() {
    this.#listeners = new AbortController();
    const { signal } = this.#listeners;

    this.addEventListener("keydown", (event) => this.#onKeyDown(event), { signal });
    this.addEventListener("click", (event) => this.#onClick(event), { signal });
    // close doesn't bubble, so this listens on the way down
    this.addEventListener("close", () => this.#onClose(), { signal, capture: true });
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  caughtUp() {
    this.#isSaving = false;
  }

  /** @param {KeyboardEvent} event */
  #onKeyDown(event) {
    const button = this.querySelector("[data-enter-presses]");

    if (event.key !== "Enter" || event.isComposing || !(event.target instanceof HTMLInputElement) || !(button instanceof HTMLButtonElement)) {
      return;
    }

    event.preventDefault();
    button.click();
  }

  // the dialog closes as it posts (ModalDialog's ClosesOnSubmit), which mustn't put the name back
  /** @param {MouseEvent} event */
  #onClick(event) {
    if (event.target instanceof Element && event.target.closest("button[type=submit]")) {
      this.#isSaving = true;
    }
  }

  #onClose() {
    if (this.#isSaving) {
      return;
    }

    this.querySelectorAll("input").forEach((input) => {
      if (input.value !== input.defaultValue) {
        input.value = input.defaultValue;
        // so EnableSaveOnChange and the picker see the form back as it was
        input.dispatchEvent(new Event("input", { bubbles: true }));
      }
    });
  }
}

customElements.define("save-theme-dialog", SaveThemeDialog);

Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("save-theme-dialog").forEach((element) => {
    if (element instanceof SaveThemeDialog) {
      element.caughtUp();
    }
  })
);
