// Dragging in PageOutline: a row, slot or component (data-drag) by its handle (data-drag-handle). Nothing moves
// while dragging: the part stays where it was, greyed (data-drag-source), and a copy follows the pointer. The places
// it can land are already in the layout, so nothing changes size: the gaps before each part and the Add links at
// each list's end (data-drop, with data-parent-id and data-index). Those it can go to light up (data-drop-ok) and
// the one under the pointer fills (data-drop-active). Over a part rather than a gap, the gap on that half of it is
// the one meant. Not lit: where it already is, a full row for a slot from another row, a slot already holding the
// component. Dropping calls the island, which saves and reads the layout again; the part stays greyed until then.
// Nothing drags while the island saves (data-saving on the root), as the layout is about to be read again.
import { dragScrollEdgePixels, dragScrollMaxPixelsPerFrame } from "/js/app-consts.js";
import { listenForGesture } from "/js/pointer-gesture.js";

/** @typedef {{ invokeMethodAsync: (method: string, ...args: unknown[]) => Promise<unknown> }} DotNetReference */
/** @typedef {"row" | "slot" | "component"} Kind */

/** @type {Record<Kind, string>} */
const dropMethods = { row: "DropRowAsync", slot: "DropSlotAsync", component: "DropComponentAsync" };

/** @param {string | undefined} value @returns {value is Kind} */
function isKind(value) {
  return value === "row" || value === "slot" || value === "component";
}

/** @param {HTMLElement} element */
function scrollingBoxOf(element) {
  for (let box = element.parentElement; box; box = box.parentElement) {
    const overflow = getComputedStyle(box).overflowY;

    if ((overflow === "auto" || overflow === "scroll") && box.scrollHeight > box.clientHeight) {
      return box;
    }
  }

  return document.scrollingElement;
}

// rows have no parent, so theirs is ""
/** @param {HTMLElement} element */
const parentOf = (element) => element.dataset.parentId ?? "";

/**
 * @param {DotNetReference} dotNetReference
 * @param {HTMLElement} root
 */
