// A link's work choice: the work picker, whose chosen work is put in the form's hidden field and posted at once
import { attachWorkPicker } from "../../Catalog/WorkPicker/WorkPickerDialog.razor.js";

customElements.define(
  "link-work-picker",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;
      const dialog = this.querySelector('dialog[data-part="work-picker"]');
      const button = this.querySelector('button[data-action="choose-work"]');
      const field = this.querySelector('input[data-part="work-id"]');

      if (!(dialog instanceof HTMLDialogElement) || !(button instanceof HTMLButtonElement) || !(field instanceof HTMLInputElement)) {
        throw new Error("The link's work picker is missing its dialog, its button or its field.");
      }

      const picker = attachWorkPicker(dialog, signal);

      button.addEventListener(
        "click",
        () =>
          picker.open(picker.chooseWorkUrl, ({ workId }) => {
            field.value = String(workId);
            this.closest("form")?.requestSubmit();
          }),
        { signal }
      );
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }
  }
);
