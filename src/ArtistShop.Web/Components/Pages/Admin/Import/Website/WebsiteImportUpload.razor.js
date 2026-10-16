import { createFileCollection } from "/js/collect-files.js";
import { createUploadProgress } from "/js/upload-progress.js";
import { startPostImport } from "/js/post-import-run.js";
import { uploadWithRetries } from "/js/upload-request.js";
import { importBrandImages } from "/js/brand-image-import.js";

// a courtesy to the server, which has its own limits and doesn't trust this number. Each worker
// takes a whole work, so a work's images still go in order
const CONCURRENT_WORKS = 3;

/**
 * The files of a chosen whole-website folder, kept here until the import sends them. .NET reads
 * their list, and the few text files it plans with, as streams
 * @param {HTMLElement} zone an element wrapping the drop zone
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 * @param {number} maximumFiles
 * @param {string} appendImageUrl where each work's image goes, ImageUploadEndpoints.AppendWorkImagePath
 * @param {string} libraryImageUrl where each post's image goes, ImageUploadEndpoints.LibraryImageUploadPath
 */
export function createWebsiteImporter(zone, dotNetReference, maximumFiles, appendImageUrl, libraryImageUrl) {
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

  /** @param {{ workId: number, fileIds: string[] }} work */
  async function importWork(work) {
    for (const fileId of work.fileIds) {
      const file = files.fileOf(fileId);
      const outcome = file
        ? await uploadWithRetries({
            id: fileId,
            url: appendImageUrl,
            file,
            fields: { workId: String(work.workId) },
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
   * through OnImagesFinished instead. The logos and icon go first, as there are only a few
   * @param {{ workId: number, fileIds: string[] }[]} works
   * @param {{ fileId: string, url: string }[]} brandImages
   */
  async function run(works, brandImages) {
    isStopped = false;
    isRunning = true;
    const queue = [...works];
    progress = createUploadProgress(
      [...brandImages.map(({ fileId }) => fileId), ...works.flatMap(({ fileIds }) => fileIds)].reduce(
        (sum, id) => sum + (files.fileOf(id)?.size ?? 0),
        0
      ),
      reportProgress
    );

    async function work() {
      while (!isStopped && queue.length > 0) {
        const work = queue.shift();
        if (work) {
          await importWork(work);
        }
      }
    }

    try {
      await importBrandImages(brandImages, {
        fileOf: files.fileOf,
        progress,
        aborts,
        isStopped: () => isStopped,
        onAdded: (fileId) => notify("OnBrandImageAdded", fileId),
        onFailed: (fileId, problem) => notify("OnImageFailed", fileId, problem),
      });
      await Promise.all(Array.from({ length: CONCURRENT_WORKS }, work));
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
      uploadUrl: libraryImageUrl,
      fileOf: files.fileOf,
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

  const files = createFileCollection(zone, maximumFiles, { isRunning: () => isRunning, notify });

  return {
    metadata: files.metadata,
    file: files.file,
    /**
     * @param {{ workId: number, fileIds: string[] }[]} works
     * @param {{ fileId: string, url: string }[]} brandImages
     */
    importImages(works, brandImages) {
      void run(works, brandImages);
    },
    /** @param {{ index: number, fileIds: string[] }[]} batches */
    importPosts(batches) {
      runPosts(batches);
    },
    stop,
    dispose() {
      stop();
      files.dispose();
    },
  };
}
