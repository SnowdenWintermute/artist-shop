import { sendUpload, uploadErrorMessage } from "/js/upload-request.js";

// A Branding section's upload form, sent from here so a refused image's reason shows under the field
// rather than on a page of its own. The endpoint answers a stored image with a redirect to this page,
// which the request follows, so a 200 means it's in and the page reloads to show it
class BrandImageUpload extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  #isSending = false;

  connectedCallback() {
    this.#listeners = new AbortController();
    this.addEventListener("submit", (event) => this.#onSubmit(event), { signal: this.#listeners.signal });
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  /** @param {SubmitEvent} event */
  async #onSubmit(event) {
    const form = event.target;

    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    // at once, before anything is awaited, so neither the browser nor static-form-submit.js posts it
    event.preventDefault();

    if (this.#isSending) {
      return;
    }

    const input = form.querySelector('input[type="file"]');
    const file = input instanceof HTMLInputElement ? input.files?.[0] : undefined;

    if (file === undefined) {
      this.#showError("Choose an image.");
      return;
    }

    const button = event.submitter instanceof HTMLElement ? event.submitter : null;
    this.#isSending = true;
    this.#showError("");
    button?.setAttribute("data-busy", "");
    button?.setAttribute("aria-busy", "true");

    const response = await sendUpload({ url: form.action, file }).finished;

    // the button stays busy while the page reloads
    if (response?.status === 200) {
      location.reload();
      return;
    }

    this.#isSending = false;
    button?.removeAttribute("data-busy");
    button?.removeAttribute("aria-busy");

    if (response !== null) {
      this.#showError(uploadErrorMessage(response));
    }
  }

  /** @param {string} message */
  #showError(message) {
    const error = this.querySelector('[data-part="error"]');

    if (error !== null) {
      error.textContent = message;
    }
  }
}

customElements.define("brand-image-upload", BrandImageUpload);
