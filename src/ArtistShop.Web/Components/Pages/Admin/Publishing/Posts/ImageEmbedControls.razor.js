// What the artwork embed and the uploaded image embed share: the caption, size and layout
// controls ImageEmbedControls renders into their toolbars, and the widths their sizes are shown at
import { readToolbarSetting } from "./PostEmbedToolbar.razor.js";

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
  const alignmentButtons = [...toolbar.querySelectorAll("button[data-layout]")].filter(
    (button) => button instanceof HTMLButtonElement
  );
  const wrapButton = toolbar.querySelector('button[data-action="wrap"]');
  const captionField = toolbar.querySelector('input[data-part="caption"]');

  if (!(captionField instanceof HTMLInputElement) || !(wrapButton instanceof HTMLButtonElement)) {
    throw new Error("The image embed toolbar is missing its caption field or Wrap text.");
  }

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

  return {
    /** @param {ImageEmbedLook} value */
    show(value) {
      // only when it differs: setting a field's value moves the cursor to its end, which would
      // happen on every keystroke as the embed is replaced under it
      if (captionField.value !== (value.caption ?? "")) {
        captionField.value = value.caption ?? "";
      }

      toolbar.querySelectorAll("button[data-size]").forEach((button) => {
        button.setAttribute("aria-pressed", String(button instanceof HTMLElement && button.dataset.size === value.size));
      });

      const alignment = alignmentOf(value.layout);
      alignmentButtons.forEach((button) => button.setAttribute("aria-pressed", String(button === alignment)));

      wrapButton.setAttribute("aria-pressed", String(isWrapped(value.layout)));
      // centred has nothing beside it to wrap
      wrapButton.disabled = alignment?.dataset.wrappedLayout === undefined;
    },

    /**
     * Whether the button was one of the controls'
     * @param {HTMLButtonElement} button
     * @param {ImageEmbedLook} current
     * @param {(change: Partial<ImageEmbedLook>) => void} update the open embed's
     */
    onButton(button, current, update) {
      const { size, layout, wrappedLayout, action } = button.dataset;

      if (action === "wrap") {
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
