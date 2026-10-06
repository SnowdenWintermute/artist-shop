// A press that becomes a gesture, followed from start to end, the same for a mouse, a pen and a
// finger. A finger holds still first, so a swipe still scrolls the page and a tap is still a tap;
// a mouse or pen moves a little with its button down, so a click stays a click. Once the gesture
// has started, the page doesn't scroll under it and the click its release would make is swallowed
import {
  gestureHoldMilliseconds,
  gestureHoldTolerancePixels,
  gestureMouseDragPixels,
} from "/js/app-consts.js";

/** @typedef {{ x: number, y: number }} GesturePoint where the pointer is, as clientX and clientY */

/**
 * @template T
 * @typedef {object} Gesture
 * @property {(event: PointerEvent) => T | null} pick what a press here would move, or null for no
 *   gesture
 * @property {(subject: T, point: GesturePoint) => void} onStart
 * @property {(point: GesturePoint) => void} onMove
 * @property {(point: GesturePoint) => void} onEnd
 * @property {() => void} onCancel the browser took the press over, such as for a second finger
 */

/**
 * @template T
 * @param {HTMLElement} element
 * @param {Gesture<T>} gesture
 * @param {AbortSignal} signal
 */
export function listenForGesture(element, gesture, signal) {
  /**
   * A press that may become a gesture, then the gesture
   * @type {{
   *   pointerId: number,
   *   subject: T,
   *   start: GesturePoint,
   *   isTouch: boolean,
   *   isActive: boolean,
   *   holdTimer: ReturnType<typeof setTimeout> | undefined,
   * } | null}
   */
  let press = null;

  /** @param {PointerEvent} event */
  const pointOf = (event) => ({ x: event.clientX, y: event.clientY });

  function stop() {
    clearTimeout(press?.holdTimer);
    press = null;
  }

  function begin() {
    if (press === null) {
      return;
    }

    press.isActive = true;
    // so a mouse released outside the window still ends it
    element.setPointerCapture(press.pointerId);
    gesture.onStart(press.subject, press.start);
  }

  function cancel() {
    const wasActive = press?.isActive ?? false;
    stop();

    if (wasActive) {
      gesture.onCancel();
    }
  }

  // The click that follows a gesture's release, which would otherwise land on what was pressed.
  // None comes after a finger has moved, so it's let go of soon after, before a click of the
  // artist's own could come
  function swallowNextClick() {
    /** @param {MouseEvent} event */
    const swallow = (event) => {
      event.preventDefault();
      event.stopPropagation();
    };

    window.addEventListener("click", swallow, { capture: true, once: true });
    setTimeout(() => window.removeEventListener("click", swallow, { capture: true }), 300);
  }

  element.addEventListener(
    "pointerdown",
    (event) => {
      // another finger joining in is a pinch, not this gesture
      if (press !== null) {
        cancel();
        return;
      }

      if (!event.isPrimary || event.button !== 0) {
        return;
      }

      const subject = gesture.pick(event);

      if (subject === null) {
        return;
      }

      const isTouch = event.pointerType === "touch";

      press = {
        pointerId: event.pointerId,
        subject,
        start: pointOf(event),
        isTouch,
        isActive: false,
        holdTimer: isTouch ? setTimeout(begin, gestureHoldMilliseconds) : undefined,
      };
    },
    { signal }
  );

  // On the window, as a mouse dragged off the element still belongs to the gesture
  window.addEventListener(
    "pointermove",
    (event) => {
      if (press === null || event.pointerId !== press.pointerId) {
        return;
      }

      const point = pointOf(event);

      if (press.isActive) {
        gesture.onMove(point);
        return;
      }

      const distance = Math.hypot(point.x - press.start.x, point.y - press.start.y);

      if (press.isTouch && distance > gestureHoldTolerancePixels) {
        // a finger that moves before the hold is up is scrolling
        stop();
      } else if (!press.isTouch && distance > gestureMouseDragPixels) {
        begin();
        gesture.onMove(point);
      }
    },
    { signal }
  );

  window.addEventListener(
    "pointerup",
    (event) => {
      if (press === null || event.pointerId !== press.pointerId) {
        return;
      }

      const wasActive = press.isActive;
      stop();

      if (wasActive) {
        swallowNextClick();
        gesture.onEnd(pointOf(event));
      }
    },
    { signal }
  );

  window.addEventListener(
    "pointercancel",
    (event) => {
      if (press !== null && event.pointerId === press.pointerId) {
        cancel();
      }
    },
    { signal }
  );

  window.addEventListener(
    "keydown",
    (event) => {
      if (event.key === "Escape" && press?.isActive) {
        cancel();
      }
    },
    { signal }
  );

  // A phone only lets a page stop it scrolling from a touchmove listener that isn't passive, so
  // this is on the element from the start: one added once the gesture begins can come too late
  element.addEventListener(
    "touchmove",
    (event) => {
      if (press?.isActive) {
        event.preventDefault();
      }
    },
    { signal, passive: false }
  );

  // A long press would otherwise open the browser's menu, as for an image, on Android
  element.addEventListener(
    "contextmenu",
    (event) => {
      if (press?.isTouch) {
        event.preventDefault();
      }
    },
    { signal }
  );

  // the browser's own dragging of an image, which would take the mouse away from the gesture
  element.addEventListener(
    "dragstart",
    (event) => {
      if (press !== null) {
        event.preventDefault();
      }
    },
    { signal }
  );
}
