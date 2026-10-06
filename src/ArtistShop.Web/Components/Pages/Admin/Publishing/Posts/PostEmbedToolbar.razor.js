// The toolbar that opens under an embed the artist clicks, for any kind of embed. Each kind brings
// its own buttons and says what they do; this places the toolbar and handles Remove and Done.
// A change replaces the embed with a new one holding the new value, as one edit, so a single undo
// takes it back

// the same module for every toolbar, so the first import is the one that loads it
/** @type {Promise<FloatingUi> | null} */
let floatingUiLoaded = null;

// src is Assets' address, relative to the page's <base> ("lib/…"), which import() would read as a
// bare module name and refuse, so it's made a full address first
/** @param {string} src */
function loadFloatingUi(src) {
  floatingUiLoaded ??= import(new URL(src, document.baseURI).href);
  return floatingUiLoaded;
}

// Browser bars appearing and disappearing change the visible area by less than this; a keyboard
// covers more
const KEYBOARD_MIN_HEIGHT = 150;

// A phone's keyboard shrinks the visible area but not the layout viewport the page is laid out in.
// The visible area is scaled back up by the zoom, so a pinch-zoomed page isn't taken for a keyboard
function isKeyboardUp() {
  const viewport = window.visualViewport;
  return (
    viewport !== null &&
    document.documentElement.clientHeight - viewport.height * viewport.scale > KEYBOARD_MIN_HEIGHT
  );
}

// Resolves once a phone's keyboard has finished closing. A resize while it's still up, partway
// through closing or from the browser's bars, isn't the end. The timeout is in case the end never
// comes
function keyboardClosed() {
  return new Promise((resolve) => {
    const viewport = window.visualViewport;

    if (viewport === null) {
      resolve(undefined);
      return;
    }

    const timeout = setTimeout(done, 600);

    function onResize() {
      if (!isKeyboardUp()) {
        done();
      }
    }

    function done() {
      clearTimeout(timeout);
      viewport?.removeEventListener("resize", onResize);
      resolve(undefined);
    }

    viewport.addEventListener("resize", onResize);
  });
}

// Quill's own toolbar icons, in the buttons that name one as Quill's toolbar does: a format, and
// for some, a value, such as align and center
/** @param {HTMLElement} toolbar */
function showQuillIcons(toolbar) {
  const icons = Quill.import("ui/icons");

  for (const button of toolbar.querySelectorAll("button[data-quill-icon]")) {
    if (!(button instanceof HTMLButtonElement)) {
      continue;
    }

    const { quillIcon = "", quillIconValue = "" } = button.dataset;
    const icon = icons[quillIcon];
    const markup = typeof icon === "string" ? icon : icon?.[quillIconValue];

    if (markup === undefined) {
      throw new Error(`Quill has no ${quillIcon} icon for "${quillIconValue}".`);
    }

    button.innerHTML = markup;
  }
}

// one of the data- attributes the server renders onto a kind's toolbar, such as an image address
/**
 * @param {HTMLElement} toolbar
 * @param {string} name
 */
export function readToolbarSetting(toolbar, name) {
  const setting = toolbar.dataset[name];

  if (setting === undefined) {
    throw new Error(`The ${toolbar.dataset.part ?? "embed toolbar"} is missing its ${name}.`);
  }

  return setting;
}

// presses the button whose data-<key> is the open embed's, such as its layout, and no other
/**
 * @param {HTMLElement} toolbar
 * @param {"size" | "layout"} key
 * @param {string} value
 */
export function showPressed(toolbar, key, value) {
  toolbar.querySelectorAll(`button[data-${key}]`).forEach((button) => {
    button.setAttribute("aria-pressed", String(button instanceof HTMLElement && button.dataset[key] === value));
  });
}

/**
 * The open embed, as a kind's button handler sees it
 * @template V
 * @typedef {object} OpenEmbed
 * @property {V} value
 * @property {(change: Partial<V>) => void} update swaps in the changed embed and keeps the toolbar
 *   open on it
 * @property {(change: Partial<V>) => void} replace swaps in the changed embed when an answer comes
 *   later, such as from a dialog or an upload. The toolbar stays on it if it's still open there;
 *   otherwise it's left as it is. Does nothing if an edit has removed the embed since
 */

/**
 * @template V
 * @typedef {object} EmbedKind
 * @property {string} blotName
 * @property {string} className the class on every embed of this kind
 * @property {(node: HTMLElement) => V} readValue
 * @property {(value: V) => void} show shows the embed the toolbar is open for, such as which
 *   buttons match it
 * @property {(button: HTMLButtonElement, embed: OpenEmbed<V>) => void} onButton any button but
 *   Remove and Done
 * @property {(field: HTMLInputElement, embed: OpenEmbed<V>) => void} [onInput] typing in one of the
 *   kind's fields
 */

/**
 * @template V
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 * @param {EmbedKind<V>} kind
 * @param {import("/js/embed-lines.js").EmbedLines} lines
 * @param {AbortSignal} signal
 */
