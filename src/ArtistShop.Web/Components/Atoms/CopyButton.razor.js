// Puts its data-text on the clipboard when its button is clicked, and says so beside it for a moment
customElements.define(
  "copy-button",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    /** @type {ReturnType<typeof setTimeout> | undefined} */
    #clearStatus;

    connectedCallback() {
      this.#listeners = new AbortController();

      this.addEventListener(
        "click",
        (event) => {
          if (event.target instanceof Element && event.target.closest("button")) {
            this.#copy();
          }
        },
        { signal: this.#listeners.signal },
      );
    }

    disconnectedCallback() {
      this.#listeners?.abort();
      clearTimeout(this.#clearStatus);
    }

    async #copy() {
      const text = this.dataset.text;

      if (text === undefined) {
        return;
      }

      // the browser only allows this on https or localhost
      try {
        await navigator.clipboard.writeText(text);
        this.#show("Copied");
      } catch {
        this.#show("Couldn't copy: select the code instead");
      }
    }

    /** @param {string} message */
    #show(message) {
      const status = this.querySelector("[data-copy-status]");

      if (!status) {
        return;
      }

      status.textContent = message;
      clearTimeout(this.#clearStatus);
      this.#clearStatus = setTimeout(() => (status.textContent = ""), 2000);
    }
  },
);
