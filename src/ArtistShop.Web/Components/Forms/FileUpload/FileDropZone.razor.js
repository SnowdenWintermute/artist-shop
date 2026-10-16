import { uploadWithRetries } from "/js/upload-request.js";

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
   * each file's upload while it runs, so forget can stop it, and its retries, rather than letting it
   * finish and then fail
   * @type {Map<string, { aborts: Set<() => void>, isStopped: boolean }>}
   */
  const running = new Map();

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

  // sent again while the server is busy, as the import runs' uploads are
  /** @param {string} id */
  async function upload(id) {
    const file = pendingFiles.get(id);
    if (!file) {
      return;
    }

    const run = { aborts: new Set(), isStopped: false };
    running.set(id, run);

    /** @param {number} percentComplete */
    const report = (percentComplete) =>
      dotNetReference
        .invokeMethodAsync("OnUploadProgress", id, percentComplete)
        .catch((error) => console.error("OnUploadProgress failed", error));

    const outcome = await uploadWithRetries({
      id,
      url: uploadUrl,
      file,
      progress: {
        // the request is a little larger than the file, so this can pass 100 near the end. An empty
        // file is refused by the server, but mustn't report NaN on the way
        loaded: (_, loaded) => report(file.size > 0 ? Math.min(100, Math.round((loaded / file.size) * 100)) : 100),
        retrying: () => report(0),
        finished: () => {},
      },
      aborts: run.aborts,
      isStopped: () => run.isStopped,
    });

    if (running.get(id) === run) {
      running.delete(id);
    }

    // stopped, by forget or dispose, so nobody is waiting for the answer
    if (outcome === null) {
      return;
    }

    if ("failure" in outcome) {
      dotNetReference
        .invokeMethodAsync("OnUploadFailed", id, outcome.failure)
        .catch((error) => console.error("OnUploadFailed failed", error));
    } else {
      pendingFiles.delete(id);
      dotNetReference
        .invokeMethodAsync("OnUploadCompleted", id, JSON.parse(outcome.response.text))
        .catch((error) => console.error("OnUploadCompleted failed", error));
    }
  }

  /** @param {string} id */
  function stop(id) {
    const run = running.get(id);

    if (run) {
      run.isStopped = true;
      for (const abort of run.aborts) {
        abort();
      }
      running.delete(id);
    }
  }

  fileInput.addEventListener("change", onChange);

  return {
    // not awaited, so .NET hears back through its callbacks rather than waiting here
    /** @param {string} id */
    upload(id) {
      void upload(id);
    },
    /** @param {string} id */
    forget(id) {
      stop(id);
      pendingFiles.delete(id);
    },
    dispose() {
      for (const id of [...running.keys()]) {
        stop(id);
      }
      fileInput.removeEventListener("change", onChange);
      pendingFiles.clear();
    },
  };
}
