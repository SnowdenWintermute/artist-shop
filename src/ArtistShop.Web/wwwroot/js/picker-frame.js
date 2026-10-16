// The pickers show their pages in a frame inside a dialog of the page that opened them, the text editor's
// or an image component's settings page. The pages tell that page what was chosen, and pass on Escape,
// which a frame keeps to itself

// marks the pickers' messages, since any page in any frame can post to this window
const PICKER_MESSAGE = "artshop-picker";

/**
 * @template C
 * @typedef {{ action: "close" } | { action: "choose", choice: C }} PickerMessage
 */

// Called by a picker's page inside the frame. A picker page opened on its own, outside a frame,
// has no editor to tell
/** @param {PickerMessage<unknown>} message */
export function sendToPickerOwner(message) {
  if (window.parent !== window) {
    window.parent.postMessage({ type: PICKER_MESSAGE, ...message }, location.origin);
  }
}

/**
 * Hears only this frame's messages, from a page of this site, so another picker's frame on the same
 * page, or any other page, is ignored
 * @template C
 * @param {HTMLIFrameElement} frame
 * @param {(message: PickerMessage<C>) => void} handle
 * @param {AbortSignal} signal
 */
export function listenToPickerFrame(frame, handle, signal) {
  window.addEventListener(
    "message",
    (event) => {
      if (event.origin !== location.origin || event.source !== frame.contentWindow) {
        return;
      }

      /** @type {(PickerMessage<C> & { type: unknown }) | null} */
      const message = event.data;

      if (message?.type === PICKER_MESSAGE) {
        handle(message);
      }
    },
    { signal }
  );
}
