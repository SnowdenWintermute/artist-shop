const UPLOAD_URL = "/admin/uploads/artwork-image-by-name";
const REQUEST_VERIFICATION_TOKEN_INPUT_NAME = "__RequestVerificationToken";

// a courtesy to the server, which has its own limits and doesn't trust this number
const CONCURRENT_UPLOADS = 4;

const MAXIMUM_ATTEMPTS = 4;
const LONGEST_RETRY_MILLISECONDS = 30000;
const PROGRESS_INTERVAL_MILLISECONDS = 250;

// the server is busy or the request was shaped out by a limit, so the same file is worth sending again
const RETRYABLE_STATUSES = [429, 503, 504];

/**
 * @param {HTMLElement} zone an element wrapping the drop zone: both "change" and "entriesdropped"
 *   bubble up to it
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {number} maximumFiles
 */
export function createBulkUploader(zone, dotNetReference, maximumFiles) {
  /** @type {Map<string, File>} */
  const collectedFiles = new Map();
  /** @type {{ id: string, path: string, size: number, type: string }[]} */
  let collectedMetadata = [];

  /** @type {Map<string, XMLHttpRequest>} */
  const inFlightRequests = new Map();
  /** @type {string[]} */
  let queue = [];
  /** bytes of files whose response has arrived */
  let finishedBytes = 0;
  /** bytes sent so far by each upload still in flight */
  const loadedByFile = new Map();
  let totalBytes = 0;
  let artworkTypeId = 0;
  let isStopped = false;
  let isRunning = false;
  let progressReportedAt = 0;

  /** @param {string} method */
  function notify(method, ...args) {
    dotNetReference
      .invokeMethodAsync(method, ...args)
      .catch((error) => console.error(`${method} failed`, error));
  }

  /** @param {FileSystemEntry[]} entries */
  async function collectEntries(entries) {
    /** @type {{ path: string, file: File }[]} */
    const found = [];

    for (const entry of entries) {
      await collectEntry(entry, found);
    }

    remember(found);
  }

  /**
   * @param {FileSystemEntry} entry
   * @param {{ path: string, file: File }[]} found
   */
  async function collectEntry(entry, found) {
    // one past the cap is enough to know it was passed, and stops a runaway folder being read out
    if (found.length > maximumFiles) {
      return;
    }

    if (entry.isFile) {
      found.push({ path: entry.fullPath, file: await fileOf(entry) });
      return;
    }

    for (const child of await readAllEntries(entry.createReader())) {
      await collectEntry(child, found);
    }
  }

  /**
   * readEntries hands back one batch at a time -- Chrome stops at 100 -- and an empty batch means
   * the folder has been read to the end
   * @param {FileSystemDirectoryReader} reader
   * @returns {Promise<FileSystemEntry[]>}
   */
  function readAllEntries(reader) {
    return new Promise((resolve, reject) => {
      /** @type {FileSystemEntry[]} */
      const all = [];

      const readBatch = () =>
        reader.readEntries((batch) => {
          if (batch.length === 0) {
            resolve(all);
            return;
          }

          all.push(...batch);
          readBatch();
        }, reject);

      readBatch();
    });
  }

  /**
   * @param {FileSystemFileEntry} entry
   * @returns {Promise<File>}
   */
  function fileOf(entry) {
    return new Promise((resolve, reject) => entry.file(resolve, reject));
  }

  /** @param {{ path: string, file: File }[]} found */
  function remember(found) {
    // the zone is disabled during a run, so this is a backstop: clearing the map under the workers
    // would strand every file they haven't started
    if (isRunning) {
      return;
    }

    collectedFiles.clear();
    collectedMetadata = [];

    // the folder picker can hand over a whole tree at once, so this catches both ways in
    if (found.length > maximumFiles) {
      notify("OnTooManyFiles");
      return;
    }

    for (const { path, file } of found) {
      const id = crypto.randomUUID();
      collectedFiles.set(id, file);
      collectedMetadata.push({ id, path, size: file.size, type: file.type });
    }

    notify("OnFilesCollected");
  }

  /** @param {Event} event */
  function onChange(event) {
    const input = event.target;
    if (!(input instanceof HTMLInputElement) || input.files === null) {
      return;
    }

    // a folder picker fills in webkitRelativePath; a file picker leaves it empty
    remember([...input.files].map((file) => ({ path: file.webkitRelativePath || file.name, file })));

    // so that choosing the same folder a second time still counts as a change
    input.value = "";
  }

  /** @param {Event} event */
  function onEntriesDropped(event) {
    collectEntries(/** @type {CustomEvent} */ (event).detail).catch((error) =>
      console.error("Reading the dropped folder failed", error)
    );
  }

  function antiforgeryToken() {
    const input = document.querySelector(
      `input[name="${REQUEST_VERIFICATION_TOKEN_INPUT_NAME}"]`
    );
    return input instanceof HTMLInputElement ? input.value : "";
  }

  /**
   * one request, resolved however it ends. A network error arrives as status 0
   * @param {string} id
   * @param {File} file
   */
  function sendOnce(id, file) {
    return new Promise((resolve) => {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("artworkTypeId", String(artworkTypeId));
      formData.append(REQUEST_VERIFICATION_TOKEN_INPUT_NAME, antiforgeryToken());

      const request = new XMLHttpRequest();
      inFlightRequests.set(id, request);

      request.upload.addEventListener("progress", (event) => {
        if (event.lengthComputable) {
          loadedByFile.set(id, event.loaded);
          reportProgress(false);
        }
      });

      // "loadend" fires exactly once after load, error, abort or timeout
      request.addEventListener("loadend", () => {
        inFlightRequests.delete(id);
        resolve({
          status: request.status,
          text: request.responseText,
          contentType: request.getResponseHeader("Content-Type") ?? "",
          retryAfter: request.getResponseHeader("Retry-After"),
        });
      });

      request.open("POST", UPLOAD_URL);
      request.send(formData);
    });
  }

  /**
   * Retry-After says how long the server wants; without one the wait doubles each time. The
   * random half keeps a batch of uploads from all coming back at the same moment
   * @param {number} attempt
   * @param {string | null} retryAfter
   */
  function retryDelay(attempt, retryAfter) {
    const seconds = Number(retryAfter);
    const wanted =
      Number.isFinite(seconds) && seconds > 0 ? seconds * 1000 : 1000 * 2 ** attempt;

    return Math.min(wanted, LONGEST_RETRY_MILLISECONDS) * (0.5 + Math.random() / 2);
  }

  /** @param {number} milliseconds */
  function wait(milliseconds) {
    return new Promise((resolve) => setTimeout(resolve, milliseconds));
  }

  /** @param {{ status: number, text: string, contentType: string }} response */
  function errorMessageFrom(response) {
    // an unhandled exception answers with a whole HTML page; only a short plain message is ours
    const isPlainMessage =
      response.contentType.startsWith("text/plain") && response.text.length <= 300;

    return isPlainMessage
      ? response.text
      : `The upload failed (${response.status || "no answer from the server"}).`;
  }

  /** @param {string} id */
  async function uploadFile(id) {
    const file = collectedFiles.get(id);
    if (!file) {
      return;
    }

    for (let attempt = 1; attempt <= MAXIMUM_ATTEMPTS; attempt += 1) {
      const response = await sendOnce(id, file);

      // a stopped run leaves the file as it was, so pressing Upload again picks it up
      if (isStopped) {
        return;
      }

      if (response.status === 200) {
        finish(id, file.size);
        notify("OnFileFinished", id, JSON.parse(response.text));
        return;
      }

      if (!RETRYABLE_STATUSES.includes(response.status) || attempt === MAXIMUM_ATTEMPTS) {
        finish(id, file.size);
        notify("OnFileFailed", id, errorMessageFrom(response));
        return;
      }

      // none of this file's bytes count while it waits to be sent again
      loadedByFile.delete(id);
      reportProgress(false);
      await wait(retryDelay(attempt, response.retryAfter));
    }
  }

  /**
   * @param {string} id
   * @param {number} size
   */
  function finish(id, size) {
    loadedByFile.delete(id);
    finishedBytes += size;
    reportProgress(false);
  }

  /** @param {boolean} force sends even inside the throttling interval, for the last update of a run */
  function reportProgress(force) {
    const now = Date.now();
    if (!force && now - progressReportedAt < PROGRESS_INTERVAL_MILLISECONDS) {
      return;
    }

    progressReportedAt = now;

    let sent = finishedBytes;
    for (const bytes of loadedByFile.values()) {
      sent += bytes;
    }

    notify("OnUploadProgress", totalBytes === 0 ? 100 : Math.min(100, Math.round((sent / totalBytes) * 100)));
  }

  async function runWorker() {
    while (queue.length > 0 && !isStopped) {
      await uploadFile(/** @type {string} */ (queue.shift()));
    }
  }

  /**
   * Started but not awaited: an InvokeAsync from .NET gives up after
   * CircuitOptions.JSInteropDefaultCallTimeout, one minute by default, and a real run passes that.
   * The island hears the end through OnUploadFinished instead
   * @param {string[]} ids
   * @param {number} typeId
   */
  async function run(ids, typeId) {
    isStopped = false;
    isRunning = true;
    artworkTypeId = typeId;
    queue = [...ids];
    finishedBytes = 0;
    totalBytes = ids.reduce((sum, id) => sum + (collectedFiles.get(id)?.size ?? 0), 0);
    loadedByFile.clear();

    try {
      await Promise.all(
        Array.from({ length: Math.min(CONCURRENT_UPLOADS, queue.length) }, runWorker)
      );
    } catch (error) {
      console.error("The upload run stopped early", error);
    } finally {
      // without this the island stays "uploading" for good when a worker throws
      isRunning = false;
      reportProgress(true);
      notify("OnUploadFinished");
    }
  }

  function stop() {
    isStopped = true;
    queue = [];

    // copy the keys first, since aborting removes them from inFlightRequests as we iterate
    for (const id of [...inFlightRequests.keys()]) {
      inFlightRequests.get(id)?.abort();
    }
  }

  zone.addEventListener("change", onChange);
  zone.addEventListener("entriesdropped", onEntriesDropped);

  return {
    // .NET reads this as a stream: as one interop call it would hit SignalR's 32 KB message cap
    // at about 200 files. Returning the blob is all it takes -- .NET asked for an
    // IJSStreamReference, so it wraps what comes back
    metadata() {
      return new Blob([JSON.stringify(collectedMetadata)]);
    },
    /**
     * @param {string[]} ids
     * @param {number} typeId
     */
    upload(ids, typeId) {
      // "void" says the promise is deliberately not awaited
      void run(ids, typeId);
    },
    stop,
    dispose() {
      stop();
      zone.removeEventListener("change", onChange);
      zone.removeEventListener("entriesdropped", onEntriesDropped);
      collectedFiles.clear();
      collectedMetadata = [];
    },
  };
}
