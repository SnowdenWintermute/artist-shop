// Moving a post editor's embed by dragging it: held with a finger, or dragged with the mouse. Over
// text, a cursor shows where it will go, and letting go there splits the line round it, as an
// embed is a line of its own. Over another embed, a line shows it going above or below that one.
// The page scrolls when the pointer nears the top or bottom of the window. The move is one edit,
// so a single undo puts it back
import { dragScrollEdgePixels, dragScrollMaxPixelsPerFrame } from "/js/app-consts.js";
import { listenForGesture } from "/js/pointer-gesture.js";

// the place in the page under a point, as the browser would put a cursor there
/**
 * @param {number} x
 * @param {number} y
 */
function caretAt(x, y) {
  // missing before Safari 18.4, whatever the types say
  if (typeof document.caretPositionFromPoint === "function") {
    const position = document.caretPositionFromPoint(x, y);
    return position === null ? null : { node: position.offsetNode, offset: position.offset };
  }

  const range = document.caretRangeFromPoint(x, y);
  return range === null ? null : { node: range.startContainer, offset: range.startOffset };
}

/**
 * @param {Quill} quill
 * @param {AbortSignal} signal
 */
export function attachEmbedDrag(quill, signal) {
  const BlockEmbed = Quill.import("blots/block/embed");
  const Delta = Quill.import("delta");

  // Outside the editing area, whose every change Quill reads as an edit, in the box round it, which
  // they're placed in. Placed fixed on the screen instead, a phone put them off by the way it
  // scrolled. The marker is the cursor over text and the line over an embed
  const marker = document.createElement("div");
  marker.dataset.part = "embed-drop-marker";
  marker.hidden = true;
  // the embed faded where it is, under a cover rather than restyled, for the same reason
  const cover = document.createElement("div");
  cover.dataset.part = "embed-drag-cover";
  cover.hidden = true;
  quill.root.after(marker, cover);
  const box = quill.root.parentElement ?? quill.root;

  /**
   * @type {{
   *   embed: HTMLElement,
   *   point: import("/js/pointer-gesture.js").GesturePoint,
   *   dropAt: number | null,
   *   scrollFrame: number,
   * } | null}
   */
  let drag = null;

  // the line of the editing area a press is on, if that line is an embed
  /** @param {PointerEvent} event */
  function embedPressed(event) {
    let node = event.target instanceof Element ? event.target : null;

    while (node !== null && node.parentElement !== quill.root) {
      node = node.parentElement;
    }

    return node instanceof HTMLElement && Quill.find(node) instanceof BlockEmbed ? node : null;
  }

  // where in the text a place in the page is: in a text node, by its characters, and between an
  // element's children, by the child after it
  /**
   * @param {Node} node
   * @param {number} offset
   */
  function indexOfPlace(node, offset) {
    if (node instanceof Text) {
      const blot = Quill.find(node);
      return blot === null || blot instanceof Quill ? null : quill.getIndex(blot) + offset;
    }

    const after = node.childNodes[offset];
    const blot = Quill.find(after ?? node, true);

    if (blot === null || blot instanceof Quill) {
      return node === quill.root ? quill.getLength() - 1 : null;
    }

    // past an element's last child is the end of its line
    return quill.getIndex(blot) + (after === undefined ? blot.length() - 1 : 0);
  }

  /** @param {Element} line */
  function isEmbedLine(line) {
    return Quill.find(line) instanceof BlockEmbed;
  }

  // where a line of the editing area starts in the text
  /** @param {Element} line */
  function indexOfLine(line) {
    const blot = Quill.find(line);
    return blot === null || blot instanceof Quill ? null : quill.getIndex(blot);
  }

  // The embed under a point, if it's on one. Its own place is all an embed has to offer, so the
  // drop goes above or below it, by which half the point is in. Across as well as down, as text
  // wraps beside an embed that's to the left or right
  /** @param {import("/js/pointer-gesture.js").GesturePoint} point */
  function embedLineAt({ x, y }) {
    return [...quill.root.children].find((line) => {
      const { top, bottom, left, right } = line.getBoundingClientRect();
      return isEmbedLine(line) && y >= top && y < bottom && x >= left && x < right;
    });
  }

  // places an element by where it's to be on screen
  /**
   * @param {HTMLElement} element
   * @param {{ top: number, left: number, width: number, height: number }} place
   */
  function placeMarker(element, { top, left, width, height }) {
    const boxBounds = box.getBoundingClientRect();
    Object.assign(element.style, {
      top: `${top - boxBounds.top - box.clientTop}px`,
      left: `${left - boxBounds.left - box.clientLeft}px`,
      width: `${width}px`,
      height: `${height}px`,
    });
  }

  // A line at y: across the text's width, or under an embed text wraps beside, only as wide as it,
  // since the text beside it has no line there to go between
  /**
   * @param {number} y
   * @param {Element} embedLine
   */
  function showLine(y, embedLine) {
    const isWrapped = getComputedStyle(embedLine).float !== "none";
    const area = (isWrapped ? embedLine : quill.root).getBoundingClientRect();
    const style = getComputedStyle(quill.root);
    const left = isWrapped ? area.left : area.left + parseFloat(style.paddingLeft);
    const right = isWrapped ? area.right : area.right - parseFloat(style.paddingRight);
    placeMarker(marker, { top: y - 1.5, left, width: right - left, height: 3 });
  }

  // a cursor where the text at index is, which getBounds gives from the editor's corner
  /** @param {number} index */
  function showCaret(index) {
    const bounds = quill.getBounds(index);

    if (bounds === null) {
      marker.hidden = true;
      return;
    }

    const editor = box.getBoundingClientRect();
    placeMarker(marker, {
      top: editor.top + bounds.top,
      left: editor.left + bounds.left - 1,
      width: 2,
      height: bounds.bottom - bounds.top,
    });
  }

  // Where the embed would go, and the marker shown there, or null where it would stay put
  /** @param {{ embed: HTMLElement, point: import("/js/pointer-gesture.js").GesturePoint }} at */
  function findDrop({ embed, point }) {
    const from = indexOfLine(embed);
    const embedLine = embedLineAt(point);

    if (from === null) {
      return null;
    }

    if (embedLine !== undefined) {
      const { top, bottom } = embedLine.getBoundingClientRect();
      const isAbove = point.y < (top + bottom) / 2;
      const lineIndex = indexOfLine(embedLine);

      if (lineIndex === null) {
        return null;
      }

      const to = lineIndex + (isAbove ? 0 : 1);
      // Halfway to the line beside it, unless that line wraps beside this embed, or this one
      // beside it, where halfway would be over the embed
      const beside = isAbove ? embedLine.previousElementSibling : embedLine.nextElementSibling;
      const besideBounds = beside?.getBoundingClientRect();
      const edge = isAbove ? top : bottom;
      const besideEdge = isAbove ? besideBounds?.bottom : besideBounds?.top;
      const isClear = besideEdge !== undefined && (isAbove ? besideEdge <= top : besideEdge >= bottom);
      const y = isClear ? (edge + besideEdge) / 2 : edge;

      return to === from || to === from + 1 ? null : { to, show: () => showLine(y, embedLine) };
    }

    const place = caretAt(point.x, point.y);
    const index = place !== null && quill.root.contains(place.node) ? indexOfPlace(place.node, place.offset) : null;

    if (index === null) {
      return null;
    }

    // the line end takes the embed after the line, rather than leaving an empty line below it
    const [line, offset] = quill.getLine(index);
    const isLineEnd = line !== null && offset > 0 && offset === line.length() - 1;
    const to = isLineEnd ? index + 1 : index;

    return to === from || to === from + 1 ? null : { to, show: () => showCaret(index) };
  }

  function show() {
    if (drag === null) {
      return;
    }

    const drop = findDrop(drag);
    drag.dropAt = drop?.to ?? null;
    marker.hidden = drop === null;
    drop?.show();

    const { top, left, width, height } = drag.embed.getBoundingClientRect();
    placeMarker(cover, { top, left, width, height });
  }

  // Faster the nearer the edge, on every frame while the pointer is there, since it may rest there
  // without moving
  function scrollNearEdge() {
    if (drag === null) {
      return;
    }

    const { y } = drag.point;
    const intoTop = dragScrollEdgePixels - y;
    const intoBottom = y - (window.innerHeight - dragScrollEdgePixels);
    const into = intoTop > 0 ? -intoTop : intoBottom > 0 ? intoBottom : 0;

    if (into !== 0) {
      window.scrollBy(0, Math.max(-1, Math.min(1, into / dragScrollEdgePixels)) * dragScrollMaxPixelsPerFrame);
      show();
    }

    drag.scrollFrame = requestAnimationFrame(scrollNearEdge);
  }

  function finish() {
    if (drag !== null) {
      cancelAnimationFrame(drag.scrollFrame);
    }

    drag = null;
    marker.hidden = true;
    cover.hidden = true;
  }

  // Taken out and put in again as one change. Inside a line, the line is split first, its first
  // part keeping the line's formats, such as a heading, as the second part does
  /**
   * @param {HTMLElement} embed
   * @param {number} to
   */
  function move(embed, to) {
    const blot = Quill.find(embed);

    if (!(blot instanceof BlockEmbed)) {
      return;
    }

    const from = quill.getIndex(blot);
    const [op] = quill.getContents(from, 1).ops;
    const value = op?.insert;

    if (typeof value !== "object") {
      return;
    }

    // the "\n" ending the line it goes into, which holds the line's formats
    const [line, offset] = quill.getLine(to);
    const [lineEnd] = line !== null && offset > 0 ? quill.getContents(to - offset + line.length() - 1, 1).ops : [];

    /** @param {QuillDelta} change */
    const putIn = (change) => (lineEnd === undefined ? change : change.insert("\n", lineEnd.attributes)).insert(value);

    const change =
      to > from
        ? putIn(new Delta().retain(from).delete(1).retain(to - from - 1))
        : putIn(new Delta().retain(to)).retain(from - to).delete(1);

    quill.updateContents(change, "user");
  }

  listenForGesture(
    quill.root,
    {
      pick: embedPressed,

      onStart(embed, point) {
        drag = { embed, point, dropAt: null, scrollFrame: 0 };
        cover.hidden = false;
        show();
        drag.scrollFrame = requestAnimationFrame(scrollNearEdge);
      },

      onMove(point) {
        if (drag !== null) {
          drag.point = point;
          show();
        }
      },

      onEnd() {
        const ended = drag;
        finish();

        if (ended !== null && ended.dropAt !== null && ended.embed.isConnected) {
          move(ended.embed, ended.dropAt);
        }
      },

      onCancel: finish,
    },
    signal
  );
}
