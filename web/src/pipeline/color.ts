/**
 * 色彩空间工具 —— 对应 MATLAB 的 rgb2gray / applycform(srgb2lab / lab2srgb)
 *
 * 约定：
 * - RGB 用 Uint8ClampedArray（RGBA 打包，A 不参与运算），值域 [0,255]
 * - Lab 用 Float32Array，每像素 3 个连续分量：L ∈ [0,100]，a/b ∈ 约 [-128,127]
 * - 灰度用 Float32Array，值域 [0,255]（01() 版本归一到 [0,1]）
 * - 白点 D65，与 MATLAB makecform('srgb2lab') 一致
 */

/** Rec.601 亮度权重，与 MATLAB rgb2gray 相同 */
export const GRAY_WEIGHTS = [0.2989, 0.587, 0.114] as const;

/** RGBA 打包的每像素字节数 */
export const PIXEL_STRIDE = 4;

/** sRGB（线性化后）→ CIE XYZ，D65 参考白 */
const SRGB_TO_XYZ = [
  0.4124564, 0.3575761, 0.1804375,
  0.2126729, 0.7151522, 0.072175,
  0.0193339, 0.119192, 0.9503041,
] as const;

/** CIE XYZ → sRGB（线性化前），D65 参考白 */
const XYZ_TO_SRGB = [
  3.2404542, -1.5371385, -0.4985314,
  -0.969266, 1.8760108, 0.041556,
  0.0556434, -0.2040259, 1.0572252,
] as const;

/** D65 白点 */
const WHITE_X = 0.95047;
const WHITE_Z = 1.08883;

/** CIE Lab 常量 */
const EPS = 0.008856;
const KAPPA = 903.3;

function srgbToLinear(c: number): number {
  return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
}

function linearToSrgb(c: number): number {
  // c 可能因浮点误差轻微越界，先钳制
  const v = c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(c, 1 / 2.4) - 0.055;
  return v < 0 ? 0 : v > 1 ? 1 : v;
}

function fLab(t: number): number {
  return t > EPS ? Math.cbrt(t) : (KAPPA * t + 16) / 116;
}

function fInv(t: number): number {
  const t3 = t * t * t;
  return t3 > EPS ? t3 : (116 * t - 16) / KAPPA;
}

export function rgb2gray(src: Uint8ClampedArray, dst?: Float32Array): Float32Array {
  const n = src.length / PIXEL_STRIDE;
  const out = dst ?? new Float32Array(n);
  for (let p = 0; p < n; p++) {
    const i = p * PIXEL_STRIDE;
    out[p] = GRAY_WEIGHTS[0] * src[i] + GRAY_WEIGHTS[1] * src[i + 1] + GRAY_WEIGHTS[2] * src[i + 2];
  }
  return out;
}

export function rgb2gray01(src: Uint8ClampedArray, dst?: Float32Array): Float32Array {
  const out = rgb2gray(src, dst);
  for (let p = 0; p < out.length; p++) out[p] /= 255;
  return out;
}

export function rgbToLab(
  src: Uint8ClampedArray,
  pixelCount: number,
  dst?: Float32Array,
): Float32Array {
  const out = dst ?? new Float32Array(pixelCount * 3);
  for (let p = 0; p < pixelCount; p++) {
    const i = p * PIXEL_STRIDE;
    const r = srgbToLinear(src[i] / 255);
    const g = srgbToLinear(src[i + 1] / 255);
    const b = srgbToLinear(src[i + 2] / 255);

    const x = SRGB_TO_XYZ[0] * r + SRGB_TO_XYZ[1] * g + SRGB_TO_XYZ[2] * b;
    const y = SRGB_TO_XYZ[3] * r + SRGB_TO_XYZ[4] * g + SRGB_TO_XYZ[5] * b;
    const z = SRGB_TO_XYZ[6] * r + SRGB_TO_XYZ[7] * g + SRGB_TO_XYZ[8] * b;

    const fx = fLab(x / WHITE_X);
    const fy = fLab(y);
    const fz = fLab(z / WHITE_Z);

    const o = p * 3;
    out[o] = 116 * fy - 16;
    out[o + 1] = 500 * (fx - fy);
    out[o + 2] = 200 * (fy - fz);
  }
  return out;
}

export function labToRgb(
  lab: Float32Array,
  pixelCount: number,
  dst?: Uint8ClampedArray,
): Uint8ClampedArray {
  const out = dst ?? new Uint8ClampedArray(pixelCount * PIXEL_STRIDE);
  for (let p = 0; p < pixelCount; p++) {
    const o = p * 3;
    const fy = (lab[o] + 16) / 116;
    const fx = fy + lab[o + 1] / 500;
    const fz = fy - lab[o + 2] / 200;

    const x = fInv(fx) * WHITE_X;
    const y = fInv(fy);
    const z = fInv(fz) * WHITE_Z;

    const rl = XYZ_TO_SRGB[0] * x + XYZ_TO_SRGB[1] * y + XYZ_TO_SRGB[2] * z;
    const gl = XYZ_TO_SRGB[3] * x + XYZ_TO_SRGB[4] * y + XYZ_TO_SRGB[5] * z;
    const bl = XYZ_TO_SRGB[6] * x + XYZ_TO_SRGB[7] * y + XYZ_TO_SRGB[8] * z;

    const i = p * PIXEL_STRIDE;
    out[i] = Math.round(linearToSrgb(rl) * 255);
    out[i + 1] = Math.round(linearToSrgb(gl) * 255);
    out[i + 2] = Math.round(linearToSrgb(bl) * 255);
    out[i + 3] = 255;
  }
  return out;
}
