import { listenForFiles } from "/js/collect-files.js";

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

  /** @param {string} method */
  function notify(method, ...args) {
    dotNetReference
      .invokeMethodAsync(method, ...args)
      .catch((error) => console.error(`${method} failed`, error));
  }

  /** @param {{ path: string, file: File }[]} found */
  function remember(found) {
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
    dispose() {
      stopListening();
      collectedFiles.clear();
      collectedMetadata = [];
    },
  };
}
