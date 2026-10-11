// The files a FileDropZoneFrame is given, by its file picker, its folder picker or a dropped
// folder, each with its path inside what was chosen

/**
 * @param {HTMLElement} zone an element wrapping the drop zone: both "change" and "entriesdropped"
 *   bubble up to it
 * @param {number} maximumFiles reading stops one past this, which is enough to know it was passed
 * @param {(found: { path: string, file: File }[]) => void} onFiles
 * @returns {() => void} stops listening
 */
export function listenForFiles(zone, maximumFiles, onFiles) {
  /** @param {FileSystemEntry[]} entries */
  async function collectEntries(entries) {
    /** @type {{ path: string, file: File }[]} */
    const found = [];

    for (const entry of entries) {
      await collectEntry(entry, found);
    }

    onFiles(found);
  }

  /**
   * @param {FileSystemEntry} entry
   * @param {{ path: string, file: File }[]} found
   */
  async function collectEntry(entry, found) {
    // one past the cap is enough to know it was passed, and stops a runaway folder being read out
    if (found.length > maximumFiles) {
      return;
    }

    if (isFileEntry(entry)) {
      found.push({ path: entry.fullPath, file: await fileOf(entry) });
      return;
    }

    if (isDirectoryEntry(entry)) {
      for (const child of await readAllEntries(entry.createReader())) {
        await collectEntry(child, found);
      }
    }
  }

  /** @param {Event} event */
  function onChange(event) {
    const input = event.target;
    if (!(input instanceof HTMLInputElement) || input.files === null) {
      return;
    }

    // a folder picker fills in webkitRelativePath; a file picker leaves it empty
    onFiles([...input.files].map((file) => ({ path: file.webkitRelativePath || file.name, file })));

    // so that choosing the same folder a second time still counts as a change
    input.value = "";
  }

  /** @param {Event} event */
  function onEntriesDropped(event) {
    collectEntries(/** @type {CustomEvent} */ (event).detail).catch((error) =>
      console.error("Reading the dropped folder failed", error)
    );
  }

  zone.addEventListener("change", onChange);
  zone.addEventListener("entriesdropped", onEntriesDropped);

  return () => {
    zone.removeEventListener("change", onChange);
    zone.removeEventListener("entriesdropped", onEntriesDropped);
  };
}

/**
 * The files a drop zone was given, each kept under a random id until the next choice. .NET reads
 * their list from metadata() and a text file from file(id), both as streams
 * @param {HTMLElement} zone an element wrapping the drop zone
 * @param {number} maximumFiles
 * @param {object} owner
 * @param {() => boolean} owner.isRunning a choice made during a run is ignored: the zone is disabled
 *   then, so this is a backstop, as clearing the files would strand the ones not yet sent
 * @param {(method: "OnTooManyFiles" | "OnFilesCollected") => void} owner.notify
 */
export function createFileCollection(zone, maximumFiles, { isRunning, notify }) {
  /** @type {Map<string, File>} */
  const files = new Map();
  /** @type {{ id: string, path: string, size: number, type: string }[]} */
  let metadata = [];

  /** @param {{ path: string, file: File }[]} found */
  function remember(found) {
    if (isRunning()) {
      return;
    }

    files.clear();
    metadata = [];

    // the folder picker can hand over a whole tree at once, so this catches both ways in
    if (found.length > maximumFiles) {
      notify("OnTooManyFiles");
      return;
    }

    for (const { path, file } of found) {
      const id = crypto.randomUUID();
      files.set(id, file);
      metadata.push({ id, path, size: file.size, type: file.type });
    }

    notify("OnFilesCollected");
  }

  const stopListening = listenForFiles(zone, maximumFiles, remember);

  return {
    /** @param {string} id */
    fileOf(id) {
      return files.get(id);
    },
    // as one interop call this would hit SignalR's 32 KB message cap at about 200 files. Returning
    // the blob is all it takes -- .NET asked for an IJSStreamReference, so it wraps what comes back
    metadata() {
      return new Blob([JSON.stringify(metadata)]);
    },
    /** @param {string} id */
    file(id) {
      return files.get(id) ?? new Blob([]);
    },
    dispose() {
      stopListening();
      files.clear();
      metadata = [];
    },
  };
}

/**
 * readEntries hands back one batch at a time -- Chrome stops at 100 -- and an empty batch means
 * the folder has been read to the end
 * @param {FileSystemDirectoryReader} reader
 * @returns {Promise<FileSystemEntry[]>}
 */
function readAllEntries(reader) {
  return new Promise((resolve, reject) => {
    /** @type {FileSystemEntry[]} */
    const all = [];

    const readBatch = () =>
      reader.readEntries((batch) => {
        if (batch.length === 0) {
          resolve(all);
          return;
        }

        all.push(...batch);
        readBatch();
      }, reject);

    readBatch();
  });
}

/**
 * @param {FileSystemFileEntry} entry
 * @returns {Promise<File>}
 */
function fileOf(entry) {
  return new Promise((resolve, reject) => entry.file(resolve, reject));
}

// isFile and isDirectory say which an entry is, which the browser's types can't tell from them
/**
 * @param {FileSystemEntry} entry
 * @returns {entry is FileSystemFileEntry}
 */
function isFileEntry(entry) {
  return entry.isFile;
}

/**
 * @param {FileSystemEntry} entry
 * @returns {entry is FileSystemDirectoryEntry}
 */
function isDirectoryEntry(entry) {
  return entry.isDirectory;
}
