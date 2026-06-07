'use strict';

// ─── Defaults matching configManager.js ────────────────────────────────────
const DEFAULTS = {
  version: 'classic',
  effect: '',
  font: '',
  numColumns: null,
  bloomSize: null,
  bloomStrength: null,
  animationSpeed: null,
  fallSpeed: null,
  cycleSpeed: null,
  raindropLength: null,
  ditherMagnitude: null,
  resolution: 1.0,
  fps: 60,
  cursorIntensity: null,
  glintIntensity: null,
  density: null,
  forwardSpeed: null,
  slant: 0,
  skipIntro: true,
  glyphFlip: false,
  volumetric: null,
  loops: false,
  backgroundColor: '#000000',
  cursorColor: '#00ff41',
  glintColor: '#ffffff',
  stripeColors: '',
  palette: '',
  imageUrl: '',
};

// ─── Section → field mapping (for per-section resets) ──────────────────────
const SECTION_FIELDS = {
  version:   ['version', 'effect', 'font'],
  display:   ['numColumns', 'resolution', 'fps', 'density'],
  animation: ['animationSpeed', 'fallSpeed', 'cycleSpeed', 'raindropLength',
              'forwardSpeed', 'slant'],
  bloom:     ['bloomSize', 'bloomStrength', 'ditherMagnitude'],
  colors:    ['backgroundColor', 'cursorColor', 'glintColor'],
  intensity: ['cursorIntensity', 'glintIntensity'],
  options:   ['skipIntro', 'glyphFlip', 'volumetric', 'loops'],
  custom:    ['stripeColors', 'palette', 'imageUrl'],
};

// ─── State ──────────────────────────────────────────────────────────────────
let currentConfig = { ...DEFAULTS };
let previewDebounce = null;

// ─── DOM helpers ────────────────────────────────────────────────────────────
const el = (id) => document.getElementById(id);

// Slider display format per field
const SLIDER_FORMAT = {
  slant:         (v) => `${v}°`,
  fps:           (v) => `${v} fps`,
  resolution:    (v) => `${parseFloat(v).toFixed(2)}×`,
  numColumns:    (v) => `${Math.round(v)}`,
  density:       (v) => `${parseFloat(v).toFixed(1)}`,
  cycleSpeed:    (v) => `${parseFloat(v).toFixed(3)}`,
  ditherMagnitude:(v)=> `${parseFloat(v).toFixed(3)}`,
};

function sliderFmt(id, v) {
  return (SLIDER_FORMAT[id] || ((x) => parseFloat(x).toFixed(2)))(v);
}

// ─── Load config into form ──────────────────────────────────────────────────
function populateForm(cfg) {
  // Selects
  ['version', 'effect', 'font'].forEach((id) => {
    const elem = el(id);
    if (elem) elem.value = cfg[id] || '';
  });

  // Sliders
  const sliderIds = [
    'numColumns', 'resolution', 'fps', 'density',
    'animationSpeed', 'fallSpeed', 'cycleSpeed', 'raindropLength',
    'forwardSpeed', 'slant',
    'bloomSize', 'bloomStrength', 'ditherMagnitude',
    'cursorIntensity', 'glintIntensity',
  ];

  sliderIds.forEach((id) => {
    const slider = el(id);
    const valEl  = el(`${id}-val`);
    if (!slider) return;

    const val = cfg[id];
    const isNullable = slider.hasAttribute('data-nullable');

    if (val == null && isNullable) {
      // Show mid-point as visual placeholder; null => omit from URL
      slider.value = (parseFloat(slider.min) + parseFloat(slider.max)) / 2;
      if (valEl) valEl.textContent = 'auto';
    } else {
      slider.value = val ?? slider.getAttribute('data-default') ?? slider.min;
      if (valEl) valEl.textContent = sliderFmt(id, slider.value);
    }
    // Track whether slider is in "auto" state
    slider.dataset.isAuto = (val == null && isNullable) ? '1' : '0';
  });

  // Toggles
  ['skipIntro', 'glyphFlip', 'loops'].forEach((id) => {
    const cb = el(id);
    if (cb) cb.checked = !!cfg[id];
  });
  // volumetric may be null (use version default) or boolean
  const vol = el('volumetric');
  if (vol) vol.checked = !!cfg.volumetric;

  // Colors
  ['backgroundColor', 'cursorColor', 'glintColor'].forEach((id) => {
    const picker = el(id);
    const hexInp = el(`${id}-hex`);
    const hex = cfg[id] || DEFAULTS[id];
    if (picker) picker.value = hex;
    if (hexInp) hexInp.value = hex;
  });

  // Text inputs
  ['stripeColors', 'palette', 'imageUrl'].forEach((id) => {
    const inp = el(id);
    if (inp) inp.value = cfg[id] || '';
  });
}

