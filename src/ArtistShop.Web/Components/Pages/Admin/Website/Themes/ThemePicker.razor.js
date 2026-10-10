import { FormChanges } from "/js/form-changes.js";

// The Theme page's picker. Choosing another theme while the theme form holds changes opens the
// switch dialog (SwitchThemeDialog, elsewhere on the page) instead: Switch and discard submits the
// picker after all (data-discards-changes), and Save and switch posts the theme form with
// the theme chosen (data-takes-choice). Closing it puts the choice back
class ThemePicker extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  // the dialog's, while it's open
  /** @type {AbortController | null} */
  #dialogListeners = null;
  #changes = new FormChanges(() => {
    const form = document.getElementById(this.dataset.themeForm ?? "");
    return form instanceof HTMLFormElement ? form : null;
  });

  connectedCallback() {
    this.#listeners = new AbortController();
    const { signal } = this.#listeners;

    this.addEventListener("submit", (event) => this.#onSubmit(event), { signal });
    // Blazor adds an element to the page before its children, and the theme form comes after this
    queueMicrotask(() => this.reset());
  }

  disconnectedCallback() {
    this.#listeners?.abort();
    this.#dialogListeners?.abort();
  }

  reset() {
    this.#changes.reset();
  }

  /** @param {SubmitEvent} event */
  #onSubmit(event) {
    const form = this.#pickerForm();

    if (form === null || event.target !== form || event.submitter?.hasAttribute("data-discards-changes") || !this.#changes.hasChanges) {
      return;
    }

    event.preventDefault();
    // SubmitOnChange marked the page as catching up, which would hold up the theme form's posts
    this.querySelectorAll("[data-pending]").forEach((element) => element.removeAttribute("data-pending"));

    const dialog = this.#dialog();

    if (dialog === null) {
      return;
    }

    const choice = form.querySelector("select")?.value ?? "";
    dialog.querySelectorAll("[data-takes-choice]").forEach((button) => {
      if (button instanceof HTMLButtonElement) {
        button.value = choice;
      }
    });

    this.#dialogListeners?.abort();
    this.#dialogListeners = new AbortController();
    const { signal } = this.#dialogListeners;
    // it closes as it posts too (ModalDialog's ClosesOnSubmit), once the post has its data, so
    // putting the choice back then is no loss to the post
    dialog.addEventListener(
      "close",
      () => {
        form.reset();
        this.#dialogListeners?.abort();
      },
      { signal }
    );

    dialog.showModal();
  }

  #pickerForm() {
    return this.querySelector("form");
  }

  #dialog() {
    const dialog = document.getElementById(this.dataset.dialog ?? "");
    return dialog instanceof HTMLDialogElement ? dialog : null;
  }
}

customElements.define("theme-picker", ThemePicker);

// a save or a preview patches the page in place without connecting this again
Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("theme-picker").forEach((element) => {
    if (element instanceof ThemePicker) {
      element.reset();
    }
  })
);
