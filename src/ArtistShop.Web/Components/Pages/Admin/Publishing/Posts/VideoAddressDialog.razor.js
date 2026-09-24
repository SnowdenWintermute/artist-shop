// The dialog the editor's Video button, and a video embed's Change video, open. The server reads
// a pasted YouTube or Vimeo link into which video it is, and the dialog says so when it can't,
// rather than closing. Imported by PostBodyEditor.razor.js, which finds the element by its id

/** @typedef {import("./PostVideoEmbed.razor.js").VideoSource} VideoSource */

export class VideoAddressDialog extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  // what the current opening does with the video
  /** @type {((source: VideoSource) => void) | null} */
  #onChosen = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    const { signal } = this.#listeners;

    this.addEventListener("submit", (event) => this.#submit(event), { signal });
    // the messages are about the link as it was, so they go once the artist changes it
    this.addEventListener("input", () => this.#hideMessages(), { signal });
    // close doesn't bubble, so this listens on the way down
    this.addEventListener("close", () => (this.#onChosen = null), { signal, capture: true });
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  /**
   * @param {string} address the video's link to start with, empty for a new video
   * @param {(source: VideoSource) => void} onChosen
   */
  open(address, onChosen) {
    const { dialog, field } = this.#parts();

    this.#onChosen = onChosen;
    field.value = address;
    this.#hideMessages();
    dialog.showModal();
    // so pasting a new link replaces the one being changed
    field.select();
  }

  // Always prevented, so the dialog is closed here, before the video goes in, and the focus it
  // hands back doesn't land after the cursor is placed
  /** @param {SubmitEvent} event */
  async #submit(event) {
    event.preventDefault();

    const { dialog, field, submitButton, notAVideo, failed, videoLinkUrl } = this.#parts();
    const onChosen = this.#onChosen;

    // Enter again while the link is being read
    if (submitButton.hasAttribute("data-busy")) {
      return;
    }

    submitButton.setAttribute("data-busy", "");
    submitButton.setAttribute("aria-busy", "true");

    /** @type {VideoSource | null} */
    let source;

    try {
      source = await this.#readLink(videoLinkUrl, field.value);
    } catch {
      failed.hidden = false;
      return;
    } finally {
      submitButton.removeAttribute("data-busy");
      submitButton.removeAttribute("aria-busy");
    }

    // closed, or opened again for another embed, while the link was being read
    if (this.#onChosen !== onChosen) {
      return;
    }

    if (source === null) {
      notAVideo.hidden = false;
      field.focus();
      return;
    }

    dialog.close();
    onChosen?.(source);
  }

  // Which video the link is to, or null if it isn't one the server can read. Throws when the
  // server can't be asked, such as a lost connection or a login that has run out, which answers
  // with a redirect to the login page
  /**
   * @param {string} url
   * @param {string} link
   * @returns {Promise<VideoSource | null>}
   */
  async #readLink(url, link) {
    const response = await fetch(`${url}?${new URLSearchParams({ link })}`, { redirect: "error" });

    // VideoLinkEndpoints' answer for a link it can't read
    if (response.status === 422) {
      return null;
    }

    if (!response.ok) {
      throw new Error(`Reading the video link failed with ${response.status}.`);
    }

    return response.json();
  }

  #hideMessages() {
    const { notAVideo, failed } = this.#parts();
    notAVideo.hidden = true;
    failed.hidden = true;
  }

  #parts() {
    const dialog = this.querySelector("dialog");
    const field = this.querySelector('input[data-part="address"]');
    const submitButton = this.querySelector('button[type="submit"]');
    const notAVideo = this.querySelector('[data-part="not-a-video"]');
    const failed = this.querySelector('[data-part="failed"]');
    // read here, outside #submit's try, so a missing one throws rather than showing as a failed check
    const videoLinkUrl = this.dataset.videoLinkUrl;

    if (
      !(dialog instanceof HTMLDialogElement) ||
      !(field instanceof HTMLInputElement) ||
      !(submitButton instanceof HTMLButtonElement) ||
      !(notAVideo instanceof HTMLElement) ||
      !(failed instanceof HTMLElement) ||
      videoLinkUrl === undefined
    ) {
      throw new Error("The video address dialog is missing its dialog, field, button, messages or link reader's address.");
    }

    return { dialog, field, submitButton, notAVideo, failed, videoLinkUrl };
  }
}

customElements.define("video-address-dialog", VideoAddressDialog);
