// The blurred copy saved with an image is a placeholder. Once the picture is there it has nothing
// left to do, and behind an uncropped image it would otherwise go on showing in the space either
// side of the work
/** @param {EventTarget | null} target */
function clearBlur(target) {
  // complete is also true for an image that failed, and then the blur is the better thing
  // to keep looking at
  if (!(target instanceof HTMLImageElement) || !target.complete || target.naturalWidth === 0) {
    return;
  }

  const box = target.parentElement;

  if (box instanceof HTMLElement && box.style.backgroundImage !== "") {
    box.style.backgroundImage = "";
  }
}

// load doesn't bubble, so it is caught on the way down (capture) instead. One listener covers
// every image on the page, including ones an enhanced navigation adds later
document.addEventListener("load", (event) => clearBlur(event.target), true);

// the images that finished before this ran, and the ones an enhanced navigation patched in place:
// a patched element is given the server's style attribute again, blur and all, and a picture it
// already holds raises no second load event
function clearLoadedBlurs() {
  document.querySelectorAll("img").forEach(clearBlur);
}

clearLoadedBlurs();
Blazor.addEventListener("enhancedload", clearLoadedBlurs);
