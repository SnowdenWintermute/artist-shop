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
 * @param {AbortSignal} signal
 */
export function attachEmbedToolbar(quill, toolbar, kind, signal) {
  const floatingUiSource = toolbar.dataset.floatingUiSrc;

  if (floatingUiSource === undefined) {
    throw new Error("The embed toolbar is missing its Floating UI address.");
  }

  // started now, so it has usually arrived before the first click
  const floatingUi = loadFloatingUi(floatingUiSource);
  const Delta = Quill.import("delta");

  // the embed the toolbar is open for
  /** @type {HTMLElement | null} */
  let embed = null;
  /** @type {(() => void) | null} */
  let stopFollowing = null;

  /** @param {HTMLElement} node */
  async function follow(node) {
    /** @type {FloatingUi} */
    let loaded;

    try {
      loaded = await floatingUi;
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
        // a dialog closes the toolbar, but the file picker doesn't, and an open toolbar that
        // had let go of its embed would have buttons that did nothing
        const wasOpenOnIt = embed === node && toolbar.matches(":popover-open");
        const replacement = replace(node, change);

        if (wasOpenOnIt && replacement !== null) {
          open(replacement);
        }
      },
    };

    return opened;
  }

  /** @param {HTMLElement} node */
  function remove(node) {
    const index = indexOf(node);

    if (index !== null) {
      quill.updateContents(new Delta().retain(index).delete(1), "user");
    }

    close();
  }

  quill.root.addEventListener(
    "click",
    (event) => {
      const clicked = event.target instanceof Element ? event.target.closest(`.${kind.className}`) : null;

      if (clicked instanceof HTMLElement) {
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
        close();
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
        close();
      }
    },
    { signal }
  );

  // Closed by a click elsewhere, Escape, or one of the handlers above. The event arrives a moment
  // later and can be merged with another, so it's the popover's state now that counts: a click on
  // a second embed closes the toolbar and opens it again before this runs
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