export function attachEmbedToolbar(quill, toolbar, kind, lines, signal) {
  const floatingUiSource = toolbar.dataset.floatingUiSrc;

  if (floatingUiSource === undefined) {
    throw new Error("The embed toolbar is missing its Floating UI address.");
  }

  // started now, so it has usually arrived before the first click
  const floatingUi = loadFloatingUi(floatingUiSource);
  const Delta = Quill.import("delta");
  showQuillIcons(toolbar);

  // the embed the toolbar is open for
  /** @type {HTMLElement | null} */
  let embed = null;
  /** @type {(() => void) | null} */
  let stopFollowing = null;
  // Placing waits for this. While a phone's keyboard is up, only the part of the screen above it
  // counts as visible, so a toolbar placed then can be flipped above the embed, then jump below it
  // once the keyboard has gone
  /** @type {Promise<unknown>} */
  let keyboardClosing = Promise.resolve();
  // whether the cursor was in the text when the toolbar opened, so closing it puts the cursor back
  let returnsToText = false;

  /** @param {HTMLElement} node */
  async function follow(node) {
    /** @type {FloatingUi} */
    let loaded;

    try {
      [loaded] = await Promise.all([floatingUi, keyboardClosing]);
    } catch (error) {
      // shown where the browser puts a popover, mid-screen, rather than not at all
      toolbar.style.visibility = "";
      throw error;
    }

    const { computePosition, autoUpdate, offset, flip, shift } = loaded;

    // closed, or moved to another embed, while Floating UI was loading
    if (embed !== node) {
      return;
    }

    // stopped only now, after the wait: two clicks while Floating UI loads both get this far, and
    // the second must stop the first's
    stopFollowing?.();
    stopFollowing = autoUpdate(node, toolbar, async () => {
      // fixed, because an open popover sits in the top layer, above the page's own layout
      const { x, y } = await computePosition(node, toolbar, {
        placement: "bottom",
        strategy: "fixed",
        middleware: [offset(8), flip(), shift({ padding: 8 })],
      });

      // a placing that began before the toolbar moved to another embed
      if (embed !== node) {
        return;
      }

      toolbar.style.left = `${x}px`;
      toolbar.style.top = `${y}px`;
      toolbar.style.visibility = "";
    });
  }

  /** @param {HTMLElement} node */
  function open(node) {
    embed = node;
    kind.show(kind.readValue(node));

    // hidden until it's placed under this embed; otherwise it shows for a moment where it last
    // was, under another embed or, the first time, mid-screen. Hidden still takes up its size,
    // which placing it needs
    toolbar.style.visibility = "hidden";
    stopFollowing?.();
    stopFollowing = null;

    if (!toolbar.matches(":popover-open")) {
      toolbar.showPopover();
    }

    follow(node);
  }

  function close() {
    if (toolbar.matches(":popover-open")) {
      toolbar.hidePopover();
    }
  }

  // Done, Escape, or the open embed clicked again. Quill puts the cursor back where it was. A click
  // elsewhere places the cursor itself, so it only closes
  function closeBackToText() {
    const wasOpen = toolbar.matches(":popover-open");
    close();

    if (wasOpen && returnsToText) {
      quill.focus();
    }
  }

  // where an embed is in the document, or null if an edit has removed it
  /** @param {HTMLElement | null} node */
  function indexOf(node) {
    const blot = node?.isConnected ? Quill.find(node) : null;
    return blot === null || blot instanceof Quill ? null : quill.getIndex(blot);
  }

  // swaps the embed for one holding the changed value, and returns the new node
  /**
   * @param {HTMLElement} node
   * @param {Partial<V>} change
   */
  function replace(node, change) {
    const index = indexOf(node);

    if (index === null) {
      return null;
    }

    const value = { ...kind.readValue(node), ...change };
    // let go of the node being replaced first, or the text-change handler below would take its
    // removal for an edit that deleted the embed, and close the toolbar
    if (embed === node) {
      embed = null;
    }
    quill.updateContents(new Delta().retain(index).delete(1).insert({ [kind.blotName]: value }), "user");

    const [replacement] = quill.getLine(index);
    return replacement?.domNode instanceof HTMLElement ? replacement.domNode : null;
  }

  /**
   * @param {HTMLElement} node
   * @param {Partial<V>} change
   */
  function update(node, change) {
    const replacement = replace(node, change);

    if (replacement !== null) {
      open(replacement);
    } else {
      close();
    }
  }

  /** @param {HTMLElement} node */
  function openEmbed(node) {
    /** @type {OpenEmbed<V>} */
    const opened = {
      value: kind.readValue(node),
      update: (change) => update(node, change),
      replace: (change) => {
        // the artist may have closed the toolbar or moved it while waiting, such as during an
        // upload. Still open on this embed, it moves to the replacement, or its buttons would do nothing
        const wasOpenOnIt = embed === node && toolbar.matches(":popover-open");
        const replacement = replace(node, change);

        if (wasOpenOnIt && replacement !== null) {
          open(replacement);
        }
      },
    };

    return opened;
  }

  // Text above or below: an empty line next to the embed, with the cursor in it. The toolbar
  // closes, since the artist is going on to type
  /**
   * @param {HTMLElement} node
   * @param {0 | 1} offset 0 for above, 1 for below
   */
  function addLine(node, offset) {
    const index = indexOf(node);
    close();

    if (index !== null) {
      lines.add(index + offset);
    }
  }

  /** @param {HTMLElement} node */
  function remove(node) {
    const index = indexOf(node);

    if (index !== null) {
      quill.updateContents(new Delta().retain(index).delete(1), "user");
    }

    close();
  }

  /** @param {Event} event */
  function embedAt(event) {
    const node = event.target instanceof Element ? event.target.closest(`.${kind.className}`) : null;
    return node instanceof HTMLElement ? node : null;
  }

  // a click on a modal dialog's backdrop lands on the dialog itself
  /** @param {Event} event */
  function isInDialog(event) {
    return event.target instanceof Element && event.target.closest("dialog") !== null;
  }

  // A tap on an embed would otherwise focus the editing area, and on a phone the keyboard then
  // covers half the screen, toolbar included
  quill.root.addEventListener(
    "mousedown",
    (event) => {
      if (embedAt(event) !== null) {
        event.preventDefault();
      }
    },
    { signal }
  );

  quill.root.addEventListener(
    "click",
    (event) => {
      const clicked = embedAt(event);

      if (clicked !== null && clicked === embed && toolbar.matches(":popover-open")) {
        closeBackToText();
      } else if (clicked !== null) {
        // moving the toolbar to another embed keeps where it came from
        if (!toolbar.matches(":popover-open")) {
          returnsToText = quill.hasFocus();
        }

        // a phone's keyboard, for the text or a caption field, would cover the toolbar
        if (isKeyboardUp()) {
          keyboardClosing = keyboardClosed();
          if (document.activeElement instanceof HTMLElement) {
            document.activeElement.blur();
          }
        } else {
          keyboardClosing = Promise.resolve();
        }

        open(clicked);
      }
    },
    { signal }
  );

  toolbar.addEventListener(
    "click",
    (event) => {
      const button = event.target instanceof Element ? event.target.closest("button") : null;
      const node = embed;

      if (button === null || node === null) {
        return;
      }

      const { action } = button.dataset;

      if (action === "done") {
        closeBackToText();
      } else if (action === "text-above") {
        addLine(node, 0);
      } else if (action === "text-below") {
        addLine(node, 1);
      } else if (action === "remove") {
        remove(node);
      } else {
        kind.onButton(button, openEmbed(node));
      }
    },
    { signal }
  );

  toolbar.addEventListener(
    "input",
    (event) => {
      if (event.target instanceof HTMLInputElement && embed !== null) {
        kind.onInput?.(event.target, openEmbed(embed));
      }
    },
    { signal }
  );

  // The toolbar is inside the post's form, so Enter in one of its fields would submit the post.
  // Here it means Done, unless it's confirming a character an input method is composing
  toolbar.addEventListener(
    "keydown",
    (event) => {
      if (event.key === "Enter" && !event.isComposing && event.target instanceof HTMLInputElement) {
        event.preventDefault();
        closeBackToText();
      }
    },
    { signal }
  );

  // A manual popover, because the browser would close one of its own on any click outside it, the
  // embed included, and the click handler above would open it again, flickering. So closing on a
  // click elsewhere and on Escape is done here. A click rather than a pointerdown, so scrolling the
  // page by touch leaves it open. Capture, so a handler that stops the click can't keep it open.
  // A click made by code, such as Replace image opening the file picker, isn't the artist's. A
  // dialog opened from the toolbar leaves it open, so clicks and Escape in a dialog are the dialog's
  document.addEventListener(
    "click",
    (event) => {
      const isOnToolbar = event.target instanceof Node && toolbar.contains(event.target);

      if (event.isTrusted && !isOnToolbar && !isInDialog(event) && embedAt(event) === null) {
        close();
      }
    },
    { signal, capture: true }
  );

  document.addEventListener(
    "keydown",
    (event) => {
      if (event.key === "Escape" && !isInDialog(event)) {
        closeBackToText();
      }
    },
    { signal }
  );

  // Closed by one of the handlers above. The event arrives a moment later and can be merged with
  // another, so it's the popover's state now that counts
  toolbar.addEventListener(
    "toggle",
    () => {
      if (!toolbar.matches(":popover-open")) {
        stopFollowing?.();
        stopFollowing = null;
        embed = null;
      }
    },
    { signal }
  );

  // an edit that deletes the open embed, such as selecting across it and typing
  quill.on("text-change", () => {
    if (embed !== null && !embed.isConnected) {
      close();
    }
  });
}