export function startOutlineDrag(dotNetReference, root) {
  const listeners = new AbortController();

  /**
   * @type {{
   *   kind: Kind,
   *   item: HTMLElement,
   *   copy: HTMLElement,
   *   offsetX: number,
   *   offsetY: number,
   *   x: number,
   *   y: number,
   *   place: HTMLElement | null,
   *   scrollFrame: number,
   * } | null}
   */
  let dragging = null;

  /** @param {string} selector */
  const elementsIn = (selector) =>
    [...root.querySelectorAll(selector)].flatMap((element) => (element instanceof HTMLElement ? [element] : []));

  /** @param {Kind} kind @param {string} parentId */
  const partsIn = (kind, parentId) => elementsIn(`[data-drag="${kind}"]`).filter((part) => parentOf(part) === parentId);

  /** @param {Kind} kind @param {string} parentId @param {number} index */
  const placeAt = (kind, parentId, index) =>
    elementsIn(`[data-drop="${kind}"]`).find((place) => parentOf(place) === parentId && Number(place.dataset.index) === index) ??
    null;

  // whether the dragged part would move by landing here, and is allowed to
  /** @param {Kind} kind @param {HTMLElement} item @param {HTMLElement} place */
  function canLand(kind, item, place) {
    const parentId = parentOf(place);
    const index = Number(place.dataset.index);

    if (parentId === parentOf(item)) {
      const fromIndex = partsIn(kind, parentId).indexOf(item);
      return index !== fromIndex && index !== fromIndex + 1;
    }

    if (kind === "component") {
      return !partsIn(kind, parentId).some((placed) => placed.dataset.id === item.dataset.id);
    }

    if (kind === "slot") {
      const row = elementsIn(`[data-drag="row"][data-id="${parentId}"]`)[0];
      return row !== undefined && !row.hasAttribute("data-full");
    }

    return true;
  }

  // the gap or Add link under the pointer; over a part, the gap on its nearer side; over a slot's or a row's own
  // room, its end
  /** @param {Kind} kind @param {number} x @param {number} y */
  function placeUnder(kind, x, y) {
    const under = document.elementFromPoint(x, y);

    if (!under || !root.contains(under)) {
      return null;
    }

    const place = under.closest(`[data-drop="${kind}"]`);

    if (place instanceof HTMLElement) {
      return place;
    }

    const part = under.closest(`[data-drag="${kind}"]`);

    if (part instanceof HTMLElement) {
      const rect = part.getBoundingClientRect();
      const isBefore = kind === "slot" ? x < rect.left + rect.width / 2 : y < rect.top + rect.height / 2;
      const parentId = parentOf(part);
      return placeAt(kind, parentId, partsIn(kind, parentId).indexOf(part) + (isBefore ? 0 : 1));
    }

    const container =
      kind === "component" ? under.closest('[data-drag="slot"]') : kind === "slot" ? under.closest('[data-drag="row"]') : null;

    if (container instanceof HTMLElement && container.dataset.id) {
      return placeAt(kind, container.dataset.id, partsIn(kind, container.dataset.id).length);
    }

    return null;
  }

  function show() {
    if (!dragging) {
      return;
    }

    dragging.copy.style.transform = `translate(${dragging.x - dragging.offsetX}px, ${dragging.y - dragging.offsetY}px)`;

    const place = placeUnder(dragging.kind, dragging.x, dragging.y);
    dragging.place = place?.hasAttribute("data-drop-ok") ? place : null;

    for (const active of elementsIn("[data-drop-active]")) {
      if (active !== dragging.place) {
        active.removeAttribute("data-drop-active");
      }
    }

    dragging.place?.setAttribute("data-drop-active", "");
  }

  // near the scrolling box's top or bottom, it scrolls, faster the nearer the edge as embed-drag.js does, and
  // what's under the pointer changes with it
  function scrollAtEdges() {
    if (!dragging) {
      return;
    }

    const box = scrollingBoxOf(root);

    if (box) {
      const rect = box === document.scrollingElement ? { top: 0, bottom: window.innerHeight } : box.getBoundingClientRect();
      const intoTop = rect.top + dragScrollEdgePixels - dragging.y;
      const intoBottom = dragging.y - (rect.bottom - dragScrollEdgePixels);
      const into = intoTop > 0 ? -intoTop : intoBottom > 0 ? intoBottom : 0;

      if (into !== 0) {
        box.scrollBy(0, Math.max(-1, Math.min(1, into / dragScrollEdgePixels)) * dragScrollMaxPixelsPerFrame);
        show();
      }
    }

    dragging.scrollFrame = requestAnimationFrame(scrollAtEdges);
  }

  /** @param {HTMLElement} item @param {{ x: number, y: number }} point */
  function start(item, { x, y }) {
    const kind = item.dataset.drag;

    if (!isKind(kind)) {
      return;
    }

    const rect = item.getBoundingClientRect();
    const copy = /** @type {HTMLElement} */ (item.cloneNode(true));
    copy.removeAttribute("data-drag");
    copy.style.cssText = `position: fixed; left: 0; top: 0; width: ${rect.width}px; margin: 0; pointer-events: none;
      z-index: 50; opacity: 0.85; box-shadow: 0 4px 12px rgb(0 0 0 / 0.25);`;
    document.body.append(copy);

    for (const place of elementsIn(`[data-drop="${kind}"]`)) {
      place.toggleAttribute("data-drop-ok", canLand(kind, item, place));
    }

    item.setAttribute("data-drag-source", "");
    dragging = { kind, item, copy, offsetX: x - rect.left, offsetY: y - rect.top, x, y, place: null, scrollFrame: 0 };
    dragging.scrollFrame = requestAnimationFrame(scrollAtEdges);
    show();
  }

  // the source keeps its grey while the island saves, so nothing jumps before the layout is read again
  function finish({ keepSource = false } = {}) {
    if (dragging) {
      cancelAnimationFrame(dragging.scrollFrame);
      dragging.copy.remove();

      if (!keepSource) {
        dragging.item.removeAttribute("data-drag-source");
      }
    }

    for (const place of elementsIn("[data-drop-ok], [data-drop-active]")) {
      place.removeAttribute("data-drop-ok");
      place.removeAttribute("data-drop-active");
    }

    dragging = null;
  }

  function drop() {
    if (!dragging?.place) {
      finish();
      return;
    }

    const { kind, item, place } = dragging;
    const id = Number(item.dataset.id);
    const index = Number(place.dataset.index);
    // the page's rows are one list, with no parent
    const landing = kind === "row" ? [id, index] : [id, Number(parentOf(item)), Number(parentOf(place)), index];
    finish({ keepSource: true });
    dotNetReference
      .invokeMethodAsync(dropMethods[kind], ...landing)
      .catch((error) => console.error(`${dropMethods[kind]} failed`, error))
      .finally(() => item.removeAttribute("data-drag-source"));
  }

  listenForGesture(
    root,
    {
      // a handle's part, unless the layout is about to be read again
      pick(event) {
        const handle = event.target instanceof Element ? event.target.closest("[data-drag-handle]") : null;
        const item = handle?.closest("[data-drag]");
        return item instanceof HTMLElement && isKind(item.dataset.drag) && !root.hasAttribute("data-saving") ? item : null;
      },

      onStart: start,

      onMove({ x, y }) {
        if (dragging) {
          dragging.x = x;
          dragging.y = y;
          show();
        }
      },

      onEnd: drop,

      onCancel: () => finish(),

      // the handles are touch-none, so a finger on one drags at once rather than scrolling
      holdMilliseconds: 0,
    },
    listeners.signal
  );

  return {
    dispose() {
      finish();
      listeners.abort();
    },
  };
}
