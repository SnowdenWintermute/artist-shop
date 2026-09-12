const DRAG_OVER_CLASS = "bg-blue-50";
const REQUEST_VERIFICATION_TOKEN_INPUT_NAME = "__RequestVerificationToken";

/**
 * @param {HTMLElement} dropZone
 * @param {HTMLInputElement} fileInput
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {string} uploadUrl
 */
export function createUploader(
  dropZone,
  fileInput,
  dotNetReference,
  uploadUrl
) {
  /** @type {Map<string, File>} */
  const pendingFiles = new Map();

  /** @param {FileList} files */
  function announce(files) {
    for (const file of files) {
      const id = crypto.randomUUID();
      pendingFiles.set(id, file);
      dotNetReference
        .invokeMethodAsync("OnFileSelected", id, file.name, file.size)
        .catch((error) => console.error("OnUploadCompleted failed", error));
    }
  }

  /** @param {DragEvent} event */
  function onDragOver(event) {
    event.preventDefault();
    dropZone.classList.add(DRAG_OVER_CLASS);
  }

  function onDragLeave() {
    dropZone.classList.remove(DRAG_OVER_CLASS);
  }

  /** @param {DragEvent} event */
  function onDrop(event) {
    event.preventDefault();
    dropZone.classList.remove(DRAG_OVER_CLASS);

    if (!event.dataTransfer) {
      return;
    }

    announce(event.dataTransfer.files);
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

  dropZone.addEventListener("dragover", onDragOver);
  dropZone.addEventListener("dragleave", onDragLeave);
  dropZone.addEventListener("drop", onDrop);
  fileInput.addEventListener("change", onChange);

  return {
    open() {
      fileInput.click();
    },
    /** @param {string} id */
    upload(id) {
      upload(id);
    },
    /** @param {string} id */
    forget(id) {
      pendingFiles.delete(id);
    },
    dispose() {
      dropZone.removeEventListener("dragover", onDragOver);
      dropZone.removeEventListener("dragleave", onDragLeave);
      dropZone.removeEventListener("drop", onDrop);
      fileInput.removeEventListener("change", onChange);
      pendingFiles.clear();
    },
  };
}
