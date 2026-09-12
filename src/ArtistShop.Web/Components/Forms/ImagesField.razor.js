/**
 * @param {HTMLElement} dropZone
 * @param {HTMLInputElement} fileInput
 * @param {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} dotNetReference
 */
export function createUploader(dropZone, fileInput, dotNetReference) {
  /** @type {Map<string, File>} */
  const pendingFiles = new Map();

  /** @param {FileList} files */
  function announce(files) {
    for (const file of files) {
      const id = crypto.randomUUID();
      pendingFiles.set(id, file);
      dotNetReference.invokeMethodAsync(
        "OnFileSelected",
        id,
        file.name,
        file.size
      );
    }
  }

  const dragOverClass = "bg-blue-50";

  /** @param {DragEvent} event */
  function onDragOver(event) {
    event.preventDefault();
    dropZone.classList.add(dragOverClass);
  }

  function onDragLeave() {
    dropZone.classList.remove(dragOverClass);
  }

  /** @param {DragEvent} event */
  function onDrop(event) {
    event.preventDefault();
    dropZone.classList.remove(dragOverClass);

    if (!event.dataTransfer) {
      return;
    }

    announce(event.dataTransfer.files);
  }

  function onChange() {
    if (fileInput.files !== null) {
      announce(fileInput.files);
    }
    fileInput.value = "";
  }

  dropZone.addEventListener("dragover", onDragOver);
  dropZone.addEventListener("dragleave", onDragLeave);
  dropZone.addEventListener("drop", onDrop);
  fileInput.addEventListener("change", onChange);

  return {
    open() {
      fileInput.click();
    },

    dispose() {
      dropZone.removeEventListener("dragover", onDragOver);
      dropZone.removeEventListener("dragleave", onDragLeave);
      dropZone.removeEventListener("drop", onDrop);
      fileInput.removeEventListener("change", onChange);
      pendingFiles.clear();
    },
  };
}
