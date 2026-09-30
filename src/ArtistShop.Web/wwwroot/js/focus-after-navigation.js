// Where the focus goes after an enhanced navigation. Blazor keeps an element the old and new pages
// share, such as a menu link or an artwork's Next, and the focus stays on it. When the focused
// element was removed instead, the focus has fallen to the page itself, and this moves it to the new
// page's h1, which a screen reader reads out as the page it has arrived on. Blazor's FocusOnNavigate
// would move it to the h1 after every navigation, even from a link that is still there

let hadFocus = false;

function isFocusOnSomething() {
  return document.activeElement !== null && document.activeElement !== document.body;
}

Blazor.addEventListener("enhancednavigationstart", () => {
  hadFocus = isFocusOnSomething();
});

Blazor.addEventListener("enhancednavigationend", () => {
  if (!hadFocus || isFocusOnSomething()) {
    return;
  }

  const heading = document.querySelector("h1");

  if (!(heading instanceof HTMLElement)) {
    return;
  }

  // tabindex -1 lets a script focus it without adding it to the Tab order, as FocusOnNavigate did.
  // preventScroll leaves the scrolling to Blazor and scroll-restoration.js
  if (!heading.hasAttribute("tabindex")) {
    heading.tabIndex = -1;
  }

  heading.focus({ preventScroll: true });
});
