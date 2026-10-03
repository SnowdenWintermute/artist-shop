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
    const { saveUrl, inputName, nameField, valueField, tokenField, token } = this.dataset;

    if (
      !(target instanceof HTMLSelectElement) ||
      saveUrl === undefined ||
      inputName === undefined ||
      nameField === undefined ||
      valueField === undefined
    ) {
      return;
    }

    const body = new URLSearchParams({ [nameField]: inputName, [valueField]: target.value });

    if (tokenField !== undefined && token !== undefined) {
      body.append(tokenField, token);
    }

    // keepalive lets the save finish if the page is closed or left by a full load right after
    fetch(saveUrl, { method: "POST", body, keepalive: true }).catch(() => {});
  }
}

customElements.define("remembered-select", RememberedSelect);
