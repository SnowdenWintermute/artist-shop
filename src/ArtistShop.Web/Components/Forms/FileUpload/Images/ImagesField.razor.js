/**
 * @param {string} name
 * @param {string} value
 */
export function fillInputIfEmpty(name, value) {
  const input = document.querySelector(`input[name="${name}"]`);

  if (!(input instanceof HTMLInputElement) || input.value !== "") {
    return;
  }

  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
}
