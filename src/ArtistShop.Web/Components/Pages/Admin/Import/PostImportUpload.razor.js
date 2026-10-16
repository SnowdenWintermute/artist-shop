import { createFileCollection } from "/js/collect-files.js";
import { startPostImport } from "/js/post-import-run.js";

/**
 * @param {HTMLElement} zone an element wrapping the drop zone
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {number} maximumFiles
 * @param {string} uploadUrl where each image goes, ImageUploadEndpoints.LibraryImageUploadPath
 */
export function createPostImporter(zone, dotNetReference, maximumFiles, uploadUrl) {
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

  /**
   * Started but not awaited, as the bulk uploader's run is: .NET gives up on a call it awaits
   * after a minute, and hears the end through OnImportFinished instead
   * @param {{ index: number, fileIds: string[] }[]} posts
   */
  function run(posts) {
    isRunning = true;
    current = startPostImport(posts, {
      uploadUrl,
      fileOf: files.fileOf,
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

  const files = createFileCollection(zone, maximumFiles, { isRunning: () => isRunning, notify });

  return {
    metadata: files.metadata,
    file: files.file,
    /** @param {{ index: number, fileIds: string[] }[]} posts */
    importPosts(posts) {
      run(posts);
    },
    stop,
    dispose() {
      stop();
      files.dispose();
    },
  };
}
