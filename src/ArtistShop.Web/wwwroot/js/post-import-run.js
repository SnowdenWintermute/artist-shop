// A post import's run, for the post import page and the whole-website import: each post's files
// sent to the post editor's upload, then the post, which .NET saves before the next one starts

import { createUploadProgress } from "/js/upload-progress.js";
import { uploadWithRetries } from "/js/upload-request.js";

// the post editor's own upload, so an imported image is processed as one dropped into a post
const UPLOAD_URL = "/admin/uploads/post-image";

/**
 * Started at once. .NET hears each file (OnFileUploaded), each post that couldn't upload
 * (OnPostFailed) and each post to save (OnPostUploaded), each its own call, since SignalR turns
 * away a message over 32 KB and a blur alone can be 1 KB. finished resolves with whether Stop ended it
 * @param {{ index: number, fileIds: string[] }[]} posts
 * @param {object} options
 * @param {(id: string) => File | undefined} options.fileOf
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} options.dotNetReference
 * @param {(percentComplete: number) => void} options.onProgress
 * @returns {{ finished: Promise<boolean>, stop: () => void }}
 */
export function startPostImport(posts, { fileOf, dotNetReference, onProgress }) {
  /** @type {Set<() => void>} */
  const aborts = new Set();
  let isStopped = false;
  const progress = createUploadProgress(
    posts.flatMap(({ fileIds }) => fileIds).reduce((sum, id) => sum + (fileOf(id)?.size ?? 0), 0),
    onProgress
  );

  /** @param {{ index: number, fileIds: string[] }} post */
  async function importPost(post) {
    for (const fileId of post.fileIds) {
      const file = fileOf(fileId);
      const outcome = file
        ? await uploadWithRetries({ id: fileId, url: UPLOAD_URL, file, progress, aborts, isStopped: () => isStopped })
        : { failure: "The file is no longer there." };

      if (outcome === null) {
        return;
      }

      if ("failure" in outcome) {
        await dotNetReference.invokeMethodAsync("OnPostFailed", post.index, outcome.failure);
        return;
      }

      await dotNetReference.invokeMethodAsync("OnFileUploaded", fileId, JSON.parse(outcome.response.text));
    }

    await dotNetReference.invokeMethodAsync("OnPostUploaded", post.index);
  }

  async function run() {
    try {
      for (const post of posts) {
        if (isStopped) {
          break;
        }

        await importPost(post);
      }
    } catch (error) {
      console.error("The post import stopped early", error);
    } finally {
      progress.done();
    }

    return isStopped;
  }

  return {
    finished: run(),
    stop() {
      isStopped = true;
      for (const abort of aborts) {
        abort();
      }
    },
  };
}
