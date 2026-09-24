// The artwork embed: a block in a post showing one of an artwork's images. The Delta holds only
// what PostDocumentParser reads, and the public page looks up the title and image when it renders.
// Imported by PostBodyEditor.razor.js; nothing here runs until an editor calls it
import { createEmbedFigure } from "../../../../Publishing/EmbedFigure.razor.js";
import { attachImageEmbedControls, readEmbedWidths } from "./ImageEmbedControls.razor.js";
import { attachEmbedToolbar, readToolbarSetting } from "./PostEmbedToolbar.razor.js";

// the name Quill stores it under, and the parser and set_post_artworks read
export const ARTWORK_EMBED = "artshop-artwork";

const EMBED_CLASS = "artshop-artwork";

/**
 * @typedef {import("./ImageEmbedControls.razor.js").ImageEmbedLook & {
 *   artworkId: number,
 *   storageKey: string,
 * }} ArtworkEmbedValue
 */

/** @param {HTMLElement} node */
function readValue(node) {
  const { artworkId, storageKey, size, layout, caption } = node.dataset;

  /** @type {ArtworkEmbedValue} */
  const value = {
    artworkId: Number(artworkId),
    storageKey: storageKey ?? "",
    size: size === "small" ? "small" : "medium",
    // the parser centres a layout it doesn't know
    layout: layout ?? "",
    ...(caption === undefined ? {} : { caption }),
  };

  return value;
}

// Called once, when Quill has loaded. Every editor's toolbar carries the same image addresses, so
// the first one to connect is as good as any
/** @param {HTMLElement} toolbar */
export function registerArtworkEmbed(toolbar) {
  const storageKeyPlaceholder = readToolbarSetting(toolbar, "storageKeyPlaceholder");
  const smallImageUrl = readToolbarSetting(toolbar, "smallImageUrl");
  const mediumImageUrl = readToolbarSetting(toolbar, "mediumImageUrl");
  const widths = readEmbedWidths(toolbar);
  const captionClass = readToolbarSetting(toolbar, "captionClass");
  // the post page's own classes for each layout, so the editor places an embed as the page will
  const classesByLayout = /** @type {Record<string, string>} */ (
    JSON.parse(readToolbarSetting(toolbar, "layoutClasses"))
  );

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
      if (value.caption !== undefined) {
        node.dataset.caption = value.caption;
      }
      node.classList.add(...(classesByLayout[value.layout] ?? "").split(" ").filter(Boolean));

      const figure = createEmbedFigure({
        src: imageUrl(value),
        alt: "",
        width: widths.of(value.size),
        caption: value.caption,
        captionClass,
      });

      node.append(figure);
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
 * @param {number} index
 */
function insertArtworkEmbed(quill, value, index) {
  quill.insertEmbed(index, ARTWORK_EMBED, value, "user");
  quill.setSelection(index + 1, 0, "user");
}

// marks the picker's messages, since any page in any frame can post to this window
const ARTWORK_PICKER_MESSAGE = "artshop-artwork-picker";

/**
 * What the artist picked. Size and layout come only when adding: changing an embed's image keeps
 * the size and layout it has
 * @typedef {object} ArtworkPickerChoice
 * @property {number} artworkId
 * @property {string} storageKey
 * @property {"small" | "medium"} [size]
 * @property {string} [layout]
 */

/** @typedef {{ action: "close" } | { action: "choose", choice: ArtworkPickerChoice }} ArtworkPickerMessage */

/**
 * @typedef {object} ArtworkPicker
 * @property {string} addUrl the picker's first page for adding an embed
 * @property {(artworkId: number) => string} changeImageUrl its page of an artwork's images, for
 *   changing an embed's image
 * @property {(url: string, onChosen: (choice: ArtworkPickerChoice) => void) => void} open
 */

// Called by the picker's pages inside the frame. A picker page opened on its own, outside a frame,
// has no editor to tell
/** @param {ArtworkPickerMessage} message */
export function sendToArtworkPickerOwner(message) {
  if (window.parent !== window) {
    window.parent.postMessage({ type: ARTWORK_PICKER_MESSAGE, ...message }, location.origin);
  }
}

// Wires up the dialog holding the picker's frame. Its addresses come from the server, with a
// placeholder where an artwork's id goes
/**
 * @param {HTMLDialogElement} dialog
 * @param {AbortSignal} signal
 * @returns {ArtworkPicker}
 */
export function attachArtworkPicker(dialog, signal) {
  const frame = dialog.querySelector("iframe");
  const { addSrc, changeImageSrc, artworkIdPlaceholder } = frame?.dataset ?? {};

  if (frame === null || addSrc === undefined || changeImageSrc === undefined || artworkIdPlaceholder === undefined) {
    throw new Error("The artwork picker is missing its frame or the frame's addresses.");
  }

  // what the current opening does with the choice
  /** @type {((choice: ArtworkPickerChoice) => void) | null} */
  let onChosen = null;

  window.addEventListener(
    "message",
    (event) => {
      // only this editor's own frame, showing a page of this site
      if (event.origin !== location.origin || event.source !== frame.contentWindow) {
        return;
      }

      /** @type {(ArtworkPickerMessage & { type: unknown }) | null} */
      const message = event.data;

      if (message?.type !== ARTWORK_PICKER_MESSAGE) {
        return;
      }

      const chosen = onChosen;
      // closed first, so the focus it hands back doesn't land after the cursor is placed
      dialog.close();

      if (message.action === "choose") {
        chosen?.(message.choice);
      }
    },
    { signal }
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
    changeImageUrl: (artworkId) => changeImageSrc.replace(artworkIdPlaceholder, String(artworkId)),
    open(url, handleChoice) {
      onChosen = handleChoice;
      frame.src = url;
      dialog.showModal();
    },
  };
}

// The toolbar's Artwork button. The cursor's place is kept now, since the dialog takes the focus
/**
 * @param {Quill} quill
 * @param {ArtworkPicker} picker
 */
export function addArtworkEmbed(quill, picker) {
  const { index } = quill.getSelection(true);

  picker.open(picker.addUrl, ({ artworkId, storageKey, size, layout }) => {
    // the adding steps always send both
    if (size !== undefined && layout !== undefined) {
      insertArtworkEmbed(quill, { artworkId, storageKey, size, layout }, index);
    }
  });
}

// The toolbar's caption field and buttons for an artwork embed. Opening the picker for Change
// image closes the toolbar, and the embed keeps its size and layout
/**
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 * @param {ArtworkPicker} picker
 * @param {AbortSignal} signal
 */
export function attachArtworkEmbedToolbar(quill, toolbar, picker, signal) {
  const controls = attachImageEmbedControls(toolbar);

  attachEmbedToolbar(
    quill,
    toolbar,
    {
      blotName: ARTWORK_EMBED,
      className: EMBED_CLASS,
      readValue,
      show: controls.show,

      onButton(button, embed) {
        if (controls.onButton(button, embed.value, embed.update)) {
          return;
        }

        if (button.dataset.action === "change-image") {
          picker.open(picker.changeImageUrl(embed.value.artworkId), ({ artworkId, storageKey }) => {
            embed.replace({ artworkId, storageKey });
          });
        }
      },

      onInput(field, embed) {
        controls.onInput(field, embed.update);
      },
    },
    signal
  );
}
