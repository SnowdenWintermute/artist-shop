const REQUEST_VERIFICATION_TOKEN_INPUT_NAME = "__RequestVerificationToken";

/**
 * FileDropZoneFrame's script opens the picker and handles drops, so both arrive here as "change"
 * @param {HTMLInputElement} fileInput
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {string} uploadUrl
 */
export function createUploader(
  fileInput,
  dotNetReference,
  uploadUrl
) {
  /** @type {Map<string, File>} */
  const pendingFiles = new Map();
  /**
   * we want to track requests so they can be cancelled
   * to save bandwidth instead of letting them finish then fail
   * @type {Map<string, XMLHttpRequest>}
   * */
  const inFlightRequests = new Map();

  /** @param {FileList} files */
  function announce(files) {
    for (const file of files) {
      const id = crypto.randomUUID();
      pendingFiles.set(id, file);
      dotNetReference
        .invokeMethodAsync("OnFileSelected", id, file.name, file.size)
        .catch((error) => console.error("OnFileSelected failed", error));
    }
  }

  function onChange() {
    if (fileInput.files !== null) {
      announce(fileInput.files);
    }
    fileInput.value = "";
  }

  function antiforgeryToken() {
    const input = document.querySelector(
      `input[name="${REQUEST_VERIFICATION_TOKEN_INPUT_NAME}"]`
    );
    return input instanceof HTMLInputElement ? input.value : "";
  }

  /** @param {string} id */
  function upload(id) {
    const file = pendingFiles.get(id);
    if (!file) {
      return;
    }

    const formData = new FormData();
    formData.append("file", file);
    formData.append(REQUEST_VERIFICATION_TOKEN_INPUT_NAME, antiforgeryToken());

    const request = new XMLHttpRequest();
    inFlightRequests.set(id, request);

    request.upload.addEventListener("progress", (event) => {
      if (!event.lengthComputable) {
        return;
      }

      const percentComplete = Math.round((event.loaded / event.total) * 100);

      dotNetReference
        .invokeMethodAsync("OnUploadProgress", id, percentComplete)
        .catch((error) => console.error("OnUploadProgress failed", error));
    });

    request.addEventListener("load", () => {
      if (request.status === 200) {
        pendingFiles.delete(id);
        dotNetReference
          .invokeMethodAsync(
            "OnUploadCompleted",
            id,
            JSON.parse(request.responseText)
          )
          .catch((error) => console.error("OnUploadCompleted failed", error));
      } else {
        dotNetReference
          .invokeMethodAsync("OnUploadFailed", id, errorMessageFrom(request))
          .catch((error) => console.error("OnUploadFailed failed", error));
      }
    });

    // "loadend" fires exactly once after load, error, abort or timeout
    request.addEventListener("loadend", () => {
      inFlightRequests.delete(id);
    });

    request.addEventListener("error", () => {
      dotNetReference
        .invokeMethodAsync(
          "OnUploadFailed",
          id,
          "The upload could not reach the server."
        )
        .catch((error) => console.error("OnUploadFailed failed", error));
    });

    request.open("POST", uploadUrl);
    request.send(formData);
  }

  /** @param {string} id */
  function abort(id) {
    inFlightRequests.get(id)?.abort();
  }

  /** @param {XMLHttpRequest} request */
  function errorMessageFrom(request) {
    const contentType = request.getResponseHeader("Content-Type") ?? "";
    const isPlainMessage =
      contentType.startsWith("text/plain") &&
      request.responseText.length <= 300;

    return isPlainMessage
      ? request.responseText
      : `Upload failed (${request.status}).`;
  }

  fileInput.addEventListener("change", onChange);

  return {
    /** @param {string} id */
    upload(id) {
      upload(id);
    },
    /** @param {string} id */
    forget(id) {
      abort(id);
      pendingFiles.delete(id);
    },
    dispose() {
      // copy the keys first since we delete them from
      // inFlightRequests as we iterate
      for (const id of [...inFlightRequests.keys()]) {
        abort(id);
      }
      fileInput.removeEventListener("change", onChange);
      pendingFiles.clear();
    },
  };
}
