// The uploaded image embed: an image the artist put into the post itself, rather than an artwork's.
// The file goes through the same upload pipeline as an artwork's, and the Delta holds everything
// the page needs to show it, since no table does.
// While a new image uploads, a placeholder stands where it will go. The placeholder comes and
// goes as "api" changes, which the editor's history (userOnly) never records, so the only undo
// step is the finished image. It's never saved either: the editor leaves placeholders out of
// what it writes to the form. Imported by PostBodyEditor.razor.js; nothing here runs until an
// editor calls it
import { sendUpload, uploadErrorMessage } from "/js/upload-request.js";
import { createEmbedFigure } from "../../../../Publishing/EmbedFigure.razor.js";
import { attachImageEmbedControls, readEmbedWidths } from "./ImageEmbedControls.razor.js";
import { attachEmbedToolbar, readToolbarSetting } from "./PostEmbedToolbar.razor.js";

// the name Quill stores it under, and the parser and get_all_post_image_storage_keys read
export const IMAGE_EMBED = "artshop-image";

const EMBED_CLASS = "artshop-image";

// never saved, so not a name the parser knows
export const UPLOAD_PLACEHOLDER = "artshop-image-upload";

/**
 * Which placeholder, found by its id when the upload has news
 * @typedef {object} UploadPlaceholderValue
 * @property {string} uploadId
 * @property {string} fileName
 */

// for the editor, which writes the Delta to the form without them
/** @param {QuillOperation} op */
export function isUploadPlaceholder(op) {
  return typeof op.insert === "object" && UPLOAD_PLACEHOLDER in op.insert;
}

/**
 * width and height are the upload's own, which may be narrower than the size. blur is the tiny
 * copy the page shows while the image loads
 * @typedef {import("./ImageEmbedControls.razor.js").ImageEmbedLook & {
 *   storageKey: string,
 *   width: number,
 *   height: number,
 *   blur?: string,
 *   alt: string,
 *   lightbox?: true,
 * }} PostImageEmbedValue
 */

/**
 * What the upload endpoint answers with, its ImageUploadResult
 * @typedef {object} UploadedImage
 * @property {string} storageKey
 * @property {string} originalFileName
 * @property {number} width
 * @property {number} height
 * @property {string} blurDataUri
 */

/** @param {HTMLElement} node */
function readValue(node) {
  const { storageKey, width, height, blur, size, layout, caption, alt, lightbox } = node.dataset;

  /** @type {PostImageEmbedValue} */
  const value = {
    storageKey: storageKey ?? "",
    width: Number(width),
    height: Number(height),
    ...(blur === undefined ? {} : { blur }),
    size: size === "small" ? "small" : "medium",
    // the parser centres a layout it doesn't know
    layout: layout ?? "",
    ...(caption === undefined ? {} : { caption }),
    alt: alt ?? "",
    ...(lightbox === "true" ? { lightbox: true } : {}),
  };

  return value;
}

