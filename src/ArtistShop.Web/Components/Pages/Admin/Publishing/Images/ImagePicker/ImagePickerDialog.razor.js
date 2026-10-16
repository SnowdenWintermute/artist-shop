// The image picker's dialog, opened by the text editor and by an image component's settings page, and
// the hidden file input its Upload new image opens
import { listenToPickerFrame } from "/js/picker-frame.js";

/**
 * What the upload endpoint answers with, its LibraryImageUploadResult, and what the image picker's
 * library step sends in the same shape
 * @typedef {object} UploadedImage
 * @property {string} storageKey
 * @property {number} width
 * @property {number} height
 * @property {string} blurDataUri
 * @property {string} altText
 */

// The hidden file input the image picker's Upload new image opens, for the Image button or Replace
// image, which says what was picked to whichever opened it last
/** @param {HTMLInputElement} fileInput */
export function createFilePicker(fileInput) {
  /** @type {((files: File[]) => void) | null} */
  let onPicked = null;

  fileInput.addEventListener("change", () => {
    const files = [...(fileInput.files ?? [])];
    // emptied, so picking the same file again is still a change
    fileInput.value = "";

    if (files.length > 0) {
      onPicked?.(files);
    }
  });

  return {
    /**
     * @param {{ multiple: boolean }} options
     * @param {(files: File[]) => void} handlePicked
     */
    open({ multiple }, handlePicked) {
      onPicked = handlePicked;
      fileInput.multiple = multiple;
      fileInput.click();
    },
  };
}

/**
 * What one opening of the image picker does with the artist's answer
 * @typedef {object} ImagePickerOpening
 * @property {() => void} onUpload Upload new image was clicked, so the file chooser can open
 * @property {(image: UploadedImage) => void} onChosen an image from the library
 */

// The dialog the Image button and Replace image open: the library in a frame, which is only given its
// page on opening and goes blank again on closing, and Upload new image above it
/**
 * @param {HTMLDialogElement} dialog
 * @param {AbortSignal} signal
 */
export function attachImagePicker(dialog, signal) {
  const frame = dialog.querySelector("iframe");
  const uploadButton = dialog.querySelector('button[data-action="upload"]');
  const librarySrc = frame?.dataset.src;

  if (!(frame instanceof HTMLIFrameElement) || !(uploadButton instanceof HTMLButtonElement) || librarySrc === undefined) {
    throw new Error("The image picker is missing its library frame or its upload button.");
  }

  /** @type {ImagePickerOpening | null} */
  let opening = null;

  // closed first, so the focus it hands back doesn't land after the cursor is placed
  /** @param {(current: ImagePickerOpening) => void} answer */
  const closeAndAnswer = (answer) => {
    const current = opening;
    dialog.close();

    if (current !== null) {
      answer(current);
    }
  };

  // still inside the click, which is what lets the file chooser open
  uploadButton.addEventListener("click", () => closeAndAnswer((current) => current.onUpload()), { signal });

  listenToPickerFrame(
    frame,
    /** @param {import("/js/picker-frame.js").PickerMessage<UploadedImage>} message */
    (message) =>
      closeAndAnswer((current) => {
        if (message.action === "choose") {
          current.onChosen(message.choice);
        }
      }),
    signal
  );

  dialog.addEventListener(
    "close",
    () => {
      opening = null;
      frame.setAttribute("src", "about:blank");
    },
    { signal }
  );

  return {
    /** @param {ImagePickerOpening} handlers */
    open(handlers) {
      opening = handlers;
      frame.setAttribute("src", librarySrc);
      dialog.showModal();
    },
  };
}

/** @typedef {ReturnType<typeof createFilePicker>} FilePicker */
/** @typedef {ReturnType<typeof attachImagePicker>} ImagePicker */
