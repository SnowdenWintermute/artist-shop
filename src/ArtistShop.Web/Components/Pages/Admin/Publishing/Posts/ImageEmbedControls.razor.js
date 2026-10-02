// What the artwork embed and the uploaded image embed share: the caption, size and layout
// controls ImageEmbedControls renders into their toolbars, and the widths their sizes are shown at
import { readToolbarSetting, showPressed } from "./PostEmbedToolbar.razor.js";

/**
 * The keys every image embed's value has, whatever else its kind holds
 * @typedef {object} ImageEmbedLook
 * @property {"small" | "medium"} size
 * @property {string} layout one of EmbedLayoutNames, which the toolbar's buttons carry
 * @property {string} [caption] as typed; the parser trims it, and counts a blank one as none
 */

/**
 * The width each size is shown at, from the toolbar's data-small-width and data-medium-width
 * @param {HTMLElement} toolbar
 */
export function readEmbedWidths(toolbar) {
  const small = Number(readToolbarSetting(toolbar, "smallWidth"));
  const medium = Number(readToolbarSetting(toolbar, "mediumWidth"));

  return {
    small,
    medium,
    /** @param {ImageEmbedLook["size"]} size */
    of: (size) => (size === "small" ? small : medium),
  };
}

/**
 * The controls' part of a toolbar kind: showing an embed's look, and changing it
 * @param {HTMLElement} toolbar
 */
export function attachImageEmbedControls(toolbar) {
  const captionField = toolbar.querySelector('input[data-part="caption"]');

  if (!(captionField instanceof HTMLInputElement)) {
    throw new Error("The image embed toolbar is missing its caption field.");
  }

  return {
    /** @param {ImageEmbedLook} value */
    show(value) {
      // only when it differs: setting a field's value moves the cursor to its end, which would
      // happen on every keystroke as the embed is replaced under it
      if (captionField.value !== (value.caption ?? "")) {
        captionField.value = value.caption ?? "";
      }

      showPressed(toolbar, "size", value.size);
      showPressed(toolbar, "layout", value.layout);
    },

    /**
     * Whether the button was one of the controls'
     * @param {HTMLButtonElement} button
     * @param {(change: Partial<ImageEmbedLook>) => void} update the open embed's
     */
    onButton(button, update) {
      const { size, layout } = button.dataset;

      if (size === "small" || size === "medium") {
        update({ size });
      } else if (layout !== undefined) {
        update({ layout });
      } else {
        return false;
      }

      return true;
    },

    /**
     * Applied as it's typed, so no way of closing the toolbar can lose it. An emptied field leaves
     * no caption key behind. Whether the field was the caption
     * @param {HTMLInputElement} field
     * @param {(change: Partial<ImageEmbedLook>) => void} update the open embed's
     */
    onInput(field, update) {
      if (field !== captionField) {
        return false;
      }

      update({ caption: field.value === "" ? undefined : field.value });
      return true;
    },
  };
}
