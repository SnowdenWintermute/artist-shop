// The video embed: a YouTube or Vimeo player on a line of its own. The Delta holds only which video
// it is and where it sits, and the public page builds the player's address from those.
// Imported by PostBodyEditor.razor.js; nothing here runs until an editor calls it
import { attachEmbedToolbar, readToolbarSetting } from "./PostEmbedToolbar.razor.js";

// the name Quill stores it under, and the parser reads
export const VIDEO_EMBED = "artshop-video";

const EMBED_CLASS = "artshop-video";

/**
 * Which video, as the server's VideoParts names it
 * @typedef {object} VideoSource
 * @property {"youtube" | "vimeo"} provider
 * @property {string} videoId
 * @property {string} [hash] the second part of an unlisted Vimeo video's link
 */

/**
 * @typedef {VideoSource & { layout: string }} VideoEmbedValue layout is one of EmbedLayoutNames,
 *   which the toolbar's buttons carry
 */

/** @param {HTMLElement} node */
function readValue(node) {
  const { provider, videoId, hash, layout } = node.dataset;

  /** @type {VideoEmbedValue} */
  const value = {
    provider: provider === "vimeo" ? "vimeo" : "youtube",
    videoId: videoId ?? "",
    ...(hash === undefined ? {} : { hash }),
    // the parser centres a layout it doesn't know
    layout: layout ?? "",
  };

  return value;
}

// A video's addresses, from the templates VideoUrls renders onto the toolbar: its player's, and its
// own page's, which is also a link the address dialog reads back
/** @param {HTMLElement} toolbar */
function videoAddresses(toolbar) {
  const videoIdPlaceholder = readToolbarSetting(toolbar, "videoIdPlaceholder");
  const hashPlaceholder = readToolbarSetting(toolbar, "hashPlaceholder");

  /**
   * @param {string} youtube
   * @param {string} vimeo
   * @param {string} unlistedVimeo
   */
  function addressFrom(youtube, vimeo, unlistedVimeo) {
    /** @param {VideoSource} source */
    return (source) => {
      const template =
        source.provider === "youtube"
          ? youtube
          : source.hash === undefined
            ? vimeo
            : unlistedVimeo.replace(hashPlaceholder, source.hash);

      return template.replace(videoIdPlaceholder, source.videoId);
    };
  }

  return {
    player: addressFrom(
      readToolbarSetting(toolbar, "youtubePlayerUrl"),
      readToolbarSetting(toolbar, "vimeoPlayerUrl"),
      readToolbarSetting(toolbar, "unlistedVimeoPlayerUrl")
    ),
    page: addressFrom(
      readToolbarSetting(toolbar, "youtubePageUrl"),
      readToolbarSetting(toolbar, "vimeoPageUrl"),
      readToolbarSetting(toolbar, "unlistedVimeoPageUrl")
    ),
  };
}

// Called once, when Quill has loaded. Every editor's toolbar carries the same addresses, so the
// first one to connect is as good as any
/** @param {HTMLElement} toolbar */
export function registerVideoEmbed(toolbar) {
  const addresses = videoAddresses(toolbar);

  // the post page's own classes for each layout, so the editor places a video as the page will
  const classesByLayout = /** @type {Record<string, string>} */ (JSON.parse(readToolbarSetting(toolbar, "layoutClasses")));
  const playerClassesByLayout = /** @type {Record<string, string>} */ (
    JSON.parse(readToolbarSetting(toolbar, "playerClasses"))
  );
  const playerStyle = readToolbarSetting(toolbar, "playerStyle");

  /** @param {string | undefined} classes */
  const classList = (classes) => (classes ?? "").split(" ").filter(Boolean);

  const BlockEmbed = Quill.import("blots/block/embed");

  // A block embed takes a line of its own. An inline one would end its line in a "\n", which the
  // parser reads as an empty paragraph after every embed
  class VideoEmbed extends BlockEmbed {
    static blotName = VIDEO_EMBED;
    static tagName = "div";
    static className = EMBED_CLASS;

    /** @param {VideoEmbedValue} value */
    static create(value) {
      const node = /** @type {HTMLElement} */ (super.create(value));
      // otherwise the cursor can land inside the embed, where there is nothing to type into
      node.contentEditable = "false";
      node.dataset.provider = value.provider;
      node.dataset.videoId = value.videoId;
      if (value.hash !== undefined) {
        node.dataset.hash = value.hash;
      }
      node.dataset.layout = value.layout;
      node.classList.add(...classList(classesByLayout[value.layout]));

      // the frame is sized as the page sizes the player, and the player fills it
      const frame = document.createElement("div");
      frame.classList.add(...classList(playerClassesByLayout[value.layout]));
      frame.setAttribute("style", playerStyle);

      const player = document.createElement("iframe");
      player.src = addresses.player(value);
      player.title = value.provider === "youtube" ? "YouTube video" : "Vimeo video";
      player.referrerPolicy = "strict-origin-when-cross-origin";

      // over the player, which would otherwise take every click, so a click opens the toolbar
      const cover = document.createElement("div");
      cover.dataset.part = "cover";

      frame.append(player, cover);
      node.append(frame);
      return node;
    }

    /** @param {HTMLElement} node */
    static value(node) {
      return readValue(node);
    }
  }

  Quill.register(VideoEmbed);
}

/**
 * Asks for a video's address; VideoAddressDialog.razor.js is what does. It starts with the address
 * given, empty for a new video
 * @typedef {object} VideoAddressAsker
 * @property {(address: string, onChosen: (source: VideoSource) => void) => void} open
 */

// The toolbar's Video button. The cursor's place is kept now, since the dialog takes the focus.
// A new video fills the column, the layout the toolbar names for it
/**
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 * @param {VideoAddressAsker} dialog
 */
export function addVideoEmbed(quill, toolbar, dialog) {
  const { index } = quill.getSelection(true);
  const layout = readToolbarSetting(toolbar, "newLayout");

  dialog.open("", (source) => {
    quill.insertEmbed(index, VIDEO_EMBED, { ...source, layout }, "user");
    quill.setSelection(index + 1, 0, "user");
  });
}

// The toolbar for a video embed: the video's link, Change video, and its layout. Changing the video
// keeps its layout. The dialog is asked for each time, since it lives outside the editor
/**
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 * @param {() => VideoAddressAsker} dialog
 * @param {AbortSignal} signal
 */
export function attachVideoEmbedToolbar(quill, toolbar, dialog, signal) {
  const addresses = videoAddresses(toolbar);
  const link = toolbar.querySelector('a[data-part="video-link"]');

  if (!(link instanceof HTMLAnchorElement)) {
    throw new Error("The video embed toolbar is missing its link.");
  }

  attachEmbedToolbar(
    quill,
    toolbar,
    {
      blotName: VIDEO_EMBED,
      className: EMBED_CLASS,
      readValue,

      show(value) {
        link.href = addresses.page(value);
        link.textContent = link.href;

        toolbar.querySelectorAll("button[data-layout]").forEach((button) => {
          button.setAttribute(
            "aria-pressed",
            String(button instanceof HTMLElement && button.dataset.layout === value.layout)
          );
        });
      },

      onButton(button, embed) {
        const { layout, action } = button.dataset;

        if (action === "change-video") {
          // hash written out even when the new video has none, or an unlisted video's would stay
          dialog().open(addresses.page(embed.value), ({ provider, videoId, hash }) => {
            embed.replace({ provider, videoId, hash });
          });
        } else if (layout !== undefined) {
          embed.update({ layout });
        }
      },
    },
    signal
  );
}
