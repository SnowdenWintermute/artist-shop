// EmbedFigure.razor's figure, built in the editor so an image embed looks there as it will on
// the page. Imported by the editor's image embeds; the page itself needs no script

/**
 * The image at the size's width, so a caption wraps under it, with a note in its place if the file
 * is gone. A file not made yet, just after an upload, falls back to fallbackSrc, the smallest size,
 * which an upload makes before it answers, with ProcessingLabel.razor's label over it
 * @param {object} figureParts
 * @param {string} figureParts.src
 * @param {string | undefined} figureParts.fallbackSrc
 * @param {string} figureParts.alt
 * @param {number} figureParts.width the size's width in pixels
 * @param {string | undefined} figureParts.caption
 * @param {string} figureParts.captionClass
 */
export function createEmbedFigure({ src, fallbackSrc, alt, width, caption, captionClass }) {
  const figure = document.createElement("figure");
  figure.style.width = `${width}px`;
  figure.style.maxWidth = "100%";

  const box = document.createElement("span");
  box.className = "relative block";

  const image = document.createElement("img");
  image.src = src;
  image.alt = alt;

  // the same classes and words as ProcessingLabel.razor
  const processing = document.createElement("span");
  processing.className = "absolute top-0 left-0 bg-black/60 px-0.5 text-[10px] leading-tight text-white";
  processing.title = "Recently uploaded. Refresh in a minute for a sharper picture.";
  processing.textContent = "Processing…";
  processing.hidden = true;

  // all made now, so a failed load only switches which one shows
  const missing = document.createElement("span");
  missing.textContent = "This image was removed.";
  missing.hidden = true;

  let isFallingBack = false;
  image.addEventListener("error", () => {
    if (fallbackSrc !== undefined && fallbackSrc !== src && !isFallingBack) {
      isFallingBack = true;
      processing.hidden = false;
      image.src = fallbackSrc;
      return;
    }

    image.hidden = true;
    processing.hidden = true;
    missing.hidden = false;
  });

  box.append(image, processing);
  figure.append(box, missing);

  if (caption !== undefined && caption.trim() !== "") {
    const figcaption = document.createElement("figcaption");
    figcaption.className = captionClass;
    figcaption.textContent = caption;
    figure.append(figcaption);
  }

  return figure;
}
