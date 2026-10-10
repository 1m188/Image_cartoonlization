import './style.css';
import { CartoonEngine } from './engine';
import { DEFAULT_PARAMS, type CartoonParams } from './pipeline/pipeline';

/** 预览图长边上限（像素），保证滑条交互流畅 */
const PREVIEW_MAX_EDGE = 1100;
/**
 * 加载时允许的最大像素数，超过则按比例缩小。
 * 16.7M 是老版 iOS Safari 的单 canvas 面积上限，以此封顶保移动端可用。
 */
const MAX_LOAD_PIXELS = 16_700_000;
/** 预览防抖间隔 ms */
const PREVIEW_DEBOUNCE = 250;

// ---------- DOM ----------

const $ = <T extends HTMLElement>(id: string): T => {
  const el = document.getElementById(id);
  if (!el) throw new Error(`缺少元素 #${id}`);
  return el as T;
};

const stage = $('stage');
const canvasBox = $('canvasBox');
const beforeCanvas = $<HTMLCanvasElement>('beforeCanvas');
const afterCanvas = $<HTMLCanvasElement>('afterCanvas');
const divider = $('divider');
const emptyState = $('emptyState');
const progressRow = $('progressRow');
const progressBar = $('progressBar');
const progressText = $('progressText');
const controlsRoot = $('controls');
const resetBtn = $<HTMLButtonElement>('resetBtn');
const exportBtn = $<HTMLButtonElement>('exportBtn');
const exportScale = $<HTMLSelectElement>('exportScale');
const exportFormat = $<HTMLSelectElement>('exportFormat');
const imageInfo = $('imageInfo');
const replaceBtn = $<HTMLButtonElement>('replaceBtn');
const pickBtn = $<HTMLButtonElement>('pickBtn');
const sampleBtn = $<HTMLButtonElement>('sampleBtn');
const fileInput = $<HTMLInputElement>('fileInput');
const toast = $('toast');

const beforeCtx = beforeCanvas.getContext('2d')!;
const afterCtx = afterCanvas.getContext('2d')!;

// ---------- 状态 ----------

interface SourceImage {
  data: ImageData;
  canvas: HTMLCanvasElement; // 原始分辨率（已封顶），导出时用它缩放
  width: number;
  height: number;
  name: string;
}

interface PreviewImage {
  data: ImageData;
  width: number;
  height: number;
}

let source: SourceImage | null = null;
let preview: PreviewImage | null = null;
let params: CartoonParams = { ...DEFAULT_PARAMS };
let previewTimer: ReturnType<typeof setTimeout> | null = null;
let previewQueued = false;
let exporting = false;
let toastTimer: ReturnType<typeof setTimeout> | null = null;

const engine = new CartoonEngine();

// ---------- 参数滑条 ----------

interface SliderDef {
  key: keyof CartoonParams;
  label: string;
  min: number;
  max: number;
  step: number;
  format: (v: number) => string;
}

const SLIDERS: SliderDef[] = [
  { key: 'saturation', label: '饱和度', min: 0.5, max: 3, step: 0.1, format: (v) => v.toFixed(1) },
  { key: 'radius', label: '滤波半径', min: 2, max: 15, step: 1, format: (v) => String(v) },
  { key: 'sigmaD', label: '空间 σd', min: 1, max: 6, step: 0.5, format: (v) => v.toFixed(1) },
  { key: 'sigmaR', label: '色彩 σr', min: 0.03, max: 0.3, step: 0.01, format: (v) => v.toFixed(2) },
  { key: 'edgeThresh', label: '边缘阈值', min: 0.005, max: 0.15, step: 0.005, format: (v) => v.toFixed(3) },
  { key: 'iterations', label: '滤波轮数', min: 1, max: 3, step: 1, format: (v) => String(v) },
];

