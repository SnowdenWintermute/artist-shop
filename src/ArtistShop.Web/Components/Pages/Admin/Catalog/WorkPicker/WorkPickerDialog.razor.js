// The work picker's dialog, opened by the text editor, by an image component's settings page and by a link's
// work choice
import { listenToPickerFrame } from "/js/picker-frame.js";

/**
 * What the artist picked. Size and layout come only when adding: changing an embed's image keeps
 * the size and layout it has
 * @typedef {object} WorkPickerChoice
 * @property {number} workId
 * @property {string} storageKey
 * @property {"small" | "medium"} [size]
 * @property {string} [layout]
 */

/**
 * @typedef {object} WorkPicker
 * @property {string} addUrl the picker's first page for adding an embed
 * @property {(workId: number) => string} changeImageUrl its page of a work's images, for
 *   changing an embed's image
 * @property {string} chooseImageUrl its first page for picking one image of any work, which asks
 *   nothing more, for an image component
 * @property {string} chooseWorkUrl its first page for picking a work, for a link to its page
 * @property {(url: string, onChosen: (choice: WorkPickerChoice) => void) => void} open
 */

// Wires up the dialog holding the picker's frame. Its addresses come from the server, with a
// placeholder where a work's id goes
/**
 * @param {HTMLDialogElement} dialog
 * @param {AbortSignal} signal
 * @returns {WorkPicker}
 */
export function attachWorkPicker(dialog, signal) {
  const frame = dialog.querySelector("iframe");
  const { addSrc, changeImageSrc, chooseImageSrc, chooseWorkSrc, workIdPlaceholder } = frame?.dataset ?? {};

  if (
    frame === null ||
    addSrc === undefined ||
    changeImageSrc === undefined ||
    chooseImageSrc === undefined ||
    chooseWorkSrc === undefined ||
    workIdPlaceholder === undefined
  ) {
    throw new Error("The work picker is missing its frame or the frame's addresses.");
  }

  // what the current opening does with the choice
  /** @type {((choice: WorkPickerChoice) => void) | null} */
  let onChosen = null;

  listenToPickerFrame(
    frame,
    /** @param {import("/js/picker-frame.js").PickerMessage<WorkPickerChoice>} message */
    (message) => {
      const chosen = onChosen;
      // closed first, so the focus it hands back doesn't land after the cursor is placed
      dialog.close();

      if (message.action === "choose") {
        chosen?.(message.choice);
      }
    },
    signal
  );

  // the next opening starts again from its first page, and the page left behind stops running
  dialog.addEventListener(
    "close",
    () => {
      onChosen = null;
      frame.setAttribute("src", "about:blank");
    },
    { signal }
  );

  return {
    addUrl: addSrc,
    changeImageUrl: (workId) => changeImageSrc.replace(workIdPlaceholder, String(workId)),
    chooseImageUrl: chooseImageSrc,
    chooseWorkUrl: chooseWorkSrc,
    open(url, handleChoice) {
      onChosen = handleChoice;
      frame.src = url;
      dialog.showModal();
    },
  };
}
