// The cursor around a post editor's embeds. An embed is a line of its own that can't hold the
// cursor, and the browser can't show one just before an embed either. So the cursor goes:
// - before an embed: to the end of the line of text above it
// - after an embed: to the start of the line of text below it
// Where there is no line of text there, between two embeds or above an embed that starts the post,
// that place is a gap, and the cursor goes to an empty line added there. The artist reaches a gap
// with the arrow keys, by clicking it, or with an embed toolbar's Text above and Text below. The
// line stays once something is typed in it, and is taken out if the cursor leaves it empty, since
// the post page shows an empty line as a gap
import { indexOfNode, isEmbedAt, isEmbedLine } from "/js/quill-document.js";

// whether two places in the text are on the same row on screen, as in a paragraph that wraps
/**
 * @param {Quill} quill
 * @param {number} first
 * @param {number} second
 */
function isSameRow(quill, first, second) {
  const firstBounds = quill.getBounds(first);
  const secondBounds = quill.getBounds(second);
  return firstBounds !== null && secondBounds !== null && Math.abs(firstBounds.top - secondBounds.top) < 1;
}

// Where Up or Down takes the cursor out of its line: the last place of the line above, or the
// first of the line below. Null away from the line's first or last row, where they stay in the line
/**
 * @param {Quill} quill
 * @param {"ArrowUp" | "ArrowDown"} key
 * @param {number} index
 */
export function placeBeyondLine(quill, key, index) {
  const [line, offset] = quill.getLine(index);
  const lineStart = index - offset;
  const lineEnd = lineStart + (line?.length() ?? 1) - 1;

  if (key === "ArrowUp") {
    return isSameRow(quill, index, lineStart) ? lineStart - 1 : null;
  }

  return isSameRow(quill, index, lineEnd) ? lineEnd + 1 : null;
}

// Quill's own arrow key bindings for embeds, which only step over an embed within a line. Quill
// merges a binding given to it by name into its own, so null turns them off
export const quillEmbedArrowBindingsOff = {
  "embed left": null,
  "embed right": null,
  "embed left shift": null,
  "embed right shift": null,
};

/**
 * @typedef {object} EmbedLines
 * @property {(index: number) => void} add puts an empty line at index, with the cursor in it
 */

/**
 * @param {Quill} quill
 * @param {AbortSignal} signal
 * @returns {EmbedLines}
 */
