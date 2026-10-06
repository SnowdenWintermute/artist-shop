// Shift and an arrow key around a post editor's embeds. The browser has no stopping place just
// before an embed, so from one it goes wrong, and to one it steps on past every embed in a row,
// selecting them all. Near an embed the selection's moving end is moved here instead, one embed at
// a time

import { isEmbedAt, placeBeyondLine } from "/js/embed-lines.js";

// Which end of the selection moves with Shift and an arrow key: the one the browser calls its focus
function isSelectionBackward() {
  const selection = document.getSelection();

  if (selection === null || selection.anchorNode === null || selection.focusNode === null) {
    return false;
  }

  const range = document.createRange();
  range.setStart(selection.anchorNode, selection.anchorOffset);
  range.setEnd(selection.focusNode, selection.focusOffset);
  // a range whose end comes before its start collapses onto the end
  return range.collapsed && !selection.isCollapsed;
}

// Quill sets every selection forwards, so a backward one is turned round after it, for the browser
// to carry on from the right end
function makeSelectionBackward() {
  const selection = document.getSelection();

  if (selection === null || selection.rangeCount === 0) {
    return;
  }

  const range = selection.getRangeAt(0);
  selection.setBaseAndExtent(range.endContainer, range.endOffset, range.startContainer, range.startOffset);
}

// Where an arrow key moves the selection's moving end, or null for the browser to move it as usual.
// Up and Down select a whole embed, as Left and Right reach either side of it. From the hidden
// place before an embed, which Left and Right stop at, every key moves off it
/**
 * @param {Quill} quill
 * @param {"ArrowLeft" | "ArrowRight" | "ArrowUp" | "ArrowDown"} key
 * @param {number} focus
 */
function focusAhead(quill, key, focus) {
  if (key === "ArrowLeft" || key === "ArrowRight") {
    const moved = key === "ArrowLeft" ? focus - 1 : focus + 1;
    // moving off the hidden place, over an embed, or onto the hidden place before one
    const nearEmbed = isEmbedAt(quill, focus) || isEmbedAt(quill, moved);
    return moved >= 0 && nearEmbed ? moved : null;
  }

  if (isEmbedAt(quill, focus)) {
    return key === "ArrowUp" ? (focus > 0 ? focus - 1 : null) : focus + 1;
  }

  const beyond = placeBeyondLine(quill, key, focus);

  if (beyond === null || !isEmbedAt(quill, beyond)) {
    return null;
  }

  // before the embed above, or after the one below
  return key === "ArrowUp" ? beyond : beyond + 1;
}

/** @param {Quill} quill */
export function attachEmbedSelection(quill) {
  const keyboard = quill.getModule("keyboard");

  for (const key of /** @type {const} */ (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"])) {
    keyboard.addBinding({
      key,
      shiftKey: true,
      handler(range) {
        const backward = isSelectionBackward();
        const start = range.index;
        const end = range.index + range.length;
        const anchor = backward ? end : start;
        const focus = backward ? start : end;
        const moved = focusAhead(quill, key, focus);

        // returning true lets the browser move it
        if (moved === null) {
          return true;
        }

        quill.setSelection(Math.min(anchor, moved), Math.abs(anchor - moved), "user");

        if (moved < anchor) {
          makeSelectionBackward();
        }

        return false;
      },
    });
  }
}
