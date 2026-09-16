const DRAG_OVER_CLASS = "bg-blue-50";

customElements.define(
  "file-drop-zone",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    // Listeners go on the element itself, and children are looked up when an event arrives:
    // Blazor puts an element into the page before its children, so they may not exist yet here
    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      this.addEventListener("click", (event) => this.#onClick(event), { signal });
      this.addEventListener("dragover", (event) => this.#onDragOver(event), { signal });
      this.addEventListener("dragleave", () => this.classList.remove(DRAG_OVER_CLASS), { signal });
      this.addEventListener("drop", (event) => this.#onDrop(event), { signal });
      // "change" bubbles up from the input, whether the picker or a drop set its files
      this.addEventListener("change", () => this.#showFileNames(), { signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    #fileInput() {
      const input = this.querySelector('input[type="file"]');
      return input instanceof HTMLInputElement ? input : null;
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      if (event.target instanceof Element && event.target.closest('[data-part="choose"]')) {
        this.#fileInput()?.click();
      }
    }

    /** @param {DragEvent} event */
    #onDragOver(event) {
      // without this the browser opens the dropped file instead of letting us have it
      event.preventDefault();
      this.classList.add(DRAG_OVER_CLASS);
    }

    /** @param {DragEvent} event */
    #onDrop(event) {
      event.preventDefault();
      this.classList.remove(DRAG_OVER_CLASS);

      const input = this.#fileInput();
      // ":disabled" also matches an input inside a disabled fieldset, as in an island not yet attached
      if (!input || input.matches(":disabled") || !event.dataTransfer) {
        return;
      }

      const dropped = [...event.dataTransfer.files];
      if (dropped.length === 0) {
        return;
      }

      // an input's files can only be replaced by a FileList, and a DataTransfer is the way to build one.
      // A drop ignores "multiple" and "accept", so "multiple" is applied here; the server checks the type
      const kept = new DataTransfer();
      for (const file of input.multiple ? dropped : dropped.slice(0, 1)) {
        kept.items.add(file);
      }
      input.files = kept.files;

      // setting files from script fires no event, so send the one picking a file would
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    #showFileNames() {
      const names = this.querySelector('[data-part="file-names"]');
      const input = this.#fileInput();
      if (!names || !input) {
        return;
      }

      names.textContent = [...(input.files ?? [])].map((file) => file.name).join(", ");
    }
  }
);
