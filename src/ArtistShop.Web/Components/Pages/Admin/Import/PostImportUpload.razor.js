import { listenForFiles } from "/js/collect-files.js";
import {
  MAXIMUM_UPLOAD_ATTEMPTS,
  isRetryable,
  retryDelay,
  sendUpload,
  uploadErrorMessage,
  wait,
} from "/js/upload-request.js";

// the post editor's own upload, so an imported image is processed as one dropped into a post
const UPLOAD_URL = "/admin/uploads/post-image";

/**
 * @param {HTMLElement} zone an element wrapping the drop zone
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {number} maximumFiles
 */
export function createPostImporter(zone, dotNetReference, maximumFiles) {
  /** @type {Map<string, File>} */
  const collectedFiles = new Map();
  /** @type {{ id: string, path: string, size: number, type: string }[]} */
  let collectedMetadata = [];

  /** @type {(() => void) | null} */
  let abortInFlight = null;
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
    // the zone is disabled during a run, so this is a backstop
    if (isRunning) {
      return;
    }

    collectedFiles.clear();
    collectedMetadata = [];

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
   * The upload endpoint's answer, parsed, or the reason it failed. Null when Stop aborted it
   * @param {File} file
   * @returns {Promise<{ result: unknown } | { failure: string } | null>}
   */
  async function upload(file) {
    for (let attempt = 1; attempt <= MAXIMUM_UPLOAD_ATTEMPTS; attempt += 1) {
      const { finished, abort } = sendUpload({ url: UPLOAD_URL, file });
      abortInFlight = abort;
      const response = await finished;
      abortInFlight = null;

      if (response === null || isStopped) {
        return null;
      }

      if (response.status === 200) {
        return { result: JSON.parse(response.text) };
      }

      if (!isRetryable(response.status) || attempt === MAXIMUM_UPLOAD_ATTEMPTS) {
        return { failure: uploadErrorMessage(response) };
      }

      await wait(retryDelay(attempt, response.retryAfter));
    }

    return { failure: "The upload failed." };
  }

  // One post at a time: its files, each reported as it arrives, then the post, which .NET saves
  // before this goes on. Each report is its own call, since SignalR turns away a message over
  // 32 KB and a blur alone can be 1 KB
  /** @param {{ index: number, fileIds: string[] }} post */
  async function importPost(post) {
    for (const fileId of post.fileIds) {
      const file = collectedFiles.get(fileId);
      const outcome = file ? await upload(file) : { failure: "The file is no longer there." };

      if (outcome === null) {
        return;
      }

      if ("failure" in outcome) {
        await dotNetReference.invokeMethodAsync("OnPostFailed", post.index, outcome.failure);
        return;
      }

      await dotNetReference.invokeMethodAsync("OnFileUploaded", fileId, outcome.result);
    }

    await dotNetReference.invokeMethodAsync("OnPostUploaded", post.index);
  }

  /**
   * Started but not awaited, as the bulk uploader's run is: .NET gives up on a call it awaits
   * after a minute, and hears the end through OnImportFinished instead
   * @param {{ index: number, fileIds: string[] }[]} posts
   */
  async function run(posts) {
    isStopped = false;
    isRunning = true;

    try {
      for (const post of posts) {
        if (isStopped) {
          break;
        }

        await importPost(post);
      }
    } catch (error) {
      console.error("The import stopped early", error);
    } finally {
      isRunning = false;
      notify("OnImportFinished");
    }
  }

  function stop() {
    isStopped = true;
    abortInFlight?.();
  }

  const stopListening = listenForFiles(zone, maximumFiles, remember);

  return {
    // read as a stream, as the bulk uploader's is: one interop call would hit SignalR's 32 KB cap
    metadata() {
      return new Blob([JSON.stringify(collectedMetadata)]);
    },
    /** @param {string} id a post.json, which .NET reads as a stream */
    file(id) {
      return collectedFiles.get(id) ?? new Blob([]);
    },
    /** @param {{ index: number, fileIds: string[] }[]} posts */
    importPosts(posts) {
      void run(posts);
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
