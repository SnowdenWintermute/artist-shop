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
