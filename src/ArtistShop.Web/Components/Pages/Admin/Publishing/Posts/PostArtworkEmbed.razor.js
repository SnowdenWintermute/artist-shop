// The artwork embed: a block in a post showing one of an artwork's images. The Delta holds only
// what PostDocumentParser reads, and the public page looks up the title and image when it renders.
// Imported by PostBodyEditor.razor.js; nothing here runs until an editor calls it

// the name Quill stores it under, and the parser and set_post_artworks read
export const ARTWORK_EMBED = "artshop-artwork";

const EMBED_CLASS = "artshop-artwork";

/**
 * @typedef {object} ArtworkEmbedValue
 * @property {number} artworkId
 * @property {string} storageKey
 * @property {"small" | "medium"} size
 * @property {string} layout one of EmbedLayoutNames, which the toolbar's buttons carry
 */

/** @param {HTMLElement} node */
function readValue(node) {
  const { artworkId, storageKey, size, layout } = node.dataset;

  /** @type {ArtworkEmbedValue} */
  const value = {
    artworkId: Number(artworkId),
    storageKey: storageKey ?? "",
    size: size === "small" ? "small" : "medium",
    // the parser centres a layout it doesn't know
    layout: layout ?? "",
  };

  return value;
}

// Called once, when Quill has loaded. Every editor's toolbar carries the same image addresses, so
// the first one to connect is as good as any
/** @param {HTMLElement} toolbar */
export function registerArtworkEmbed(toolbar) {
  const { storageKeyPlaceholder, smallImageUrl, mediumImageUrl, layoutClasses } = toolbar.dataset;

  if (
    storageKeyPlaceholder === undefined ||
    smallImageUrl === undefined ||
    mediumImageUrl === undefined ||
    layoutClasses === undefined
  ) {
    throw new Error("The artwork embed toolbar is missing its image addresses or layout classes.");
  }

  // the post page's own classes for each layout, so the editor places an embed as the page will
  const classesByLayout = /** @type {Record<string, string>} */ (JSON.parse(layoutClasses));

  /** @param {ArtworkEmbedValue} value */
  const imageUrl = (value) =>
    (value.size === "small" ? smallImageUrl : mediumImageUrl).replace(storageKeyPlaceholder, value.storageKey);

  const BlockEmbed = Quill.import("blots/block/embed");

  // A block embed takes a line of its own. An inline one would end its line in a "\n", which the
  // parser reads as an empty paragraph after every embed
  class ArtworkEmbed extends BlockEmbed {
    static blotName = ARTWORK_EMBED;
    static tagName = "div";
    static className = EMBED_CLASS;

    /** @param {ArtworkEmbedValue} value */
    static create(value) {
      const node = /** @type {HTMLElement} */ (super.create(value));
      // otherwise the cursor can land inside the embed, where there is nothing to type into
      node.contentEditable = "false";
      node.dataset.artworkId = String(value.artworkId);
      node.dataset.storageKey = value.storageKey;
      node.dataset.size = value.size;
      node.dataset.layout = value.layout;
      node.classList.add(...(classesByLayout[value.layout] ?? "").split(" ").filter(Boolean));

      const image = document.createElement("img");
      image.src = imageUrl(value);
      image.alt = "";

      // both are made now, so a failed load only switches which one shows
      const missing = document.createElement("span");
      missing.textContent = "This image was removed.";
      missing.hidden = true;
      image.addEventListener(
        "error",
        () => {
          image.hidden = true;
          missing.hidden = false;
        },
        { once: true }
      );

      node.append(image, missing);
      return node;
    }

    /** @param {HTMLElement} node */
    static value(node) {
      return readValue(node);
    }
  }

  Quill.register(ArtworkEmbed);
}

/**
 * @param {Quill} quill
 * @param {ArtworkEmbedValue} value
 */
export function insertArtworkEmbed(quill, value) {
  const { index } = quill.getSelection(true);
  quill.insertEmbed(index, ARTWORK_EMBED, value, "user");
  quill.setSelection(index + 1, 0, "silent");
}

// the same module for every editor, so the first import is the one that loads it
/** @type {Promise<FloatingUi> | null} */
let floatingUiLoaded = null;

/** @param {string} src */
function loadFloatingUi(src) {
  floatingUiLoaded ??= import(src);
  return floatingUiLoaded;
}

// Opens the toolbar under an embed the artist clicks. A change replaces the embed with a new one
// holding the new value, as one edit, so a single undo takes it back
/**
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 * @param {AbortSignal} signal
 */