// ─── Read form into config object ───────────────────────────────────────────
function readForm() {
  const cfg = {};

  // Selects
  ['version', 'effect', 'font'].forEach((id) => {
    const elem = el(id);
    cfg[id] = elem ? elem.value : DEFAULTS[id];
  });

  // Sliders
  const sliderIds = [
    'numColumns', 'resolution', 'fps', 'density',
    'animationSpeed', 'fallSpeed', 'cycleSpeed', 'raindropLength',
    'forwardSpeed', 'slant',
    'bloomSize', 'bloomStrength', 'ditherMagnitude',
    'cursorIntensity', 'glintIntensity',
  ];

  sliderIds.forEach((id) => {
    const slider = el(id);
    if (!slider) { cfg[id] = DEFAULTS[id]; return; }

    const isNullable = slider.hasAttribute('data-nullable');
    const isAuto = slider.dataset.isAuto === '1';

    if (isNullable && isAuto) {
      cfg[id] = null;
    } else {
      const v = parseFloat(slider.value);
      cfg[id] = isNaN(v) ? DEFAULTS[id] : v;
    }
  });

  // Toggles
  ['skipIntro', 'glyphFlip', 'volumetric', 'loops'].forEach((id) => {
    const cb = el(id);
    cfg[id] = cb ? cb.checked : !!DEFAULTS[id];
  });
  // volumetric null when unchecked means "use version default"
  if (!cfg.volumetric) cfg.volumetric = null;

  // Colors
  ['backgroundColor', 'cursorColor', 'glintColor'].forEach((id) => {
    const hexInp = el(`${id}-hex`);
    cfg[id] = hexInp ? hexInp.value : DEFAULTS[id];
  });

  // Text inputs
  ['stripeColors', 'palette', 'imageUrl'].forEach((id) => {
    const inp = el(id);
    cfg[id] = inp ? inp.value.trim() : '';
  });

  return cfg;
}

// ─── Preview refresh ────────────────────────────────────────────────────────
async function refreshPreview() {
  clearTimeout(previewDebounce);
  const cfg = readForm();
  currentConfig = cfg;

  try {
    const url = await window.configAPI.getMatrixURL(cfg);
    // Append skipIntro and lower fps for the live preview
    const previewURL = new URL(url);
    previewURL.searchParams.set('skipIntro', 'true');
    previewURL.searchParams.set('suppressWarnings', 'true');
    if (!cfg.fps || cfg.fps > 30) previewURL.searchParams.set('fps', '30');

    const iframe = el('preview-iframe');
    const overlay = el('preview-overlay');
    const urlBar  = el('url-bar');

    iframe.src = previewURL.toString();
    urlBar.textContent = previewURL.toString();
    urlBar.title = previewURL.toString();

    overlay.style.opacity = '0';
    iframe.onload = () => { overlay.style.opacity = '0'; };
  } catch (e) {
    console.error('Preview failed:', e);
  }
}

function schedulePreview() {
  clearTimeout(previewDebounce);
  previewDebounce = setTimeout(refreshPreview, 700);
}

// ─── Save ───────────────────────────────────────────────────────────────────
async function saveSettings() {
  const cfg = readForm();
  const result = await window.configAPI.saveConfig(cfg);

  const toast = el('toast');
  toast.textContent = result && result.ok !== false ? '✓ Settings saved' : '✗ Save failed';
  toast.classList.add('show');
  setTimeout(() => toast.classList.remove('show'), 2000);

  // Refresh preview with saved config
  currentConfig = cfg;
  refreshPreview();
}

