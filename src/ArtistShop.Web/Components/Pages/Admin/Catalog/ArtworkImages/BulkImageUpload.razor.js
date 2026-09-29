import { listenForFiles } from "/js/collect-files.js";
import { createUploadProgress } from "/js/upload-progress.js";
import {
  MAXIMUM_UPLOAD_ATTEMPTS,
  isRetryable,
  retryDelay,
  sendUpload,
  uploadErrorMessage,
  wait,
} from "/js/upload-request.js";

// a first image, matched to its artwork by name on the server
const UPLOAD_URL = "/admin/uploads/artwork-image-by-name";

// another image of an artwork, like "Dawn (2)", after the ones it has
const APPEND_URL = "/admin/uploads/artwork-image-appended";

// a courtesy to the server, which has its own limits and doesn't trust this number. Each worker
// takes a whole artwork, so an artwork's files still go in order
const CONCURRENT_UPLOADS = 4;

/** @typedef {{ id: string, artworkId: number | null }} UploadItem */

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

  /** @type {Map<string, () => void>} */
  const inFlightAborts = new Map();
  /** @type {UploadItem[][]} one artwork's files each */
  let queue = [];
  let progress = createUploadProgress(0, reportProgress);
  let artworkTypeId = 0;
  let isStopped = false;
  let isRunning = false;

  /** @param {string} method */
  function notify(method, ...args) {
    dotNetReference
      .invokeMethodAsync(method, ...args)
      .catch((error) => console.error(`${method} failed`, error));
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

  /**
   * one request, resolved however it ends: null when Stop aborted it
   * @param {UploadItem} item
   * @param {File} file
   */
  function sendOnce({ id, artworkId }, file) {
    const { finished, abort } = sendUpload({
      url: artworkId === null ? UPLOAD_URL : APPEND_URL,
      file,
      fields: artworkId === null ? { artworkTypeId: String(artworkTypeId) } : { artworkId: String(artworkId) },
      onProgress: (loaded) => progress.loaded(id, loaded),
    });
    inFlightAborts.set(id, abort);

    return finished.finally(() => inFlightAborts.delete(id));
  }

  /** @param {UploadItem} item */
  async function uploadFile(item) {
    const { id, artworkId } = item;
    const file = collectedFiles.get(id);
    if (!file) {
      return;
    }

    for (let attempt = 1; attempt <= MAXIMUM_UPLOAD_ATTEMPTS; attempt += 1) {
      // Stop pressed while waiting to retry
      if (isStopped) {
        return;
      }

      const response = await sendOnce(item, file);

      // a stopped run leaves the file as it was, so pressing Upload again picks it up
      if (response === null || isStopped) {
        return;
      }

      if (response.status === 200) {
        progress.finished(id, file.size);
        if (artworkId === null) {
          notify("OnFileFinished", id, JSON.parse(response.text));
        } else {
          notify("OnExtraAdded", id);
        }
        return;
      }

      if (!isRetryable(response.status) || attempt === MAXIMUM_UPLOAD_ATTEMPTS) {
        progress.finished(id, file.size);
        notify("OnFileFailed", id, uploadErrorMessage(response));
        return;
      }

      progress.retrying(id);
      await wait(retryDelay(attempt, response.retryAfter));
    }
  }

  /** @param {number} percentComplete */
  function reportProgress(percentComplete) {
    notify("OnUploadProgress", percentComplete);
  }

  async function runWorker() {
    while (queue.length > 0 && !isStopped) {
      for (const item of queue.shift() ?? []) {
        if (isStopped) {
          return;
        }

        await uploadFile(item);
      }
    }
  }

  /**
   * Started but not awaited: an InvokeAsync from .NET gives up after
   * CircuitOptions.JSInteropDefaultCallTimeout, one minute by default, and a real run passes that.
   * The island hears the end through OnUploadFinished instead
   * @param {UploadItem[][]} artworks
   * @param {number} typeId
   */
  async function run(artworks, typeId) {
    isStopped = false;
    isRunning = true;
    artworkTypeId = typeId;
    queue = [...artworks];
    progress = createUploadProgress(
      artworks.flat().reduce((sum, { id }) => sum + (collectedFiles.get(id)?.size ?? 0), 0),
      reportProgress
    );

    try {
      await Promise.all(
        Array.from({ length: Math.min(CONCURRENT_UPLOADS, queue.length) }, runWorker)
      );
    } catch (error) {
      console.error("The upload run stopped early", error);
    } finally {
      // without this the island stays "uploading" for good when a worker throws
      isRunning = false;
      progress.done();
      notify("OnUploadFinished");
    }
  }

  function stop() {
    isStopped = true;
    queue = [];

    for (const abort of inFlightAborts.values()) {
      abort();
    }
  }

  const stopListening = listenForFiles(zone, maximumFiles, remember);

  return {
    // .NET reads this as a stream: as one interop call it would hit SignalR's 32 KB message cap
    // at about 200 files. Returning the blob is all it takes -- .NET asked for an
    // IJSStreamReference, so it wraps what comes back
    metadata() {
      return new Blob([JSON.stringify(collectedMetadata)]);
    },
    /**
     * @param {UploadItem[][]} artworks
     * @param {number} typeId
     */
    upload(artworks, typeId) {
      // "void" says the promise is deliberately not awaited
      void run(artworks, typeId);
    },
    stop,
    dispose() {
      stop();
      stopListening();
      collectedFiles.clear();
      collectedMetadata = [];
    },
  };
}
