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
      // ":not([webkitdirectory])" so a zone with both pickers doesn't hand a dropped file to the folder input
      const input = this.querySelector('input[type="file"]:not([webkitdirectory])');
      return input instanceof HTMLInputElement ? input : null;
    }

    #directoryInput() {
      const input = this.querySelector('input[type="file"][webkitdirectory]');
      return input instanceof HTMLInputElement ? input : null;
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      if (!(event.target instanceof Element)) {
        return;
      }

      if (event.target.closest('[data-part="choose"]')) {
        this.#fileInput()?.click();
      } else if (event.target.closest('[data-part="choose-directory"]')) {
        this.#directoryInput()?.click();
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

      // a zone that can pick a folder can take one from a drop, so that input is the one whose
      // disabled state decides. dataTransfer.files never describes a folder's contents, so this
      // kind of zone gets entries and reads them itself
      const directoryInput = this.#directoryInput();
      const input = directoryInput ?? this.#fileInput();

      // ":disabled" also matches an input inside a disabled fieldset, as in an island not yet attached
      if (!input || input.matches(":disabled") || !event.dataTransfer) {
        return;
      }

      if (directoryInput) {
        this.#dispatchEntries(event.dataTransfer);
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

    /** @param {DataTransfer} dataTransfer */
    #dispatchEntries(dataTransfer) {
      // the items list is emptied as soon as this handler returns, so every entry is taken now
      // and read afterwards
      const entries = [...dataTransfer.items]
        .map((item) => item.webkitGetAsEntry())
        .filter((entry) => entry !== null);

      if (entries.length === 0) {
        return;
      }

      // bubbles, so a listener on a wrapping element hears it alongside "change"
      this.dispatchEvent(new CustomEvent("entriesdropped", { detail: entries, bubbles: true }));
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
