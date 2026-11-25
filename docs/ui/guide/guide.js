/*
 * Presenting a guide (#251): one step at a time, full screen, with the arrow
 * keys, the buttons or a swipe. Without this file a guide is an ordinary page.
 * `?present` opens it presenting, at the step the address names (#id) if any.
 */
(() => {
  const slides = [...document.querySelectorAll("[data-slide]")];
  const bar = document.querySelector(".bar");
  const present = document.querySelector("[data-present]");
  if (!slides.length || !bar || !present) return;
  const [previous, next, leave] = ["previous", "next", "leave"].map((name) => bar.querySelector(`[data-${name}]`));
  const count = bar.querySelector(".count");
  let at = 0;

  const presenting = () => document.body.classList.contains("presenting");

  function show(index) {
    at = Math.max(0, Math.min(slides.length - 1, index));
    slides.forEach((slide, i) => {
      slide.classList.toggle("current", i === at);
      // A step taller than a phone scrolls, so the keyboard must reach it.
      slide.tabIndex = i === at ? 0 : -1;
    });
    previous.disabled = at === 0;
    next.disabled = at === slides.length - 1;
    count.textContent = `${at + 1} of ${slides.length}`;
    history.replaceState(null, "", `?present#${slides[at].id}`);
    slides[at].scrollTop = 0;
    // Where a screen reader and the keyboard carry on from.
    slides[at].focus({ preventScroll: true });
  }

  function start(index) {
    document.body.classList.add("presenting");
    show(index);
  }

  function stop() {
    const here = slides[at];
    document.body.classList.remove("presenting");
    slides.forEach((slide) => {
      slide.classList.remove("current");
      slide.tabIndex = -1;
    });
    history.replaceState(null, "", `${location.pathname}#${here.id}`);
    if (document.fullscreenElement) document.exitFullscreen();
    here.scrollIntoView();
    present.focus({ preventScroll: true });
  }

  slides.forEach((slide) => (slide.tabIndex = -1));
  present.hidden = false;
  present.addEventListener("click", () => {
    // Allowed only from a press, and refused on some phones: presenting works without it.
    document.documentElement.requestFullscreen?.().catch(() => {});
    start(0);
  });
  previous.addEventListener("click", () => show(at - 1));
  next.addEventListener("click", () => show(at + 1));
  leave.addEventListener("click", stop);

  document.addEventListener("keydown", (event) => {
    if (!presenting() || event.altKey || event.ctrlKey || event.metaKey) return;
    // Space on a button presses it.
    if (event.key === " " && event.target instanceof HTMLButtonElement) return;
    const move = {
      ArrowRight: at + 1, PageDown: at + 1, " ": at + 1,
      ArrowLeft: at - 1, PageUp: at - 1,
      Home: 0, End: slides.length - 1,
    }[event.key];
    if (move !== undefined) {
      event.preventDefault();
      show(move);
    } else if (event.key === "Escape") {
      stop();
    }
  });

  // Leaving full screen with the browser's own Escape ends presenting too.
  document.addEventListener("fullscreenchange", () => {
    if (!document.fullscreenElement && presenting()) stop();
  });

  let touchX = null;
  document.addEventListener("touchstart", (event) => { touchX = event.changedTouches[0].clientX; }, { passive: true });
  document.addEventListener("touchend", (event) => {
    if (!presenting() || touchX === null) return;
    const moved = event.changedTouches[0].clientX - touchX;
    if (Math.abs(moved) > 60) show(at + (moved < 0 ? 1 : -1));
  }, { passive: true });

  if (new URLSearchParams(location.search).has("present")) {
    start(Math.max(0, slides.findIndex((slide) => `#${slide.id}` === location.hash)));
  }
})();