for (const def of SLIDERS) {
  const wrap = document.createElement('div');
  wrap.className = 'control';
  const head = document.createElement('div');
  head.className = 'control-head';
  const label = document.createElement('label');
  label.textContent = def.label;
  label.htmlFor = `p-${def.key}`;
  const output = document.createElement('output');
  output.id = `v-${def.key}`;
  head.append(label, output);

  const input = document.createElement('input');
  input.type = 'range';
  input.id = `p-${def.key}`;
  input.min = String(def.min);
  input.max = String(def.max);
  input.step = String(def.step);
  input.value = String(params[def.key]);
  output.textContent = def.format(params[def.key]);

  input.addEventListener('input', () => {
    const v = Number(input.value);
    params[def.key] = v;
    output.textContent = def.format(v);
    schedulePreview();
  });

  wrap.append(head, input);
  controlsRoot.appendChild(wrap);
}

function syncSliders(): void {
  for (const def of SLIDERS) {
    const input = $<HTMLInputElement>(`p-${def.key}`);
    const output = $(`v-${def.key}`);
    input.value = String(params[def.key]);
    output.textContent = def.format(params[def.key]);
  }
}

resetBtn.addEventListener('click', () => {
  params = { ...DEFAULT_PARAMS };
  syncSliders();
  schedulePreview();
});

// ---------- 图片加载 ----------

async function loadImage(blob: Blob, name: string): Promise<void> {
  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(blob, { imageOrientation: 'from-image' });
  } catch {
    // 老浏览器不支持 imageOrientation 选项，退回默认行为重试
    try {
      bitmap = await createImageBitmap(blob);
    } catch {
      showToast('无法解码该图片文件');
      return;
    }
  }

  // 像素数封顶，防止极端大图爆内存
  let scale = 1;
  if (bitmap.width * bitmap.height > MAX_LOAD_PIXELS) {
    scale = Math.sqrt(MAX_LOAD_PIXELS / (bitmap.width * bitmap.height));
    showToast(`图片较大，已缩放到 ${Math.round(bitmap.width * scale)}×${Math.round(bitmap.height * scale)} 处理`);
  }
  const width = Math.max(1, Math.round(bitmap.width * scale));
  const height = Math.max(1, Math.round(bitmap.height * scale));

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d')!;
  ctx.imageSmoothingEnabled = scale < 1;
  ctx.imageSmoothingQuality = 'high';
  // 透明像素先合成到白底：管线按不透明 RGB 处理，避免透明区被当成黑色
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, width, height);
  ctx.drawImage(bitmap, 0, 0, width, height);
  bitmap.close();

  engine.cancel();

  // 预览版本
  const previewScale = Math.min(1, PREVIEW_MAX_EDGE / Math.max(width, height));
  const pw = Math.max(1, Math.round(width * previewScale));
  const ph = Math.max(1, Math.round(height * previewScale));
  const pCanvas = document.createElement('canvas');
  pCanvas.width = pw;
  pCanvas.height = ph;
  const pCtx = pCanvas.getContext('2d')!;
  pCtx.imageSmoothingQuality = 'high';
  pCtx.drawImage(canvas, 0, 0, pw, ph);

  source = { data: ctx.getImageData(0, 0, width, height), canvas, width, height, name };
  preview = { data: pCtx.getImageData(0, 0, pw, ph), width: pw, height: ph };

  beforeCanvas.width = pw;
  beforeCanvas.height = ph;
  afterCanvas.width = pw;
  afterCanvas.height = ph;
  beforeCtx.putImageData(preview.data, 0, 0);
  afterCtx.clearRect(0, 0, pw, ph);

  canvasBox.hidden = false;
  emptyState.hidden = true;
  divider.hidden = false;
  replaceBtn.disabled = false;
  exportBtn.disabled = false;
  updateInfo();

  runPreview();
}

function updateInfo(): void {
  if (!source || !preview) {
    imageInfo.textContent = '未加载图片';
    return;
  }
  imageInfo.textContent =
    `${source.name} · ${source.width}×${source.height}px · ` +
    `预览 ${preview.width}×${preview.height}px`;
}

