import { sendUpload, uploadErrorMessage } from "/js/upload-request.js";

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
   * @type {Map<string, () => void>}
   * */
  const inFlightAborts = new Map();

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

  /** @param {string} id */
  function upload(id) {
    const file = pendingFiles.get(id);
    if (!file) {
      return;
    }

    const { finished, abort } = sendUpload({
      url: uploadUrl,
      file,
      onProgress: (loaded, total) => {
        dotNetReference
          .invokeMethodAsync("OnUploadProgress", id, Math.round((loaded / total) * 100))
          .catch((error) => console.error("OnUploadProgress failed", error));
      },
    });
    inFlightAborts.set(id, abort);

    finished.then((response) => {
      inFlightAborts.delete(id);

      // aborted, by forget or dispose, so nobody is waiting for the answer
      if (response === null) {
        return;
      }

      if (response.status === 200) {
        pendingFiles.delete(id);
        dotNetReference
          .invokeMethodAsync("OnUploadCompleted", id, JSON.parse(response.text))
          .catch((error) => console.error("OnUploadCompleted failed", error));
      } else {
        dotNetReference
          .invokeMethodAsync("OnUploadFailed", id, uploadErrorMessage(response))
          .catch((error) => console.error("OnUploadFailed failed", error));
      }
    });
  }

  /** @param {string} id */
  function abort(id) {
    inFlightAborts.get(id)?.();
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
      for (const abort of inFlightAborts.values()) {
        abort();
      }
      fileInput.removeEventListener("change", onChange);
      pendingFiles.clear();
    },
  };
}
