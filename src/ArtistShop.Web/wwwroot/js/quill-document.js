// Finding things in a Quill editor's document, shared by the post editor's scripts

// where a node's blot is in the document, or null if the node isn't one of Quill's
/**
 * @param {Quill} quill
 * @param {Node} node
 */
export function indexOfNode(quill, node) {
  const blot = Quill.find(node);
  return blot === null || blot instanceof Quill ? null : quill.getIndex(blot);
}

// whether a line of the editing area is an embed
/** @param {Element} line */
export function isEmbedLine(line) {
  return Quill.find(line) instanceof Quill.import("blots/block/embed");
}

/**
 * @param {Quill} quill
 * @param {number} index
 */
export function isEmbedAt(quill, index) {
  if (index < 0) {
    return false;
  }

  const [op] = quill.getContents(index, 1).ops;
  return typeof op?.insert === "object";
}