pickBtn.addEventListener('click', () => fileInput.click());
replaceBtn.addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', () => {
  const file = fileInput.files?.[0];
  if (file) void loadImage(file, file.name);
  fileInput.value = '';
});

sampleBtn.addEventListener('click', async () => {
  try {
    // 用 baseURI 解析，兼容部署在无尾斜杠子路径下的情况
    const url = new URL('test.jpg', document.baseURI).href;
    const res = await fetch(url);
    if (!res.ok) throw new Error(String(res.status));
    const blob = await res.blob();
    await loadImage(blob, 'test.jpg');
  } catch {
    showToast('示例图片加载失败');
  }
});

// 拖拽
window.addEventListener('dragover', (e) => {
  e.preventDefault();
  stage.classList.add('dropping');
});
window.addEventListener('dragleave', (e) => {
  if (e.target === document || e.relatedTarget === null) stage.classList.remove('dropping');
});
window.addEventListener('drop', (e) => {
  e.preventDefault();
  stage.classList.remove('dropping');
  const file = e.dataTransfer?.files?.[0];
  if (!file) return;
  if (!file.type.startsWith('image/')) {
    showToast('请拖入图片文件');
    return;
  }
  void loadImage(file, file.name);
});

// 粘贴
window.addEventListener('paste', (e) => {
  const item = Array.from(e.clipboardData?.items ?? []).find((i) =>
    i.type.startsWith('image/'),
  );
  const file = item?.getAsFile();
  if (file) void loadImage(file, '粘贴的图片.png');
});

// ---------- 预览处理 ----------

function schedulePreview(): void {
  if (!preview) return;
  if (previewTimer) clearTimeout(previewTimer);
  previewTimer = setTimeout(runPreview, PREVIEW_DEBOUNCE);
}

let previewSeq = 0;

async function runPreview(): Promise<void> {
  if (!preview) return;
  if (exporting) {
    previewQueued = true; // 导出结束后补一次预览
    return;
  }
  if (previewTimer) {
    clearTimeout(previewTimer);
    previewTimer = null;
  }

  const run = ++previewSeq;
  progressRow.hidden = false;

  try {
    const p = preview;
    const result = await engine.process(
      p.data.data,
      p.width,
      p.height,
      params,
      (fraction, stageName) => updateProgress(fraction, `预览 · ${stageName}`),
    );
    drawResult(afterCtx, result, p.width, p.height);
  } catch (err) {
    if ((err as Error).message !== 'cancelled') {
      console.error(err);
      showToast('处理失败：' + (err as Error).message);
    }
  } finally {
    // 只有最新一次预览有权收尾 UI（旧任务被取消时不动新任务的状态）
    if (run === previewSeq) {
      if (previewQueued && !exporting) {
        previewQueued = false;
        schedulePreview();
      }
    }
  }
}

/** worker 返回的独立 ArrayBuffer 直接构造 ImageData，零拷贝上屏 */
function drawResult(
  ctx: CanvasRenderingContext2D,
  result: Uint8ClampedArray<ArrayBuffer>,
  width: number,
  height: number,
): void {
  ctx.putImageData(new ImageData(result, width, height), 0, 0);
}

let progressHideTimer: ReturnType<typeof setTimeout> | null = null;

function updateProgress(fraction: number, label: string): void {
  if (progressHideTimer) {
    clearTimeout(progressHideTimer);
    progressHideTimer = null;
  }
  progressRow.hidden = false;
  progressBar.style.width = `${Math.round(fraction * 100)}%`;
  progressText.textContent = `${label} ${Math.round(fraction * 100)}%`;
  if (fraction >= 1) {
    progressHideTimer = setTimeout(() => {
      progressHideTimer = null;
      progressRow.hidden = true;
      progressBar.style.width = '0%';
    }, 400);
  }
}

