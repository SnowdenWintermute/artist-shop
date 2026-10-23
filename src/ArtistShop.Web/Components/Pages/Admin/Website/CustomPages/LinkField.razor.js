// A component's link choice. Choosing Work opens the work picker instead of saving, and puts the radio back until
// a work is picked, which then saves with Work checked; choosing Address only shows its field, saved with Save.
// Both are caught on the way down, before the SubmitOnChange inside sees the change
import { attachWorkPicker } from "../../Catalog/WorkPicker/WorkPickerDialog.razor.js";

customElements.define(
  "link-field",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;
      const dialog = this.querySelector('dialog[data-part="work-picker"]');
      const workField = this.querySelector('input[data-part="work-id"]');
      const workRadio = this.querySelector('input[type="radio"][value="Work"]');
      const addressRadio = this.querySelector('input[type="radio"][value="Address"]');

      if (
        !(dialog instanceof HTMLDialogElement) ||
        !(workField instanceof HTMLInputElement) ||
        !(workRadio instanceof HTMLInputElement) ||
        !(addressRadio instanceof HTMLInputElement)
      ) {
        throw new Error("The link field is missing its work picker, its work field or its Work and Address choices.");
      }

      const picker = attachWorkPicker(dialog, signal);
      const chooseWork = () =>
        picker.open(picker.chooseWorkUrl, ({ workId }) => {
          workField.value = String(workId);
          workRadio.checked = true;
          this.closest("form")?.requestSubmit();
        });

      this.addEventListener(
        "change",
        (event) => {
          if (event.target === workRadio) {
            event.stopPropagation();
            // the type as the page was drawn with it
            const saved = [...this.querySelectorAll(`input[type="radio"][name="${workRadio.name}"]`)].find(
              (radio) => radio instanceof HTMLInputElement && radio.defaultChecked
            );
            if (saved instanceof HTMLInputElement) {
              saved.checked = true;
            }
            chooseWork();
          } else if (event.target === addressRadio) {
            event.stopPropagation();
            const address = this.querySelector('input[name="Input.LinkTo.Address"]');
            if (address instanceof HTMLInputElement) {
              address.focus();
            }
          }
        },
        { capture: true, signal }
      );

      this.addEventListener(
        "click",
        (event) => {
          if (event.target instanceof Element && event.target.closest('button[data-action="choose-work"]')) {
            chooseWork();
          }
        },
        { signal }
      );
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }
  }
);
