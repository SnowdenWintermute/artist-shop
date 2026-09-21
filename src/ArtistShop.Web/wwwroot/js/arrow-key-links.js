// Left and right follow whichever links the page marked, so the keys are a shortcut for something
// already on screen rather than the only way to get there
document.addEventListener("keydown", (event) => {
  if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") {
    return;
  }

  // a held modifier is the browser's own shortcut, usually going back or forward
  if (event.metaKey || event.ctrlKey || event.altKey || event.shiftKey) {
    return;
  }

  // a dialog keeps its own keys while it is open, and inside a field the arrows belong to the text
  if (document.querySelector("dialog[open]")) {
    return;
  }

  const from = event.target;

  if (
    from instanceof HTMLElement &&
    (from.isContentEditable || from.closest("input, textarea, select"))
  ) {
    return;
  }

  const which = event.key === "ArrowLeft" ? "previous" : "next";
  const link = document.querySelector(`[data-arrow-key-link="${which}"]`);

  // clicked rather than followed, so enhanced navigation patches the page the way it does for
  // any other internal link
  if (link instanceof HTMLAnchorElement) {
    link.click();
  }
});