// ---------- 导出 ----------

exportBtn.addEventListener('click', () => void exportResult());

async function exportResult(): Promise<void> {
  if (!source || exporting) return;

  const scaleOption = exportScale.value;
  let tw = source.width;
  let th = source.height;
  if (scaleOption !== 'full') {
    const maxEdge = Number(scaleOption);
    const s = Math.min(1, maxEdge / Math.max(source.width, source.height));
    tw = Math.max(1, Math.round(source.width * s));
    th = Math.max(1, Math.round(source.height * s));
  }

  exporting = true;
  exportBtn.disabled = true;
  exportBtn.textContent = '处理中…';

  try {
    // 按目标分辨率取样
    const eCanvas = document.createElement('canvas');
    eCanvas.width = tw;
    eCanvas.height = th;
    const eCtx = eCanvas.getContext('2d')!;
    eCtx.imageSmoothingQuality = 'high';
    eCtx.drawImage(source.canvas, 0, 0, tw, th);
    const exportData = eCtx.getImageData(0, 0, tw, th);

    const result = await engine.process(
      exportData.data,
      tw,
      th,
      params,
      (fraction, stageName) => updateProgress(fraction, `导出 · ${stageName}`),
    );

    drawResult(eCtx, result, tw, th);

    const mime = exportFormat.value;
    const blob = await new Promise<Blob | null>((resolve) =>
      eCanvas.toBlob(resolve, mime, mime === 'image/jpeg' ? 0.92 : undefined),
    );
    if (!blob) throw new Error('编码图片失败');

    const base = source.name.replace(/\.[^.]+$/, '') || 'image';
    const ext = mime === 'image/jpeg' ? 'jpg' : 'png';
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `cartoon-${base}.${ext}`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 5000);
    updateProgress(1, '导出完成');
  } catch (err) {
    if ((err as Error).message !== 'cancelled') {
      console.error(err);
      showToast('导出失败：' + (err as Error).message);
    }
  } finally {
    exporting = false;
    exportBtn.disabled = false;
    exportBtn.textContent = '处理并下载';
    if (previewQueued) {
      previewQueued = false;
      schedulePreview();
    }
  }
}

// ---------- 前后对比分割线 ----------

let splitFraction = 0.5;

function setSplit(fraction: number): void {
  splitFraction = Math.min(1, Math.max(0, fraction));
  divider.style.left = `${splitFraction * 100}%`;
  divider.setAttribute('aria-valuenow', String(Math.round(splitFraction * 100)));
  afterCanvas.style.clipPath = `inset(0 0 0 ${splitFraction * 100}%)`;
}

divider.addEventListener('keydown', (e) => {
  if (e.key === 'ArrowLeft') {
    setSplit(splitFraction - 0.02);
    e.preventDefault();
  } else if (e.key === 'ArrowRight') {
    setSplit(splitFraction + 0.02);
    e.preventDefault();
  }
});

let dragging = false;
function pointerFraction(e: PointerEvent): number {
  const rect = canvasBox.getBoundingClientRect();
  return (e.clientX - rect.left) / rect.width;
}

divider.addEventListener('pointerdown', (e) => {
  dragging = true;
  divider.setPointerCapture(e.pointerId);
});
divider.addEventListener('pointermove', (e) => {
  if (dragging) setSplit(pointerFraction(e));
});
divider.addEventListener('pointerup', () => {
  dragging = false;
});
divider.addEventListener('pointercancel', () => {
  dragging = false;
});

// ---------- 其他 ----------

function showToast(message: string): void {
  toast.textContent = message;
  toast.hidden = false;
  if (toastTimer) clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    toast.hidden = true;
  }, 3200);
}

// 空态时点击舞台 = 选图
stage.addEventListener('click', (e) => {
  if (!source && !(e.target as HTMLElement).closest('button')) fileInput.click();
});

setSplit(0.5);