// Called once, when Quill has loaded. Every editor's toolbar carries the same image address, so
// the first one to connect is as good as any
/** @param {HTMLElement} toolbar */
export function registerPostImageEmbed(toolbar) {
  const variantUrl = readToolbarSetting(toolbar, "variantUrl");
  const storageKeyPlaceholder = readToolbarSetting(toolbar, "storageKeyPlaceholder");
  const widthPlaceholder = readToolbarSetting(toolbar, "widthPlaceholder");
  const widths = readEmbedWidths(toolbar);
  const captionClass = readToolbarSetting(toolbar, "captionClass");
  const newLayout = readToolbarSetting(toolbar, "newLayout");
  // the post page's own classes for each layout, so the editor places an embed as the page will
  const classesByLayout = /** @type {Record<string, string>} */ (
    JSON.parse(readToolbarSetting(toolbar, "layoutClasses"))
  );

  // The file the page shows: the narrower of the size and the image itself, which
  // ImageVariants.WidthsFor makes sure exists. The page stretches a narrower one to the size
  /** @param {PostImageEmbedValue} value */
  const imageUrl = (value) =>
    variantUrl
      .replace(storageKeyPlaceholder, value.storageKey)
      .replace(widthPlaceholder, String(Math.min(widths.of(value.size), value.width)));

  const BlockEmbed = Quill.import("blots/block/embed");

  // A block embed takes a line of its own. An inline one would end its line in a "\n", which the
  // parser reads as an empty paragraph after every embed
  class PostImageEmbed extends BlockEmbed {
    static blotName = IMAGE_EMBED;
    static tagName = "div";
    static className = EMBED_CLASS;

    /** @param {PostImageEmbedValue} value */
    static create(value) {
      const node = /** @type {HTMLElement} */ (super.create(value));
      // otherwise the cursor can land inside the embed, where there is nothing to type into
      node.contentEditable = "false";
      node.dataset.storageKey = value.storageKey;
      node.dataset.width = String(value.width);
      node.dataset.height = String(value.height);
      if (value.blur !== undefined) {
        node.dataset.blur = value.blur;
      }
      node.dataset.size = value.size;
      node.dataset.layout = value.layout;
      if (value.caption !== undefined) {
        node.dataset.caption = value.caption;
      }
      node.dataset.alt = value.alt;
      if (value.lightbox === true) {
        node.dataset.lightbox = "true";
      }
      node.classList.add(...(classesByLayout[value.layout] ?? "").split(" ").filter(Boolean));

      node.append(
        createEmbedFigure({
          src: imageUrl(value),
          alt: value.alt,
          width: widths.of(value.size),
          caption: value.caption,
          captionClass,
        })
      );
      return node;
    }

    /** @param {HTMLElement} node */
    static value(node) {
      return readValue(node);
    }
  }

  // A box a new image's size, where the image will go, saying how its upload is doing. Only its
  // message changes as it does, which Quill doesn't count as an edit
  class UploadPlaceholder extends BlockEmbed {
    static blotName = UPLOAD_PLACEHOLDER;
    static tagName = "div";
    static className = UPLOAD_PLACEHOLDER;

    /** @param {UploadPlaceholderValue} value */
    static create(value) {
      const node = /** @type {HTMLElement} */ (super.create(value));
      node.contentEditable = "false";
      node.dataset.uploadId = value.uploadId;
      node.dataset.fileName = value.fileName;
      node.classList.add(...(classesByLayout[newLayout] ?? "").split(" ").filter(Boolean));

      // the new image's width, and 4:3 until the file's own shape is known
      const box = document.createElement("div");
      box.dataset.part = "box";
      box.style.width = `${widths.medium}px`;

      const message = document.createElement("p");
      message.dataset.part = "message";
      message.textContent = `Waiting to upload ${value.fileName}…`;

      box.append(message);
      node.append(box);
      return node;
    }

    /** @param {HTMLElement} node */
    static value(node) {
      /** @type {UploadPlaceholderValue} */
      const value = { uploadId: node.dataset.uploadId ?? "", fileName: node.dataset.fileName ?? "" };
      return value;
    }
  }

  Quill.register(PostImageEmbed);
  Quill.register(UploadPlaceholder);
}

/**
 * Sends one file, reporting how much has gone. Rejects with a message for the artist
 * @param {string} url
 * @param {File} file
 * @param {(percent: number) => void} onProgress
 * @returns {Promise<UploadedImage>}
 */
async function uploadImage(url, file, onProgress) {
  const response = await sendUpload({
    url,
    file,
    onProgress: (loaded, total) => onProgress(Math.round((loaded / total) * 100)),
  }).finished;

  // nothing here aborts an upload, so there is always a response
  if (response === null) {
    throw new Error("An image upload was aborted, though nothing in the editor aborts one.");
  }

  if (response.status !== 200) {
    throw new Error(uploadErrorMessage(response));
  }

  return JSON.parse(response.text);
}

