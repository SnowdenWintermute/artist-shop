// One file sent to an upload endpoint, for every page that uploads: with the page's antiforgery
// token, and read back with the server's short plain-text message when it turns the file away

const REQUEST_VERIFICATION_TOKEN_INPUT_NAME = "__RequestVerificationToken";

/**
 * How the request ended. A network error arrives as status 0
 * @typedef {object} UploadResponse
 * @property {number} status
 * @property {string} text
 * @property {string} contentType
 * @property {string | null} retryAfter
 */

function antiforgeryToken() {
  const input = document.querySelector(`input[name="${REQUEST_VERIFICATION_TOKEN_INPUT_NAME}"]`);
  return input instanceof HTMLInputElement ? input.value : "";
}

/**
 * Sends the file as the form field "file", which the endpoints bind by that name, with any other
 * fields beside it. XMLHttpRequest rather than fetch, which can't report an upload's progress.
 * finished resolves however the request ends, and with null once abort has stopped it
 * @param {object} upload
 * @param {string} upload.url
 * @param {File} upload.file
 * @param {Record<string, string>} [upload.fields]
 * @param {(loaded: number, total: number) => void} [upload.onProgress]
 * @returns {{ finished: Promise<UploadResponse | null>, abort: () => void }}
 */
export function sendUpload({ url, file, fields = {}, onProgress }) {
  const formData = new FormData();
  formData.append("file", file);
  for (const [name, value] of Object.entries(fields)) {
    formData.append(name, value);
  }
  formData.append(REQUEST_VERIFICATION_TOKEN_INPUT_NAME, antiforgeryToken());

  const request = new XMLHttpRequest();
  let isAborted = false;

  request.upload.addEventListener("progress", (event) => {
    if (event.lengthComputable) {
      onProgress?.(event.loaded, event.total);
    }
  });

  /** @type {Promise<UploadResponse | null>} */
  const finished = new Promise((resolve) => {
    // "loadend" fires exactly once after load, error, abort or timeout
    request.addEventListener("loadend", () => {
      resolve(
        isAborted
          ? null
          : {
              status: request.status,
              text: request.responseText,
              contentType: request.getResponseHeader("Content-Type") ?? "",
              retryAfter: request.getResponseHeader("Retry-After"),
            }
      );
    });
  });

  request.open("POST", url);
  request.send(formData);

  return {
    finished,
    abort() {
      isAborted = true;
      request.abort();
    },
  };
}

// What to tell the artist about a failed upload. An unhandled exception answers with a whole HTML
// page, so only a short plain message is the endpoint's own
/** @param {UploadResponse} response */
export function uploadErrorMessage(response) {
  if (response.status === 0) {
    return "The upload could not reach the server.";
  }

  const isPlainMessage = response.contentType.startsWith("text/plain") && response.text.length <= 300;

  return isPlainMessage ? response.text : `The upload failed (${response.status}).`;
}

export const MAXIMUM_UPLOAD_ATTEMPTS = 4;

const LONGEST_RETRY_MILLISECONDS = 30000;

// the server is busy or the request was shaped out by a limit, so the same file is worth sending again
const RETRYABLE_STATUSES = [429, 503, 504];

/** @param {number} status */
export function isRetryable(status) {
  return RETRYABLE_STATUSES.includes(status);
}

/**
 * Retry-After says how long the server wants; without one the wait doubles each time. The
 * random half keeps a batch of uploads from all coming back at the same moment
 * @param {number} attempt
 * @param {string | null} retryAfter
 */
export function retryDelay(attempt, retryAfter) {
  const seconds = Number(retryAfter);
  const wanted = Number.isFinite(seconds) && seconds > 0 ? seconds * 1000 : 1000 * 2 ** attempt;

  return Math.min(wanted, LONGEST_RETRY_MILLISECONDS) * (0.5 + Math.random() / 2);
}

/** @param {number} milliseconds */
export function wait(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

/**
 * The file sent, and sent again while the server is busy, until it's in or turned away. Each
 * request's abort sits in aborts while it's in flight, for Stop to call
 * @param {object} upload
 * @param {string} upload.id the file's id, for progress
 * @param {string} upload.url
 * @param {File} upload.file
 * @param {Record<string, string>} [upload.fields]
 * @param {{ loaded: (id: string, loaded: number) => void, retrying: (id: string) => void, finished: (id: string, size: number) => void }} upload.progress
 * @param {Set<() => void>} upload.aborts
 * @param {() => boolean} upload.isStopped
 * @returns {Promise<{ response: UploadResponse } | { failure: string } | null>} null when Stop ended it
 */
export async function uploadWithRetries({ id, url, file, fields, progress, aborts, isStopped }) {
  for (let attempt = 1; attempt <= MAXIMUM_UPLOAD_ATTEMPTS; attempt += 1) {
    // Stop pressed while waiting to retry, or between one file and the next
    if (isStopped()) {
      return null;
    }

    const { finished, abort } = sendUpload({
      url,
      file,
      fields,
      onProgress: (loaded) => progress.loaded(id, loaded),
    });
    aborts.add(abort);
    const response = await finished;
    aborts.delete(abort);

    if (response === null || isStopped()) {
      return null;
    }

    if (response.status === 200) {
      progress.finished(id, file.size);
      return { response };
    }

    if (!isRetryable(response.status) || attempt === MAXIMUM_UPLOAD_ATTEMPTS) {
      progress.finished(id, file.size);
      return { failure: uploadErrorMessage(response) };
    }

    progress.retrying(id);
    await wait(retryDelay(attempt, response.retryAfter));
  }

  return { failure: "The upload failed." };
}
