// EmbedFigure.razor's figure, built in the post editor so an image embed looks there as it will on
// the page. Imported by the editor's image embeds; the page itself needs no script

/**
 * The image at the size's width, so a caption wraps under it, with a note in its place if the file
 * is gone
 * @param {object} figureParts
 * @param {string} figureParts.src
 * @param {string} figureParts.alt
 * @param {number} figureParts.width the size's width in pixels
 * @param {string | undefined} figureParts.caption
 * @param {string} figureParts.captionClass
 */
export function createEmbedFigure({ src, alt, width, caption, captionClass }) {
  const figure = document.createElement("figure");
  figure.style.width = `${width}px`;
  figure.style.maxWidth = "100%";

  const image = document.createElement("img");
  image.src = src;
  image.alt = alt;

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

  figure.append(image, missing);

  if (caption !== undefined && caption.trim() !== "") {
    const figcaption = document.createElement("figcaption");
    figcaption.className = captionClass;
    figcaption.textContent = caption;
    figure.append(figcaption);
  }

  return figure;
}
