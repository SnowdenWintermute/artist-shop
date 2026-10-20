// The buttons choosing an image: the image library's picker, whose chosen or newly uploaded image is put in
// the form's hidden fields and posted at once, and the work picker, whose image is too
import { attachImagePicker, createFilePicker } from "../../Publishing/Images/ImagePicker/ImagePickerDialog.razor.js";
import { attachWorkPicker } from "../../Catalog/WorkPicker/WorkPickerDialog.razor.js";
import { uploadWithRetries } from "/js/upload-request.js";

customElements.define(
  "image-picker-buttons",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    /** @type {Set<() => void>} */
    #aborts = new Set();
    /** @type {HTMLButtonElement[]} */
    #chooseButtons = [];

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;
      const imageDialog = this.querySelector('dialog[data-part="image-picker"]');
      const workDialog = this.querySelector('dialog[data-part="work-picker"]');
      const fileInput = this.querySelector('input[data-part="image-file"]');
      const libraryButton = this.querySelector('button[data-action="choose-library-image"]');
      const workButton = this.querySelector('button[data-action="choose-work-image"]');

      if (
        !(imageDialog instanceof HTMLDialogElement) ||
        !(workDialog instanceof HTMLDialogElement) ||
        !(fileInput instanceof HTMLInputElement) ||
        !(libraryButton instanceof HTMLButtonElement) ||
        !(workButton instanceof HTMLButtonElement)
      ) {
        throw new Error("The image picker buttons are missing a picker, their file input or a Choose button.");
      }

      this.#chooseButtons = [libraryButton, workButton];
      const imagePicker = attachImagePicker(imageDialog, signal);
      const workPicker = attachWorkPicker(workDialog, signal);
      const filePicker = createFilePicker(fileInput);

      libraryButton.addEventListener(
        "click",
        () =>
          imagePicker.open({
            onUpload: () => filePicker.open({ multiple: false }, ([file]) => this.#upload(file)),
            onChosen: (image) => this.#save(image.storageKey, ""),
          }),
        { signal }
      );

      workButton.addEventListener(
        "click",
        () => workPicker.open(workPicker.chooseImageUrl, ({ workId, storageKey }) => this.#save(storageKey, String(workId))),
        { signal }
      );
    }

    disconnectedCallback() {
      this.#listeners?.abort();
      this.#aborts.forEach((abort) => abort());
    }

    // workId is empty for a library image
    /**
     * @param {string} storageKey
     * @param {string} workId
     */
    #save(storageKey, workId) {
      const storageKeyField = this.querySelector('input[data-part="storage-key"]');
      const workIdField = this.querySelector('input[data-part="work-id"]');

      if (storageKeyField instanceof HTMLInputElement && workIdField instanceof HTMLInputElement) {
        storageKeyField.value = storageKey;
        workIdField.value = workId;
        this.closest("form")?.requestSubmit();
      }
    }

    /** @param {File} file */
    async #upload(file) {
      const url = this.dataset.uploadUrl;

      if (url === undefined) {
        throw new Error("The image picker buttons are missing their upload address.");
      }

      this.#showStatus("Uploading…");
      // held until it's done, so a second choice can't race it to the form
      this.#setChoosing(false);
      const outcome = await uploadWithRetries({
        id: "image",
        url,
        file,
        progress: { loaded: () => {}, retrying: () => this.#showStatus("The server is busy. Trying again…"), finished: () => {} },
        aborts: this.#aborts,
        isStopped: () => !this.isConnected,
      });

      if (outcome === null) {
        return;
      }

      if ("failure" in outcome) {
        this.#showStatus(outcome.failure);
        this.#setChoosing(true);
        return;
      }

      /** @type {import("../../Publishing/Images/ImagePicker/ImagePickerDialog.razor.js").UploadedImage} */
      const image = JSON.parse(outcome.response.text);
      this.#save(image.storageKey, "");
    }

    /** @param {boolean} isOn */
    #setChoosing(isOn) {
      this.#chooseButtons.forEach((button) => (button.disabled = !isOn));
    }

    /** @param {string} text */
    #showStatus(text) {
      const status = this.querySelector('[data-part="upload-status"]');

      if (status instanceof HTMLElement) {
        status.textContent = text;
      }
    }
  }
);