// The picture's size, read in the browser, or null for a file it can't show, such as most
// browsers with a TIFF. Loading it only as far as its size is known, and the browser turns it
// upright from its orientation tag as the server does
/**
 * @param {File} file
 * @returns {Promise<{ width: number, height: number } | null>}
 */
function readImageSize(file) {
  const url = URL.createObjectURL(file);
  const image = new Image();

  return new Promise((resolve) => {
    image.addEventListener("load", () => resolve({ width: image.naturalWidth, height: image.naturalHeight }), {
      once: true,
    });
    image.addEventListener("error", () => resolve(null), { once: true });
    image.src = url;
  }).finally(() => URL.revokeObjectURL(url));
}

/**
 * Where an upload says how it's going: a placeholder in the text, or the toolbar's status line
 * @typedef {object} UploadReport
 * @property {(percent: number) => void} progress
 * @property {() => void} done
 * @property {(message: string) => void} fail
 */

// the file types the server takes, which Quill's drop and paste handling filters by
/** @param {HTMLElement} toolbar */
export function acceptedImageTypes(toolbar) {
  return readToolbarSetting(toolbar, "accept").split(",");
}

/**
 * Uploads the editor's images: from the Image button, a drop, a paste, or Replace image
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 */
export function createImageUploads(quill, toolbar) {
  const uploadUrl = readToolbarSetting(toolbar, "uploadUrl");
  const newLayout = readToolbarSetting(toolbar, "newLayout");
  const Delta = Quill.import("delta");

  /** @param {string} uploadId */
  function findPlaceholder(uploadId) {
    const node = quill.root.querySelector(`[data-upload-id="${CSS.escape(uploadId)}"]`);
    return node instanceof HTMLElement ? node : null;
  }

  // where a placeholder is in the document, or null if the artist has deleted it
  /** @param {string} uploadId */
  function placeholderIndex(uploadId) {
    const node = findPlaceholder(uploadId);
    const blot = node === null ? null : Quill.find(node);
    return blot === null || blot instanceof Quill ? null : quill.getIndex(blot);
  }

  /** @param {string} uploadId */
  function removePlaceholder(uploadId) {
    const index = placeholderIndex(uploadId);

    if (index !== null) {
      quill.updateContents(new Delta().retain(index).delete(1), "api");
    }
  }

  // the placeholder's own report, found afresh each time, since the artist may delete it
  /**
   * @param {string} uploadId
   * @returns {UploadReport}
   */
  function placeholderReport(uploadId) {
    /** @param {string} text */
    const say = (text) => {
      const message = findPlaceholder(uploadId)?.querySelector('[data-part="message"]');

      if (message instanceof HTMLElement) {
        message.textContent = text;
      }

      return message instanceof HTMLElement ? message : null;
    };
    const fileName = findPlaceholder(uploadId)?.dataset.fileName ?? "";

    return {
      progress: (percent) => say(`Uploading ${fileName}… ${percent}%`),
      // the image takes its place
      done: () => {},
      fail(message) {
        const shown = say(`Couldn't upload ${fileName}: ${message}`);

        if (shown !== null) {
          shown.classList.add("text-red-400");

          const dismiss = document.createElement("button");
          dismiss.type = "button";
          dismiss.className = "block cursor-pointer underline mx-auto";
          dismiss.textContent = "Dismiss";
          dismiss.addEventListener("click", () => removePlaceholder(uploadId), { once: true });
          shown.after(dismiss);
        }
      },
    };
  }

  /**
   * @param {File} file
   * @param {UploadReport} report
   * @returns {Promise<UploadedImage | null>} null when it failed, which the report shows
   */
  async function upload(file, report) {
    try {
      const uploaded = await uploadImage(uploadUrl, file, report.progress);
      report.done();
      return uploaded;
    } catch (error) {
      report.fail(error instanceof Error ? error.message : String(error));
      return null;
    }
  }

  return {
    // A placeholder for each file goes in at index at once, then the files upload one at a time,
    // each image taking its placeholder's place. Deleting a placeholder drops its image
    /**
     * @param {number} index
     * @param {File[]} files
     */
    async insert(index, files) {
      const placeholders = files.map((file) => ({ file, uploadId: crypto.randomUUID() }));

      quill.updateContents(
        placeholders.reduce(
          (delta, { file, uploadId }) => delta.insert({ [UPLOAD_PLACEHOLDER]: { uploadId, fileName: file.name } }),
          new Delta().retain(index)
        ),
        "api"
      );

      // each box takes its image's shape as soon as that's known, while the uploads go in turn
      for (const { file, uploadId } of placeholders) {
        readImageSize(file).then((size) => {
          const box = findPlaceholder(uploadId)?.querySelector('[data-part="box"]');

          if (size !== null && size.width > 0 && size.height > 0 && box instanceof HTMLElement) {
            box.style.aspectRatio = `${size.width} / ${size.height}`;
          }
        });
      }

      for (const { file, uploadId } of placeholders) {
        if (placeholderIndex(uploadId) === null) {
          continue;
        }

        const uploaded = await upload(file, placeholderReport(uploadId));
        const at = placeholderIndex(uploadId);

        if (uploaded === null || at === null) {
          continue;
        }

        // two changes, so the history records only the image
        quill.updateContents(new Delta().retain(at).delete(1), "api");
        quill.insertEmbed(
          at,
          IMAGE_EMBED,
          {
            storageKey: uploaded.storageKey,
            width: uploaded.width,
            height: uploaded.height,
            blur: uploaded.blurDataUri,
            size: "medium",
            layout: newLayout,
            // the file name without its extension, until the artist writes something better
            alt: uploaded.originalFileName.replace(/\.[^.]*$/, ""),
          },
          "user"
        );
      }
    },

    upload,
  };
}