export function attachEmbedLines(quill, signal) {
  const Delta = Quill.import("delta");
  const quillToolbar = quill.getModule("toolbar").container;

  // the line last added, until the cursor leaves it
  /** @type {QuillBlot | null} */
  let added = null;

  // where the added line is, or null once an edit has removed it
  /** @param {QuillBlot} line */
  function indexOf(line) {
    return line.domNode.isConnected ? quill.getIndex(line) : null;
  }

  // Lets go of the added line, taking it out if it's still empty. Returns where it was taken from
  function letGo() {
    const line = added;
    added = null;
    const index = line === null ? null : indexOf(line);

    if (line === null || index === null || line.length() !== 1) {
      return null;
    }

    quill.updateContents(new Delta().retain(index).delete(1), "api");
    return index;
  }

  // the index once the added line, if it's let go of and taken out, is gone
  /** @param {number} index */
  function afterLetGo(index) {
    const removedAt = letGo();
    return removedAt !== null && removedAt < index ? index - 1 : index;
  }

  // Added and taken out as "api", which undo skips: arrowing past a row of embeds adds and takes out
  // a line between each, and as "user" each would leave an undo step that does nothing
  /** @param {number} index */
  function add(index) {
    const at = afterLetGo(index);

    quill.updateContents(new Delta().retain(at).insert("\n"), "api");
    // set before the cursor moves, so the selection-change below sees the cursor is in it
    [added] = quill.getLine(at);
    quill.setSelection(at, 0, "user");
  }

  // Leaving for Quill's toolbar, such as to make the line a heading, isn't leaving the line
  quill.on("selection-change", (range) => {
    const line = added;

    if (line === null || (range === null && quillToolbar.contains(document.activeElement))) {
      return;
    }

    const isInLine = range !== null && range.length === 0 && range.index === indexOf(line);

    if (!isInLine) {
      letGo();
    }
  });

  /** @param {number} embed */
  function goBefore(embed) {
    const at = afterLetGo(embed);

    if (at > 0 && !isEmbedAt(quill, at - 1)) {
      quill.setSelection(at - 1, 0, "user");
    } else {
      add(at);
    }
  }

  /** @param {number} embed */
  function goAfter(embed) {
    const at = afterLetGo(embed);

    if (isEmbedAt(quill, at + 1)) {
      add(at + 1);
    } else {
      quill.setSelection(at + 1, 0, "user");
    }
  }

  // The embed an arrow key moves the cursor onto, or null for the browser to move it as usual. Up
  // and Down only leave a line from its first and last rows. From the hidden place before an
  // embed, which a click can still reach, every key moves off that embed
  /**
   * @param {"ArrowLeft" | "ArrowRight" | "ArrowUp" | "ArrowDown"} key
   * @param {number} index
   */
  function embedAhead(key, index) {
    if (isEmbedAt(quill, index)) {
      return index;
    }

    // an embed starts a line, so Right only ever reaches one from the end of the line before it
    const ahead =
      key === "ArrowLeft" ? index - 1 : key === "ArrowRight" ? index + 1 : placeBeyondLine(quill, key, index);

    return ahead !== null && isEmbedAt(quill, ahead) ? ahead : null;
  }

  // returning true lets the browser move the cursor
  const keyboard = quill.getModule("keyboard");

  for (const [key, go] of /** @type {const} */ ([
    ["ArrowLeft", goBefore],
    ["ArrowUp", goBefore],
    ["ArrowRight", goAfter],
    ["ArrowDown", goAfter],
  ])) {
    keyboard.addBinding({
      key,
      collapsed: true,
      handler(range) {
        const embed = embedAhead(key, range.index);

        if (embed === null) {
          return true;
        }

        go(embed);
        return false;
      },
    });
  }

  // Backspace removes what's just before the cursor, which below an embed is the embed itself, as
  // an embed is its own line's end. Delete does the same above one. Instead, an empty line goes,
  // as it would below text, and the cursor moves past the embed. An embed is removed from its
  // toolbar, or by selecting it first
  /** @param {"backward" | "forward"} direction */
  function deleteBesideEmbed(direction) {
    const range = quill.getSelection();

    if (range === null || range.length > 0) {
      return false;
    }

    const { index } = range;
    const [line] = quill.getLine(index);
    const isEmptyLine = line !== null && line.length() === 1;

    // An empty first line above an embed has nothing before it to join, and a phone has no Delete
    // key, so Backspace takes it out, and the cursor goes below the embed
    if (direction === "backward" && index === 0 && isEmptyLine && isEmbedAt(quill, 1)) {
      if (line === added) {
        goAfter(1);
      } else {
        quill.updateContents(new Delta().delete(1), "user");
        goAfter(0);
      }

      return true;
    }

    if (direction === "backward") {
      if (!isEmbedAt(quill, index - 1)) {
        return false;
      }

      // the added line goes as the cursor leaves it
      if (isEmptyLine && line !== added) {
        quill.updateContents(new Delta().retain(index).delete(1), "user");
      }

      goBefore(index - 1);
      return true;
    }

    // from the hidden place before an embed, or from the end of the line above one
    const embed = isEmbedAt(quill, index) ? index : isEmbedAt(quill, index + 1) ? index + 1 : null;

    if (embed === null) {
      return false;
    }

    if (embed === index + 1 && isEmptyLine && line !== added) {
      quill.updateContents(new Delta().retain(index).delete(1), "user");
      goAfter(embed - 1);
    } else {
      goAfter(embed);
    }

    return true;
  }

  // Before Quill's own Backspace, which would remove the embed: it skips a key already handled
  quill.root.addEventListener(
    "keydown",
    (event) => {
      const direction = event.key === "Backspace" ? "backward" : event.key === "Delete" ? "forward" : null;

      if (direction !== null && !event.isComposing && deleteBesideEmbed(direction)) {
        event.preventDefault();
      }
    },
    { signal, capture: true }
  );

  // An Android keyboard often sends no Backspace key, only the deletion it's about to make
  quill.root.addEventListener(
    "beforeinput",
    (event) => {
      // every input ending so is a deletion, such as deleteContentBackward or deleteWordForward
      const direction = event.inputType.endsWith("Backward")
        ? "backward"
        : event.inputType.endsWith("Forward")
          ? "forward"
          : null;

      if (direction !== null && deleteBesideEmbed(direction)) {
        event.preventDefault();
      }
    },
    { signal }
  );

  // The embed below a click in the space between two embeds, or above the first line when that's
  // an embed, or null if the click was anywhere else. Embeds side by side have no space between
  // them in height, so only Text above and Text below reach a line there
  /** @param {number} y */
  function embedBelowGap(y) {
    /** @type {{ isEmbed: boolean, bottom: number } | null} */
    let previous = null;

    for (const child of quill.root.children) {
      const isEmbed = isEmbedLine(child);
      const { top, bottom } = child.getBoundingClientRect();

      if (y < top) {
        const isBetweenEmbeds = previous === null || (previous.isEmbed && y >= previous.bottom);
        return isEmbed && isBetweenEmbeds ? child : null;
      }

      previous = { isEmbed, bottom };
    }

    return null;
  }

  // A click there lands on the editing area itself. Shift extends the selection as usual
  quill.root.addEventListener(
    "mousedown",
    (event) => {
      if (event.target !== quill.root || event.button !== 0 || event.shiftKey) {
        return;
      }

      const embed = embedBelowGap(event.clientY);
      const index = embed === null ? null : indexOfNode(quill, embed);

      if (index !== null) {
        // otherwise the browser puts the cursor on the nearest line it can
        event.preventDefault();
        add(index);
      }
    },
    { signal }
  );

  return { add };
}
