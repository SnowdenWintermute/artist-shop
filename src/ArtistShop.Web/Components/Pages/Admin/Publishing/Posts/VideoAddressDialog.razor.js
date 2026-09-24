// The dialog the editor's Video button, and a video embed's Change video, open. It reads a pasted YouTube or Vimeo link into which
// video it is, and says so when it can't, rather than closing.
// Imported by PostBodyEditor.razor.js, which finds the element by its id when it opens it

/** @typedef {import("./PostVideoEmbed.razor.js").VideoSource} VideoSource */

// PostDocumentParser checks the stored parts against the same patterns
const YOUTUBE_ID = /^[A-Za-z0-9_-]{11}$/;
const VIMEO_ID = /^[0-9]{1,12}$/;
const VIMEO_HASH = /^[A-Za-z0-9]{1,32}$/;

const HAS_SCHEME = /^[a-z][a-z0-9+.-]*:\/\//i;

// the pages a YouTube video is watched from, besides watch?v=, whose next part is the id
const YOUTUBE_ID_PATHS = ["shorts", "embed", "live", "v"];

/**
 * youtube.com/watch?v=…, youtu.be/…, and the shorts, embed and live addresses
 * @param {string} host
 * @param {string[]} path
 * @param {URLSearchParams} query
 * @returns {VideoSource | null}
 */
function readYouTube(host, path, query) {
  const id =
    host === "youtu.be"
      ? path[0]
      : host !== "youtube.com" && host !== "youtube-nocookie.com"
        ? undefined
        : path[0] === "watch"
          ? query.get("v")
          : YOUTUBE_ID_PATHS.includes(path[0])
            ? path[1]
            : undefined;

  return id != null && YOUTUBE_ID.test(id) ? { provider: "youtube", videoId: id } : null;
}

/**
 * vimeo.com/…/{id}, where an unlisted video's link has its hash after the id, and the player's own
 * player.vimeo.com/video/{id}?h={hash}
 * @param {string} host
 * @param {string[]} path
 * @param {URLSearchParams} query
 * @returns {VideoSource | null}
 */
function readVimeo(host, path, query) {
  if (host !== "vimeo.com" && host !== "player.vimeo.com") {
    return null;
  }

  // A video's own link starts with its id, and a hash after it can be all digits too. A channel's
  // or showcase's link has the video's id last, with the showcase's own number earlier
  const idIndex = VIMEO_ID.test(path[0] ?? "") ? 0 : path.findLastIndex((part) => VIMEO_ID.test(part));

  if (idIndex === -1) {
    return null;
  }

  const hash = path[idIndex + 1] ?? query.get("h");

  if (hash == null) {
    return { provider: "vimeo", videoId: path[idIndex] };
  }

  return VIMEO_HASH.test(hash) ? { provider: "vimeo", videoId: path[idIndex], hash } : null;
}

// Which video a link is to, or null if it isn't one this can read. A link pasted without its
// https:// is read as if it had it
/**
 * @param {string} text
 * @returns {VideoSource | null}
 */
export function readVideoAddress(text) {
  const trimmed = text.trim();

  /** @type {URL} */
  let url;

  try {
    url = new URL(HAS_SCHEME.test(trimmed) ? trimmed : `https://${trimmed}`);
  } catch {
    return null;
  }

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    return null;
  }

  const host = url.hostname.replace(/^(www|m|music)\./, "");
  const path = url.pathname.split("/").filter(Boolean);

  return readYouTube(host, path, url.searchParams) ?? readVimeo(host, path, url.searchParams);
}

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
    // the error is about the link as it was, so it goes once the artist changes it
    this.addEventListener("input", () => (this.#parts().error.hidden = true), { signal });
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
    const { dialog, field, error } = this.#parts();

    this.#onChosen = onChosen;
    field.value = address;
    error.hidden = true;
    dialog.showModal();
    // so pasting a new link replaces the one being changed
    field.select();
  }

  // Always prevented, so the dialog is closed here, before the video goes in, and the focus it
  // hands back doesn't land after the cursor is placed
  /** @param {SubmitEvent} event */
  #submit(event) {
    event.preventDefault();

    const { dialog, field, error } = this.#parts();
    const source = readVideoAddress(field.value);

    if (source === null) {
      error.hidden = false;
      field.focus();
      return;
    }

    const chosen = this.#onChosen;
    dialog.close();
    chosen?.(source);
  }

  #parts() {
    const dialog = this.querySelector("dialog");
    const field = this.querySelector('input[data-part="address"]');
    const error = this.querySelector('[data-part="error"]');

    if (!(dialog instanceof HTMLDialogElement) || !(field instanceof HTMLInputElement) || !(error instanceof HTMLElement)) {
      throw new Error("The video address dialog is missing its dialog, field or error message.");
    }

    return { dialog, field, error };
  }
}

customElements.define("video-address-dialog", VideoAddressDialog);
