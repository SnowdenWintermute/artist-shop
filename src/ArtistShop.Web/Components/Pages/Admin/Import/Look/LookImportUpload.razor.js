import { createFileCollection } from "/js/collect-files.js";
import { createUploadProgress } from "/js/upload-progress.js";
import { importBrandImages } from "/js/brand-image-import.js";

/**
 * The files of a chosen look and wording folder, kept here until the import sends the logos and
 * icon. .NET reads their list, and look.json, as streams
 * @param {HTMLElement} zone an element wrapping the drop zone
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {number} maximumFiles
 */
export function createLookImporter(zone, dotNetReference, maximumFiles) {
  /** @type {Set<() => void>} */
  const aborts = new Set();
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

  /**
   * Started but not awaited: .NET hears the end through OnBrandImagesFinished
   * @param {{ fileId: string, url: string }[]} images
   */
  async function run(images) {
    isStopped = false;
    isRunning = true;

    try {
      await importBrandImages(images, {
        fileOf: files.fileOf,
        // too few files for a progress bar
        progress: createUploadProgress(0, () => {}),
        aborts,
        isStopped: () => isStopped,
        onAdded: (fileId) => notify("OnBrandImageAdded", fileId),
        onFailed: (fileId, problem) => notify("OnBrandImageFailed", fileId, problem),
      });
    } catch (error) {
      console.error("The logo import stopped early", error);
    } finally {
      isRunning = false;
      notify("OnBrandImagesFinished");
    }
  }

  function stop() {
    isStopped = true;
    for (const abort of aborts) {
      abort();
    }
  }

  const files = createFileCollection(zone, maximumFiles, { isRunning: () => isRunning, notify });

  return {
    metadata: files.metadata,
    file: files.file,
    /** @param {{ fileId: string, url: string }[]} images */
    importBrandImages(images) {
      void run(images);
    },
    dispose() {
      stop();
      files.dispose();
    },
  };
}