// ─── Reset helpers ──────────────────────────────────────────────────────────
function resetSection(sectionKey, event) {
  if (event) event.stopPropagation();
  const fields = SECTION_FIELDS[sectionKey] || [];
  const partial = {};
  fields.forEach((f) => { partial[f] = DEFAULTS[f]; });
  const merged = { ...currentConfig, ...partial };
  populateForm(merged);
  currentConfig = merged;
  schedulePreview();
}

function resetAll() {
  populateForm(DEFAULTS);
  currentConfig = { ...DEFAULTS };
  schedulePreview();
}

// ─── Section collapse ────────────────────────────────────────────────────────
function toggleSection(id) {
  el(id).classList.toggle('collapsed');
}

// ─── Wire up all inputs ──────────────────────────────────────────────────────
function wireSlider(id) {
  const slider = el(id);
  const valEl  = el(`${id}-val`);
  if (!slider) return;

  const isNullable = slider.hasAttribute('data-nullable');

  // Double-click to toggle "auto" on nullable sliders
  if (isNullable) {
    slider.addEventListener('dblclick', () => {
      const wasAuto = slider.dataset.isAuto === '1';
      slider.dataset.isAuto = wasAuto ? '0' : '1';
      if (slider.dataset.isAuto === '1' && valEl) valEl.textContent = 'auto';
      else if (valEl) valEl.textContent = sliderFmt(id, slider.value);
      schedulePreview();
    });
  }

  slider.addEventListener('input', () => {
    // Once user manually drags, clear auto state
    if (isNullable) slider.dataset.isAuto = '0';
    if (valEl) valEl.textContent = sliderFmt(id, slider.value);
    schedulePreview();
  });
}

function wireColor(id) {
  const picker = el(id);
  const hexInp = el(`${id}-hex`);
  if (!picker || !hexInp) return;

  picker.addEventListener('input', () => {
    hexInp.value = picker.value.toUpperCase();
    schedulePreview();
  });

  hexInp.addEventListener('input', () => {
    const v = hexInp.value.trim();
    if (/^#[0-9a-fA-F]{6}$/.test(v)) {
      picker.value = v;
      schedulePreview();
    }
  });

  hexInp.addEventListener('blur', () => {
    // Normalize on blur
    let v = hexInp.value.trim();
    if (!v.startsWith('#')) v = '#' + v;
    if (/^#[0-9a-fA-F]{6}$/.test(v)) {
      hexInp.value = v.toUpperCase();
      picker.value = v;
    } else {
      hexInp.value = picker.value.toUpperCase(); // revert
    }
  });
}

function wireInputs() {
  // Selects
  ['version', 'effect', 'font'].forEach((id) => {
    const elem = el(id);
    if (elem) elem.addEventListener('change', schedulePreview);
  });

  // Sliders
  [
    'numColumns', 'resolution', 'fps', 'density',
    'animationSpeed', 'fallSpeed', 'cycleSpeed', 'raindropLength',
    'forwardSpeed', 'slant',
    'bloomSize', 'bloomStrength', 'ditherMagnitude',
    'cursorIntensity', 'glintIntensity',
  ].forEach(wireSlider);

  // Toggles
  ['skipIntro', 'glyphFlip', 'volumetric', 'loops'].forEach((id) => {
    const cb = el(id);
    if (cb) cb.addEventListener('change', schedulePreview);
  });

  // Colors
  ['backgroundColor', 'cursorColor', 'glintColor'].forEach(wireColor);

  // Text inputs (debounced)
  ['stripeColors', 'palette', 'imageUrl'].forEach((id) => {
    const inp = el(id);
    if (inp) inp.addEventListener('input', schedulePreview);
  });
}

// ─── Init ────────────────────────────────────────────────────────────────────
async function init() {
  try {
    const cfg = await window.configAPI.getConfig();
    currentConfig = { ...DEFAULTS, ...cfg };
    populateForm(currentConfig);
    wireInputs();
    // Initial preview after a short delay to let the window settle
    setTimeout(refreshPreview, 400);
  } catch (e) {
    console.error('Failed to load config:', e);
    populateForm(DEFAULTS);
    wireInputs();
    setTimeout(refreshPreview, 400);
  }
}

// Expose to HTML onclick handlers
window.toggleSection  = toggleSection;
window.resetSection   = resetSection;
window.resetAll       = resetAll;
window.saveSettings   = saveSettings;
window.refreshPreview = refreshPreview;

document.addEventListener('DOMContentLoaded', init);