export function attachArtworkEmbedToolbar(quill, toolbar, signal) {
  const floatingUiSource = toolbar.dataset.floatingUiSrc;

  if (floatingUiSource === undefined) {
    return;
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
    stopFollowing?.();
    stopFollowing = null;
    const { computePosition, autoUpdate, offset, flip, shift } = await floatingUi;

    // closed, or moved to another embed, while Floating UI was loading
    if (embed !== node) {
      return;
    }

    stopFollowing = autoUpdate(node, toolbar, async () => {
      // fixed, because an open popover sits in the top layer, above the page's own layout
      const { x, y } = await computePosition(node, toolbar, {
        placement: "bottom",
        strategy: "fixed",
        middleware: [offset(8), flip(), shift({ padding: 8 })],
      });
      toolbar.style.left = `${x}px`;
      toolbar.style.top = `${y}px`;
    });
  }

  const alignmentButtons = [...toolbar.querySelectorAll("button[data-layout]")].filter(
    (button) => button instanceof HTMLButtonElement
  );
  const wrapButton = toolbar.querySelector('button[data-action="wrap"]');

  // the alignment button a layout belongs to, whether or not its text wraps
  /** @param {string} layout */
  function alignmentOf(layout) {
    return alignmentButtons.find(
      (button) => button.dataset.layout === layout || button.dataset.wrappedLayout === layout
    );
  }

  /** @param {string} layout */
  function isWrapped(layout) {
    return alignmentOf(layout)?.dataset.wrappedLayout === layout;
  }

  /** @param {ArtworkEmbedValue} value */
  function showPressed(value) {
    toolbar.querySelectorAll("button[data-size]").forEach((button) => {
      button.setAttribute("aria-pressed", String(button instanceof HTMLElement && button.dataset.size === value.size));
    });

    const alignment = alignmentOf(value.layout);
    alignmentButtons.forEach((button) => button.setAttribute("aria-pressed", String(button === alignment)));

    if (wrapButton instanceof HTMLButtonElement) {
      wrapButton.setAttribute("aria-pressed", String(isWrapped(value.layout)));
      // centred has nothing beside it to wrap
      wrapButton.disabled = alignment?.dataset.wrappedLayout === undefined;
    }
  }

  /** @param {HTMLElement} node */
  function open(node) {
    embed = node;
    showPressed(readValue(node));

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

  // where the open embed is in the document, or null if an edit has removed it
  function embedIndex() {
    const blot = embed?.isConnected ? Quill.find(embed) : null;
    return blot === null || blot instanceof Quill ? null : quill.getIndex(blot);
  }

  /** @param {Partial<ArtworkEmbedValue>} change */
  function update(change) {
    const index = embedIndex();

    if (embed === null || index === null) {
      close();
      return;
    }

    const value = { ...readValue(embed), ...change };
    // let go of the node being replaced first, or the text-change handler below would take its
    // removal for an edit that deleted the embed, and close the toolbar
    embed = null;
    quill.updateContents(new Delta().retain(index).delete(1).insert({ [ARTWORK_EMBED]: value }), "user");

    const [replacement] = quill.getLine(index);
    if (replacement?.domNode instanceof HTMLElement) {
      open(replacement.domNode);
    } else {
      close();
    }
  }

  function remove() {
    const index = embedIndex();

    if (index !== null) {
      quill.updateContents(new Delta().retain(index).delete(1), "user");
    }

    close();
  }

  quill.root.addEventListener(
    "click",
    (event) => {
      const clicked = event.target instanceof Element ? event.target.closest(`.${EMBED_CLASS}`) : null;

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

      if (button === null) {
        return;
      }

      const { size, layout, wrappedLayout, action } = button.dataset;
      const current = embed === null ? null : readValue(embed);

      if (current === null) {
        return;
      }

      if (action === "remove") {
        remove();
      } else if (action === "wrap") {
        const alignment = alignmentOf(current.layout)?.dataset;
        const toggled = isWrapped(current.layout) ? alignment?.layout : alignment?.wrappedLayout;

        if (toggled !== undefined) {
          update({ layout: toggled });
        }
      } else if (size === "small" || size === "medium") {
        update({ size });
      } else if (layout !== undefined) {
        // a new alignment keeps whether the text wraps, where it can
        update({ layout: isWrapped(current.layout) ? wrappedLayout ?? layout : layout });
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