// The hidden file input both the Image button and Replace image open, which says what was picked
// to whichever opened it last
/** @param {HTMLInputElement} fileInput */
export function createImagePicker(fileInput) {
  /** @type {((files: File[]) => void) | null} */
  let onPicked = null;

  fileInput.addEventListener("change", () => {
    const files = [...(fileInput.files ?? [])];
    // emptied, so picking the same file again is still a change
    fileInput.value = "";

    if (files.length > 0) {
      onPicked?.(files);
    }
  });

  return {
    /**
     * @param {{ multiple: boolean }} options
     * @param {(files: File[]) => void} handlePicked
     */
    open({ multiple }, handlePicked) {
      onPicked = handlePicked;
      fileInput.multiple = multiple;
      fileInput.click();
    },
  };
}

/** @typedef {ReturnType<typeof createImageUploads>} ImageUploads */
/** @typedef {ReturnType<typeof createImagePicker>} ImagePicker */

// The toolbar's Image button. The cursor's place is kept now, since the file picker takes the focus
/**
 * @param {Quill} quill
 * @param {ImagePicker} picker
 * @param {ImageUploads} uploads
 */
export function addPostImageEmbed(quill, picker, uploads) {
  const { index } = quill.getSelection(true);
  picker.open({ multiple: true }, (files) => uploads.insert(index, files));
}

// The toolbar for an uploaded image: its alt text, the controls it shares with the artwork embed,
// and Replace image, which keeps everything but the picture itself
/**
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 * @param {ImagePicker} picker
 * @param {ImageUploads} uploads
 * @param {AbortSignal} signal
 */
