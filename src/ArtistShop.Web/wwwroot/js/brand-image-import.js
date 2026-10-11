import { uploadWithRetries } from "/js/upload-request.js";

// The logos and icon of a look import, each sent to the Branding page's upload endpoint, so they're
// checked and processed as an upload there is. Shared by the look import and the whole-website import

/**
 * One at a time, in the order given. Resolves once each is in or turned away, or Stop ended it
 * @param {{ fileId: string, url: string }[]} images
 * @param {object} run
 * @param {(fileId: string) => File | undefined} run.fileOf
 * @param {{ loaded: (id: string, loaded: number) => void, retrying: (id: string) => void, finished: (id: string, size: number) => void }} run.progress
 * @param {Set<() => void>} run.aborts
 * @param {() => boolean} run.isStopped
 * @param {(fileId: string) => void} run.onAdded
 * @param {(fileId: string, problem: string) => void} run.onFailed
 */
export async function importBrandImages(images, { fileOf, progress, aborts, isStopped, onAdded, onFailed }) {
  for (const { fileId, url } of images) {
    const file = fileOf(fileId);
    const outcome = file
      ? await uploadWithRetries({ id: fileId, url, file, progress, aborts, isStopped })
      : { failure: "The file is no longer there." };

    if (outcome === null) {
      return;
    }

    if ("failure" in outcome) {
      onFailed(fileId, outcome.failure);
    } else {
      onAdded(fileId);
    }
  }
}
