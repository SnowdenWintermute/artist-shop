// Whether a static form holds anything other than what was loaded. It compares the form's data,
// which on a static page names every field. A page rendered from a post that didn't save, marked
// data-holds-unsaved-changes on the form or an element round it, holds changes whatever its fields
// say. What to do about changes is up to the caller, like EnableSaveOnChange's Save button
export class FormChanges {
  /** @type {() => HTMLFormElement | null} */
  #findForm;
  #loadedState = "";

  // a finder rather than the form, since Blazor adds an element to the page before its children
  /** @param {() => HTMLFormElement | null} findForm */
  constructor(findForm) {
    this.#findForm = findForm;
    this.reset();
  }

  // what the form holds now becomes what was loaded
  reset() {
    this.#loadedState = this.#state();
  }

  get hasChanges() {
    const holdsUnsavedChanges = this.#findForm()?.closest("[data-holds-unsaved-changes]") != null;

    return holdsUnsavedChanges || this.#state() !== this.#loadedState;
  }

  // Not FormData, which leaves out disabled controls: an island's controls are disabled until its
  // circuit connects, so the state would change by itself a moment after the page loads
  #state() {
    const form = this.#findForm();

    if (form === null) {
      return "";
    }

    const entries = new URLSearchParams();

    for (const control of form.elements) {
      // a control with no name is never posted, like the header dropdown Quill hides in its toolbar
      if (!("name" in control) || control.name === "") {
        continue;
      }

      if (control instanceof HTMLSelectElement) {
        [...control.selectedOptions].forEach((option) => entries.append(control.name, option.value));
      } else if (control instanceof HTMLTextAreaElement) {
        entries.append(control.name, control.value);
      } else if (control instanceof HTMLInputElement) {
        const isUncheckedChoice = (control.type === "checkbox" || control.type === "radio") && !control.checked;

        if (control.type !== "file" && !isUncheckedChoice) {
          entries.append(control.name, control.value);
        }
      }
    }

    return entries.toString();
  }
}
