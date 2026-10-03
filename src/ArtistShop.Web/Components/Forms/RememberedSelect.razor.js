// Saves the option its select is changed to, for RememberedSelect to render the next time the page
// loads. Nothing waits on the save: one that fails only means the page comes back at the old option
class RememberedSelect extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    this.addEventListener("change", (event) => this.#save(event.target), { signal: this.#listeners.signal });
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  /** @param {EventTarget | null} target */
  #save(target) {
    const { saveUrl, inputName, tokenField, token } = this.dataset;

    if (!(target instanceof HTMLSelectElement) || saveUrl === undefined || inputName === undefined) {
      return;
    }

    // the field names RememberedInputEndpoints binds
    const body = new URLSearchParams({ name: inputName, value: target.value });

    if (tokenField !== undefined && token !== undefined) {
      body.append(tokenField, token);
    }

    fetch(saveUrl, { method: "POST", body }).catch(() => {});
  }
}

customElements.define("remembered-select", RememberedSelect);
