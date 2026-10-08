import { listenForFiles } from "/js/collect-files.js";
import { createUploadProgress } from "/js/upload-progress.js";
import { uploadWithRetries } from "/js/upload-request.js";

// a first image, matched to its work by name on the server
const UPLOAD_URL = "/admin/uploads/work-image-by-name";

// another image of a work, like "Dawn (2)", after the ones it has
const APPEND_URL = "/admin/uploads/work-image-appended";

// a courtesy to the server, which has its own limits and doesn't trust this number. Each worker
// takes a whole work, so a work's files still go in order
const CONCURRENT_UPLOADS = 4;

// Shared by the Upload images page and the Add from images page

/** @typedef {{ id: string, workId: number | null }} UploadItem */

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

  /** @type {Set<() => void>} */
  const aborts = new Set();
  /** @type {UploadItem[][]} one work's files each */
  let queue = [];
  let progress = createUploadProgress(0, reportProgress);
  let workTypeId = 0;
  let isStopped = false;
  let isRunning = false;

  /**
   * @param {string} method
   * @param {...unknown} args
   */
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

  /** @param {UploadItem} item */
  async function uploadFile({ id, workId }) {
    const file = collectedFiles.get(id);
    if (!file) {
      return;
    }

    const outcome = await uploadWithRetries({
      id,
      url: workId === null ? UPLOAD_URL : APPEND_URL,
      file,
      fields: workId === null ? { workTypeId: String(workTypeId) } : { workId: String(workId) },
      progress,
      aborts,
      isStopped: () => isStopped,
    });

    // a stopped run leaves the file as it was, so pressing Upload again picks it up
    if (outcome === null) {
      return;
    }

    if ("failure" in outcome) {
      notify("OnFileFailed", id, outcome.failure);
    } else if (workId === null) {
      notify("OnFileFinished", id, JSON.parse(outcome.response.text));
    } else {
      notify("OnExtraAdded", id, JSON.parse(outcome.response.text));
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
   * @param {UploadItem[][]} works
   * @param {number} typeId
   */
  async function run(works, typeId) {
    isStopped = false;
    isRunning = true;
    workTypeId = typeId;
    queue = [...works];
    progress = createUploadProgress(
      works.flat().reduce((sum, { id }) => sum + (collectedFiles.get(id)?.size ?? 0), 0),
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

    for (const abort of aborts) {
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
     * @param {UploadItem[][]} works
     * @param {number} typeId
     */
    upload(works, typeId) {
      // "void" says the promise is deliberately not awaited
      void run(works, typeId);
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
