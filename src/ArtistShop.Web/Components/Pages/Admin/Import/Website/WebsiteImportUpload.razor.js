import { listenForFiles } from "/js/collect-files.js";
import { createUploadProgress } from "/js/upload-progress.js";
import { startPostImport } from "/js/post-import-run.js";
import { uploadWithRetries } from "/js/upload-request.js";

const APPEND_IMAGE_URL = "/admin/uploads/artwork-image-appended";

// a courtesy to the server, which has its own limits and doesn't trust this number. Each worker
// takes a whole artwork, so an artwork's images still go in order
const CONCURRENT_ARTWORKS = 3;

/**
 * The files of a chosen whole-website folder, kept here until the import sends them. .NET reads
 * their list, and the few text files it plans with, as streams
 * @param {HTMLElement} zone an element wrapping the drop zone
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {number} maximumFiles
 */
export function createWebsiteImporter(zone, dotNetReference, maximumFiles) {
  /** @type {Map<string, File>} */
  const collectedFiles = new Map();
  /** @type {{ id: string, path: string, size: number, type: string }[]} */
  let collectedMetadata = [];

  /** @type {Set<() => void>} */
  const aborts = new Set();
  let isStopped = false;
  let isRunning = false;
  let progress = createUploadProgress(0, reportProgress);
  /** @type {{ finished: Promise<boolean>, stop: () => void } | null} */
  let posts = null;

  /**
   * @param {string} method
   * @param {...unknown} args
   */
  function notify(method, ...args) {
    dotNetReference
      .invokeMethodAsync(method, ...args)
      .catch((error) => console.error(`${method} failed`, error));
  }

  /** @param {number} percentComplete */
  function reportProgress(percentComplete) {
    notify("OnUploadProgress", percentComplete);
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

  /** @param {{ artworkId: number, fileIds: string[] }} artwork */
  async function importArtwork(artwork) {
    for (const fileId of artwork.fileIds) {
      const file = collectedFiles.get(fileId);
      const outcome = file
        ? await uploadWithRetries({
            id: fileId,
            url: APPEND_IMAGE_URL,
            file,
            fields: { artworkId: String(artwork.artworkId) },
            progress,
            aborts,
            isStopped: () => isStopped,
          })
        : { failure: "The file is no longer there." };

      if (outcome === null) {
        return;
      }

      if ("failure" in outcome) {
        notify("OnImageFailed", fileId, outcome.failure);
      } else {
        notify("OnImageAdded", fileId);
      }
    }
  }

  /**
   * Started but not awaited: .NET gives up on a call it awaits after a minute, and hears the end
   * through OnImagesFinished instead
   * @param {{ artworkId: number, fileIds: string[] }[]} artworks
   */
  async function run(artworks) {
    isStopped = false;
    isRunning = true;
    const queue = [...artworks];
    progress = createUploadProgress(
      artworks.flatMap(({ fileIds }) => fileIds).reduce((sum, id) => sum + (collectedFiles.get(id)?.size ?? 0), 0),
      reportProgress
    );

    async function work() {
      while (!isStopped && queue.length > 0) {
        const artwork = queue.shift();
        if (artwork) {
          await importArtwork(artwork);
        }
      }
    }

    try {
      await Promise.all(Array.from({ length: CONCURRENT_ARTWORKS }, work));
    } catch (error) {
      console.error("The image import stopped early", error);
    } finally {
      isRunning = false;
      progress.done();
      notify("OnImagesFinished", isStopped);
    }
  }

  /**
   * The posts, after the images: started but not awaited, as the images are, and heard through
   * OnPostsFinished
   * @param {{ index: number, fileIds: string[] }[]} batches
   */
  function runPosts(batches) {
    isRunning = true;
    posts = startPostImport(batches, {
      fileOf: (id) => collectedFiles.get(id),
      dotNetReference,
      onProgress: reportProgress,
    });

    void posts.finished.then((wasStopped) => {
      isRunning = false;
      notify("OnPostsFinished", wasStopped);
    });
  }

  function stop() {
    isStopped = true;
    posts?.stop();
    for (const abort of aborts) {
      abort();
    }
  }

  const stopListening = listenForFiles(zone, maximumFiles, remember);

  return {
    // read as a stream: as one interop call it would hit SignalR's 32 KB cap
    metadata() {
      return new Blob([JSON.stringify(collectedMetadata)]);
    },
    /** @param {string} id */
    file(id) {
      return collectedFiles.get(id) ?? new Blob([]);
    },
    /** @param {{ artworkId: number, fileIds: string[] }[]} artworks */
    importImages(artworks) {
      void run(artworks);
    },
    /** @param {{ index: number, fileIds: string[] }[]} batches */
    importPosts(batches) {
      runPosts(batches);
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