export function attachPostImageEmbedToolbar(quill, toolbar, picker, uploads, signal) {
  const controls = attachImageEmbedControls(toolbar);
  const altField = toolbar.querySelector('input[data-part="alt"]');
  const altHelpButton = toolbar.querySelector('button[data-action="alt-help"]');
  const altHelp = document.getElementById(altHelpButton?.getAttribute("aria-controls") ?? "");
  const uploadStatus = toolbar.querySelector('[data-part="upload-status"]');
  const lightboxBox = toolbar.querySelector('input[data-part="lightbox"]');
  const lightboxNote = toolbar.querySelector('[data-part="lightbox-note"]');

  if (
    !(altField instanceof HTMLInputElement) ||
    !(altHelpButton instanceof HTMLButtonElement) ||
    altHelp === null ||
    !(uploadStatus instanceof HTMLElement) ||
    !(lightboxBox instanceof HTMLInputElement) ||
    !(lightboxNote instanceof HTMLElement)
  ) {
    throw new Error("The image embed toolbar is missing its alt text field, its help, its upload status or its lightbox choice.");
  }

  const widths = readEmbedWidths(toolbar);
  const variantWidths = readToolbarSetting(toolbar, "variantWidths").split(",").map(Number);

  // ImageVariants.LargestWidthFor, which the page checks too: an image narrower than the medium
  // size has a copy at its own width, and any other the widest standard one it reaches
  /** @param {number} imageWidth */
  const largestWidthFor = (imageWidth) =>
    imageWidth < widths.medium ? imageWidth : Math.max(...variantWidths.filter((width) => width <= imageWidth));

  /** @param {PostImageEmbedValue} value */
  const hasWiderVersion = (value) => largestWidthFor(value.width) > widths.of(value.size);

  // the image a replacement upload is for, so opening the toolbar on another one hides its news
  /** @type {string | null} */
  let uploadStatusFor = null;

  /**
   * @param {string} storageKey
   * @param {string} fileName
   * @returns {UploadReport}
   */
  const toolbarReport = (storageKey, fileName) => {
    uploadStatusFor = storageKey;
    uploadStatus.classList.remove("text-red-400");
    uploadStatus.textContent = `Uploading ${fileName}…`;
    uploadStatus.hidden = false;

    return {
      progress(percent) {
        uploadStatus.textContent = `Uploading ${fileName}… ${percent}%`;
      },
      done() {
        uploadStatus.hidden = true;
        uploadStatusFor = null;
      },
      fail(message) {
        uploadStatus.textContent = `Couldn't upload ${fileName}: ${message}`;
        uploadStatus.classList.add("text-red-400");
      },
    };
  };

  attachEmbedToolbar(
    quill,
    toolbar,
    {
      blotName: IMAGE_EMBED,
      className: EMBED_CLASS,
      readValue,

      show(value) {
        controls.show(value);
        uploadStatus.hidden = uploadStatusFor !== value.storageKey;

        // only when it differs, as with the caption, or the cursor would jump on every keystroke
        if (altField.value !== value.alt) {
          altField.value = value.alt;
        }

        // Kept in the embed when a new size or image leaves nothing wider, and shown as off, as the
        // page treats it
        const canOpenLightbox = hasWiderVersion(value);
        lightboxBox.disabled = !canOpenLightbox;
        lightboxBox.checked = canOpenLightbox && value.lightbox === true;
        lightboxNote.hidden = canOpenLightbox;
      },

      onButton(button, embed) {
        if (controls.onButton(button, embed.value, embed.update)) {
          return;
        }

        const { action } = button.dataset;

        if (action === "alt-help") {
          altHelp.hidden = !altHelp.hidden;
          altHelpButton.setAttribute("aria-expanded", String(!altHelp.hidden));
        } else if (action === "replace-image") {
          picker.open({ multiple: false }, async ([file]) => {
            const uploaded = await uploads.upload(file, toolbarReport(embed.value.storageKey, file.name));

            if (uploaded !== null) {
              const { storageKey, width, height, blurDataUri } = uploaded;
              embed.replace({ storageKey, width, height, blur: blurDataUri });
            }
          });
        }
      },

      // Applied as it's typed, like the caption. An emptied field stays empty rather than going
      // back to the file name: no alt text is the right answer for a purely decorative image
      onInput(field, embed) {
        if (field === altField) {
          embed.update({ alt: field.value });
        } else if (field === lightboxBox) {
          embed.update({ lightbox: field.checked ? true : undefined });
        } else {
          controls.onInput(field, embed.update);
        }
      },
    },
    signal
  );
}
