import { listenForFiles } from "/js/collect-files.js";
import { startPostImport } from "/js/post-import-run.js";

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

  /** @type {{ finished: Promise<boolean>, stop: () => void } | null} */
  let current = null;
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
   * Started but not awaited, as the bulk uploader's run is: .NET gives up on a call it awaits
   * after a minute, and hears the end through OnImportFinished instead
   * @param {{ index: number, fileIds: string[] }[]} posts
   */
  function run(posts) {
    isRunning = true;
    current = startPostImport(posts, {
      fileOf: (id) => collectedFiles.get(id),
      dotNetReference,
      onProgress: (percentComplete) => notify("OnUploadProgress", percentComplete),
    });

    void current.finished.then(() => {
      isRunning = false;
      notify("OnImportFinished");
    });
  }

  function stop() {
    current?.stop();
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
      run(posts);
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
